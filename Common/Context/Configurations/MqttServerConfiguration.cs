using Common.Context.Entities;
using Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class MqttServerConfiguration : IEntityTypeConfiguration<MqttServer>
{
    public void Configure(EntityTypeBuilder<MqttServer> builder)
    {
        builder.HasIndex(a => a.Name);
        builder.Property(a => a.Name).HasMaxLength(128);
        
        builder.Property(a => a.Host).HasMaxLength(128);
        
        builder.Property(a => a.Username).HasMaxLength(128);
        
        builder.Property(a => a.Password).HasMaxLength(128);
        
        builder.Property(a => a.Topics).ListOfString().HasMaxLength(128);
        
        builder.Property(a => a.IsARelayType).EnumToStringNotRequired(32);
    }
}