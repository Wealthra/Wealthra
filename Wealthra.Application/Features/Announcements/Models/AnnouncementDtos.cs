using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.Announcements.Models;

public record SystemAnnouncementDto(
    int Id,
    string TitleEn,
    string TitleTr,
    string BodyEn,
    string BodyTr,
    AnnouncementSeverity Severity,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool TargetAllSubscribers,
    string? TargetPlanIdsJson,
    string? TargetTiersJson,
    bool IsPublished);

public record ActiveAnnouncementBannerDto(
    int Id,
    string TitleEn,
    string TitleTr,
    string BodyEn,
    string BodyTr,
    AnnouncementSeverity Severity);
