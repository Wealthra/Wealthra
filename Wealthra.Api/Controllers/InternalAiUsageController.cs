using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Api.Controllers;

[ApiController]
[Route("api/internal/ai-usage")]
public class InternalAiUsageController : ControllerBase
{
    private readonly IAiUsageRecorder _aiUsageRecorder;
    private readonly IConfiguration _configuration;

    public InternalAiUsageController(IAiUsageRecorder aiUsageRecorder, IConfiguration configuration)
    {
        _aiUsageRecorder = aiUsageRecorder;
        _configuration = configuration;
    }

    public sealed class IngestBody
    {
        public string Feature { get; set; } = "Copilot";
        public string Model { get; set; } = string.Empty;
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public decimal? EstimatedCostUsd { get; set; }
        public string? UserId { get; set; }
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromHeader(Name = "X-Internal-Api-Key")] string? apiKey, [FromBody] IngestBody body, CancellationToken cancellationToken)
    {
        var expected = _configuration["InternalApi:AiUsageIngestKey"];
        if (string.IsNullOrEmpty(expected) || !string.Equals(apiKey, expected, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        await _aiUsageRecorder.RecordAsync(
            body.Feature,
            body.Model,
            body.PromptTokens,
            body.CompletionTokens,
            body.EstimatedCostUsd,
            body.UserId,
            cancellationToken);

        return NoContent();
    }
}
