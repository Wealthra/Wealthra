using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<Budget> Budgets { get; }
        DbSet<Category> Categories { get; }
        DbSet<Expense> Expenses { get; }
        DbSet<Income> Incomes { get; }
        DbSet<Goal> Goals { get; }
        DbSet<Notification> Notifications { get; }
        DbSet<MonthlyCategoryMetric> MonthlyCategoryMetrics { get; }
        DbSet<FinancialTip> FinancialTips { get; }
        DbSet<SubscriptionPlan> SubscriptionPlans { get; }
        DbSet<AppConfigurationEntry> AppConfigurationEntries { get; }
        DbSet<AdminAuditLog> AdminAuditLogs { get; }
        DbSet<ApiErrorLog> ApiErrorLogs { get; }
        DbSet<AiUsageRecord> AiUsageRecords { get; }
        DbSet<UsageDailyAggregate> UsageDailyAggregates { get; }
        DbSet<SystemAnnouncement> SystemAnnouncements { get; }
        DbSet<SupportTicket> SupportTickets { get; }
        DbSet<ManualExchangeRate> ManualExchangeRates { get; }
        DbSet<BlockedIpAddress> BlockedIpAddresses { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}