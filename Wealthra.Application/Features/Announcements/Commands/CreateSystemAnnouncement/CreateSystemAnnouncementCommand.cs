using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;
using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.Announcements.Commands.CreateSystemAnnouncement;

public class CreateSystemAnnouncementCommand : IRequest<int>
{
    public string TitleEn { get; set; } = string.Empty;
    public string TitleTr { get; set; } = string.Empty;
    public string BodyEn { get; set; } = string.Empty;
    public string BodyTr { get; set; } = string.Empty;
    public AnnouncementSeverity Severity { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool TargetAllSubscribers { get; set; } = true;
    public string? TargetPlanIdsJson { get; set; }
    public string? TargetTiersJson { get; set; }
    public bool IsPublished { get; set; }
}

public class CreateSystemAnnouncementCommandValidator : AbstractValidator<CreateSystemAnnouncementCommand>
{
    public CreateSystemAnnouncementCommandValidator()
    {
        RuleFor(x => x.TitleEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TitleTr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BodyEn).NotEmpty();
        RuleFor(x => x.BodyTr).NotEmpty();
    }
}

public class CreateSystemAnnouncementCommandHandler : IRequestHandler<CreateSystemAnnouncementCommand, int>
{
    private readonly IApplicationDbContext _db;

    public CreateSystemAnnouncementCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int> Handle(CreateSystemAnnouncementCommand request, CancellationToken cancellationToken)
    {
        var entity = new SystemAnnouncement
        {
            TitleEn = request.TitleEn.Trim(),
            TitleTr = request.TitleTr.Trim(),
            BodyEn = request.BodyEn.Trim(),
            BodyTr = request.BodyTr.Trim(),
            Severity = request.Severity,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            TargetAllSubscribers = request.TargetAllSubscribers,
            TargetPlanIdsJson = request.TargetPlanIdsJson,
            TargetTiersJson = request.TargetTiersJson,
            IsPublished = request.IsPublished,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        _db.SystemAnnouncements.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
