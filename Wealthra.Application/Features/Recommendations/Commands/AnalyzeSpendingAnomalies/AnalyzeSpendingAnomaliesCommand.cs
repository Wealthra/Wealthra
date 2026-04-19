using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Models;
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
        private readonly IHeuristicRecommendationService _heuristicRecommendationService;

        public AnalyzeSpendingAnomaliesCommandHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            IHeuristicRecommendationService heuristicRecommendationService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _heuristicRecommendationService = heuristicRecommendationService;
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

            // Fetch existing alerts for this month to prevent spamming
            var existingAlerts = await _context.Notifications
                .Where(n => n.UserId == userId && 
                            n.Type == NotificationType.Alert && 
                            n.CreatedOn.Year == targetMonth.Year && 
                            n.CreatedOn.Month == targetMonth.Month)
                .ToListAsync(cancellationToken);

            var signals = _heuristicRecommendationService.Evaluate(metrics);
            foreach (var signal in signals)
            {
                if (!signal.CategoryId.HasValue)
                {
                    continue;
                }

                var msgEn = $"Alert: {signal.Evidence}";
                var msgTr = $"Uyarı: {signal.Evidence}";
                var alreadySent = existingAlerts.Any(n =>
                    n.RelatedEntityId == signal.CategoryId &&
                    n.MessageEn.Contains(signal.ReasonCode));

                if (!alreadySent)
                {
                    var notification = new Notification
                    {
                        UserId = userId,
                        MessageEn = $"{msgEn} [{signal.ReasonCode}]",
                        MessageTr = $"{msgTr} [{signal.ReasonCode}]",
                        Type = NotificationType.Alert,
                        CreatedOn = DateTime.UtcNow,
                        IsRead = false,
                        RelatedEntityId = signal.CategoryId
                    };

                    _context.Notifications.Add(notification);
                    existingAlerts.Add(notification);
                    generatedAlerts.Add(msgEn);
                }
            }

            if (generatedAlerts.Any())
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            // If no new alerts were generated this run, return existing alerts for this month so the client sees them
            if (!generatedAlerts.Any() && existingAlerts.Any())
            {
                return existingAlerts.Select(n => n.MessageEn).ToList();
            }

            return generatedAlerts;
        }
    }
}
