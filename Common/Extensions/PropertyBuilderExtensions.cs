using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
    
    public static PropertyBuilder<List<string>> ListOfString(this PropertyBuilder<List<string>> builder)
    {
        return builder.HasConversion(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null),
            new ValueComparer<ICollection<string>>(
                (c1, c2) => c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));
            ;
    }
}