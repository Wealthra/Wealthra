using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Budgets.Commands.CreateBudget;

public record CreateBudgetCommand : IRequest<int>
{
    public int CategoryId { get; init; }
    public decimal LimitAmount { get; init; }
}

public class CreateBudgetCommandValidator : AbstractValidator<CreateBudgetCommand>
{
    public CreateBudgetCommandValidator()
    {
        RuleFor(v => v.CategoryId)
            .GreaterThan(0)
            .WithMessage("CategoryId must be greater than 0.");

        RuleFor(v => v.LimitAmount)
            .GreaterThan(0)
            .WithMessage("Limit amount must be greater than 0.");
    }
}

public class CreateBudgetCommandHandler : IRequestHandler<CreateBudgetCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateBudgetCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<int> Handle(CreateBudgetCommand request, CancellationToken cancellationToken)
    {
        // Check if category exists
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category == null)
        {
            throw new Exception($"Category {request.CategoryId} not found.");
        }

        // Check if budget already exists for this category and user
        var existingBudget = await _context.Budgets
            .FirstOrDefaultAsync(b => b.CategoryId == request.CategoryId && b.CreatedBy == _currentUserService.UserId, cancellationToken);

        if (existingBudget != null)
        {
            throw new Exception($"Budget for category '{category.NameEn}' already exists.");
        }

        // Create new budget
        var entity = new Budget(request.CategoryId, request.LimitAmount);

        _context.Budgets.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
