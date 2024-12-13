using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations.Router;

public class NodeConfigurationConfiguration : IEntityTypeConfiguration<Entities.Router.NodeConfiguration>
{
    public void Configure(EntityTypeBuilder<Entities.Router.NodeConfiguration> builder)
    {
        builder.ToTable("NodeConfigurations", "router");
        
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UpdatedAt);

        builder.Property(a => a.MqttId).HasMaxLength(512);
        builder.HasIndex(a => a.MqttId);

        builder.HasIndex(a => a.Department);
        builder.Property(a => a.Department).HasMaxLength(2);

        builder.HasOne(a => a.Node)
            .WithOne(a => a.NodeConfiguration)
            .HasForeignKey<Entities.Router.NodeConfiguration>(a => a.NodeId);
        
        builder.HasOne(a => a.User)
            .WithMany(a => a.NodeConfigurations)
            .HasForeignKey(a => a.UserId);
    }
}