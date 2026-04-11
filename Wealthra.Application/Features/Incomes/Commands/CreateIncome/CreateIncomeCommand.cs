using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Incomes.Commands.CreateIncome;

public record CreateIncomeCommand : IRequest<int>
{
    public string Name { get; init; }
    public decimal Amount { get; init; }
    public string Method { get; init; }
    public bool IsRecurring { get; init; }
    public DateTime TransactionDate { get; init; }
    public string Currency { get; init; } = "TRY";
}

public class CreateIncomeCommandValidator : AbstractValidator<CreateIncomeCommand>
{
    public CreateIncomeCommandValidator()
    {
        RuleFor(v => v.Name)
            .MaximumLength(200)
            .NotEmpty();

        RuleFor(v => v.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0.");

        RuleFor(v => v.Method)
            .MaximumLength(100)
            .NotEmpty();
    }
}

public class CreateIncomeCommandHandler : IRequestHandler<CreateIncomeCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateIncomeCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<int> Handle(CreateIncomeCommand request, CancellationToken cancellationToken)
    {
        var entity = new Income
        {
            Name = request.Name,
            Amount = request.Amount,
            Method = request.Method,
            IsRecurring = request.IsRecurring,
            TransactionDate = request.TransactionDate,
            Currency = request.Currency ?? "TRY"
        };

        _context.Incomes.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
