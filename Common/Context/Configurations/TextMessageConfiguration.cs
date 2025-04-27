using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class TextMessageConfiguration : IEntityTypeConfiguration<TextMessage>
{
    public void Configure(EntityTypeBuilder<TextMessage> builder)
    {
        builder.HasIndex(a => a.CreatedAt);
        
        builder.HasOne(a => a.From)
            .WithMany(a => a.TextMessagesFrom)
            .HasForeignKey(a => a.FromId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(a => a.To)
            .WithMany(a => a.TextMessagesTo)
            .HasForeignKey(a => a.ToId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(a => a.Packet)
            .WithMany()
            .HasForeignKey(a => a.PacketId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasOne(a => a.Channel)
            .WithMany(a => a.TextMessages)
            .HasForeignKey(a => a.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.Message).HasMaxLength(512);
    }
}