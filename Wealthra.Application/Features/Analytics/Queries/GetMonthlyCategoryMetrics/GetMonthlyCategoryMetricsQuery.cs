using MediatR;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Analytics.Queries.GetMonthlyCategoryMetrics
{
    public class GetMonthlyCategoryMetricsQuery : IRequest<List<MonthlyCategoryMetricDto>>
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string? TargetCurrency { get; set; }
    }

    public class MonthlyCategoryMetricDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal TotalSpend { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal SpendPercentageOfIncome { get; set; }
        public decimal PreviousMonthSpend { get; set; }
        public string Currency { get; set; } = "TRY";
    }

    public class GetMonthlyCategoryMetricsQueryHandler : IRequestHandler<GetMonthlyCategoryMetricsQuery, List<MonthlyCategoryMetricDto>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IDisplayCurrencyService _displayCurrencyService;
        private readonly IMonthlyCategoryMetricsCalculator _metricsCalculator;

        public GetMonthlyCategoryMetricsQueryHandler(
            ICurrentUserService currentUserService,
            IDisplayCurrencyService displayCurrencyService,
            IMonthlyCategoryMetricsCalculator metricsCalculator)
        {
            _currentUserService = currentUserService;
            _displayCurrencyService = displayCurrencyService;
            _metricsCalculator = metricsCalculator;
        }

        public async Task<List<MonthlyCategoryMetricDto>> Handle(GetMonthlyCategoryMetricsQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId!;
            var effective = await _displayCurrencyService.GetEffectiveCurrencyAsync(request.TargetCurrency, cancellationToken);

            var metrics = await _metricsCalculator.ComputeForMonthAsync(
                userId,
                request.Year,
                request.Month,
                effective,
                cancellationToken);

            return metrics.ConvertAll(m => new MonthlyCategoryMetricDto
            {
                CategoryId = m.CategoryId,
                CategoryName = m.CategoryName,
                TotalSpend = m.TotalSpend,
                TotalIncome = m.TotalIncome,
                SpendPercentageOfIncome = m.SpendPercentageOfIncome,
                PreviousMonthSpend = m.PreviousMonthSpend,
                Currency = effective
            });
        }
    }
}
