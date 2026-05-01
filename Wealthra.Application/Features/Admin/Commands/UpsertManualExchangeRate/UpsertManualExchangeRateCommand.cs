using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Admin.Commands.UpsertManualExchangeRate;

public record UpsertManualExchangeRateCommand(string FromCurrency, string ToCurrency, decimal Rate) : IRequest<int>;

public class UpsertManualExchangeRateCommandValidator : AbstractValidator<UpsertManualExchangeRateCommand>
{
    public UpsertManualExchangeRateCommandValidator()
    {
        RuleFor(x => x.FromCurrency).NotEmpty().MaximumLength(8);
        RuleFor(x => x.ToCurrency).NotEmpty().MaximumLength(8);
        RuleFor(x => x.Rate).GreaterThan(0);
    }
}

public class UpsertManualExchangeRateCommandHandler : IRequestHandler<UpsertManualExchangeRateCommand, int>
{
    private readonly IApplicationDbContext _db;

    public UpsertManualExchangeRateCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int> Handle(UpsertManualExchangeRateCommand request, CancellationToken cancellationToken)
    {
        var from = request.FromCurrency.ToUpperInvariant();
        var to = request.ToCurrency.ToUpperInvariant();
        var row = await _db.ManualExchangeRates.FirstOrDefaultAsync(
            r => r.FromCurrency == from && r.ToCurrency == to,
            cancellationToken);

        if (row == null)
        {
            row = new ManualExchangeRate { FromCurrency = from, ToCurrency = to };
            _db.ManualExchangeRates.Add(row);
        }

        row.Rate = request.Rate;
        row.UpdatedOn = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}
