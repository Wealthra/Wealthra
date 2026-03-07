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

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}