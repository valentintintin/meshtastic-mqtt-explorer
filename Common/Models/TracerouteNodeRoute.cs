namespace Common.Models;

public class TracerouteNodeRoute
{
    private double? _snr;
    public uint NodeId { get; set; }

    public double? Snr
    {
        get => _snr;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (value != -128)
            {
                _snr = value / 4.0;
            }
        }
    }

    public int Hop { get; set; }
}