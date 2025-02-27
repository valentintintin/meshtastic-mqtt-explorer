using Common.Context.Entities;

namespace Common.Models;

public class NodeSnr
{
    public float? RawSnr;
    private readonly float? _snr;
    public required uint NodeId { get; set; }
    public Node? Node { get; set; }

    public float? Snr
    {
        get => _snr ?? RawSnr;
        init
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (value != -128)
            {
                RawSnr = value;
                _snr = RawSnr / 4.0f;
            }
        }
    }

    public int Hop { get; set; }
}