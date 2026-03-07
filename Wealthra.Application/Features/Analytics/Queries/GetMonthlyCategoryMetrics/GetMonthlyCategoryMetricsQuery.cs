using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Analytics.Queries.GetMonthlyCategoryMetrics
{
    public class GetMonthlyCategoryMetricsQuery : IRequest<List<MonthlyCategoryMetricDto>>
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public class MonthlyCategoryMetricDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public decimal TotalSpend { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal SpendPercentageOfIncome { get; set; }
        public decimal PreviousMonthSpend { get; set; }
    }

    public class GetMonthlyCategoryMetricsQueryHandler : IRequestHandler<GetMonthlyCategoryMetricsQuery, List<MonthlyCategoryMetricDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GetMonthlyCategoryMetricsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<List<MonthlyCategoryMetricDto>> Handle(GetMonthlyCategoryMetricsQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            
            // Generate the first day of the requested month
            var targetMonth = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var metrics = await _context.MonthlyCategoryMetrics
                .Where(m => m.UserId == userId && m.Month == targetMonth)
                .Select(m => new MonthlyCategoryMetricDto
                {
                    CategoryId = m.CategoryId,
                    CategoryName = m.CategoryName,
                    TotalSpend = m.TotalSpend,
                    TotalIncome = m.TotalIncome,
                    SpendPercentageOfIncome = m.SpendPercentageOfIncome,
                    PreviousMonthSpend = m.PreviousMonthSpend
                })
                .ToListAsync(cancellationToken);

            return metrics;
        }
    }
}
