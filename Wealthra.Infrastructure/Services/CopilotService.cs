using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Infrastructure.Services
{
    public class CopilotService : ICopilotService
    {
        private readonly HttpClient _httpClient;

        public CopilotService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("CopilotClient");
        }

        public async Task<CopilotChatResponse> ChatAsync(string message, string userId, string? authToken = null, CancellationToken cancellationToken = default)
        {
            // The python service expects message and user_id as query parameters in the POST request
            var url = $"/chat?message={Uri.EscapeDataString(message)}&user_id={Uri.EscapeDataString(userId)}";
            
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
