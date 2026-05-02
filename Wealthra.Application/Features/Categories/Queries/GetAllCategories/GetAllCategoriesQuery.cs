using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Categories.Models;
using static Wealthra.Application.Features.Categories.CategoryListCacheKeys;

namespace Wealthra.Application.Features.Categories.Queries.GetAllCategories;

public record GetAllCategoriesQuery(CategoryDisplayLanguage Language = CategoryDisplayLanguage.English)
    : IRequest<List<CategoryDto>>;

public class GetAllCategoriesQueryHandler : IRequestHandler<GetAllCategoriesQuery, List<CategoryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public GetAllCategoriesQueryHandler(IApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<List<CategoryDto>> Handle(GetAllCategoriesQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = request.Language == CategoryDisplayLanguage.Turkish ? Turkish : English;

        var cachedCategories = await _cacheService.GetAsync<List<CategoryDto>>(cacheKey, cancellationToken);
        if (cachedCategories != null)
        {
            return cachedCategories;
        }

        var useTurkish = request.Language == CategoryDisplayLanguage.Turkish;
        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => useTurkish ? c.NameTr : c.NameEn)
            .Select(c => new CategoryDto(
                c.Id,
                useTurkish ? c.NameTr : c.NameEn,
                c.IconKey,
                c.SortOrder))
            .ToListAsync(cancellationToken);

        await _cacheService.SetAsync(cacheKey, categories, null, cancellationToken);

        return categories;
    }
}
