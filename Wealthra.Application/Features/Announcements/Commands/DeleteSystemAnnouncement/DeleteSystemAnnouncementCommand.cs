using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Announcements.Commands.DeleteSystemAnnouncement;

public record DeleteSystemAnnouncementCommand(int Id) : IRequest<Unit>;

public class DeleteSystemAnnouncementCommandValidator : AbstractValidator<DeleteSystemAnnouncementCommand>
{
    public DeleteSystemAnnouncementCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class DeleteSystemAnnouncementCommandHandler : IRequestHandler<DeleteSystemAnnouncementCommand, Unit>
{
    private readonly IApplicationDbContext _db;

    public DeleteSystemAnnouncementCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Unit> Handle(DeleteSystemAnnouncementCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.SystemAnnouncements.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null) throw new NotFoundException("SystemAnnouncement", request.Id);
        _db.SystemAnnouncements.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
