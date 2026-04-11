using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Expenses.Models;
using MediatR;
using Wealthra.Application.Features.Categories.Queries.GetAllCategories;

namespace Wealthra.Infrastructure.Services
{
    public class ExpenseExtractionService : IExpenseExtractionService
    {
        private readonly HttpClient _ocrClient;
        private readonly HttpClient _sttClient;
        private readonly IMediator _mediator;

        public ExpenseExtractionService(IHttpClientFactory httpClientFactory, IMediator mediator)
        {
            _ocrClient = httpClientFactory.CreateClient("OcrClient");
            _sttClient = httpClientFactory.CreateClient("SttClient");
            _mediator = mediator;
        }

        public async Task<IReadOnlyList<ExtractedExpenseDto>> ExtractFromImageAsync(
            Stream fileStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            var categories = await GetCategoriesStringAsync(cancellationToken);
            return await ExtractExpensesAsync(_ocrClient, "extract-expenses-from-image", fileStream, fileName, categories, cancellationToken);
        }

        public async Task<IReadOnlyList<ExtractedExpenseDto>> ExtractFromAudioAsync(
            Stream fileStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            var categories = await GetCategoriesStringAsync(cancellationToken);
            return await ExtractExpensesAsync(_sttClient, "extract-expenses-from-audio", fileStream, fileName, categories, cancellationToken);
        }

        private static async Task<IReadOnlyList<ExtractedExpenseDto>> ExtractExpensesAsync(
            HttpClient client,
            string path,
            Stream fileStream,
            string fileName,
            string? categories,
            CancellationToken cancellationToken)
        {
            using var form = new MultipartFormDataContent();
            if (!string.IsNullOrEmpty(categories))
            {
                form.Add(new StringContent(categories), "categories");
            }

            using var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "file", fileName);

            using var response = await client.PostAsync(path, form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Upstream extraction failed with {(int)response.StatusCode}: {details}");
            }

            var payload = await response.Content.ReadFromJsonAsync<ExtractExpenseResponse>(cancellationToken: cancellationToken);
            if (payload?.Expenses is null)
            {
                throw new InvalidOperationException("Upstream extraction returned an invalid payload.");
            }

            // Preserve every upstream line (including repeated description+amount). Do not merge here —
            // the OCR/STT service may return distinct rows for separate purchases (e.g. two breads).
            var mapped = payload.Expenses
                .Where(x => x.Amount is not null && !string.IsNullOrWhiteSpace(x.Description))
                .Select(x => new ExtractedExpenseDto
                {
                    Description = x.Description!,
                    Amount = Convert.ToDecimal(x.Amount!.Value, CultureInfo.InvariantCulture),
                    Date = x.Date,
                    CategoryHint = x.CategoryHint,
                    Confidence = x.Confidence is null ? null : Convert.ToDecimal(x.Confidence.Value, CultureInfo.InvariantCulture),
                    Source = x.Source ?? "unknown"
                });

            return mapped.ToList();
        }

        private async Task<string> GetCategoriesStringAsync(CancellationToken cancellationToken)
        {
            try
            {
                var categories = await _mediator.Send(new GetAllCategoriesQuery(), cancellationToken);
                return string.Join(", ", categories.Select(c => c.CategoryName));
            }
            catch
            {
                return "General, Food, Market, Travel, Health, Entertainment, Others";
            }
        }

        private sealed class ExtractExpenseResponse
        {
            [JsonPropertyName("expenses")]
            public List<ExtractExpenseItem> Expenses { get; set; } = new();
        }

        private sealed class ExtractExpenseItem
        {
            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("amount")]
            public double? Amount { get; set; }

            [JsonPropertyName("date")]
            public DateTime? Date { get; set; }

            [JsonPropertyName("category_hint")]
            public string? CategoryHint { get; set; }

            [JsonPropertyName("confidence")]
            public double? Confidence { get; set; }

            [JsonPropertyName("source")]
            public string? Source { get; set; }
        }
    }
}
