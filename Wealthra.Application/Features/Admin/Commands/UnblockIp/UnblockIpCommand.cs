using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Admin.Commands.UnblockIp;

public record UnblockIpCommand(string IpAddress) : IRequest<Unit>;

public class UnblockIpCommandValidator : AbstractValidator<UnblockIpCommand>
{
    public UnblockIpCommandValidator()
    {
        RuleFor(x => x.IpAddress).NotEmpty();
    }
}

public class UnblockIpCommandHandler : IRequestHandler<UnblockIpCommand, Unit>
{
    private readonly IApplicationDbContext _db;

    public UnblockIpCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Unit> Handle(UnblockIpCommand request, CancellationToken cancellationToken)
    {
        var ip = request.IpAddress.Trim();
        var row = await _db.BlockedIpAddresses.FirstOrDefaultAsync(b => b.IpAddress == ip, cancellationToken);
        if (row == null) throw new NotFoundException("BlockedIp", ip);
        _db.BlockedIpAddresses.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
