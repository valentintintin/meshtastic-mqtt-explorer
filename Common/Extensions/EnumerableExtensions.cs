namespace Common.Extensions;

public static class EnumerableExtensions
{
    public static string JoinString(this IEnumerable<string> enumerable, string joinWith = ", ")
    {
        return string.Join(joinWith, enumerable);
    }
}