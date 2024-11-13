namespace Common.Extensions;

public static class DateTimeExtensions
{
    public static DateTime ToFrench(this DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified)
        {
            return dateTime;
        }
        
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        return TimeZoneInfo.ConvertTimeFromUtc(dateTime, timeZone);
    }
    
    public static long ToUnixTimestamp(this DateTime dateTime)
    {
        return (long)dateTime.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
    }
}