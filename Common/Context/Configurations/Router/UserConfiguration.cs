using Common.Context.Entities.Router;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Context.Configurations.Router;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "router");

        builder.Property(a => a.Ip).HasMaxLength(64);

        builder.Property(a => a.ExternalId).HasMaxLength(128);
        builder.HasIndex(a => a.ExternalId);
        
        builder.Property(a => a.TempBP).HasMaxLength(256);
    }
}