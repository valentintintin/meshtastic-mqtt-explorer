using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Common.Context;
using Common.Context.Entities;
using Common.Extensions;
using Common.Extensions.Entities;
using Common.Models;
using CS_AES_CTR;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Entities_NeighborInfo = Common.Context.Entities.NeighborInfo;
using NeighborInfo = Meshtastic.Protobufs.NeighborInfo;
using Position = Common.Context.Entities.Position;
using Telemetry = Common.Context.Entities.Telemetry;
using User = Meshtastic.Protobufs.User;
using Waypoint = Meshtastic.Protobufs.Waypoint;

namespace Common.Services;

public class MeshtasticService(ILogger<MeshtasticService> logger, IDbContextFactory<DataContext> contextFactory) : AService(logger, contextFactory)
{
    public static readonly uint NodeBroadcast = 0xFFFFFFFF;
    public static readonly List<uint> NodesIgnored = [NodeBroadcast, 0, 1, 0x10];
    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter()
        },
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        WriteIndented = true
    };
    
    public async Task<(Packet packet, MeshPacket meshPacket)?> DoReceive(uint nodeGatewayId, string channelId, MeshPacket meshPacket)
    {
        if (NodesIgnored.Contains(nodeGatewayId))
        {
            Logger.LogInformation("Node (gateway) ignored : {node}", nodeGatewayId);
            return null;
        }

        var nodes = await Context.Nodes
            .Include(n => n.Positions.OrderByDescending(a => a.UpdatedAt).Take(1))
            .Where(a => a.NodeId == nodeGatewayId || a.NodeId == meshPacket.From || a.NodeId == meshPacket.To)
            .ToListAsync();

        var nodeGateway = nodes.FindByNodeId(nodeGatewayId) ?? new Node
        {
            NodeId = nodeGatewayId,
            IsMqttGateway = true
        };
        nodeGateway.LastSeen = DateTime.UtcNow;
        if (nodeGateway.Id == 0)
        {
            Context.Add(nodeGateway);
            // await Context.SaveChangesAsync();
            Logger.LogInformation("New node (gateway) created {node}", nodeGateway);
        }
        else
        {
            if (nodeGateway.Ignored)
            {
                Logger.LogInformation("Node (gateway) ignored : {node}", nodeGateway);
                return null;
            }
            
            nodeGateway.IsMqttGateway = true;
            Context.Update(nodeGateway);
            // await Context.SaveChangesAsync();
        }

        if (NodesIgnored.Contains(meshPacket.From))
        {
            Logger.LogInformation("Node (from) ignored : {node}", meshPacket.From.ToHexString());
            await Context.SaveChangesAsync();
            return null;
        }
        
        var nodeFrom = nodes.FindByNodeId(meshPacket.From) ?? new Node
        {
            NodeId = meshPacket.From,
            ModemPreset = nodeGateway.ModemPreset,
            RegionCode = nodeGateway.RegionCode
        };
        nodeFrom.LastSeen = DateTime.UtcNow;
        if (nodeFrom.Id == 0)
        {
            Context.Add(nodeFrom);
            // await Context.SaveChangesAsync();
            Logger.LogInformation("New node (from) created {node}", nodeFrom);
        }
        else
        {
            if (nodeFrom.Ignored)
            {
                Logger.LogInformation("Node (from) ignored : {node}", nodeFrom);
                await Context.SaveChangesAsync();
                return null;
            }

            Context.Update(nodeFrom);
            await UpdateRegionCodeAndModemPreset(nodeFrom, nodeGateway.RegionCode, nodeGateway.ModemPreset, RegionCodeAndModemPresetSource.Gateway);
        }
        
        if (meshPacket.To == NodeBroadcast && meshPacket.Decoded?.Portnum is PortNum.NodeinfoApp or PortNum.PositionApp or PortNum.TextMessageApp)
        {
            nodeFrom.HopStart = (int?)meshPacket.HopStart;
            // await Context.SaveChangesAsync();
        }

        var nodeTo = nodes.FindByNodeId(meshPacket.To) ?? new Node
        {
            NodeId = meshPacket.To,
            ModemPreset = nodeGateway.ModemPreset,
            RegionCode = nodeGateway.RegionCode
        };
        if (nodeTo.Id == 0)
        {
            Context.Add(nodeTo);
            // await Context.SaveChangesAsync();
            Logger.LogInformation("New node (to) created {node}", nodeTo);
        }
        
        var isEncrypted = meshPacket.Decoded == null;
        
        var channel = await Context.Channels.FindByNameAsync(channelId) ?? new Context.Entities.Channel
        {
            Name = channelId,
            Index = isEncrypted ? 0 : meshPacket.Channel
        };
        if (channel.Id == 0)
        {
            Context.Add(channel);
            // await Context.SaveChangesAsync();
            Logger.LogInformation("New channel created {channel}", channel);
        }
        else
        {
            channel.UpdatedAt = DateTime.UtcNow;
            if (!isEncrypted)
            {
                channel.Index = meshPacket.Channel;
            }
            Context.Update(channel);
            // await Context.SaveChangesAsync();
        }
        
        meshPacket.Decoded ??= Decrypt(meshPacket.Encrypted.ToByteArray(), "AQ==", meshPacket.Id, meshPacket.From);

        if (meshPacket.Decoded == null && meshPacket.Encrypted.IsEmpty)
        {
            Logger.LogWarning("Packet #{id} is empty", meshPacket.Id);
            return null;
        }

        var isOnPrimaryChannel = meshPacket.Decoded?.Portnum != null && meshPacket.Decoded?.Portnum != PortNum.TextMessageApp;
        if (isOnPrimaryChannel)
        {
            nodeFrom.PrimaryChannel = channel.Name;
            // Context.Update(nodeFrom);
            // await Context.SaveChangesAsync();
        }

        var intervalDuplicated = TimeSpan.FromHours(1);
        var payload = meshPacket.GetPayload();

        var packetDuplicated = await Context.Packets
            .Where(a => a.PortNum != PortNum.MapReportApp)
            .Where(a => a.PacketId == meshPacket.Id && a.From == nodeFrom && a.To == nodeTo && DateTime.UtcNow - a.CreatedAt <= intervalDuplicated)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        var packet = new Packet
        {
            Channel = channel,
            Gateway = nodeGateway,
            GatewayPosition = nodeGateway.Positions.FirstOrDefault(),
            Encrypted = isEncrypted,
            Priority = meshPacket.Priority,
            PacketId = meshPacket.Id,
            WantAck = meshPacket.WantAck,
            ViaMqtt = meshPacket.ViaMqtt,
            RxSnr = meshPacket.RxSnr,
            RxRssi = meshPacket.RxRssi,
            HopStart = (int) meshPacket.HopStart,
            HopLimit = (int) meshPacket.HopLimit,
            ChannelIndex = meshPacket.Channel,
            RxTime = meshPacket.RxTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(meshPacket.RxTime) : null,
            PortNum = meshPacket.Decoded?.Portnum,
            WantResponse = meshPacket.Decoded?.WantResponse,
            RequestId = meshPacket.Decoded?.RequestId,
            ReplyId = meshPacket.Decoded?.ReplyId,
            Payload = meshPacket.ToByteArray(),
            PayloadJson = payload != null ? Regex.Unescape(JsonSerializer.Serialize(payload, JsonSerializerOptions)) : null,
            From = nodeFrom,
            To = nodeTo,
            PacketDuplicated = packetDuplicated.FirstOrDefault(),
            RelayNode = meshPacket.RelayNode,
            NextHop = meshPacket.NextHop
        };

        // Check if owner send multiple time same packet
        if (packetDuplicated.Any(a => a.Gateway == nodeGateway))
        {
            await Context.SaveChangesAsync();
            return null;
        }
        
        Context.Add(packet);
        await Context.SaveChangesAsync();
        
        Logger.LogInformation("Add new packet #{id}|{idPacket} of type {type} from {from} to {to} by {gateway}. Encrypted : {encrypted}", packet.Id, meshPacket.Id, packet.PortNum, nodeFrom, nodeTo, nodeGateway, packet.Encrypted);
        Logger.LogDebug("New packet #{id} of type {type} {payload}", meshPacket.Id, meshPacket.Decoded?.Portnum, meshPacket.GetPayload());

        return await DoReceive(packet, meshPacket);
    }

    public async Task<(Packet packet, MeshPacket meshPacket)?> DoReceive(Packet packet, MeshPacket? meshPacket = null)
    {
        if (meshPacket == null)
        {
            Context.Attach(packet);
            meshPacket = new MeshPacket();
            meshPacket.MergeFrom(packet.Payload);
        }
        
        var nodeFromPosition = packet.From.Positions.FirstOrDefault();
        var shouldCompute = true;
        
        if (packet.PacketDuplicated != null)
        {
            shouldCompute = packet.PortNum == PortNum.TracerouteApp; // Interessant pour avoir le dernier noeud du chemin retour d'un traceroute 

            if (packet.From == packet.Gateway)
            {
                Logger.LogInformation("Packet #{packetId} from {from} to {to} by {gateway} duplicated but it's the real one for #{packetDuplicated}, saved as original and set others as duplicated", meshPacket.Id, packet.From, packet.To, packet.Gateway, packet.PacketDuplicated.Id);

                // Tous les doublons sauf celui en cours
                var packetsDuplicated = await Context.Packets
                    .Where(a => a.Id != packet.Id)
                    .Where(a => a.PacketId == packet.PacketId && a.From == packet.From && a.To == packet.To)
                    .ToListAsync();
                
                foreach (var packetDuplicated in packetsDuplicated)
                {
                    packetDuplicated.PacketDuplicated = packet;
                    Context.Update(packetDuplicated);
                }

                packet.PacketDuplicated = null;
                Context.Update(packet);

                await Context.SaveChangesAsync();
            }
            else
            {
                Logger.LogInformation("Packet #{packetId} from {from} to {to} by {gateway} duplicated with #{packetDuplicated}", 
                    meshPacket.Id, packet.From, packet.To, packet.Gateway, packet.PacketDuplicated.Id);
            }
        }

        if (shouldCompute)
        {
            await Context.NeighborInfos.Where(p => p.Packet == packet).ExecuteDeleteAsync();
            await Context.SignalHistories.Where(p => p.Packet == packet).ExecuteDeleteAsync();
            
            switch (meshPacket.Decoded?.Portnum)
            {
                case PortNum.MapReportApp:
                    nodeFromPosition = await DoMapReportingPacket(packet.From, meshPacket.GetPayload<MapReport>()) ?? nodeFromPosition;
                    break;
                case PortNum.NodeinfoApp:
                    await DoNodeInfoPacket(packet.From, meshPacket.GetPayload<User>());
                    break;
                case PortNum.PositionApp:
                    nodeFromPosition = await DoPositionPacket(packet.From, packet, meshPacket.GetPayload<Meshtastic.Protobufs.Position>()) ?? nodeFromPosition;
                    break;
                case PortNum.TelemetryApp:
                    await DoTelemetryPacket(packet.From, packet, meshPacket.GetPayload<Meshtastic.Protobufs.Telemetry>());
                    break;
                case PortNum.NeighborinfoApp:
                    await DoNeighborInfoPacket(packet.From, packet, meshPacket.GetPayload<NeighborInfo>());
                    break;
                case PortNum.TextMessageApp:
                    await DoTextMessagePacket(packet.From, packet.To, packet, meshPacket.GetPayload<string>());
                    break;
                case PortNum.WaypointApp:
                    await DoWaypointPacket(packet.From, packet, meshPacket.GetPayload<Waypoint>());
                    break;
                case PortNum.TracerouteApp:
                    await DoTraceroutePacket(packet.From, packet.To, packet, meshPacket.GetPayload<RouteDiscovery>());
                    break;
                case PortNum.PaxcounterApp:
                    await DoPaxCounterPacket(packet.From, packet, meshPacket.GetPayload<Paxcount>());
                    break;
            }
        }
        else
        {
            Logger.LogTrace("Packet #{packetId} don't need to compute again", meshPacket.Id);
        }

        await SetPositionAndUpdateNeighborsForPacket(packet, nodeFromPosition);

        return (packet, meshPacket);
    }

    private async Task<Node?> GetNodeFromLastByteForAnother(Node from, uint lastByte)
    {
        var nodesCandidate = await Context.NeighborInfos
            // .Include(a => a.Positions.OrderByDescending(b => b.UpdatedAt).Take(1))
            .Where(a => a.NodeReceiver == from)
            .Where(a => (a.NodeHeard.NodeId & 0xFF) == lastByte)
            .Where(a => a.DataSource != Entities_NeighborInfo.Source.Unknown && a.DataSource != Entities_NeighborInfo.Source.NextHop)
            .OrderByDescending(a => a.UpdatedAt)
            .Select(a => a.NodeHeard)
            .ToListAsync();

        // if (nodesCandidate.Count == 1)
        // {
            // return nodesCandidate.First();
        // }

        // var fromPosition = from.Positions.FirstOrDefault();

        // if (fromPosition != null)
        // { 
            // return nodesCandidate
                // .Where(n => n is { Latitude: not null, Longitude: not null })
                // .OrderBy(n => MeshtasticUtils.CalculateDistance(n.Latitude!.Value, n.Longitude!.Value, fromPosition.Latitude, fromPosition.Longitude))
                // .FirstOrDefault(n => MeshtasticUtils.CalculateDistance(n.Latitude!.Value, n.Longitude!.Value, fromPosition.Latitude, fromPosition.Longitude) <= MeshtasticUtils.DefaultDistanceAllowed)
                // ?? nodesCandidate.FirstOrDefault();
        // }

        // return null;

        return nodesCandidate.FirstOrDefault();
    }

    private async Task SetPositionAndUpdateNeighborsForPacket(Packet packet, Position? nodeFromPosition)
    {
        var latitude1 = nodeFromPosition?.Latitude ?? packet.From.Latitude;
        var longitude1 = nodeFromPosition?.Longitude ?? packet.From.Longitude;
        var latitude2 = packet.GatewayPosition?.Latitude ?? packet.Gateway.Latitude;
        var longitude2 = packet.GatewayPosition?.Longitude ?? packet.Gateway.Longitude;

        if (latitude1.HasValue && longitude1.HasValue && latitude2.HasValue && longitude2.HasValue)
        {
            packet.Position = nodeFromPosition;
            packet.GatewayDistanceKm = MeshtasticUtils.CalculateDistance(latitude1.Value, longitude1.Value, latitude2.Value, longitude2.Value);
            Context.Update(packet);
            await Context.SaveChangesAsync();
        }

        if (packet.HopLimit == packet.HopStart)
        {
            await SetNeighbor(Entities_NeighborInfo.Source.Gateway, packet, packet.Gateway, packet.From, packet.RxSnr!.Value, packet.RxRssi!.Value, packet.GatewayPosition, nodeFromPosition);
        }
        else
        {
            if (packet.RelayNode > 0)
            {
                packet.RelayNodeNode = await GetNodeFromLastByteForAnother(packet.Gateway, packet.RelayNode!.Value);

                if (packet.RelayNodeNode != null)
                {
                    await SetNeighbor(Entities_NeighborInfo.Source.Relay, packet, packet.Gateway, packet.RelayNodeNode, packet.RxSnr!.Value, packet.RxRssi, packet.RelayNodeNode.Positions.FirstOrDefault(), packet.GatewayPosition);
                    Context.Update(packet);
                }
            }

            if (packet.RelayNodeNode == null)
            {
                await SetNeighbor(Entities_NeighborInfo.Source.Unknown, packet, packet.Gateway, packet.From, -999, null, packet.GatewayPosition, nodeFromPosition);
            }
        }
        
        if (packet.NextHop > 0)
        {
            packet.NextHopNode = await GetNodeFromLastByteForAnother(packet.Gateway, packet.NextHop!.Value);

            if (packet is { RelayNodeNode: not null, NextHopNode: not null })
            {
                await SetNeighbor(Entities_NeighborInfo.Source.NextHop, packet, packet.NextHopNode, packet.RelayNodeNode, -999, null,
                    packet.NextHopNode.Positions.FirstOrDefault(), packet.RelayNodeNode.Positions.FirstOrDefault());
                Context.Update(packet);
            }
        }

        await Context.SaveChangesAsync();
    }

    private async Task<Position?> DoMapReportingPacket(Node nodeFrom, MapReport? mapReport)
    {
        if (mapReport == null)
        {
            return null;
        }
        
        nodeFrom.ShortName = mapReport.ShortName;
        nodeFrom.LongName = mapReport.LongName;
        nodeFrom.Role = mapReport.Role;
        nodeFrom.HardwareModel = mapReport.HwModel;
        nodeFrom.NumOnlineLocalNodes = (int?)mapReport.NumOnlineLocalNodes;
        nodeFrom.FirmwareVersion = mapReport.FirmwareVersion;
        nodeFrom.HasDefaultChannel = mapReport.HasDefaultChannel;

        Logger.LogInformation("Update {node} with MapReport", nodeFrom);
        
        Context.Update(nodeFrom);
        await Context.SaveChangesAsync();
        
        await UpdateRegionCodeAndModemPreset(nodeFrom, mapReport.Region, mapReport.ModemPreset, RegionCodeAndModemPresetSource.MapReport);
        return await UpdatePosition(nodeFrom, mapReport.LatitudeI, mapReport.LongitudeI, mapReport.Altitude, null);
    }
    
    public async Task<Position?> DoPositionPacket(Node nodeFrom, Packet packet, Meshtastic.Protobufs.Position? positionPayload)
    {
        if (positionPayload == null)
        {
            return null;
        }

        return await UpdatePosition(nodeFrom, positionPayload.LatitudeI, positionPayload.LongitudeI, positionPayload.Altitude, packet);
    }

    public async Task DoNodeInfoPacket(Node nodeFrom, User? userPayload)
    {
        if (userPayload == null)
        {
            return;
        }

        nodeFrom.Role = userPayload.Role;
        nodeFrom.LongName = userPayload.LongName;
        nodeFrom.ShortName = userPayload.ShortName;
        nodeFrom.HardwareModel = userPayload.HwModel;
        
        Logger.LogInformation("Update {node} with new NodeInfo", nodeFrom);
        
        Context.Update(nodeFrom);
        await Context.SaveChangesAsync();
    }

    public async Task DoTelemetryPacket(Node nodeFrom, Packet packet, Meshtastic.Protobufs.Telemetry? telemetryPayload)
    {
        if (telemetryPayload == null || packet.WantResponse == true)
        {
            return;
        }
        
        await Context.Telemetries.Where(p => p.Packet == packet).ExecuteDeleteAsync();

        if (telemetryPayload.LocalStats != null)
        {
            nodeFrom.NumOnlineLocalNodes = (int?)telemetryPayload.LocalStats.NumTotalNodes;
            Context.Update(nodeFrom);
        }
        
        var telemetry = new Telemetry
        {
            Node = nodeFrom,
            Packet = packet,
            Type = telemetryPayload.VariantCase,
            BatteryLevel = telemetryPayload.DeviceMetrics?.BatteryLevel,
            Voltage = telemetryPayload.DeviceMetrics?.Voltage.IfNaNOrInfinityGetNull(),
            ChannelUtilization = telemetryPayload.DeviceMetrics?.ChannelUtilization.IfNaNOrInfinityGetNull() ?? telemetryPayload.LocalStats?.ChannelUtilization.IfNaNOrInfinityGetNull(),
            AirUtilTx = telemetryPayload.DeviceMetrics?.AirUtilTx.IfNaNOrInfinityGetNull() ?? telemetryPayload.LocalStats?.AirUtilTx.IfNaNOrInfinityGetNull(),
            Uptime = telemetryPayload.DeviceMetrics?.UptimeSeconds > 0 ? TimeSpan.FromSeconds(telemetryPayload.DeviceMetrics.UptimeSeconds) : 
                telemetryPayload.LocalStats?.UptimeSeconds > 0 ? TimeSpan.FromSeconds(telemetryPayload.LocalStats.UptimeSeconds) : null,
            Temperature = telemetryPayload.EnvironmentMetrics?.Temperature.IfNaNOrInfinityGetNull(),
            RelativeHumidity = telemetryPayload.EnvironmentMetrics?.RelativeHumidity.IfNaNOrInfinityGetNull(),
            BarometricPressure = telemetryPayload.EnvironmentMetrics?.BarometricPressure.IfNaNOrInfinityGetNull(),
            GasResistance = telemetryPayload.EnvironmentMetrics?.GasResistance.IfNaNOrInfinityGetNull(),
            Iaq = telemetryPayload.EnvironmentMetrics?.Iaq,
            Channel1Voltage = telemetryPayload.PowerMetrics?.Ch1Voltage.IfNaNOrInfinityGetNull(),
            Channel2Voltage = telemetryPayload.PowerMetrics?.Ch2Voltage.IfNaNOrInfinityGetNull(),
            Channel3Voltage = telemetryPayload.PowerMetrics?.Ch3Voltage.IfNaNOrInfinityGetNull(),
            Channel1Current = telemetryPayload.PowerMetrics?.Ch1Current.IfNaNOrInfinityGetNull(),
            Channel2Current = telemetryPayload.PowerMetrics?.Ch2Current.IfNaNOrInfinityGetNull(),
            Channel3Current = telemetryPayload.PowerMetrics?.Ch3Current.IfNaNOrInfinityGetNull(),
            NumPacketsRx = telemetryPayload.LocalStats?.NumPacketsRx,
            NumPacketsRxBad = telemetryPayload.LocalStats?.NumPacketsRxBad,
            NumTxRelayCanceled = telemetryPayload.LocalStats?.NumTxRelayCanceled,
            NumPacketsTx = telemetryPayload.LocalStats?.NumPacketsTx,
            NumRxDupe = telemetryPayload.LocalStats?.NumRxDupe,
            NumTxRelay = telemetryPayload.LocalStats?.NumTxRelay,
            CreatedAt = packet.CreatedAt,
            UpdatedAt = packet.UpdatedAt
        };
        
        Logger.LogInformation("Update {node} with new Telemetry {type}", nodeFrom, telemetry.Type);
        
        Context.Add(telemetry);

        packet.PortNumVariant = telemetryPayload.VariantCase.ToString();
        Context.Update(packet);
        
        await Context.SaveChangesAsync();
    }
    
    public async Task DoNeighborInfoPacket(Node nodeFrom, Packet packet, NeighborInfo? neighborInfoPayload)
    {
        if (neighborInfoPayload == null)
        {
            return;
        }

        var neighborsInfo = GetNeighborsInfo(neighborInfoPayload.Neighbors);

        foreach (var neighbor in neighborsInfo)
        {
            var neighborNode = await Context.Nodes
                .Include(a => a.Positions.OrderByDescending(b => b.UpdatedAt).Take(1))
                .FindByNodeIdAsync(neighbor.NodeId) ?? new Node
            {
                NodeId = neighbor.NodeId,
                ModemPreset = nodeFrom.ModemPreset,
                RegionCode = nodeFrom.RegionCode
            };
            if (neighborNode.Id == 0)
            {
                Logger.LogInformation("Add node (neighbor) {node}", neighborNode);
                Context.Add(neighborNode);
            }
            else
            {
                await UpdateRegionCodeAndModemPreset(neighborNode, nodeFrom.RegionCode, nodeFrom.ModemPreset, RegionCodeAndModemPresetSource.Neighbor);
            }
            await Context.SaveChangesAsync();

            var nodePosition = nodeFrom.Positions.FirstOrDefault();
            var neighborPosition = neighborNode.Positions.FirstOrDefault();

            await SetNeighbor(Entities_NeighborInfo.Source.Neighbor, packet, nodeFrom, neighborNode, neighbor.Snr ?? -999, null, nodePosition, neighborPosition);
        }
    }

    public async Task DoTextMessagePacket(Node nodeFrom, Node nodeTo, Packet packet, string? textMessagePayload)
    {
        if (textMessagePayload == null)
        {
            return;
        }
        
        await Context.TextMessages.Where(p => p.Packet == packet).ExecuteDeleteAsync();
        
        Logger.LogInformation("New message {message} from {nodeFrom} to {nodeTo}", textMessagePayload, nodeFrom, nodeTo);

        var textMessage = new TextMessage
        {
            From = nodeFrom,
            To = nodeTo,
            Packet = packet,
            Channel = packet.Channel,
            Message = textMessagePayload
        };
        Context.Add(textMessage);
        await Context.SaveChangesAsync();
    }

    public async Task DoWaypointPacket(Node nodeFrom, Packet packet, Waypoint? payload)
    {
        if (payload == null)
        {
            return;
        }

        await Context.Waypoints.Where(p => p.Packet == packet).ExecuteDeleteAsync();
        
        var waypoint = await Context.Waypoints.FirstOrDefaultAsync(a => a.WaypointId == payload.Id) ??
                       new Context.Entities.Waypoint
                       {
                           Node = nodeFrom,
                           Packet = packet,
                           WaypointId = payload.Id,
                           Latitude = payload.LatitudeI * 0.0000001,
                           Longitude = payload.LongitudeI * 0.0000001,
                           Name = payload.Name,
                           ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(payload.Expire).DateTime.ToUniversalTime(),
                           Description = payload.Description,
                           Icon = payload.Icon
                       };

        if (waypoint.Id == 0)
        {
            Context.Add(waypoint);
        }
        else
        {
            waypoint.Latitude = payload.LatitudeI * 0.0000001;
            waypoint.Longitude = payload.LongitudeI * 0.0000001;
            waypoint.Name = payload.Name;
            waypoint.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(payload.Expire).DateTime.ToUniversalTime();
            waypoint.Description = payload.Description;
            waypoint.Icon = payload.Icon;
            waypoint.Packet = packet;
            
            Context.Update(waypoint);
        }
        
        await Context.SaveChangesAsync();
    }

    public async Task DoTraceroutePacket(Node nodeFrom, Node nodeTo, Packet packet, RouteDiscovery? payload)
    {
        if (payload == null)
        {
            return;
        }

        // On ne regarde que la réponse du traceroute qui PART du destinaire voulu pour arriver à l'expéditeur et donc contient la route en inversé
        // TODO voir si on peut définitivement supprimer cette condition ? 
        // if (packet.RequestId is null or 0)
        // {
            // return;
        // }

        var (routes, routesBack) = GetTracerouteInfo(nodeFrom.NodeId, nodeTo.NodeId, payload, packet.RequestId > 0);
        Node? heardNode = null;

        foreach (var route in routes)
        {
            var nodeReceiver = await Context.Nodes
                .Include(n => n.Positions.OrderByDescending(p => p.UpdatedAt).Take(1))
                .FindByNodeIdAsync(route.NodeId) ?? new Node
            {
                NodeId = route.NodeId
            };
            
            if (nodeReceiver.Id == 0)
            {
                Context.Add(nodeReceiver);
                await Context.SaveChangesAsync();
                Logger.LogInformation("New node (traceroute) created {node}", nodeReceiver);
            }

            heardNode ??= nodeReceiver;

            await SetNeighbor(Entities_NeighborInfo.Source.Traceroute, packet, nodeReceiver, heardNode, route.Snr ?? -999, 
                null, nodeReceiver.Positions.FirstOrDefault(), heardNode.Positions.FirstOrDefault());

            heardNode = nodeReceiver;
        }

        if (routesBack.Count != 0)
        {
            heardNode = null;
            
            foreach (var route in routesBack)
            {
                var nodeReceiver = await Context.Nodes
                    .Include(n => n.Positions.OrderByDescending(p => p.UpdatedAt).Take(1))
                    .FindByNodeIdAsync(route.NodeId) ?? new Node
                {
                    NodeId = route.NodeId
                };

                if (nodeReceiver.Id == 0)
                {
                    Context.Add(nodeReceiver);
                    await Context.SaveChangesAsync();
                    Logger.LogInformation("New node (traceroute) created {node}", nodeReceiver);
                }
                
                heardNode ??= nodeReceiver;

                await SetNeighbor(Entities_NeighborInfo.Source.Traceroute, packet, nodeReceiver, heardNode, route.Snr ?? -999,
                    null, nodeReceiver.Positions.FirstOrDefault(), heardNode.Positions.FirstOrDefault());

                heardNode = nodeReceiver;
            }
        }
    }

    private async Task DoPaxCounterPacket(Node nodeFrom, Packet packet, Paxcount? payload)
    {
        if (payload == null)
        {
            return;
        }

        await Context.PaxCounters.Where(p => p.Packet == packet).ExecuteDeleteAsync();

        Context.Add(new PaxCounter
        {
            Node = nodeFrom,
            Packet = packet,
            Wifi = payload.Wifi,
            Ble = payload.Ble,
            Uptime = payload.Uptime
        });
        await Context.SaveChangesAsync();
    }

    public (List<NodeSnr> routes, List<NodeSnr> routesBack) GetTracerouteInfo(uint nodeFromId, uint nodeToId, RouteDiscovery? payload, bool isTowardsDestination)
    {
        List<NodeSnr> routes = [];
        List<NodeSnr> routesBack = [];

        if (payload == null)
        {
            return (routes, routesBack);
        }

        var context = ContextFactory.CreateDbContext();
        
        var origin = isTowardsDestination ? nodeToId : nodeFromId;
        var dest = isTowardsDestination ? nodeFromId : nodeToId;

        var nodesIds = new [] { origin, dest }.Concat(payload.Route).Concat(payload.RouteBack).ToList();
        
        var nodes = context.Nodes.Where(n => nodesIds.Contains(n.NodeId));
        
        routes = new List<uint> { origin }
            .Concat(payload.Route)
            .Concat([isTowardsDestination ? dest : 0])
            .Where(a => a > 0)
            .Select((n, i) => new NodeSnr
            {
                NodeId = n,
                Node = nodes.FindByNodeId(n),
                Hop = i,
                Snr = i > 0 && i < payload.SnrTowards.Count + 1 ? payload.SnrTowards[i - 1] : null
            })
            .ToList();

        if (payload.RouteBack.Count != 0)
        {
            routesBack = new List<uint> { payload.SnrBack.Count != 0 ? nodeFromId : 0 }
                .Concat(payload.RouteBack)
                .Where(a => a > 0)
                .Select((n, i) => new NodeSnr
                {
                    NodeId = n,
                    Node = nodes.FindByNodeId(n),
                    Hop = i,
                    Snr = i > 0 && i < payload.SnrBack.Count + 1 ? payload.SnrBack[i - 1] : null
                })
                .ToList();
        }

        return (routes, routesBack);
    }

    public List<NodeSnr> GetNeighborsInfo(RepeatedField<Neighbor> neighbors)
    {
        var nodesIds = neighbors.Select(nn => nn.NodeId).ToList();
        var nodes = Context.Nodes.Where(n => nodesIds.Contains(n.NodeId));
        
        return neighbors.Select(n => new NodeSnr
        {
            NodeId = n.NodeId,
            Node = nodes.FindByNodeId(n.NodeId),
            RawSnr = n.Snr
        }).ToList();
    }

    public async Task<Position?> UpdatePosition(Node node, int latitude, int longitude, int altitude, Packet? packet)
    {
        if (packet != null)
        {
            await Context.Positions.Where(p => p.Packet == packet).ExecuteDeleteAsync();
        }

        var decimalLatitude = latitude * 0.0000001; 
        var decimalLongitude = longitude * 0.0000001; 
        
        if (latitude == longitude || latitude == 0 && longitude == 0)
        {
            Logger.LogWarning("Position given for {node} is incorrect: {latitude}, {longitude}", node, decimalLatitude, decimalLongitude);
            
            return null;
        }
        
        node.Latitude = decimalLatitude;
        node.Longitude = decimalLongitude;
        node.Altitude = altitude;

        var position = await Context.Positions
            .OrderByDescending(a => a.UpdatedAt)
            .FindByIdAsync(node.Id);

        if (position == null 
            || node.Latitude.HasValue && !position.Latitude.AreEqual(node.Latitude.Value) 
            || node.Longitude.HasValue && !position.Longitude.AreEqual(node.Longitude.Value)
            || node.Altitude.HasValue && position.Altitude != node.Altitude.Value
        )
        {
            position = new Position
            {
                Latitude = node.Latitude.Value,
                Longitude = node.Longitude.Value,
                Altitude = node.Altitude,
                Node = node,
                Packet = packet,
                UpdatedAt = packet?.UpdatedAt ?? DateTime.UtcNow
            };
            
            Context.Add(position);
        
            Logger.LogInformation("Update {node} with new Position", node);
        }
        else
        {
            position.UpdatedAt = DateTime.UtcNow;
            
            if (packet != null)
            {
                position.Packet = packet;
            }

            Context.Update(position);
        }

        Context.Update(node);
        await Context.SaveChangesAsync();
        
        return position;
    }
    
    public ChannelSet DecodeQrCodeUrl(string url)
    {
        Logger.LogTrace("Decoding of url {url}", url);

        try
        {
            var b64 = UrlSafeBase64Decode(url.Split("/#").Last());

            var packet = new ChannelSet();
            packet.MergeFrom(Convert.FromBase64String(b64));
            
            Logger.LogInformation("Decoding of url {url} OK. Main canal : {mainChanel}", url, packet.Settings.FirstOrDefault()?.Name);

            return packet;
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Error during url decode {url}", url);
            throw;
        }
    }

    public async Task UpdateRegionCodeAndModemPreset(Node node, Config.Types.LoRaConfig.Types.RegionCode? regionCode, Config.Types.LoRaConfig.Types.ModemPreset? modemPreset, RegionCodeAndModemPresetSource source)
    {
        if (regionCode.HasValue)
        {
            if (node.RegionCode == null
                || node.RegionCode != regionCode && source is RegionCodeAndModemPresetSource.MapReport or RegionCodeAndModemPresetSource.NodeInfo
            )
            {
                Logger.LogDebug("Node {node} does not have the same RegionCode ({oldRegionCode}) so we set it from {source} to {regionCode}", node, node.RegionCode, source, regionCode);
                node.RegionCode = regionCode;
            }
        }
        
        if (modemPreset.HasValue)
        {
            if (node.ModemPreset == null
                || node.ModemPreset != modemPreset && source is RegionCodeAndModemPresetSource.MapReport or RegionCodeAndModemPresetSource.NodeInfo 
            )
            {
                Logger.LogDebug("Node {node} does not have the same RegionCode ({oldModemPreset}) so we set it from {source} to {modemPreset}", node, node.ModemPreset, source, modemPreset);
                node.ModemPreset = modemPreset;
            }
        }

        Context.Update(node);
        await Context.SaveChangesAsync();
    }

    public async Task<Entities_NeighborInfo?> SetNeighbor(Entities_NeighborInfo.Source source, Packet packet, 
        Node nodeReceiver, Node heardNode, 
        float snr, float? rssi,
        Position? nodeReceiverPosition, Position? heardPosition)
    {
        if (nodeReceiver == heardNode 
            || packet.PortNum == PortNum.MapReportApp
            || NodesIgnored.Contains(nodeReceiver.NodeId) 
            || NodesIgnored.Contains(heardNode.NodeId)
            || packet.ViaMqtt == true
            || packet is { RxSnr: 0, RxRssi: 0 }
        )
        {
            return null;
        }

        double? distance = null;
        var latitudeFrom = nodeReceiverPosition?.Latitude ?? nodeReceiver.Latitude;
        var longitudeFrom = nodeReceiverPosition?.Longitude ?? nodeReceiver.Longitude;
        var latitudeNeighbor = heardPosition?.Latitude ?? heardNode.Latitude;
        var longitudeNeighbor = heardPosition?.Longitude ?? heardNode.Longitude;

        if (latitudeFrom.HasValue && longitudeFrom.HasValue && latitudeNeighbor.HasValue && longitudeNeighbor.HasValue)
        {
            distance = MeshtasticUtils.CalculateDistance(latitudeFrom.Value, longitudeFrom.Value, latitudeNeighbor.Value, longitudeNeighbor.Value);
        }
        
        var neighborInfo = await Context.NeighborInfos.FirstOrDefaultAsync(n => n.NodeReceiver == nodeReceiver && n.NodeHeard == heardNode) ?? new Entities_NeighborInfo
        {
            NodeReceiver = nodeReceiver,
            NodeReceiverPosition = nodeReceiverPosition,
            Packet = packet,
            NodeHeard = heardNode,
            NodeHeardPosition = heardPosition,
            Snr = snr,
            Rssi = rssi,
            Distance = distance,
            DataSource = source,
            CreatedAt = packet.CreatedAt,
            UpdatedAt = packet.UpdatedAt
        };
        if (neighborInfo.Id == 0)
        {
            Logger.LogInformation("Add neighbor for {node} to {neighbor} with SNR {snr} and data source {datasource} from packet #{packetId} and distance {distance} km", nodeReceiver, heardNode, snr, source, packet.Id, distance);
            Context.Add(neighborInfo);
        }
        else
        {
            if (neighborInfo.PacketId > packet.Id)
            {
                Logger.LogInformation("Update neighbor #{id} for {node} to {neighbor} ignored because old packet#{packetId} given", neighborInfo.Id, nodeReceiver, heardNode, packet.Id);
                
                return null;
            }
            
            Logger.LogInformation("Update neighbor #{id} for {node} to {neighbor} with SNR {snr} and data source {datasource} from packet #{packetId} and distance {distance} km", neighborInfo.Id, nodeReceiver, heardNode, snr, source, packet.Id, distance);
            neighborInfo.Packet = packet;
            if (snr != -999)
            {
                neighborInfo.Snr = snr;
                neighborInfo.Rssi = rssi;
            }
            neighborInfo.NodeReceiverPosition = nodeReceiverPosition;
            neighborInfo.NodeHeardPosition = heardPosition;
            neighborInfo.Distance = distance;
            neighborInfo.DataSource = source;
            Context.Update(neighborInfo);
        }

        if (snr != -999)
        {
            Context.Add(new SignalHistory
            {
                CreatedAt = packet.CreatedAt,
                UpdatedAt = packet.UpdatedAt,
                NodeReceiver = nodeReceiver,
                NodeHeard = heardNode,
                Snr = snr,
                Rssi = rssi,
                Packet = packet
            });
        }
        
        await Context.SaveChangesAsync();

        return neighborInfo;
    }

    public MeshPacket CreateMeshPacketFromDto(SentPacketDto dto)
    {
        var packetId = (uint)Random.Shared.Next();

        var channel = char.IsDigit(dto.Channel[0]) ? null : Context.Channels.FindByName(dto.Channel);
        
        // MQTT use it perhaps: GenerateHash(dto.Channel, dto.Key)

        uint channelIndex = 0;

        if (channel != null)
        {
            channelIndex = channel.Index;
            dto.Channel = channel.Name;
        }
        else
        {
            try
            {
                channelIndex = (uint)dto.Channel.ToLong();
            }
            catch (ArgumentException)
            {
                // Ignored
            }
        }

        var packet = new MeshPacket
        {
            Id = packetId,
            To = dto.NodeToId.ToInteger(),
            From = dto.NodeFromId.ToInteger(),
            Channel = channelIndex,
            WantAck = dto.Type == SentPacketDto.MessageType.Message.ToString() || dto.WantAck,
            HopLimit = dto.HopLimit
        };

        var data = CreateDataPacketFromDto(dto);
        
        if (string.IsNullOrWhiteSpace(dto.Key))
        {
            packet.Decoded = data;
        }
        else
        {
            packet.Encrypted = ByteString.CopyFrom(Encrypt(data.ToByteArray(), dto.Key, packetId, dto.NodeFromId.ToInteger()));
        }

        return packet;
    }
    
    private Data CreateDataPacketFromDto(SentPacketDto dto)
    {
        var data = new Data();

        switch (Enum.Parse<SentPacketDto.MessageType>(dto.Type))
        {
            case SentPacketDto.MessageType.Message:
                if (string.IsNullOrWhiteSpace(dto.Message))
                {
                    throw new ValidationException("Le message est vide");
                }

                data.Portnum = PortNum.TextMessageApp;
                data.Payload = ByteString.CopyFrom(Encoding.UTF8.GetBytes(dto.Message!));
                break;
            case SentPacketDto.MessageType.NodeInfo:
                if (string.IsNullOrWhiteSpace(dto.ShortName))
                {
                    throw new ValidationException("Le nom court est vide");
                }

                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    throw new ValidationException("Le nom long est vide");
                }

                data.Portnum = PortNum.NodeinfoApp;
                data.Payload = ByteString.CopyFrom(new User
                {
                    Id = dto.NodeFromId,
                    ShortName = dto.ShortName,
                    LongName = dto.Name
                }.ToByteArray());
                break;
            case SentPacketDto.MessageType.Position:
                data.Portnum = PortNum.PositionApp;
                data.Payload = ByteString.CopyFrom(new Meshtastic.Protobufs.Position
                {
                    LongitudeI = (int)(dto.Longitude / 0.0000001),
                    LatitudeI = (int)(dto.Latitude / 0.0000001),
                    Altitude = dto.Altitude
                }.ToByteArray());
                break;
            case SentPacketDto.MessageType.Waypoint:
                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    throw new ValidationException("Le nom est vide");
                }

                data.Portnum = PortNum.WaypointApp;
                data.Payload = ByteString.CopyFrom(new Waypoint
                {
                    Id = dto.Id ?? (uint)Random.Shared.NextInt64(),
                    Name = dto.Name,
                    Description = dto.Description,
                    Expire = (uint)new DateTimeOffset(dto.Expires).ToUnixTimeSeconds(),
                    LongitudeI = (int)(dto.Longitude / 0.0000001),
                    LatitudeI = (int)(dto.Latitude / 0.0000001)
                }.ToByteArray());
                break;
            case SentPacketDto.MessageType.Raw:
                if (string.IsNullOrWhiteSpace(dto.RawBase64))
                {
                    throw new ValidationException("La payload est vide");
                }

                if (!dto.PortNum.HasValue)
                {
                    throw new ValidationException("Aucun PortNum");
                }

                data.Portnum = dto.PortNum.Value;
                data.Payload = ByteString.FromBase64(dto.RawBase64);
                break;
        }

        return data;
    }
    
    public Data? Decrypt(byte[] input, string key, ulong packetId, uint nodeFromId)
    {
        if (key.ToLower() == "aq==")
        {
            key = "1PG7OiApB1nwvP+rz05pAQ==";
        }
        
        /*var nonce = new NonceGenerator(serviceEnvelope.Packet.From, serviceEnvelope.Packet.Id).Create();
        var decrypted = PacketEncryption.TransformPacket(serviceEnvelope.Packet.Encrypted.ToByteArray(), nonce, Resources.DEFAULT_PSK);
        var payload = Data.Parser.ParseFrom(decrypted);*/
        
        var nonce = CreateNonce(packetId, nodeFromId);

        var forDecrypting = new AES_CTR(Convert.FromBase64String(key), nonce);
        var decryptedContent = new byte[input.Length];
        forDecrypting.DecryptBytes(decryptedContent, input);
        
        try
        {
            var packet = new Data();
            packet.MergeFrom(decryptedContent);

            Logger.LogInformation("Decrypt packet {packetId} from {nodeId} with key {key} OK", packetId, nodeFromId.ToHexString(), key);

            return packet;
        }
        catch
        {
            Logger.LogTrace("Decrypt packet {packetId} from {nodeId} with key {key} KO. Raw decrypted : {raw}", packetId, nodeFromId.ToHexString(), key, Convert.ToBase64String(decryptedContent));

            return null;
        }
    }
    
    public byte[]? Encrypt(byte[] input, string key, ulong packetId, uint nodeFromId)
    {
        if (key.ToLower() == "aq==")
        {
            key = "1PG7OiApB1nwvP+rz05pAQ==";
        }
        
        var nonce = CreateNonce(packetId, nodeFromId);

        var forCrypting = new AES_CTR(Convert.FromBase64String(key), nonce);
        var crytedContent = new byte[input.Length];
        forCrypting.EncryptBytes(crytedContent, input);
        
        try
        {
            Logger.LogInformation("Encrypt packet {packetId} from {nodeId} with key {key} OK", packetId, nodeFromId, key);

            return crytedContent;
        }
        catch
        {
            Logger.LogWarning("Encrypt packet {packetId} from {nodeId} with key {key} KO", packetId, nodeFromId, key);

            return null;
        }
    }
    
    public byte[] CreateNonce(ulong packetId, uint fromNode)
    {
        var nonce = new byte[16];

        BitConverter.GetBytes(packetId).CopyTo(nonce, 0);
        BitConverter.GetBytes(fromNode).CopyTo(nonce, 8);
        BitConverter.GetBytes(0).CopyTo(nonce, 12);

        return nonce;
    }
    
    private uint GenerateHash(string name, string? key)
    {
        var replacedKey = key?.Replace('-', '+').Replace('_', '/');
        
        var keyBytes = string.IsNullOrWhiteSpace(replacedKey) ? [] : Convert.FromBase64String(replacedKey);
        
        var hName = XorHash(Encoding.UTF8.GetBytes(name));
        var hKey = XorHash(keyBytes);
        
        var result = hName ^ hKey;
        
        return (uint)result;
    }

    private static int XorHash(byte[] bytes)
    {
        return bytes.Aggregate(0, (current, b) => current ^ b);
    }
    
    private string UrlSafeBase64Decode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return base64;
    }

    public enum RegionCodeAndModemPresetSource
    {
        Gateway,
        NodeInfo,
        MapReport,
        Neighbor,
        Mqtt
    }
}