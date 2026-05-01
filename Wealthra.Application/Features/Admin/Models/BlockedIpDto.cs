namespace Wealthra.Application.Features.Admin.Models;

public record BlockedIpDto(int Id, string IpAddress, string? Reason, DateTimeOffset CreatedUtc, DateTimeOffset? ExpiresUtc);
