using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class WebhookConfiguration : IEntityTypeConfiguration<Webhook>
{
    public void Configure(EntityTypeBuilder<Webhook> builder)
    {
        builder.Property(a => a.Name).HasMaxLength(64);
        builder.Property(a => a.Url).HasMaxLength(1024);
        builder.Property(a => a.UrlToEditMessage).HasMaxLength(1024);
        builder.Property(a => a.Channel).HasMaxLength(128);
        
        builder.HasOne(a => a.MqttServer)
            .WithMany()
            .HasForeignKey(a => a.MqttServerId);
    }
}