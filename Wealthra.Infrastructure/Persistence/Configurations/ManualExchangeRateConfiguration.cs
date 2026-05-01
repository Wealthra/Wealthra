using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wealthra.Domain.Entities;

namespace Wealthra.Infrastructure.Persistence.Configurations;

public class ManualExchangeRateConfiguration : IEntityTypeConfiguration<ManualExchangeRate>
{
    public void Configure(EntityTypeBuilder<ManualExchangeRate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.FromCurrency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.ToCurrency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Rate).HasPrecision(18, 8);
        builder.HasIndex(x => new { x.FromCurrency, x.ToCurrency }).IsUnique();
    }
}
