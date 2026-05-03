using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;
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

            // Mapped as Pgvector.Vector so Npgsql can read/write the column; inserts still omit it (DB default + seeder UPDATE).
            var embeddingProperty = builder.Property<Vector>("Embedding")
                .HasColumnType("vector(16)");
            embeddingProperty.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            embeddingProperty.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        }
    }
}
