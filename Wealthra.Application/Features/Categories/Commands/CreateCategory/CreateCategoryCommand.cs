using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Categories.Commands.CreateCategory;

public record CreateCategoryCommand(string NameEn, string NameTr) : IRequest<int>;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(v => v.NameEn)
            .NotEmpty().WithMessage("English category name is required.")
            .MaximumLength(100).WithMessage("English category name must not exceed 100 characters.");

        RuleFor(v => v.NameTr)
            .NotEmpty().WithMessage("Turkish category name is required.")
            .MaximumLength(100).WithMessage("Turkish category name must not exceed 100 characters.");
    }
}

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private const string CacheKey = "categories_all";

    public CreateCategoryCommandHandler(IApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<int> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = new Category(request.NameEn, request.NameTr);

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKey, cancellationToken);

        return category.Id;
    }
}
