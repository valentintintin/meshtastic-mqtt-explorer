using MeshtasticMqttExplorer.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeshtasticMqttExplorer.Context.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UpdatedAt);
        
        builder.HasOne(a => a.Node)
            .WithMany(a => a.Positions)
            .HasForeignKey(a => a.NodeId);
        
        builder.HasOne(a => a.Packet)
            .WithMany()
            .HasForeignKey(a => a.PacketId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}