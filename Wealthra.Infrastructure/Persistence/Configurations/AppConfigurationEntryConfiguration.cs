using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wealthra.Domain.Entities;

namespace Wealthra.Infrastructure.Persistence.Configurations;

public class AppConfigurationEntryConfiguration : IEntityTypeConfiguration<AppConfigurationEntry>
{
    public void Configure(EntityTypeBuilder<AppConfigurationEntry> builder)
    {
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(256);
        builder.Property(x => x.Value).IsRequired();
    }
}
