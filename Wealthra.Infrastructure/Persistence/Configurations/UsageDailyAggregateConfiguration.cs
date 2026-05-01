using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wealthra.Domain.Entities;

namespace Wealthra.Infrastructure.Persistence.Configurations;

public class UsageDailyAggregateConfiguration : IEntityTypeConfiguration<UsageDailyAggregate>
{
    public void Configure(EntityTypeBuilder<UsageDailyAggregate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.DateUtc }).IsUnique();
    }
}
