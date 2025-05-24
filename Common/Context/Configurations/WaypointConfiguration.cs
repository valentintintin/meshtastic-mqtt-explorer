using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class WaypointConfiguration : IEntityTypeConfiguration<Waypoint>
{
    public void Configure(EntityTypeBuilder<Waypoint> builder)
    {
        builder.HasIndex(a => a.UpdatedAt);
        builder.HasIndex(a => a.ExpiresAt);
        
        builder.HasIndex(a => a.WaypointId);

        builder.Property(a => a.Name).HasMaxLength(30);
        
        builder.Property(a => a.Description).HasMaxLength(100);
        
        builder.HasOne(a => a.Node)
            .WithMany(a => a.Waypoints)
            .HasForeignKey(a => a.NodeId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(a => a.Packet)
            .WithMany()
            .HasForeignKey(a => a.PacketId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}