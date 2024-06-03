using MeshtasticMqttExplorer.Context.Entities;
using MeshtasticMqttExplorer.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeshtasticMqttExplorer.Context.Configurations;

public class TelemetryConfiguration : IEntityTypeConfiguration<Telemetry>
{
    public void Configure(EntityTypeBuilder<Telemetry> builder)
    {
        builder.HasIndex(a => a.CreatedAt);

        builder.Property(a => a.Type).EnumToString(32);
        builder.HasIndex(a => a.Type);
        
        builder.HasOne(a => a.Node)
            .WithMany(a => a.Telemetries)
            .HasForeignKey(a => a.NodeId);
        
        builder.HasOne(a => a.Packet)
            .WithMany()
            .HasForeignKey(a => a.PacketId);
    }
}