using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Admin.Commands.UpdateManualExchangeRate;

public record UpdateManualExchangeRateCommand(int Id, decimal Rate) : IRequest<Unit>;

public class UpdateManualExchangeRateCommandValidator : AbstractValidator<UpdateManualExchangeRateCommand>
{
    public UpdateManualExchangeRateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Rate).GreaterThan(0);
    }
}

public class UpdateManualExchangeRateCommandHandler : IRequestHandler<UpdateManualExchangeRateCommand, Unit>
{
    private readonly IApplicationDbContext _db;

    public UpdateManualExchangeRateCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Unit> Handle(UpdateManualExchangeRateCommand request, CancellationToken cancellationToken)
    {
        var row = await _db.ManualExchangeRates.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (row == null)
        {
            throw new NotFoundException(nameof(ManualExchangeRate), request.Id);
        }

        row.Rate = request.Rate;
        row.UpdatedOn = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
