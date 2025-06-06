using Common.Context.Entities;
using Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class PacketConfiguration : IEntityTypeConfiguration<Packet>
{
    public void Configure(EntityTypeBuilder<Packet> builder)
    {
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.PacketId);
        
        builder.Property(a => a.MqttTopic).HasMaxLength(128);

        builder.Property(a => a.PayloadJson).HasColumnType("TEXT");

        builder.Property(a => a.PortNum).EnumToString(64);
        builder.HasIndex(a => a.PortNum);
        
        builder.Property(a => a.PortNumVariant).HasMaxLength(128);
        builder.HasIndex(a => a.PortNumVariant);
        
        builder.Property(a => a.Priority).EnumToString();

        builder.HasOne(a => a.Gateway)
            .WithMany(a => a.PacketsGateway)
            .HasForeignKey(a => a.GatewayId)
            .OnDelete(DeleteBehavior.Cascade);

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
            .HasForeignKey(a => a.FromId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.To)
            .WithMany(a => a.PacketsTo)
            .HasForeignKey(a => a.ToId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(a => a.Channel)
            .WithMany(a => a.Packets)
            .HasForeignKey(a => a.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.RelayNodeNode)
            .WithMany(a => a.RelayFor)
            .HasForeignKey(a => a.RelayNodeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.NextHopNode)
            .WithMany(a => a.NextHopFor)
            .HasForeignKey(a => a.NextHopId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasOne(a => a.PacketDuplicated)
            .WithMany(a => a.AllDuplicatedPackets)
            .HasForeignKey(a => a.PacketDuplicatedId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.MqttServer)
            .WithMany(a => a.Packets)
            .HasForeignKey(a => a.MqttServerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}