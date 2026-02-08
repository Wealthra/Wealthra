namespace Wealthra.Application.Features.Identity.Models
{
    public record UserDto(
        string Id,
        string Email,
        string FirstName,
        string LastName,
        string? AvatarUrl,
        DateTime CreatedAt
    );
}