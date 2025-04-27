using Common.Context.Entities;
using Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class TelemetryConfiguration : IEntityTypeConfiguration<Telemetry>
{
    public void Configure(EntityTypeBuilder<Telemetry> builder)
    {
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UpdatedAt);

        builder.Property(a => a.Type).EnumToString(32);
        builder.HasIndex(a => a.Type);
        
        builder.HasOne(a => a.Node)
            .WithMany(a => a.Telemetries)
            .HasForeignKey(a => a.NodeId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(a => a.Packet)
            .WithMany()
            .HasForeignKey(a => a.PacketId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}