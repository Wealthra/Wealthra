using Wealthra.Domain.Common;
using Wealthra.Domain.Enums;

namespace Wealthra.Domain.Entities;

public class SystemAnnouncement : BaseEntity
{
    public string TitleEn { get; set; } = string.Empty;
    public string TitleTr { get; set; } = string.Empty;
    public string BodyEn { get; set; } = string.Empty;
    public string BodyTr { get; set; } = string.Empty;
    public AnnouncementSeverity Severity { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool TargetAllSubscribers { get; set; } = true;
    /// <summary>JSON array of plan ids, e.g. [1,2]. Null/empty when TargetAllSubscribers is true.</summary>
    public string? TargetPlanIdsJson { get; set; }
    /// <summary>JSON array of SubscriptionTier int values.</summary>
    public string? TargetTiersJson { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}
