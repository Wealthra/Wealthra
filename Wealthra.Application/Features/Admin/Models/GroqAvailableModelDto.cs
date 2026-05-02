namespace Wealthra.Application.Features.Admin.Models;

public record GroqAvailableModelDto(
    string Id,
    string? OwnedBy,
    bool Active,
    long? ContextWindow);

public record GroqModelsListDto(IReadOnlyList<GroqAvailableModelDto> Models, bool GroqApiKeyConfigured);
