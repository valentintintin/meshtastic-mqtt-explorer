using Common.Context.Entities.Router;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations.Router;

public class PacketActivityConfiguration : IEntityTypeConfiguration<PacketActivity>
{
    public void Configure(EntityTypeBuilder<PacketActivity> builder)
    {
        builder.ToTable("PacketActivities", "router");
        
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UpdatedAt);

        builder.HasOne(a => a.NodeConfiguration)
            .WithMany(a => a.PacketActivities)
            .HasForeignKey(a => a.NodeConfigurationId);
    }
}