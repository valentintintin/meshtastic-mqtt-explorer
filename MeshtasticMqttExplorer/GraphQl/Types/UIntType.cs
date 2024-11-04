using HotChocolate.Language;

namespace MeshtasticMqttExplorer.GraphQl.Types;

public class UIntType : IntegerTypeBase<uint>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UIntType"/> class.
    /// </summary>
    public UIntType(uint min, uint max)
        : this(
            "UInt",
            "UInt representation",
            min,
            max,
            BindingBehavior.Implicit)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UIntType"/> class.
    /// </summary>
    public UIntType(
        string name,
        string? description = null,
        uint min = uint.MinValue,
        uint max = uint.MaxValue,
        BindingBehavior bind = BindingBehavior.Explicit)
        : base(name, min, max, bind)
    {
        Description = description;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UIntType"/> class.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public UIntType()
        : this(uint.MinValue, uint.MaxValue)
    {
    }

    protected override uint ParseLiteral(IntValueNode valueSyntax)
        => valueSyntax.ToUInt32();

    protected override IntValueNode ParseValue(uint runtimeValue)
        => new(runtimeValue);
}
