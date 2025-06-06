using Common.Context.Entities;
using Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class NodeConfiguration : IEntityTypeConfiguration<Node>
{
    public void Configure(EntityTypeBuilder<Node> builder)
    {
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UpdatedAt);
        
        builder.HasIndex(a => a.NodeId);
        
        builder.Property(a => a.NodeIdString).HasMaxLength(16);
        builder.HasIndex(a => a.NodeIdString);
        
        builder.Property(a => a.AllNames).HasMaxLength(256);
        builder.HasIndex(a => a.AllNames);
        
        builder.Property(a => a.OldAllNames).HasMaxLength(256);

        builder.Property(a => a.LongName).HasMaxLength(128);
        builder.HasIndex(a => a.LongName);
        
        builder.Property(a => a.ShortName).HasMaxLength(4);
        builder.HasIndex(a => a.ShortName);
        
        builder.Property(a => a.FirmwareVersion).HasMaxLength(16);
        
        builder.Property(a => a.HardwareModel).EnumToStringNotRequired(64);
        
        builder.Property(a => a.RegionCode).EnumToStringNotRequired(64);
        builder.HasIndex(a => a.RegionCode);
        
        builder.Property(a => a.ModemPreset).EnumToStringNotRequired(64);
        builder.HasIndex(a => a.ModemPreset);
        
        builder.Property(a => a.Role).EnumToStringNotRequired(32);
        builder.HasIndex(a => a.Role);
        
        builder.HasIndex(a => a.LastSeen).IsDescending();
        
        builder.Property(a => a.PrimaryChannel).HasMaxLength(32);

        builder.HasOne(a => a.MqttServer)
            .WithMany(a => a.Nodes)
            .HasForeignKey(a => a.MqttServerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}