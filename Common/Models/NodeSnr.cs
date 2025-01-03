using Common.Context.Entities;

namespace Common.Models;

public class NodeSnr
{
    private float? _snr;
    public required uint NodeId { get; set; }
    public Node? Node { get; set; }

    public float? Snr
    {
        get => _snr;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (value != -128)
            {
                _snr = value / 4.0f;
            }
        }
    }

    public int Hop { get; set; }
}