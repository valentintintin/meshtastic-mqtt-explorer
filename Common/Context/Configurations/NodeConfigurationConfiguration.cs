using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class NodeConfigurationConfiguration : IEntityTypeConfiguration<Entities.NodeConfiguration>
{
    public void Configure(EntityTypeBuilder<Entities.NodeConfiguration> builder)
    {
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UpdatedAt);

        builder.Property(a => a.MqttId).HasMaxLength(512);
        builder.HasIndex(a => a.MqttId);

        builder.HasOne(a => a.Node)
            .WithOne()
            .HasForeignKey<Entities.NodeConfiguration>(a => a.NodeId);
        
        builder.HasOne(a => a.User)
            .WithMany(a => a.NodeConfigurations)
            .HasForeignKey(a => a.UserId);
    }
}