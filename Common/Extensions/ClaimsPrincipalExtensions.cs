using System.Security.Claims;

namespace Common.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal claims)
    {
        try
        {
            return claims.FindFirstValue(ClaimTypes.NameIdentifier).ToLong();
        }
        catch (ArgumentException)
        {
            throw new UnauthorizedAccessException("The user is not logged in");
        }
    }
    
    public static long? TryGetUserId(this ClaimsPrincipal claims)
    {
        try
        {
            return claims.FindFirstValue(ClaimTypes.NameIdentifier).ToLong();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}