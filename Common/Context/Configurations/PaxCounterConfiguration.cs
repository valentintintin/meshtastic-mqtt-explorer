using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class PaxCounterConfiguration : IEntityTypeConfiguration<PaxCounter>
{
    public void Configure(EntityTypeBuilder<PaxCounter> builder)
    {
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UpdatedAt);
        
        builder.HasOne(a => a.Node)
            .WithMany(a => a.PaxCounters)
            .HasForeignKey(a => a.NodeId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(a => a.Packet)
            .WithMany()
            .HasForeignKey(a => a.PacketId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}