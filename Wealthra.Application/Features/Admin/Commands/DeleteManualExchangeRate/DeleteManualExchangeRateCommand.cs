using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Admin.Commands.DeleteManualExchangeRate;

public record DeleteManualExchangeRateCommand(int Id) : IRequest<Unit>;

public class DeleteManualExchangeRateCommandValidator : AbstractValidator<DeleteManualExchangeRateCommand>
{
    public DeleteManualExchangeRateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class DeleteManualExchangeRateCommandHandler : IRequestHandler<DeleteManualExchangeRateCommand, Unit>
{
    private readonly IApplicationDbContext _db;

    public DeleteManualExchangeRateCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Unit> Handle(DeleteManualExchangeRateCommand request, CancellationToken cancellationToken)
    {
        var row = await _db.ManualExchangeRates.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (row == null)
        {
            throw new NotFoundException(nameof(ManualExchangeRate), request.Id);
        }

        _db.ManualExchangeRates.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
