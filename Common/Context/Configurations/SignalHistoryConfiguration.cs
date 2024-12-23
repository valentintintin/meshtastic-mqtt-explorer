using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class SignalHistoryConfiguration : IEntityTypeConfiguration<SignalHistory>
{
    public void Configure(EntityTypeBuilder<SignalHistory> builder)
    {
        builder.HasIndex(a => a.CreatedAt);

        builder.HasOne(a => a.NodeReceiver)
            .WithMany()
            .HasForeignKey(a => a.NodeReceiverId);

        builder.HasOne(a => a.NodeHeard)
            .WithMany()
            .HasForeignKey(a => a.NodeHeardId);
        
        builder.HasOne(a => a.Packet)
            .WithMany()
            .HasForeignKey(a => a.PacketId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}