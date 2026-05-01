using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.SupportTickets.Models;

public record SupportTicketDto(
    int Id,
    string UserId,
    string Subject,
    string Body,
    SupportTicketStatus Status,
    string? AdminReply,
    DateTimeOffset CreatedOn,
    DateTimeOffset? LastModifiedOn);
