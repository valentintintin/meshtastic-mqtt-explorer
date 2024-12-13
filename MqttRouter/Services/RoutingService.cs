using Common.Context;
using Common.Context.Entities;
using Common.Context.Entities.Router;
using Common.Extensions.Entities;
using Common.Services;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MqttRouter.Services;

public class RoutingService(ILogger<RoutingService> logger, IDbContextFactory<DataContext> contextFactory) : AService(logger, contextFactory)
{
    public async Task<PacketActivity> Route(string clientId, long? userId, uint? mqttUserNodeId, Packet packet)
    {
        if (mqttUserNodeId.HasValue && packet.From == packet.Gateway && packet.From.NodeId == mqttUserNodeId)
        {
            await UpdateNodeConfiguration(packet.From, clientId, userId);
        }
        else
        {
            await UpdateNodeConfiguration(packet.From);
            await UpdateNodeConfiguration(packet.Gateway);
        }

        List<string> receivers = [];

        var packetActivity = new PacketActivity
        {
            Packet = packet,
            ReceiverIds = receivers 
        };

        if (packet.To.NodeId != MeshtasticService.NodeBroadcast)
        {
            await UpdateNodeConfiguration(packet.To);
            await DoWhenPacketIsNotBroadcast(clientId, userId, packet, receivers, packetActivity);
        }
        else
        {
            switch (packet.PortNum)
            {
                case PortNum.TextMessageApp:
                    DoWhenTextMessage(clientId, userId, packet, packetActivity);
                    break;
                case PortNum.NodeinfoApp:
                    await UpdateNodeConfiguration(packet.From);
                    await DoWhenNodeInfo(clientId, userId, packet, packetActivity);
                    break;
                case PortNum.TelemetryApp:
                case PortNum.PositionApp:
                    await DoWhenPositionOrTelemetry(clientId, userId, packet, packetActivity);
                    break;
                default:
                    logger.LogWarning("Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} and user #{userId} refused", packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId, userId);
                    packetActivity.Comment = "Trame interdite";
                    break;
            }
        }

        Context.Add(packetActivity);
        await Context.SaveChangesAsync();

        return packetActivity;
    }

    private async Task DoWhenPositionOrTelemetry(string clientId, long? userId, Packet packet, PacketActivity packetActivity)
    {
        // TODO cas telemetry environemnet etc
        
        if (await HasPacketTypeBeforeDate(packet, DateTime.UtcNow.AddHours(-1)) != null)
        {
            await DoWhenPositionOrTelemetryForLocalOnly(clientId, userId, packet, packetActivity);
        }
        else
        {
            logger.LogInformation(
                "Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} and user #{userId} accepted because there isn't any packet of this type during the last hour",
                packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId, userId);

            packetActivity.Accepted = true;
            packetActivity.Comment = "Trame autorisée et la dernière date de plus d'une heure";
        }
    }

    private async Task DoWhenPositionOrTelemetryForLocalOnly(string clientId, long? userId, Packet packet, PacketActivity packetActivity)
    {
        var hasPacketTypeBeforeDate = await HasPacketTypeBeforeDate(packet, DateTime.UtcNow.AddMinutes(-15));

        if (hasPacketTypeBeforeDate != null)
        {
            logger.LogInformation(
                "Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} and user #{userId} refused because there are some packets of this type during the 15 minutes",
                packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId, userId);
            
            packetActivity.Comment = $"Trame interdite car envoyée frequemment : {hasPacketTypeBeforeDate}";
        }
        else
        {
            List<NodeConfiguration> localNodes = [];
            double minLatitude;
            double maxLatitude;
            double minLongitude;
            double maxLongitude;
                            
            if (packet.Position != null)
            {
                var rayonKm = 100;
                var rayonLatitude = rayonKm / 111.0;
                var rayonLongitude = rayonKm / 78.477;
                minLatitude = packet.Position.Latitude - rayonLatitude;
                maxLatitude = packet.Position.Latitude + rayonLatitude;
                minLongitude = packet.Position.Longitude - rayonLongitude;
                maxLongitude = packet.Position.Longitude + rayonLongitude;
                
                logger.LogDebug("Packet #{id} does have a position so we use it around {km} Km between latitude [{minLat}; {maxLat}] and longitude [{minLong}; {maxLong}]", packet.Id, rayonKm, minLatitude, maxLatitude, minLongitude, maxLongitude);

                localNodes = await Context.NodeConfigurations.Where(a =>
                    packet.Position != null && a.Node.Latitude != null && a.Node.Longitude != null 
                    && a.Node.Latitude >= minLatitude && a.Node.Latitude <= maxLatitude 
                    && a.Node.Longitude != null && a.Node.Longitude >= minLongitude && a.Node.Longitude <= maxLongitude
                    && !string.IsNullOrWhiteSpace(a.MqttId)
                ).ToListAsync();
            }
            else if (packet.From.NodeConfiguration?.Department != null)
            {
                logger.LogDebug("Packet #{id} does not have any position so we use the department {department}", packet.Id, packet.From.NodeConfiguration.Department);
                                
                localNodes = await Context.NodeConfigurations.Where(a =>
                    packet.From.NodeConfiguration != null && packet.From.NodeConfiguration.Department == a.Department
                    && !string.IsNullOrWhiteSpace(a.MqttId)
                ).ToListAsync();
            }
            else
            {
                logger.LogDebug("Packet #{id} does not have department so we refused it", packet.Id);
            }

            logger.LogInformation(
                "Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} and user #{userId} accepted but there are some packets of this type during the last hour so only for {nb} local",
                packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId, userId, localNodes.Count);
            
            if (localNodes.Count > 0)
            {
                packetActivity.Accepted = true;
                packetActivity.ReceiverIds.AddRange(localNodes.Select(a => a.MqttId!).ToList());
                packetActivity.Comment = $"Trame autorisée et la dernière date de plus de 15 minutes donc seulement pour les {localNodes.Count} locaux";
            }
            else
            {
                packetActivity.Accepted = true;
                packetActivity.Comment = "Trame autorisée et la dernière date de plus de 15 minutes. Aucun noeud local trouvé";
            }
        }
    }

    private async Task DoWhenNodeInfo(string clientId, long? userId, Packet packet, PacketActivity packetActivity)
    {
        var hasPacketTypeBeforeDate = await HasPacketTypeBeforeDate(packet, DateTime.UtcNow.AddHours(-1));

        if (hasPacketTypeBeforeDate != null)
        {
            logger.LogInformation(
                "Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} and user #{userId} refused because there is packet of this type during the last hour",
                packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId, userId);   
            packetActivity.Comment = $"Trame interdite car il y en a déjà eu dans la dernière heure : {hasPacketTypeBeforeDate}";
        }
        else
        {
            logger.LogInformation(
                "Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} and user #{userId} accepted because there isn't any packet of this type during the last hour",
                packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId, userId);
            
            packetActivity.Accepted = true;
            packetActivity.Comment = "Trame autorisée et la dernière date de plus d'une heure";
        }
    }

    private void DoWhenTextMessage(string clientId, long? userId, Packet packet, PacketActivity packetActivity)
    {
        logger.LogInformation(
            "Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} and user #{userId} accepted because important",
            packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId, userId);
                    
        packetActivity.Accepted = true;
        packetActivity.Comment = "C'est un message";
    }

    private async Task DoWhenPacketIsNotBroadcast(string clientId, long? userId, Packet packet, List<string> receivers, PacketActivity packetActivity)
    {
        var nodeToConfiguration = await Context.NodeConfigurations.FirstOrDefaultAsync(a => a.Node == packet.To && !string.IsNullOrWhiteSpace(a.MqttId) && a.Node.LastSeen >= DateTime.UtcNow.AddDays(-1));
        
        logger.LogInformation("Packet #{id} from {from} of type {portNum} via {clientId}#{gateway} and user #{userId} accepted because it's not a broadcast (to {to} which has the mqttClientId {mqttClientId})", packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId, userId, packet.To, nodeToConfiguration?.MqttId);
            
        if (nodeToConfiguration != null)
        {
            receivers.Add(nodeToConfiguration.MqttId!);
        }
            
        packetActivity.Accepted = true;
        packetActivity.Comment = "En direction d'un noeud précis";
    }

    private async Task<NodeConfiguration> UpdateNodeConfiguration(Node node, string? clientId = null, long? userId = null)
    {
        var nodeConfiguration = await Context.NodeConfigurations.FirstOrDefaultAsync(n => n.Node == node) ?? new NodeConfiguration
        {
            Node = node,
            User = userId.HasValue ? await Context.Users.FindByIdAsync(userId) : null
        };

        if (node.LongName?.Length >= 2)
        {
            nodeConfiguration.Department = node.LongName[..2];
        }

        if (clientId != null)
        {
            nodeConfiguration.MqttId = clientId;
        }

        if (nodeConfiguration.Id == 0)
        {
            Context.Add(nodeConfiguration);
        }
        else
        {
            Context.Update(nodeConfiguration);
        }
        await Context.SaveChangesAsync();

        return nodeConfiguration;
    }

    private async Task<DateTime?> HasPacketTypeBeforeDate(Packet packet, DateTime minDate)
    {
        return (await Context.PacketActivities.OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(a =>
            a.Accepted
            && a.Packet.PortNum == packet.PortNum && a.Packet.From == packet.From 
            && a.CreatedAt > minDate 
        ))?.CreatedAt;
    }
}