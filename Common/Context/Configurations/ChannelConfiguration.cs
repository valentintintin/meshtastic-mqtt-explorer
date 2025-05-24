using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations;

public class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.HasIndex(a => a.UpdatedAt).IsDescending();
        
        builder.Property(a => a.Name).HasMaxLength(128);
        builder.HasIndex(a => a.Name);
    }
}