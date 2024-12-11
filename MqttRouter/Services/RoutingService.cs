using Common.Context;
using Common.Context.Entities;
using Common.Services;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MqttRouter.Models;

namespace MqttRouter.Services;

public class RoutingService(ILogger<RoutingService> logger, IDbContextFactory<DataContext> contextFactory) : AService(logger, contextFactory)
{
    public async Task<(bool shouldPublish, List<string> receivers)> Route(string clientId, Packet packet)
    {
        // if (packet)
        
        List<string> receivers = [];

        if (packet.To.NodeId != MeshtasticService.NodeBroadcast)
        {
            logger.LogInformation("Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} accepted because it's not a broadcast (to {to})", packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId, packet.To);
            // TODO récuperer le client id du noeud destinataire pour ne lui envoyer qu'à lui
            return (true, receivers);
        }

        var noPacketOfThisTypeDuringTheLastHour = !await Context.Packets.AnyAsync(a => a.PortNum == packet.PortNum && a.From == packet.From && a.CreatedAt > DateTime.UtcNow.AddHours(-1));
        
        switch (packet.PortNum)
        {
            case PortNum.TextMessageApp:
                logger.LogInformation("Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} accepted because important", packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId);
                return (true, receivers);
            case PortNum.NodeinfoApp:
                if (noPacketOfThisTypeDuringTheLastHour)
                {
                    logger.LogInformation("Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} accepted because there isn't any packet of this type during the last hour", packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId);
                    return (true, receivers);
                }
                break;
            case PortNum.TelemetryApp:
            case PortNum.PositionApp:
                if (noPacketOfThisTypeDuringTheLastHour)
                {
                    logger.LogInformation("Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} accepted because there isn't any packet of this type during the last hour", packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId);
                    return (true, receivers);
                }
                // TODO envoyer uniquement localement avec un throttle quand même
                // else
                // {
                //     logger.LogInformation("Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} accepted but there are some packets of this type during the last hour so only for local", packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId);
                //     return (true, receivers);
                // }
                break;
        }

        logger.LogWarning("Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} refused", packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId);
        return (false, receivers);
    }
}