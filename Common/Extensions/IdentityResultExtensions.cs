using Common.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace Common.Extensions;

public static class IdentityResultExtensions
{
    public static void IsSucceedOrThrow(this IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new MqttMeshtasticException(result.ToString());
        }
    }
}