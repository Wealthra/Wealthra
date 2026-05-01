using Wealthra.Domain.Common;
using Wealthra.Domain.Enums;

namespace Wealthra.Domain.Entities;

public class SupportTicket : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;
    public string? AdminReply { get; set; }
    public string? LastRepliedByAdminUserId { get; set; }
}
