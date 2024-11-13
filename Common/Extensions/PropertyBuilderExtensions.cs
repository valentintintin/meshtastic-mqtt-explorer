using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Extensions;

public static class PropertyBuilderExtensions
{
    public static PropertyBuilder<TProperty> EnumToString<TProperty>(this PropertyBuilder<TProperty> builder, int maxLength = 16)
    {
        return builder.HasMaxLength(maxLength)
                .HasConversion<string>()
            ;
    }
    
    public static PropertyBuilder<TProperty?> EnumToStringNotRequired<TProperty>(this PropertyBuilder<TProperty?> builder, int maxLength = 16)
    {
        return builder.HasMaxLength(maxLength)
                .HasConversion<string?>()
            ;
    }
}