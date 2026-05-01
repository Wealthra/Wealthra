using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Categories.Commands.UpdateCategory;

public record UpdateCategoryCommand(int Id, string CategoryName, string? IconKey = null, int? SortOrder = null, bool? IsActive = null) : IRequest;

public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(v => v.Id)
            .GreaterThan(0).WithMessage("Category ID must be greater than 0.");

        RuleFor(v => v.CategoryName)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(100).WithMessage("Category name must not exceed 100 characters.");
    }
}

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private const string CacheKey = "categories_all";

    public UpdateCategoryCommandHandler(IApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category == null)
        {
            throw new NotFoundException(nameof(Category), request.Id);
        }

        var name = request.CategoryName.Trim();
        category.UpdateNames(name, name);
        if (request.SortOrder.HasValue || request.IconKey != null || request.IsActive.HasValue)
        {
            category.UpdateDisplay(
                request.IconKey?.Trim(),
                request.SortOrder ?? category.SortOrder,
                request.IsActive ?? category.IsActive);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKey, cancellationToken);
    }
}
