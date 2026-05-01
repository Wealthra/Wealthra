using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.ListApiErrorLogs;

public record ListApiErrorLogsQuery(int Skip = 0, int Take = 50, int? StatusCode = null)
    : IRequest<List<ApiErrorLogDto>>;

public class ListApiErrorLogsQueryHandler : IRequestHandler<ListApiErrorLogsQuery, List<ApiErrorLogDto>>
{
    private readonly IApplicationDbContext _db;

    public ListApiErrorLogsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ApiErrorLogDto>> Handle(ListApiErrorLogsQuery request, CancellationToken cancellationToken)
    {
        var q = _db.ApiErrorLogs.AsNoTracking().OrderByDescending(x => x.CreatedUtc);
        var filtered = request.StatusCode.HasValue
            ? q.Where(x => x.StatusCode == request.StatusCode.Value)
            : q;

        return await filtered
            .Skip(request.Skip)
            .Take(Math.Clamp(request.Take, 1, 200))
            .Select(x => new ApiErrorLogDto(
                x.Id,
                x.StatusCode,
                x.Path,
                x.Method,
                x.UserId,
                x.ExceptionType,
                x.Message,
                x.CorrelationId,
                x.CreatedUtc))
            .ToListAsync(cancellationToken);
    }
}
