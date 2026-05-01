using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wealthra.Domain.Entities;

namespace Wealthra.Infrastructure.Persistence.Configurations;

public class SystemAnnouncementConfiguration : IEntityTypeConfiguration<SystemAnnouncement>
{
    public void Configure(EntityTypeBuilder<SystemAnnouncement> builder)
    {
        builder.Property(x => x.TitleEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TitleTr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BodyEn).IsRequired();
        builder.Property(x => x.BodyTr).IsRequired();
        builder.HasIndex(x => x.StartsAt);
        builder.HasIndex(x => x.EndsAt);
    }
}
