namespace Wealthra.Application.Features.Identity.Models
{
    public record AuthResponse(
        string Id,
        string Email,
        string Token,
        string RefreshToken,
        DateTime RefreshTokenExpiration
    );
}