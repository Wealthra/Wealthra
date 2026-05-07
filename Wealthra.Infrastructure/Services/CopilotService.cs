using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Wealthra.Application.Common.Constants;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Infrastructure.Services
{
    public class CopilotService : ICopilotService
    {
        private readonly HttpClient _httpClient;
        private readonly IRuntimeAppSettings _runtimeAppSettings;
        private readonly IConfiguration _configuration;

        public CopilotService(
            IHttpClientFactory httpClientFactory,
            IRuntimeAppSettings runtimeAppSettings,
            IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient("CopilotClient");
            _runtimeAppSettings = runtimeAppSettings;
            _configuration = configuration;
        }

        public async Task<CopilotChatResponse> ChatAsync(
            string message,
            string userId,
            string? startDate = null,
            string? endDate = null,
            string? authToken = null,
            CancellationToken cancellationToken = default)
        {
            var modelFromDb = await _runtimeAppSettings.GetAsync(AppSettingsKeys.AiDefaultChatModel, cancellationToken);
            var defaultChatModel = !string.IsNullOrWhiteSpace(modelFromDb)
                ? modelFromDb!
                : _configuration["Groq:Model"];

            var enrichmentFromDb = await _runtimeAppSettings.GetAsync(AppSettingsKeys.AiEnrichmentModel, cancellationToken);
            var enrichmentModel = !string.IsNullOrWhiteSpace(enrichmentFromDb)
                ? enrichmentFromDb!
                : _configuration["Groq:Model"];

            // The python service expects message and user_id as query parameters in the POST request
            var url = $"/chat?message={Uri.EscapeDataString(message)}&user_id={Uri.EscapeDataString(userId)}";
            if (!string.IsNullOrWhiteSpace(startDate))
            {
                url += $"&start_date={Uri.EscapeDataString(startDate)}";
            }
            if (!string.IsNullOrWhiteSpace(endDate))
            {
                url += $"&end_date={Uri.EscapeDataString(endDate)}";
            }
            if (!string.IsNullOrWhiteSpace(defaultChatModel))
            {
                url += $"&default_chat_model={Uri.EscapeDataString(defaultChatModel)}";
            }

            if (!string.IsNullOrWhiteSpace(enrichmentModel))
            {
                url += $"&enrichment_model={Uri.EscapeDataString(enrichmentModel)}";
            }
            
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            
            if (!string.IsNullOrEmpty(authToken))
            {
                // Forward the user's JWT token so the copilot can call back into our API
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
            }

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"Copilot service error ({(int)response.StatusCode}): {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<CopilotChatResponse>(cancellationToken: cancellationToken);
            return result ?? throw new Exception("Copilot service returned empty response");
        }
    }
}
