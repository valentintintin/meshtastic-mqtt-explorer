namespace Common.Models;

public class TracerouteNodeRoute
{
    private float? _snr;
    public uint NodeId { get; set; }

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