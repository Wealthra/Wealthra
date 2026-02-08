using MediatR;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Common;
using Wealthra.Domain.Entities;
using Wealthra.Infrastructure.Identity.Models;
using Wealthra.Infrastructure.Services;

namespace Wealthra.Infrastructure.Persistence
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
    {
        private readonly DateTimeService _dateTimeService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPublisher _publisher;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            DateTimeService dateTimeService,
            ICurrentUserService currentUserService,
            IPublisher publisher) : base(options)
        {
            _dateTimeService = dateTimeService;
            _currentUserService = currentUserService;
            _publisher = publisher;
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
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            // 1. Handle Auditing
            foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedOn = _dateTimeService.NowUtc;
                    entry.Entity.CreatedBy = _currentUserService.UserId ?? "System";
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.LastModifiedOn = _dateTimeService.NowUtc;
                    entry.Entity.LastModifiedBy = _currentUserService.UserId ?? "System";
                }
            }

            // 2. Save Data
            var result = await base.SaveChangesAsync(cancellationToken);

            // 3. Dispatch Domain Events (After successful save)
            await DispatchDomainEvents(cancellationToken);

            return result;
        }

        private async Task DispatchDomainEvents(CancellationToken cancellationToken)
        {
            var entitiesWithEvents = ChangeTracker.Entries<BaseEntity>()
                .Where(e => e.Entity.DomainEvents.Any())
                .Select(e => e.Entity);

            var domainEvents = entitiesWithEvents
                .SelectMany(e => e.DomainEvents)
                .ToList();

            foreach (var entity in entitiesWithEvents)
                entity.ClearDomainEvents();

            foreach (var domainEvent in domainEvents)
                await _publisher.Publish(domainEvent, cancellationToken);
        }
    }
}