using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wealthra.Domain.Entities;

namespace Wealthra.Infrastructure.Persistence.Configurations
{
    public class IncomeConfiguration : IEntityTypeConfiguration<Income>
    {
        public void Configure(EntityTypeBuilder<Income> builder)
        {
            // Set Table Name
            builder.ToTable("Incomes");

            // Key
            builder.HasKey(x => x.Id);

            // Properties
            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Amount)
                .HasColumnType("decimal(18,6)")
                .IsRequired();

            builder.Property(x => x.Method)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.IsRecurring)
                .IsRequired();

            builder.Property(x => x.TransactionDate)
                .IsRequired();

            // Index for faster queries by User
            builder.HasIndex(x => x.CreatedBy);

            // Index for reporting (TransactionDate)
            builder.HasIndex(x => x.TransactionDate);
        }
    }
}