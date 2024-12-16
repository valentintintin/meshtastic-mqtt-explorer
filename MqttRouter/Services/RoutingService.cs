using Common.Context;
using Common.Context.Entities;
using Common.Context.Entities.Router;
using Common.Extensions;
using Common.Extensions.Entities;
using Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static Meshtastic.Protobufs.PortNum;

namespace MqttRouter.Services;

public class RoutingService(ILogger<RoutingService> logger, IDbContextFactory<DataContext> contextFactory) : AService(logger, contextFactory)
{
    public async Task<PacketActivity> Route(string clientId, long? userId, Packet packet)
    {
        await UpdateNodeConfiguration(packet.From);
        await UpdateNodeConfiguration(packet.Gateway);
     
        List<string> receivers = [];

        var packetActivity = new PacketActivity
        {
            Packet = packet,
            ReceiverIds = receivers 
        };

        if (!MeshtasticService.NodesIgnored.Contains(packet.To.NodeId)) // Message direct
        {
            await UpdateNodeConfiguration(packet.To);
            await DoWhenPacketIsNotBroadcast(clientId, userId, packet, receivers, packetActivity);
        }
        else
        {
            switch (packet.PortNum)
            {
                case TextMessageApp:
                    DoWhenTextMessage(clientId, userId, packet, packetActivity);
                    break;
                case NodeinfoApp:
                    await UpdateNodeConfiguration(packet.From);
                    await DoWhenNodeInfo(clientId, userId, packet, packetActivity);
                    break;
                case TelemetryApp:
                case PositionApp:
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
            packetActivity.Comment = $"Trame autorisée {packet.PortNumVariant} et la dernière date de plus d'une heure";
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
            
            packetActivity.Comment = $"Trame interdite {packet.PortNumVariant} car envoyée frequemment : {hasPacketTypeBeforeDate.Value.ToFrench()}";
        }
        else
        {
            var localNodes = await GetNodesAround(packet.Position, packet.From);

            logger.LogInformation(
                "Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} and user #{userId} accepted but there are some packets of this type during the last hour so only for {nb} local",
                packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId, userId, localNodes.Count);
            
            if (localNodes.Count > 0)
            {
                packetActivity.Accepted = true;
                packetActivity.ReceiverIds.AddRange(localNodes.Select(a => a.MqttId!).Distinct().ToList());
                packetActivity.Comment = $"Trame autorisée {packet.PortNumVariant} et la dernière date de plus de 15 minutes donc seulement pour les {localNodes.Count} locaux";
            }
            else
            {
                packetActivity.Accepted = false;
                packetActivity.Comment = $"Trame interdite {packet.PortNumVariant} car la dernière date de plus de 15 minuteset aucun noeud local trouvé";
            }
        }
    }

    private async Task<List<NodeConfiguration>> GetNodesAround(Position? position, Node node)
    {
        List<NodeConfiguration> localNodes = [];
        double minLatitude;
        double maxLatitude;
        double minLongitude;
        double maxLongitude;
                            
        if (position != null)
        {
            var rayonKm = 100;
            var rayonLatitude = rayonKm / 111.0;
            var rayonLongitude = rayonKm / 78.477;
            minLatitude = position.Latitude - rayonLatitude;
            maxLatitude = position.Latitude + rayonLatitude;
            minLongitude = position.Longitude - rayonLongitude;
            maxLongitude = position.Longitude + rayonLongitude;
                
            logger.LogDebug("Position #{positionId} so we use it around {km} Km between latitude [{minLat}; {maxLat}] and longitude [{minLong}; {maxLong}]", position.Id, rayonKm, minLatitude, maxLatitude, minLongitude, maxLongitude);

            localNodes = await Context.NodeConfigurations.Where(a =>
                a.Node.Latitude != null && a.Node.Longitude != null 
                && a.Node.Latitude >= minLatitude && a.Node.Latitude <= maxLatitude 
                && a.Node.Longitude != null && a.Node.Longitude >= minLongitude && a.Node.Longitude <= maxLongitude
                && !string.IsNullOrWhiteSpace(a.MqttId)
            ).ToListAsync();
        }
        else if (node.NodeConfiguration?.Department != null)
        {
            logger.LogDebug("No position so we use the department {department}", node.NodeConfiguration.Department);
                                
            localNodes = await Context.NodeConfigurations.Where(a =>
                node.NodeConfiguration != null && node.NodeConfiguration.Department == a.Department
                                                      && !string.IsNullOrWhiteSpace(a.MqttId)
            ).ToListAsync();
        }
        else
        {
            logger.LogDebug("Node #{id} does not have department", node.Id);
        }

        return localNodes;
    }

    private async Task DoWhenNodeInfo(string clientId, long? userId, Packet packet, PacketActivity packetActivity)
    {
        var hasPacketTypeBeforeDate = await HasPacketTypeBeforeDate(packet, DateTime.UtcNow.AddHours(-4));

        if (hasPacketTypeBeforeDate != null)
        {
            logger.LogInformation(
                "Packet #{id} from {from} of type {portNum} via {clientId}#{gatewayId} and user #{userId} refused because there is packet of this type during the last 4shour",
                packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId, userId);   
            packetActivity.Comment = $"Trame interdite car il y en a déjà eu dans les dernières 4 heures : {hasPacketTypeBeforeDate.Value.ToFrench()}";
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
            packetActivity.Accepted = true;
            packetActivity.Comment = $"En direction d'un noeud précis : {packet.To}";
        }
        else
        {
            if (packet.PortNum is TextMessageApp or AdminApp)
            {
                var localNodes = await GetNodesAround(packet.Position, packet.To);

                logger.LogTrace("Packet #{id} from {from} of type {portNum} via {clientId}#{gateway} and user #{userId} accepted because it's not a broadcast (to {to} which has the mqttClientId {mqttClientId}). {nb} local nodes found", packet.Id, packet.From, packet.PortNum, clientId, packet.GatewayId, userId, packet.To, nodeToConfiguration?.MqttId, localNodes.Count);
            
                if (localNodes.Count > 0)
                {
                    packetActivity.Accepted = true;
                    packetActivity.ReceiverIds.AddRange(localNodes.Select(a => a.MqttId!).Distinct().ToList());
                    packetActivity.Comment = $"En direction d'un noeud précis : {packet.To} mais pas connecté donc à son entourage : {localNodes.Count} locaux";
                }
                
                packetActivity.Accepted = true;
                packetActivity.Comment = $"En direction d'un noeud précis : {packet.To} mais envoyé à tout le monde car pas connecté ni entourage";
            }
            else
            {
                packetActivity.Accepted = false;
                packetActivity.Comment = $"Trame interdite même en direction d'un noeud précis : {packet.To}";
            }
        }
    }

    private async Task<NodeConfiguration> UpdateNodeConfiguration(Node node)
    {
        var nodeConfiguration = await Context.NodeConfigurations.FirstOrDefaultAsync(n => n.Node == node) ?? new NodeConfiguration
        {
            Node = node
        };

        if (node.LongName?.Length >= 2)
        {
            nodeConfiguration.Department = node.LongName[..2];
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
            && a.Packet.PortNumVariant == packet.PortNumVariant
            && a.CreatedAt > minDate
        ))?.CreatedAt;
    }
}