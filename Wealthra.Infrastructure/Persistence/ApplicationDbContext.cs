using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Common;
using Wealthra.Domain.Entities;
using Wealthra.Infrastructure.Identity.Models;
using Wealthra.Infrastructure.Services; // For DateTimeService

namespace Wealthra.Infrastructure.Persistence
{
    // Inherit from IdentityDbContext to get User/Role tables automatically
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
    {
        private readonly DateTimeService _dateTimeService;
        // We will inject CurrentUserService here later to get the UserId

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            DateTimeService dateTimeService) : base(options)
        {
            _dateTimeService = dateTimeService;
        }

        public DbSet<Budget> Budgets { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Income> Incomes { get; set; }
        public DbSet<Goal> Goals { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Scans the current assembly for all files implementing IEntityTypeConfiguration
            // This applies BudgetConfiguration, ExpenseConfiguration, etc. automatically.
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // Global Query Filter for Soft Delete (if we add IsDeleted later)
            // builder.Entity<BaseEntity>().HasQueryFilter(e => !e.IsDeleted);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedOn = _dateTimeService.NowUtc;
                        // entry.Entity.CreatedBy = _currentUserService.UserId; // To be implemented
                        break;

                    case EntityState.Modified:
                        entry.Entity.LastModifiedOn = _dateTimeService.NowUtc;
                        // entry.Entity.LastModifiedBy = _currentUserService.UserId; // To be implemented
                        break;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}