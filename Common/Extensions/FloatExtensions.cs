namespace Common.Extensions;

public static class FloatExtensions
{
    public static float? IfNaNGetNull(this float value)
    {
        return float.IsNaN(value) ? null : value;
    }
    
    public static bool AreEqual(this float a, float b, double epsilon = 0.000001)
    {
        return Math.Abs(a - b) < epsilon;
    }
    
    public static bool AreEqual(this double a, double b, double epsilon = 0.000001)
    {
        return Math.Abs(a - b) < epsilon;
    }
}