using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wealthra.Application.Common.Interfaces
{
    public interface ICopilotService
    {
        Task<CopilotChatResponse> ChatAsync(string message, string userId, string? authToken = null, CancellationToken cancellationToken = default);
    }

    public class CopilotChatResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("language")]
        public string Language { get; set; } = "en";

        [System.Text.Json.Serialization.JsonPropertyName("payload")]
        public Dictionary<string, object>? Payload { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("ui_hints")]
        public Dictionary<string, object>? UiHints { get; set; }
    }
}
