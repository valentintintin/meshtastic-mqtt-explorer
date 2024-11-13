using System.Globalization;

namespace Common.Extensions;

public static class StringExtensions
{
    public static uint ToInteger(this string hexString)
    {
        return uint.Parse(hexString.TrimStart('!'), NumberStyles.HexNumber);
    }
}