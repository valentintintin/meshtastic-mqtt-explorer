using System.Globalization;

namespace Common.Extensions;

public static class StringExtensions
{
    public static uint ToInteger(this string hexString)
    {
        return uint.Parse(hexString.TrimStart('!'), NumberStyles.HexNumber);
    }
    
    public static long ToLong(this string? value)
    {
        if (!long.TryParse(value, out long valueLong))
        {
            throw new ArgumentException($"{value} is not a number");
        }

        return valueLong;
    }

    public static long? ToLongNullable(this string? value)
    {
        try
        {
            return value.ToLong();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
