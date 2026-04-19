using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wealthra.Domain.Entities;

namespace Wealthra.Infrastructure.Persistence.Configurations
{
    public class FinancialTipConfiguration : IEntityTypeConfiguration<FinancialTip>
    {
        public void Configure(EntityTypeBuilder<FinancialTip> builder)
        {
            builder.ToTable("FinancialTips");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Topic)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Body)
                .HasMaxLength(2000)
                .IsRequired();

            builder.Property(x => x.Locale)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(x => x.Tags)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property<string>("Embedding")
                .HasColumnType("vector(16)");
        }
    }
}
