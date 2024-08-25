using MeshtasticMqttExplorer.Context.Entities;
using MeshtasticMqttExplorer.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeshtasticMqttExplorer.Context.Configurations;

public class PacketConfiguration : IEntityTypeConfiguration<Packet>
{
    public void Configure(EntityTypeBuilder<Packet> builder)
    {
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.PacketId);
        
        builder.Property(a => a.MqttServer).HasMaxLength(64);
        builder.HasIndex(a => a.MqttServer);
        
        builder.Property(a => a.MqttTopic).HasMaxLength(128);

        builder.Property(a => a.PayloadJson).HasColumnType("TEXT");

        builder.Property(a => a.PortNum).EnumToString(64);
        builder.HasIndex(a => a.PortNum);
        
        builder.Property(a => a.Priority).EnumToString();

        builder.HasOne(a => a.Gateway)
            .WithMany()
            .HasForeignKey(a => a.GatewayId);

        builder.HasOne(a => a.GatewayPosition)
            .WithMany()
            .HasForeignKey(a => a.GatewayPositionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Position)
            .WithMany()
            .HasForeignKey(a => a.PositionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.From)
            .WithMany(a => a.PacketsFrom)
            .HasForeignKey(a => a.FromId);

        builder.HasOne(a => a.To)
            .WithMany(a => a.PacketsTo)
            .HasForeignKey(a => a.ToId);
        
        builder.HasOne(a => a.Channel)
            .WithMany(a => a.Packets)
            .HasForeignKey(a => a.ChannelId);
    }
}