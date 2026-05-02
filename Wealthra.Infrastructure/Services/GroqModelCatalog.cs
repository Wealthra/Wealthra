using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Infrastructure.Services;

public sealed class GroqModelCatalog : IGroqModelCatalog
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GroqModelCatalog(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<GroqAvailableModelDto>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Array.Empty<GroqAvailableModelDto>();
        }

        var client = _httpClientFactory.CreateClient("GroqClient");
        using var request = new HttpRequestMessage(HttpMethod.Get, "openai/v1/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Groq models list failed: {(int)response.StatusCode} {responseText}");
        }

        var parsed = JsonSerializer.Deserialize<GroqModelsListResponse>(responseText, SerializerOptions);
        if (parsed?.Data is null || parsed.Data.Count == 0)
        {
            return Array.Empty<GroqAvailableModelDto>();
        }

        return parsed.Data
            .Where(m => m.Active)
            .Select(m => new GroqAvailableModelDto(m.Id, m.OwnedBy, m.Active, m.ContextWindow))
            .OrderBy(m => m.Id, StringComparer.Ordinal)
            .ToList();
    }

    private sealed class GroqModelsListResponse
    {
        public List<GroqModelItem> Data { get; set; } = new();
    }

    private sealed class GroqModelItem
    {
        public string Id { get; set; } = "";
        public string? OwnedBy { get; set; }
        public bool Active { get; set; }
        public long? ContextWindow { get; set; }
    }
}
