using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Models;
using Wealthra.Domain.Entities;
using Wealthra.Domain.Enums;
using Wealthra.Application.Features.Notifications.Models;

namespace Wealthra.Application.Features.Recommendations.Commands.AnalyzeSpendingAnomalies
{
    public class AnalyzeSpendingAnomaliesCommand : IRequest<List<string>>
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Language { get; set; } = "en";
    }

    public class AnalyzeSpendingAnomaliesCommandValidator : AbstractValidator<AnalyzeSpendingAnomaliesCommand>
    {
        public AnalyzeSpendingAnomaliesCommandValidator()
        {
            RuleFor(v => v.Year)
                .InclusiveBetween(2000, 2100)
                .WithMessage("Year must be between 2000 and 2100.");

            RuleFor(v => v.Month)
                .InclusiveBetween(1, 12)
                .WithMessage("Month must be between 1 and 12.");
        }
    }

    public class AnalyzeSpendingAnomaliesCommandHandler : IRequestHandler<AnalyzeSpendingAnomaliesCommand, List<string>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHeuristicRecommendationService _heuristicRecommendationService;
        private readonly IDisplayCurrencyService _displayCurrencyService;
        private readonly IMonthlyCategoryMetricsCalculator _metricsCalculator;
        private readonly INotificationRealtimeService _notificationRealtimeService;

        public AnalyzeSpendingAnomaliesCommandHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            IHeuristicRecommendationService heuristicRecommendationService,
            IDisplayCurrencyService displayCurrencyService,
            IMonthlyCategoryMetricsCalculator metricsCalculator,
            INotificationRealtimeService notificationRealtimeService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _heuristicRecommendationService = heuristicRecommendationService;
            _displayCurrencyService = displayCurrencyService;
            _metricsCalculator = metricsCalculator;
            _notificationRealtimeService = notificationRealtimeService;
        }

        public async Task<List<string>> Handle(AnalyzeSpendingAnomaliesCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId!;
            var normalizedLanguage = request.Language?.Trim().ToLowerInvariant() ?? "en";
            var isTurkish = normalizedLanguage == "tr";
            var targetMonth = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var generatedAlerts = new List<string>();

            var effective = await _displayCurrencyService.GetEffectiveCurrencyAsync(null, cancellationToken);
            var metrics = await _metricsCalculator.ComputeForMonthAsync(
                userId,
                request.Year,
                request.Month,
                effective,
                cancellationToken);

            var existingAlerts = await _context.Notifications
                .Where(n => n.UserId == userId &&
                            n.Type == NotificationType.Alert &&
                            n.CreatedOn.Year == targetMonth.Year &&
                            n.CreatedOn.Month == targetMonth.Month)
                .ToListAsync(cancellationToken);

            var signalsEn = _heuristicRecommendationService.Evaluate(metrics, "en");
            var signalsTr = _heuristicRecommendationService.Evaluate(metrics, "tr");
            var trSignalsByKey = signalsTr.ToDictionary(GetSignalKey, s => s);

            var newNotifications = new List<Notification>();

            foreach (var signalEn in signalsEn)
            {
                if (!signalEn.CategoryId.HasValue)
                {
                    continue;
                }

                trSignalsByKey.TryGetValue(GetSignalKey(signalEn), out var signalTr);
                var trEvidence = signalTr?.Evidence ?? signalEn.Evidence;

                var msgEn = $"Alert: {signalEn.Evidence}";
                var msgTr = $"Uyari: {trEvidence}";
                var alreadySent = existingAlerts.Any(n =>
                    n.RelatedEntityId == signalEn.CategoryId &&
                    n.MessageEn.Contains(signalEn.ReasonCode));

                if (!alreadySent)
                {
                    var notification = new Notification
                    {
                        UserId = userId,
                        MessageEn = $"{msgEn} [{signalEn.ReasonCode}]",
                        MessageTr = $"{msgTr} [{signalEn.ReasonCode}]",
                        Type = NotificationType.Alert,
                        CreatedOn = DateTime.UtcNow,
                        IsRead = false,
                        RelatedEntityId = signalEn.CategoryId
                    };

                    _context.Notifications.Add(notification);
                    existingAlerts.Add(notification);
                    newNotifications.Add(notification);
                    generatedAlerts.Add(isTurkish ? msgTr : msgEn);
                }
            }

            if (generatedAlerts.Any())
            {
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var n in newNotifications)
                {
                    var dto = new NotificationDto(
                        n.Id,
                        isTurkish ? n.MessageTr : n.MessageEn,
                        n.Type,
                        n.IsRead,
                        n.CreatedOn,
                        n.RelatedEntityId);

                    await _notificationRealtimeService.SendNotificationAsync(userId, dto, cancellationToken);
                }
            }

            if (!generatedAlerts.Any() && existingAlerts.Any())
            {
                return existingAlerts.Select(n => isTurkish ? n.MessageTr : n.MessageEn).ToList();
            }

            return generatedAlerts;
        }

        private static string GetSignalKey(RecommendationSignal signal)
            => $"{signal.ReasonCode}:{signal.CategoryId?.ToString() ?? "none"}";
    }
}
