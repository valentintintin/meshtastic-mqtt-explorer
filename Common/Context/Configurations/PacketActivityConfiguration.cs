using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class PacketActivityConfiguration : IEntityTypeConfiguration<PacketActivity>
{
    public void Configure(EntityTypeBuilder<PacketActivity> builder)
    {
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UpdatedAt);

        builder.HasOne(a => a.NodeConfiguration)
            .WithMany(a => a.PacketActivities)
            .HasForeignKey(a => a.NodeConfigurationId);
    }
}