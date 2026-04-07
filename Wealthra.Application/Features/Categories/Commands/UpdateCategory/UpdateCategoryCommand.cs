using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Categories.Commands.UpdateCategory;

public record UpdateCategoryCommand(int Id, string NameEn, string NameTr) : IRequest;

public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(v => v.Id)
            .GreaterThan(0).WithMessage("Category ID must be greater than 0.");

        RuleFor(v => v.NameEn)
            .NotEmpty().WithMessage("English category name is required.")
            .MaximumLength(100).WithMessage("English category name must not exceed 100 characters.");

        RuleFor(v => v.NameTr)
            .NotEmpty().WithMessage("Turkish category name is required.")
            .MaximumLength(100).WithMessage("Turkish category name must not exceed 100 characters.");
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

        category.UpdateNames(request.NameEn, request.NameTr);

        await _context.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKey, cancellationToken);
    }
}
