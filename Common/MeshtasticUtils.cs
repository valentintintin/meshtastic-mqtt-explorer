namespace Common;

public static class MeshtasticUtils
{
    public static readonly int DefaultDistanceAllowed = 150; 
    public static readonly int DifferenceBetweenDistanceAllowed = 10; 
    
    public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371; // Rayon de la Terre en kilomètres
        var lat1Rad = DegreesToRadians(lat1);
        var lon1Rad = DegreesToRadians(lon1);
        var lat2Rad = DegreesToRadians(lat2);
        var lon2Rad = DegreesToRadians(lon2);

        var dlat = lat2Rad - lat1Rad;
        var dlon = lon2Rad - lon1Rad;

        var a = Math.Sin(dlat / 2) * Math.Sin(dlat / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(dlon / 2) * Math.Sin(dlon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var distance = r * c;

        return Math.Round(distance, 2);
    }

    public static double[] GetMiddle(double x1, double y1, double x2, double y2)
    {
        return [
            (x1 + x2) / 2,
            (y1 + y2) / 2
        ];
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}