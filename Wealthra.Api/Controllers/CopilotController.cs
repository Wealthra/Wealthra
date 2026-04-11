using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Api.Controllers
{
    [Authorize]
    public class CopilotController : ApiControllerBase
    {
        private readonly ICopilotService _copilotService;
        private readonly ICurrentUserService _currentUserService;

        public CopilotController(ICopilotService copilotService, ICurrentUserService currentUserService)
        {
            _copilotService = copilotService;
            _currentUserService = currentUserService;
        }

        [HttpPost("chat")]
        public async Task<ActionResult<CopilotChatResponse>> Chat([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Extract JWT from the Authorization header to forward it to the Python service
            string? authToken = null;
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var val = authHeader.ToString();
                if (val.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    authToken = val.Substring(7);
                }
            }

            var response = await _copilotService.ChatAsync(request.Message, userId, authToken, cancellationToken);
            return Ok(response);
        }

        public class ChatRequestDto
        {
            [System.Text.Json.Serialization.JsonPropertyName("message")]
            public string Message { get; set; } = string.Empty;
        }
    }
}
