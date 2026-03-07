using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;
using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.Recommendations.Commands.AnalyzeSpendingAnomalies
{
    public class AnalyzeSpendingAnomaliesCommand : IRequest<List<string>>
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public class AnalyzeSpendingAnomaliesCommandHandler : IRequestHandler<AnalyzeSpendingAnomaliesCommand, List<string>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public AnalyzeSpendingAnomaliesCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<List<string>> Handle(AnalyzeSpendingAnomaliesCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            var targetMonth = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var generatedAlerts = new List<string>();

            // Fetch current month's calculated metrics from the SQL View
            var metrics = await _context.MonthlyCategoryMetrics
                .Where(m => m.UserId == userId && m.Month == targetMonth)
                .ToListAsync(cancellationToken);

            foreach (var metric in metrics)
            {
                // Heuristic 1: High Percentage of Total Income
                // Flag if any category (except perhaps known large ones like Housing, but keeping generic for now) takes > 30% of income.
                if (metric.SpendPercentageOfIncome > 30)
                {
                    var msg = $"Uyarı: '{metric.CategoryName}' kategorisindeki harcamalarınız bu ay toplam gelirinizin %{Math.Round(metric.SpendPercentageOfIncome, 1)}'ini oluşturuyor.";
                    
                    var notification = new Notification
                    {
                        UserId = userId,
                        Message = msg,
                        Type = NotificationType.Alert,
                        CreatedOn = DateTime.UtcNow,
                        IsRead = false,
                        RelatedEntityId = metric.CategoryId
                    };

                    _context.Notifications.Add(notification);
                    generatedAlerts.Add(msg);
                }

                // Heuristic 2: Month-over-Month Spike
                // Flag if spend increased by > 50% compared to previous month
                if (metric.PreviousMonthSpend > 0)
                {
                    var increaseRatio = metric.TotalSpend / metric.PreviousMonthSpend;
                    if (increaseRatio > 1.5m)
                    {
                        var percentageIncrease = (increaseRatio - 1) * 100;
                        var msg = $"Uyarı: '{metric.CategoryName}' kategorisindeki harcamalarınız geçen aya göre %{Math.Round(percentageIncrease, 1)} artış gösterdi.";
                        
                        var notification = new Notification
                        {
                            UserId = userId,
                            Message = msg,
                            Type = NotificationType.Alert,
                            CreatedOn = DateTime.UtcNow,
                            IsRead = false,
                            RelatedEntityId = metric.CategoryId
                        };

                        _context.Notifications.Add(notification);
                        generatedAlerts.Add(msg);
                    }
                }
            }

            if (generatedAlerts.Any())
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return generatedAlerts;
        }
    }
}
