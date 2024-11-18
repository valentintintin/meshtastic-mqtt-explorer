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
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Entities_NeighborInfo = Common.Context.Entities.NeighborInfo;
using NeighborInfo = Meshtastic.Protobufs.NeighborInfo;
using Position = Common.Context.Entities.Position;
using Telemetry = Common.Context.Entities.Telemetry;
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
    
    public async Task<(Packet packet, MeshPacket meshPacket)?> DoReceive(uint nodeGatewayId, string channelId, MeshPacket meshPacket, string origin, string[]? topics)
    {
        if (NodesIgnored.Contains(nodeGatewayId))
        {
            Logger.LogInformation("Node (gateway) ignored : {node}", nodeGatewayId);
            return null;
        }
        
        var nodeGateway = await Context.Nodes
            .Include(n => n.Positions.OrderByDescending(a => a.UpdatedAt).Take(1))
            .FindByNodeIdAsync(nodeGatewayId) ?? new Node
        {
            NodeId = nodeGatewayId,
            IsMqttGateway = true
        };
        if (nodeGateway.Id == 0)
        {
            Logger.LogInformation("New node (gateway) created {node}", nodeGateway);
            Context.Add(nodeGateway);
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
        }
        nodeGateway.LastSeen = DateTime.UtcNow;
        await Context.SaveChangesAsync();
        
        if (NodesIgnored.Contains(meshPacket.From))
        {
            Logger.LogInformation("Node (from) ignored : {node}", meshPacket.From.ToHexString());
            return null;
        }
        
        var nodeFrom = await Context.Nodes
            .Include(n => n.Positions.OrderByDescending(a => a.UpdatedAt).Take(1))
            .FindByNodeIdAsync(meshPacket.From) ?? new Node
        {
            NodeId = meshPacket.From,
            ModemPreset = nodeGateway.ModemPreset,
            RegionCode = nodeGateway.RegionCode
        };
        if (nodeFrom.Id == 0)
        {
            Context.Add(nodeFrom);
            Logger.LogInformation("New node (from) created {node}", nodeFrom);
        }
        else
        {
            if (nodeFrom.Ignored)
            {
                Logger.LogInformation("Node (from) ignored : {node}", nodeFrom);
                return null;
            }

            Context.Update(nodeFrom);
            await UpdateRegionCodeAndModemPreset(nodeFrom, nodeGateway.RegionCode, nodeGateway.ModemPreset, RegionCodeAndModemPresetSource.Gateway);
        }
        nodeFrom.LastSeen = DateTime.UtcNow;
        nodeFrom.HopStart = Math.Max((int) meshPacket.HopStart, 
            await Context.Packets
            .Where(p => p.From == nodeFrom && p.PortNum != PortNum.MapReportApp && p.ViaMqtt != true)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(10)
            .MaxAsync(p => p.HopStart) ?? 0);
        await Context.SaveChangesAsync();

        var nodeTo = await Context.Nodes.FindByNodeIdAsync(meshPacket.To) ?? new Node
        {
            NodeId = meshPacket.To,
            ModemPreset = nodeGateway.ModemPreset,
            RegionCode = nodeGateway.RegionCode
        };
        if (nodeTo.Id == 0)
        {
            Context.Add(nodeTo);
            Logger.LogInformation("New node (to) created {node}", nodeTo);
            await Context.SaveChangesAsync();
        }
        
        var channel = await Context.Channels.FindByNameAsync(channelId) ?? new Context.Entities.Channel()
        {
            Name = channelId
        };
        if (channel.Id == 0)
        {
            Context.Add(channel);
            await Context.SaveChangesAsync();
            Logger.LogInformation("New channel created {channel}", channel);
        }
        else
        {
            channel.UpdatedAt = DateTime.UtcNow;
            Context.Update(channel);
            await Context.SaveChangesAsync();
        }
        
        var isEncrypted = meshPacket.Decoded == null;
        meshPacket.Decoded ??= Decrypt(meshPacket.Encrypted.ToByteArray(), "AQ==", meshPacket.Id, meshPacket.From);

        var isOnPrimaryChannel = meshPacket.Decoded?.Portnum != null && meshPacket.Decoded?.Portnum != PortNum.TextMessageApp;
        if (isOnPrimaryChannel)
        {
            nodeFrom.PrimaryChannel = channel.Name;
            Context.Update(nodeFrom);
            await Context.SaveChangesAsync();
        }
        
        var payload = meshPacket.GetPayload();

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
            ReplyId = meshPacket.Decoded?.ReplyId > 0 ? meshPacket.Decoded?.ReplyId : null,
            Payload = meshPacket.ToByteArray(),
            PayloadJson = payload != null ? Regex.Unescape(JsonSerializer.Serialize(payload, JsonSerializerOptions)) : null,
            From = nodeFrom,
            To = nodeTo,
            MqttServer = origin,
            MqttTopic = topics?.Take(topics.Length - 1).JoinString("/"),
            PacketDuplicated = await Context.Packets.OrderBy(a => a.CreatedAt)
                .Where(a => a.PortNum != PortNum.MapReportApp)
                .FirstOrDefaultAsync(a => a.PacketId == meshPacket.Id && a.From == nodeFrom && a.To == nodeTo/* && (DateTime.UtcNow - a.CreatedAt).TotalDays < 1*/)
        };

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
            shouldCompute = packet.PortNum == PortNum.TracerouteApp; // Interresant pour avoir le dernier noeud du chemin retour d'un traceroute 

            if (packet.To == packet.Gateway)
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
            switch (meshPacket.Decoded?.Portnum)
            {
                case PortNum.MapReportApp:
                    Context.RemoveRange(Context.Positions.Where(p => p.Packet == packet));
                    await Context.SaveChangesAsync();
                    nodeFromPosition = await DoMapReportingPacket(packet.From, meshPacket.GetPayload<MapReport>()) ?? nodeFromPosition;
                    break;
                case PortNum.NodeinfoApp:
                    await DoNodeInfoPacket(packet.From, meshPacket.GetPayload<User>());
                    break;
                case PortNum.PositionApp:
                    Context.RemoveRange(Context.Positions.Where(p => p.Packet == packet));
                    await Context.SaveChangesAsync();
                    nodeFromPosition = await DoPositionPacket(packet.From, packet, meshPacket.GetPayload<Meshtastic.Protobufs.Position>()) ?? nodeFromPosition;
                    break;
                case PortNum.TelemetryApp:
                    Context.RemoveRange(Context.Telemetries.Where(p => p.Packet == packet));
                    await Context.SaveChangesAsync();
                    await DoTelemetryPacket(packet.From, packet,
                        meshPacket.GetPayload<Meshtastic.Protobufs.Telemetry>());
                    break;
                case PortNum.NeighborinfoApp:
                    Context.RemoveRange(Context.NeighborInfos.Where(p => p.Packet == packet));
                    await Context.SaveChangesAsync();
                    await DoNeighborInfoPacket(packet.From, packet, meshPacket.GetPayload<NeighborInfo>());
                    break;
                case PortNum.TextMessageApp:
                    Context.RemoveRange(Context.TextMessages.Where(p => p.Packet == packet));
                    await Context.SaveChangesAsync();
                    await DoTextMessagePacket(packet.From, packet.To, packet, meshPacket.GetPayload<string>());
                    break;
                case PortNum.WaypointApp:
                    Context.RemoveRange(Context.Waypoints.Where(p => p.Packet == packet));
                    await Context.SaveChangesAsync();
                    await DoWaypointPacket(packet.From, packet, meshPacket.GetPayload<Waypoint>());
                    break;
                case PortNum.TracerouteApp:
                    Context.RemoveRange(Context.NeighborInfos.Where(p => p.Packet == packet));
                    await Context.SaveChangesAsync();
                    await DoTraceroutePacket(packet.From, packet.To, packet, meshPacket.GetPayload<RouteDiscovery>());
                    break;
            }
        }
        else
        {
            Logger.LogTrace("Packet #{packetId} don't need to compute again", meshPacket.Id);
        }

        await SetPositionAndUpdateNeighborForPacket(packet, nodeFromPosition);

        return (packet, meshPacket);
    }

    private async Task SetPositionAndUpdateNeighborForPacket(Packet packet, Position? nodeFromPosition)
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
            await SetNeighbor(Entities_NeighborInfo.Source.Gateway, packet, packet.Gateway, packet.From, packet.RxSnr!.Value, packet.GatewayPosition, nodeFromPosition);
        }
        else
        {
            await SetNeighbor(Entities_NeighborInfo.Source.Unknown, packet, packet.Gateway, packet.From, -999, packet.GatewayPosition, nodeFromPosition);
        }
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

        var telemetry = new Telemetry
        {
            Node = nodeFrom,
            Packet = packet,
            Type = telemetryPayload.VariantCase,
            BatteryLevel = telemetryPayload.DeviceMetrics?.BatteryLevel,
            Voltage = telemetryPayload.DeviceMetrics?.Voltage.IfNaNGetNull(),
            ChannelUtilization = telemetryPayload.DeviceMetrics?.ChannelUtilization.IfNaNGetNull(),
            AirUtilTx = telemetryPayload.DeviceMetrics?.AirUtilTx.IfNaNGetNull(),
            Uptime = telemetryPayload.DeviceMetrics?.UptimeSeconds > 0 ? TimeSpan.FromSeconds(telemetryPayload.DeviceMetrics.UptimeSeconds) : null,
            Temperature = telemetryPayload.EnvironmentMetrics?.Temperature.IfNaNGetNull(),
            RelativeHumidity = telemetryPayload.EnvironmentMetrics?.RelativeHumidity.IfNaNGetNull(),
            BarometricPressure = telemetryPayload.EnvironmentMetrics?.BarometricPressure.IfNaNGetNull(),
            Channel1Voltage = telemetryPayload.PowerMetrics?.Ch1Voltage.IfNaNGetNull(),
            Channel2Voltage = telemetryPayload.PowerMetrics?.Ch2Voltage.IfNaNGetNull(),
            Channel3Voltage = telemetryPayload.PowerMetrics?.Ch3Voltage.IfNaNGetNull(),
            Channel1Current = telemetryPayload.PowerMetrics?.Ch1Current.IfNaNGetNull(),
            Channel2Current = telemetryPayload.PowerMetrics?.Ch2Current.IfNaNGetNull(),
            Channel3Current = telemetryPayload.PowerMetrics?.Ch3Current.IfNaNGetNull(),
            CreatedAt = packet.CreatedAt,
            UpdatedAt = packet.UpdatedAt
        };
        
        Logger.LogInformation("Update {node} with new Telemetry {type}", nodeFrom, telemetry.Type);
        
        Context.Add(telemetry);
        await Context.SaveChangesAsync();
    }
    
    public async Task DoNeighborInfoPacket(Node nodeFrom, Packet packet, NeighborInfo? neighborInfoPayload)
    {
        if (neighborInfoPayload == null)
        {
            return;
        }

        foreach (var neighbor in neighborInfoPayload.Neighbors)
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

            await SetNeighbor(Entities_NeighborInfo.Source.Neighbor, packet, nodeFrom, neighborNode, neighbor.Snr, nodePosition, neighborPosition);
        }
    }

    public async Task DoTextMessagePacket(Node nodeFrom, Node nodeTo, Packet packet,
        string? textMessagePayload)
    {
        if (textMessagePayload == null)
        {
            return;
        }
        
        Logger.LogInformation("Send message {message} from {nodeFrom} to {nodeTo}", textMessagePayload, nodeFrom, nodeTo);

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
        if (packet.RequestId is null or 0)
        {
            return;
        }

        var (routes, routesBack) = GetTracerouteInfo(nodeFrom.NodeId, nodeTo.NodeId, packet.Gateway.NodeId, payload);
        var lastNode = nodeTo; // C'est inversé dans un traceroute

        foreach (var route in routes)
        {
            var node = await Context.Nodes
                .Include(n => n.Positions.OrderByDescending(p => p.UpdatedAt).Take(1))
                .FindByNodeIdAsync(route.NodeId) ?? new Node
            {
                NodeId = route.NodeId
            };
            
            if (node.Id == 0)
            {
                Context.Add(node);
                Logger.LogInformation("New node (traceroute) created {node}", node);
                await Context.SaveChangesAsync();
            }

            await SetNeighbor(Entities_NeighborInfo.Source.Traceroute, packet, lastNode, node, route.Snr ?? -999, lastNode.Positions.FirstOrDefault(), node.Positions.FirstOrDefault());

            lastNode = node;
        }

        if (packet.To == packet.Gateway)
        {
            lastNode = nodeFrom;

            foreach (var route in routesBack)
            {
                var node = await Context.Nodes
                    .Include(n => n.Positions.OrderByDescending(p => p.UpdatedAt).Take(1))
                    .FindByNodeIdAsync(route.NodeId) ?? new Node
                {
                    NodeId = route.NodeId
                };

                if (node.Id == 0)
                {
                    Context.Add(node);
                    Logger.LogInformation("New node (traceroute) created {node}", node);
                    await Context.SaveChangesAsync();
                }

                await SetNeighbor(Entities_NeighborInfo.Source.Traceroute, packet, node, lastNode, route.Snr ?? -999,
                    lastNode.Positions.FirstOrDefault(), node.Positions.FirstOrDefault());

                lastNode = node;
            }
        }
    }

    public (List<TracerouteNodeRoute> routes, List<TracerouteNodeRoute> routesBack) GetTracerouteInfo(uint nodeFromId, uint nodeToId, uint nodeGatewayId, RouteDiscovery? payload)
    {
        List<TracerouteNodeRoute> routes = [];
        List<TracerouteNodeRoute> routesBack = [];

        if (payload != null)
        {
            routes = new List<uint> { nodeToId }
                .Concat(payload.Route)
                .Concat([nodeFromId])
                .Select((n, i) => new TracerouteNodeRoute
                {
                    NodeId = n,
                    Hop = i,
                    Snr = i < payload.SnrTowards.Count ? payload.SnrTowards[i] : null
                })
                .ToList();

            if (payload.RouteBack.Count != 0 || payload.SnrBack.Count != 0)
            {
                routesBack = new List<uint> { nodeFromId }
                    .Concat(payload.RouteBack)
                    // .Concat([nodeGatewayId])
                    .Select((n, i) => new TracerouteNodeRoute
                    {
                        NodeId = n,
                        Hop = i,
                        Snr = i < payload.SnrBack.Count ? payload.SnrBack[i] : null
                    })
                    .ToList();
            }
        }

        return (routes, routesBack);
    }

    public async Task<Position?> UpdatePosition(Node node, int latitude, int longitude, int altitude, Packet? packet)
    {
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
        Node nodeFrom, Node neighborNode, 
        double snr, 
        Position? nodeFromPosition, Position? neighborPosition)
    {
        if (nodeFrom == neighborNode 
            || packet.PortNum == PortNum.MapReportApp
            || NodesIgnored.Contains(nodeFrom.NodeId) 
            || NodesIgnored.Contains(neighborNode.NodeId)
            || packet.ViaMqtt == true
        )
        {
            return null;
        }

        double? distance = null;
        var latitudeFrom = nodeFromPosition?.Latitude ?? nodeFrom.Latitude;
        var longitudeFron = nodeFromPosition?.Longitude ?? nodeFrom.Longitude;
        var latitudeNeighbor = neighborPosition?.Latitude ?? neighborNode.Latitude;
        var longitudeNeighbor = neighborPosition?.Longitude ?? neighborNode.Longitude;

        if (latitudeFrom.HasValue && longitudeFron.HasValue && latitudeNeighbor.HasValue && longitudeNeighbor.HasValue)
        {
            distance = MeshtasticUtils.CalculateDistance(latitudeFrom.Value, longitudeFron.Value, latitudeNeighbor.Value, longitudeNeighbor.Value);
        }
        
        var neighborInfo = await Context.NeighborInfos.FirstOrDefaultAsync(n => n.Node == nodeFrom && n.Neighbor == neighborNode) ?? new Entities_NeighborInfo
        {
            Node = nodeFrom,
            NodePosition = nodeFromPosition,
            Packet = packet,
            Neighbor = neighborNode,
            NeighborPosition = neighborPosition,
            Snr = snr,
            Distance = distance,
            DataSource = source,
            CreatedAt = packet.CreatedAt,
            UpdatedAt = packet.UpdatedAt
        };
        if (neighborInfo.Id == 0)
        {
            Logger.LogInformation("Add neighbor {neighbor} to {node}", neighborNode, nodeFrom);
            Context.Add(neighborInfo);
        }
        else
        {
            Logger.LogInformation("Update neighbor {neighbor} to {node}", neighborNode, nodeFrom);
            neighborInfo.Packet = packet;
            neighborInfo.Snr = snr;
            neighborInfo.NodePosition = nodeFromPosition;
            neighborInfo.NeighborPosition = neighborPosition;
            neighborInfo.Distance = distance;
            neighborInfo.DataSource = source;
            Context.Update(neighborInfo);
        }
        
        await Context.SaveChangesAsync();

        return neighborInfo;
    }

    public MeshPacket CreateMeshPacket(uint nodeToId, uint nodeFromId, string channel, Data data, uint hopLimit = 3, string? key = null)
    {
        var packetId = (uint)Random.Shared.Next();
        
        var packet = new MeshPacket
        {
            Id = packetId,
            To = nodeToId,
            From = nodeFromId,
            Channel = GenerateHash(channel, key),
            WantAck = false,
            HopLimit = hopLimit
        };
        
        if (string.IsNullOrWhiteSpace(key))
        {
            packet.Decoded = data;
        }
        else
        {
            packet.Encrypted = ByteString.CopyFrom(Encrypt(data.ToByteArray(), key, packetId, nodeFromId));
        }

        return packet;
    }
    
    public Data? Decrypt(byte[] input, string key, ulong packetId, uint nodeFromId)
    {
        if (key.ToLower() == "aq==")
        {
            key = "1PG7OiApB1nwvP+rz05pAQ==";
        }
        
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