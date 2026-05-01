using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wealthra.Domain.Entities;

namespace Wealthra.Infrastructure.Persistence.Configurations;

public class ApiErrorLogConfiguration : IEntityTypeConfiguration<ApiErrorLog>
{
    public void Configure(EntityTypeBuilder<ApiErrorLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Path).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Method).HasMaxLength(16).IsRequired();
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.ExceptionType).HasMaxLength(500);
        builder.Property(x => x.Message).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.HasIndex(x => x.CreatedUtc);
        builder.HasIndex(x => x.StatusCode);
    }
}
