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
        
        builder.HasOne(a => a.NodeReceiver)
            .WithMany(a => a.MyNeighbors)
            .HasForeignKey(a => a.NodeReceiverId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(a => a.NodeHeard)
            .WithMany(a => a.NeighborsFor)
            .HasForeignKey(a => a.NodeHeardId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(a => a.Packet)
            .WithMany()
            .HasForeignKey(a => a.PacketId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasOne(a => a.NodeReceiverPosition)
            .WithMany()
            .HasForeignKey(a => a.NodeReceiverPositionId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasOne(a => a.NodeHeardPosition)
            .WithMany()
            .HasForeignKey(a => a.NodeHeardPositionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(a => a.DataSource).EnumToString();
        builder.HasIndex(a => a.DataSource);
    }
}