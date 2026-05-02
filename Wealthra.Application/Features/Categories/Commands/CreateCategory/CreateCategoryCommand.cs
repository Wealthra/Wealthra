using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Categories;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Categories.Commands.CreateCategory;

public record CreateCategoryCommand(string CategoryName, string? IconKey = null, int SortOrder = 0) : IRequest<int>;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(v => v.CategoryName)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(100).WithMessage("Category name must not exceed 100 characters.");
    }
}

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public CreateCategoryCommandHandler(IApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<int> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var name = request.CategoryName.Trim();
        var category = new Category(name, name);
        category.UpdateDisplay(request.IconKey?.Trim(), request.SortOrder, isActive: true);

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CategoryListCacheKeys.English, cancellationToken);
        await _cacheService.RemoveAsync(CategoryListCacheKeys.Turkish, cancellationToken);

        return category.Id;
    }
}
