using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Goals.Commands.CreateGoal;

public record CreateGoalCommand : IRequest<int>
{
    public string Name { get; init; } = string.Empty;
    public decimal TargetAmount { get; init; }
    public decimal CurrentAmount { get; init; }
    public DateTime Deadline { get; init; }
    public string Currency { get; init; } = "TRY";
}

public class CreateGoalCommandValidator : AbstractValidator<CreateGoalCommand>
{
    public CreateGoalCommandValidator()
    {
        RuleFor(v => v.Name)
            .MaximumLength(200)
            .NotEmpty();

        RuleFor(v => v.TargetAmount)
            .GreaterThan(0)
            .WithMessage("Target amount must be greater than 0.");

        RuleFor(v => v.CurrentAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Current amount must be 0 or greater.");

        RuleFor(v => v.Deadline)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Deadline must be in the future.");
    }
}

public class CreateGoalCommandHandler : IRequestHandler<CreateGoalCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateGoalCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<int> Handle(CreateGoalCommand request, CancellationToken cancellationToken)
    {
        var entity = new Goal
        {
            Name = request.Name,
            TargetAmount = request.TargetAmount,
            CurrentAmount = request.CurrentAmount,
            Deadline = request.Deadline,
            Currency = request.Currency ?? "TRY"
        };

        _context.Goals.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
