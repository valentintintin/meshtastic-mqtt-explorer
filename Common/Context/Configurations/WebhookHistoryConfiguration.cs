using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class WebhookHistoryConfiguration : IEntityTypeConfiguration<WebhookHistory>
{
    public void Configure(EntityTypeBuilder<WebhookHistory> builder)
    {
        builder.Property(a => a.MessageId).HasMaxLength(128);

        builder.HasOne(a => a.Packet)
            .WithMany()
            .HasForeignKey(a => a.PacketId)
            .IsRequired();

        builder.HasOne(a => a.Webhook)
            .WithMany(a => a.Histories)
            .HasForeignKey(a => a.WebhookId)
            .IsRequired();
    }
}