using Common.Context.Entities.Router;
using Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations.Router;

public class PacketActivityConfiguration : IEntityTypeConfiguration<PacketActivity>
{
    public void Configure(EntityTypeBuilder<PacketActivity> builder)
    {
        builder.ToTable("PacketActivities", "router");
        
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.UpdatedAt);

        builder.HasIndex(a => a.Accepted);
        
        builder.Property(a => a.ReceiverIds).ListOfString();
        builder.Property(a => a.Comment).HasMaxLength(256);
    }
}