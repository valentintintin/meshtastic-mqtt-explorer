using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class TracerouteConfiguration : IEntityTypeConfiguration<Traceroute>
{
    public void Configure(EntityTypeBuilder<Traceroute> builder)
    {
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UpdatedAt);
        
        builder.HasOne(a => a.From)
            .WithMany(a => a.TraceroutesFrom)
            .HasForeignKey(a => a.NodeFromId);
        
        builder.HasOne(a => a.To)
            .WithMany(a => a.TraceroutesTo)
            .HasForeignKey(a => a.NodeToId);
        
        builder.HasOne(a => a.Node)
            .WithMany(a => a.TraceroutesPart)
            .HasForeignKey(a => a.NodeId);
        
        builder.HasOne(a => a.Packet)
            .WithMany()
            .HasForeignKey(a => a.PacketId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}