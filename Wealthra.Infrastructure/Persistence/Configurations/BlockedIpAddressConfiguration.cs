using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wealthra.Domain.Entities;

namespace Wealthra.Infrastructure.Persistence.Configurations;

public class BlockedIpAddressConfiguration : IEntityTypeConfiguration<BlockedIpAddress>
{
    public void Configure(EntityTypeBuilder<BlockedIpAddress> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.IpAddress).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.IpAddress).IsUnique();
    }
}
