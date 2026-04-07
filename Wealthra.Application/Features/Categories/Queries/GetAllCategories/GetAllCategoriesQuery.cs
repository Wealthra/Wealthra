using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Categories.Models;

namespace Wealthra.Application.Features.Categories.Queries.GetAllCategories;

public record GetAllCategoriesQuery : IRequest<List<CategoryDto>>;

public class GetAllCategoriesQueryHandler : IRequestHandler<GetAllCategoriesQuery, List<CategoryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private const string CacheKey = "categories_all";

    public GetAllCategoriesQueryHandler(IApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<List<CategoryDto>> Handle(GetAllCategoriesQuery request, CancellationToken cancellationToken)
    {
        var cachedCategories = await _cacheService.GetAsync<List<CategoryDto>>(CacheKey, cancellationToken);
        if (cachedCategories != null)
        {
            return cachedCategories;
        }

        var categories = await _context.Categories
            .OrderBy(c => c.NameEn)
            .Select(c => new CategoryDto(c.Id, c.NameEn))
            .ToListAsync(cancellationToken);

        await _cacheService.SetAsync(CacheKey, categories, null, cancellationToken);

        return categories;
    }
}
