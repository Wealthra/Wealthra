using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wealthra.Domain.Entities;

namespace Wealthra.Infrastructure.Persistence.Configurations
{
    public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
    {
        public void Configure(EntityTypeBuilder<Budget> builder)
        {
            // Set Table Name
            builder.ToTable("Budgets");

            // Key
            builder.HasKey(x => x.Id);

            // Properties
            builder.Property(x => x.LimitAmount)
                .HasColumnType("decimal(18,6)") // Specific SQL type
                .IsRequired();

            builder.Property(x => x.CurrentAmount)
                .HasColumnType("decimal(18,6)")
                .IsRequired();

            // Relationships
            builder.HasOne(x => x.Category)
                .WithMany(x => x.Budgets)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting a Category if it has Budgets
        }
    }
}