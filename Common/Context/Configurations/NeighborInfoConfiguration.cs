using Common.Context.Entities;
using Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class NeighborInfoConfiguration : IEntityTypeConfiguration<NeighborInfo>
{
    public void Configure(EntityTypeBuilder<NeighborInfo> builder)
    {
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UpdatedAt);
        
        builder.HasOne(a => a.Node)
            .WithMany(a => a.MyNeighbors)
            .HasForeignKey(a => a.NodeId);
        
        builder.HasOne(a => a.Neighbor)
            .WithMany(a => a.NeighborsFor)
            .HasForeignKey(a => a.NeighborId);
        
        builder.HasOne(a => a.Packet)
            .WithMany()
            .HasForeignKey(a => a.PacketId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasOne(a => a.NodePosition)
            .WithMany()
            .HasForeignKey(a => a.NodePositionId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasOne(a => a.NeighborPosition)
            .WithMany()
            .HasForeignKey(a => a.NeighborPositionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(a => a.DataSource).EnumToString();
        builder.HasIndex(a => a.DataSource);
    }
}