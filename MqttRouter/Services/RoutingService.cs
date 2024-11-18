using Common.Context;
using Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MqttRouter.Models;

namespace MqttRouter.Services;

public class RoutingService(ILogger<AService> logger, IDbContextFactory<DataContext> contextFactory) : AService(logger, contextFactory)
{
    public async Task<bool> Route(RoutingDto dto)
    {
        var payload = Convert.FromBase64String(dto.Payload);

        // Si c'est une trame MapReport on autorise dans tous les cas car ça n'est pas transmit en LoRa 
        if (dto.Topic.Contains("map", StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }

        return false;
    }
}