using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Goals.Commands.UpdateGoal;

public record UpdateGoalCommand : IRequest<Unit>
{
    public int Id { get; init; }
    public string Name { get; init; }
    public decimal TargetAmount { get; init; }
    public decimal CurrentAmount { get; init; }
    public DateTime Deadline { get; init; }
}

public class UpdateGoalCommandValidator : AbstractValidator<UpdateGoalCommand>
{
    public UpdateGoalCommandValidator()
    {
        RuleFor(v => v.Id)
            .GreaterThan(0);

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

public class UpdateGoalCommandHandler : IRequestHandler<UpdateGoalCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateGoalCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateGoalCommand request, CancellationToken cancellationToken)
    {
        var goal = await _context.Goals
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);

        if (goal == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Goal), request.Id);
        }

        goal.Name = request.Name;
        goal.TargetAmount = request.TargetAmount;
        goal.CurrentAmount = request.CurrentAmount;
        goal.Deadline = request.Deadline;

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
