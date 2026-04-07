using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Infrastructure.Services
{
    public sealed class GroqExpenseExtractionEnrichmentService : IExpenseExtractionEnrichmentService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GroqExpenseExtractionEnrichmentService> _logger;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public GroqExpenseExtractionEnrichmentService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GroqExpenseExtractionEnrichmentService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
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
            var model = _configuration["Groq:Model"] ?? "meta-llama/llama-4-scout-17b-16e-instruct";
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Groq:ApiKey is not configured; skipping enrichment.");
                return extracted;
            }

            try
            {
                var categoriesJson = JsonSerializer.Serialize(
                    applicationCategories.Select(c => new { id = c.Id, nameEn = c.NameEn, nameTr = c.NameTr }),
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
                    "ALLOWED_CATEGORIES (JSON: id, nameEn, nameTr — pick categoryId only from id values; use names to infer the best fit in any language):\n" +
                    categoriesJson +
                    "\n\nRules:\n" +
                    "- Merge only true duplicates (same product and same amount).\n" +
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
                var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidOperationException("Groq returned empty content.");
                }

                var parsed = JsonSerializer.Deserialize<GroqExpensesWrapper>(content, SerializerOptions);
                if (parsed?.Expenses is null || parsed.Expenses.Count == 0)
                {
                    throw new InvalidOperationException("Groq JSON contained no expenses.");
                }

                var allowedIds = new HashSet<int>(applicationCategories.Select(c => c.Id));
                var idToName = applicationCategories.ToDictionary(c => c.Id, c => $"{c.NameEn} ({c.NameTr})");

                static string NormalizeDescription(string? d) =>
                    (d ?? string.Empty).Trim().ToUpperInvariant();

                var sharedCategoryHint = extracted.Select(e => e.CategoryHint).FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

                var result = new List<ExtractedExpenseDto>(parsed.Expenses.Count);
                foreach (var row in parsed.Expenses)
                {
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
                        var match = extracted.FirstOrDefault(e =>
                            NormalizeDescription(e.Description) == NormalizeDescription(description) &&
                            e.Amount == amount);
                        if (match is null)
                        {
                            var byAmount = extracted.Where(e => e.Amount == amount).ToList();
                            if (byAmount.Count == 1)
                            {
                                match = byAmount[0];
                            }
                        }

                        date = match?.Date;
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
                        CategoryHint = sharedCategoryHint,
                        SuggestedCategoryId = categoryId,
                        CategorySuggestion = categoryName,
                        Confidence = 0.85m,
                        Source = "ocr+groq"
                    });
                }

                if (result.Count == 0)
                {
                    return extracted;
                }

                if (result.Count < extracted.Count)
                {
                    _logger.LogWarning(
                        "Groq returned fewer rows ({Enriched}) than OCR ({Ocr}); using enriched list anyway.",
                        result.Count,
                        extracted.Count);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Groq enrichment failed; returning OCR deduplicated results.");
                return extracted;
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
