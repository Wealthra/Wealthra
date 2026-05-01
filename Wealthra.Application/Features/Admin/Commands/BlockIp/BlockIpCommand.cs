using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Admin.Commands.BlockIp;

public record BlockIpCommand(string IpAddress, string? Reason, DateTimeOffset? ExpiresUtc) : IRequest<int>;

public class BlockIpCommandValidator : AbstractValidator<BlockIpCommand>
{
    public BlockIpCommandValidator()
    {
        RuleFor(x => x.IpAddress).NotEmpty().MaximumLength(64);
    }
}

public class BlockIpCommandHandler : IRequestHandler<BlockIpCommand, int>
{
    private readonly IApplicationDbContext _db;

    public BlockIpCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int> Handle(BlockIpCommand request, CancellationToken cancellationToken)
    {
        var ip = request.IpAddress.Trim();
        var row = await _db.BlockedIpAddresses.FirstOrDefaultAsync(b => b.IpAddress == ip, cancellationToken);
        if (row == null)
        {
            row = new BlockedIpAddress { IpAddress = ip, CreatedUtc = DateTimeOffset.UtcNow };
            _db.BlockedIpAddresses.Add(row);
        }

        row.Reason = request.Reason;
        row.ExpiresUtc = request.ExpiresUtc;
        row.CreatedUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}
