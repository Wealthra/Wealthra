using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wealthra.Domain.Entities;

namespace Wealthra.Infrastructure.Persistence.Configurations
{
    public class GoalConfiguration : IEntityTypeConfiguration<Goal>
    {
        public void Configure(EntityTypeBuilder<Goal> builder)
        {
            // Set Table Name
            builder.ToTable("Goals");

            // Key
            builder.HasKey(x => x.Id);

            // Properties
            builder.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.TargetAmount)
                .HasColumnType("decimal(18,6)")
                .IsRequired();

            builder.Property(x => x.CurrentAmount)
                .HasColumnType("decimal(18,6)")
                .IsRequired();

            builder.Property(x => x.Deadline)
                .IsRequired();

            // Index for faster queries by User
            builder.HasIndex(x => x.CreatedBy);

            // Index for deadline tracking
            builder.HasIndex(x => x.Deadline);
        }
    }
}