using HotChocolate.Language;

namespace MeshtasticMqttExplorer.GraphQl.Types;

public class ULongType : IntegerTypeBase<ulong>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ULongType"/> class.
    /// </summary>
    public ULongType(ulong min, ulong max)
        : this(
            "ULong",
            "ULong representation",
            min,
            max,
            BindingBehavior.Implicit)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ULongType"/> class.
    /// </summary>
    public ULongType(
        string name,
        string? description = null,
        ulong min = ulong.MinValue,
        ulong max = ulong.MaxValue,
        BindingBehavior bind = BindingBehavior.Explicit)
        : base(name, min, max, bind)
    {
        Description = description;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ULongType"/> class.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public ULongType()
        : this(ulong.MinValue, ulong.MaxValue)
    {
    }

    protected override ulong ParseLiteral(IntValueNode valueSyntax)
        => valueSyntax.ToUInt64();

    protected override IntValueNode ParseValue(ulong runtimeValue)
        => new(runtimeValue);
}
