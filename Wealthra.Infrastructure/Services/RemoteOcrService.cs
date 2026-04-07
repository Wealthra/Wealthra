using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Infrastructure.Services
{
    public class RemoteOcrService : IOcrService
    {
        private readonly HttpClient _ocrClient;

        public RemoteOcrService(IHttpClientFactory httpClientFactory)
        {
            _ocrClient = httpClientFactory.CreateClient("OcrClient");
        }

        public async Task<string> ExtractTextAsync(
            Stream imageStream,
            string language = "eng",
            CancellationToken cancellationToken = default)
        {
            using var form = new MultipartFormDataContent();
            using var fileContent = new StreamContent(imageStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "file", $"upload.{language}.jpg");

            using var response = await _ocrClient.PostAsync("extract-expenses-from-image", form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"OCR service failed with {(int)response.StatusCode}: {details}");
            }

            var payload = await response.Content.ReadFromJsonAsync<ExtractExpenseResponse>(cancellationToken: cancellationToken);
            if (payload?.Expenses is null || payload.Expenses.Count == 0)
            {
                return string.Empty;
            }

            var lines = payload.Expenses
                .Where(x => !string.IsNullOrWhiteSpace(x.Description))
                .Select(x => x.Amount is null
                    ? x.Description!.Trim()
                    : $"{x.Description!.Trim()} - {x.Amount.Value}");

            return string.Join(Environment.NewLine, lines);
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
        }
    }
}
