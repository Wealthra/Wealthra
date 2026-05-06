using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wealthra.Application.Common.Constants;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Infrastructure.Services
{
    public sealed class GroqExpenseExtractionEnrichmentService : IExpenseExtractionEnrichmentService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GroqExpenseExtractionEnrichmentService> _logger;
        private readonly IRuntimeAppSettings _runtimeAppSettings;
        private readonly IAiUsageRecorder _aiUsageRecorder;
        private readonly ICurrentUserService _currentUserService;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public GroqExpenseExtractionEnrichmentService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GroqExpenseExtractionEnrichmentService> logger,
            IRuntimeAppSettings runtimeAppSettings,
            IAiUsageRecorder aiUsageRecorder,
            ICurrentUserService currentUserService)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _runtimeAppSettings = runtimeAppSettings;
            _aiUsageRecorder = aiUsageRecorder;
            _currentUserService = currentUserService;
        }

        public async Task<IReadOnlyList<ExtractedExpenseDto>> EnrichAsync(
            IReadOnlyList<ExtractedExpenseDto> extracted,
            IReadOnlyList<ExpenseCategoryOption> applicationCategories,
            CancellationToken cancellationToken = default)
        {
            if (extracted.Count == 0)
            {
                return extracted;
            }

            var apiKey = _configuration["Groq:ApiKey"];
            var modelFromDb = await _runtimeAppSettings.GetAsync(AppSettingsKeys.AiEnrichmentModel, cancellationToken);
            var model = !string.IsNullOrWhiteSpace(modelFromDb)
                ? modelFromDb!
                : _configuration["Groq:Model"] ?? "meta-llama/llama-4-scout-17b-16e-instruct";
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Groq:ApiKey is not configured; skipping enrichment.");
                return extracted;
            }

            try
            {
                var categoriesJson = JsonSerializer.Serialize(
                    applicationCategories.Select(c => new { id = c.Id, name = c.Name }),
                    SerializerOptions);

                var inputJson = JsonSerializer.Serialize(
                    extracted.Select(e => new
                    {
                        e.Description,
                        e.Amount,
                        e.Date,
                        e.CategoryHint,
                        e.Confidence
                    }),
                    SerializerOptions);

                var prompt =
                    "You normalize receipt line items extracted by OCR. Input is a JSON array of objects.\n" +
                    "ALLOWED_CATEGORIES (JSON array — you MUST ONLY pick categoryId from these id values, or null if none fit):\n" +
                    categoriesJson +
                    "\n\nRules:\n" +
                    "- CRITICAL: Output exactly one expense per input element, in the same order. Same length as INPUT. " +
                    "Never merge or drop rows — if two lines are both 'bread' with the same price, output two separate objects.\n" +
                    "- Expand terse store abbreviations when obvious (e.g. GV → Great Value on grocery receipts).\n" +
                    "- description: short, clear product name.\n" +
                    "- amount: preserve numeric totals exactly as in input.\n" +
                    "- transactionDate: ISO 8601 date (YYYY-MM-DD) if inferable from input, else null.\n" +
                    "- categoryId: must be exactly one of the numeric ids from ALLOWED_CATEGORIES, or null.\n" +
                    "Respond with a JSON object only, shape: {\"expenses\":[{\"description\":string,\"amount\":number,\"transactionDate\":string|null,\"categoryId\":number|null}]}\n\n" +
                    "INPUT:\n" + inputJson;

                var client = _httpClientFactory.CreateClient("GroqClient");
                using var request = new HttpRequestMessage(HttpMethod.Post, "openai/v1/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var chatBody = new GroqChatRequest
                {
                    Model = model,
                    Temperature = 0.1,
                    ResponseFormat = new GroqResponseFormat { Type = "json_object" },
                    Messages =
                    [
                        new GroqMessage
                        {
                            Role = "system",
                            Content = "You output only valid JSON matching the user instructions."
                        },
                        new GroqMessage { Role = "user", Content = prompt }
                    ]
                };
                request.Content = JsonContent.Create(chatBody, options: SerializerOptions);

                using var response = await client.SendAsync(request, cancellationToken);
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Groq request failed: {(int)response.StatusCode} {responseText}");
                }

                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;
                await RecordUsageFromGroqResponseAsync(root, model, cancellationToken);
                var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidOperationException("Groq returned empty content.");
                }

                var parsed = JsonSerializer.Deserialize<GroqExpensesWrapper>(content, SerializerOptions);
                if (parsed?.Expenses is null || parsed.Expenses.Count == 0)
                {
                    throw new InvalidOperationException("Groq JSON contained no expenses.");
                }

                if (parsed.Expenses.Count != extracted.Count)
                {
                    _logger.LogWarning(
                        "Groq returned {GroqCount} rows but OCR/STT had {ExtractedCount}; refusing merge — using raw extraction with no enrichment.",
                        parsed.Expenses.Count,
                        extracted.Count);
                    return extracted;
                }

                var allowedIds = new HashSet<int>(applicationCategories.Select(c => c.Id));
                var idToName = applicationCategories.ToDictionary(c => c.Id, c => c.Name);

                var result = new List<ExtractedExpenseDto>(parsed.Expenses.Count);
                for (var i = 0; i < parsed.Expenses.Count; i++)
                {
                    var row = parsed.Expenses[i];
                    var source = extracted[i];
                    if (string.IsNullOrWhiteSpace(row.Description))
                    {
                        continue;
                    }

                    DateTime? date = null;
                    if (!string.IsNullOrWhiteSpace(row.TransactionDate) &&
                        DateTime.TryParse(row.TransactionDate, CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDate))
                    {
                        date = parsedDate;
                    }

                    var amount = Convert.ToDecimal(row.Amount, CultureInfo.InvariantCulture);
                    var description = row.Description.Trim();
                    if (!date.HasValue)
                    {
                        date = source.Date;
                    }

                    int? categoryId = row.CategoryId;
                    if (categoryId is not null && !allowedIds.Contains(categoryId.Value))
                    {
                        _logger.LogWarning("Groq returned invalid categoryId {CategoryId}; clearing.", categoryId);
                        categoryId = null;
                    }

                    string? categoryName = null;
                    if (categoryId is not null && idToName.TryGetValue(categoryId.Value, out var n))
                    {
                        categoryName = n;
                    }

                    result.Add(new ExtractedExpenseDto
                    {
                        Description = description,
                        Amount = amount,
                        Date = date,
                        CategoryHint = source.CategoryHint,
                        SuggestedCategoryId = categoryId,
                        CategorySuggestion = categoryName,
                        Confidence = 0.85m,
                        Source = "ocr+groq",
                        Currency = source.Currency
                    });
                }

                if (result.Count == 0)
                {
                    return extracted;
                }

                if (result.Count != extracted.Count)
                {
                    _logger.LogWarning(
                        "After filtering, enriched row count ({Enriched}) != OCR count ({Ocr}); using raw extraction.",
                        result.Count,
                        extracted.Count);
                    return extracted;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Groq enrichment failed; returning raw extraction results.");
                return extracted;
            }
        }

        private async Task RecordUsageFromGroqResponseAsync(JsonElement root, string model, CancellationToken cancellationToken)
        {
            try
            {
                if (!root.TryGetProperty("usage", out var usage))
                {
                    return;
                }

                var prompt = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                var completion = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                decimal? cost = null;
                var promptPrice = _configuration.GetValue<decimal?>("Groq:PricePer1MPromptUsd");
                var completionPrice = _configuration.GetValue<decimal?>("Groq:PricePer1MCompletionUsd");
                if (promptPrice is not null || completionPrice is not null)
                {
                    cost = prompt / 1_000_000m * (promptPrice ?? 0) + completion / 1_000_000m * (completionPrice ?? 0);
                }

                await _aiUsageRecorder.RecordAsync(
                    "GroqExpenseEnrichment",
                    model,
                    prompt,
                    completion,
                    cost,
                    _currentUserService.UserId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping AI usage record.");
            }
        }

        private sealed class GroqChatRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("messages")]
            public List<GroqMessage> Messages { get; set; } = new();

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; }

            [JsonPropertyName("response_format")]
            public GroqResponseFormat? ResponseFormat { get; set; }
        }

        private sealed class GroqMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private sealed class GroqResponseFormat
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "json_object";
        }

        private sealed class GroqExpensesWrapper
        {
            [JsonPropertyName("expenses")]
            public List<GroqExpenseRow> Expenses { get; set; } = new();
        }

        private sealed class GroqExpenseRow
        {
            [JsonPropertyName("description")]
            public string Description { get; set; } = string.Empty;

            [JsonPropertyName("amount")]
            public double Amount { get; set; }

            [JsonPropertyName("transactionDate")]
            public string? TransactionDate { get; set; }

            [JsonPropertyName("categoryId")]
            public int? CategoryId { get; set; }
        }
    }
}
