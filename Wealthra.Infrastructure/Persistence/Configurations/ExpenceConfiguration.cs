using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wealthra.Domain.Entities;

namespace Wealthra.Infrastructure.Persistence.Configurations
{
    public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
    {
        public void Configure(EntityTypeBuilder<Expense> builder)
        {
            builder.ToTable("Expenses");

            builder.Property(x => x.Amount)
                .HasColumnType("decimal(18,6)")
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            // Index for faster queries by User
            builder.HasIndex(x => x.CreatedBy);

            // Index for reporting (Category + Date)
            builder.HasIndex(x => new { x.CategoryId, x.CreatedOn });
        }
    }
}