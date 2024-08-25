using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CS_AES_CTR;
using Google.Protobuf;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using MeshtasticMqttExplorer.Components;
using MeshtasticMqttExplorer.Context;
using MeshtasticMqttExplorer.Context.Entities;
using MeshtasticMqttExplorer.Extensions;
using MeshtasticMqttExplorer.Extensions.Entities;
using Microsoft.EntityFrameworkCore;
using NeighborInfo = Meshtastic.Protobufs.NeighborInfo;
using Position = MeshtasticMqttExplorer.Context.Entities.Position;
using Telemetry = MeshtasticMqttExplorer.Context.Entities.Telemetry;
using Waypoint = Meshtastic.Protobufs.Waypoint;

namespace MeshtasticMqttExplorer.Services;

public class MeshtasticService(ILogger<MeshtasticService> logger, IDbContextFactory<DataContext> contextFactory) : AService(logger, contextFactory)
{
    public static readonly uint NodeBroadcast = 0xFFFFFFFF;
    public static readonly List<uint> NodesIgnored = [NodeBroadcast, 0x1, 0x10];
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
            LastSeen = DateTime.UtcNow,
            IsMqttGateway = true
        };
        if (nodeGateway.Id == 0)
        {
            Logger.LogInformation("New node (gateway) created {node}", nodeGateway);
            Context.Add(nodeGateway);
        }
        else
        {
            nodeGateway.LastSeen = DateTime.UtcNow;
            nodeGateway.IsMqttGateway = true;
            Context.Update(nodeGateway);
        }
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
            LastSeen = DateTime.UtcNow,
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
            nodeFrom.LastSeen = DateTime.UtcNow;
            Context.Update(nodeFrom);
            await UpdateRegionCodeAndModemPreset(nodeFrom, nodeGateway.RegionCode, nodeGateway.ModemPreset, RegionCodeAndModemPresetSource.Gateway);
        }
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

        if (await Context.Packets.AnyAsync(a => a.PacketId == meshPacket.Id && a.FromId == meshPacket.From && (DateTime.UtcNow - a.CreatedAt).TotalMinutes < 1))
        {
            Logger.LogWarning("Packet #{packetId} from {from} to {to} by {gateway} duplicated, ignored", meshPacket.Id, nodeFrom, nodeTo, nodeGateway);
            return null;
        }

        var isEncrypted = meshPacket.Decoded == null;
        meshPacket.Decoded ??= Decrypt(meshPacket.Encrypted.ToByteArray(), "AQ==", meshPacket.Id, meshPacket.From);
        
        var payload = meshPacket.GetPayload();
        
        var nodeGatewayPosition = nodeGateway.Positions.FirstOrDefault();
        var nodeFromPosition = nodeFrom.Positions.FirstOrDefault();
        double? distance = null;
        
        if (nodeFromPosition != null && nodeGatewayPosition != null)
        {
            distance = Utils.CalculateDistance(nodeFromPosition.Latitude, nodeFromPosition.Longitude, nodeGatewayPosition.Latitude, nodeGatewayPosition.Longitude);
        }
        
        var packet = new Packet
        {
            Channel = channel,
            Gateway = nodeGateway,
            GatewayPosition = nodeGatewayPosition,
            Position = nodeFromPosition,
            GatewayDistanceKm = distance,
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
            ReplyId = meshPacket.Decoded?.ReplyId > 0 ? meshPacket.Decoded?.ReplyId : null,
            Payload = meshPacket.ToByteArray(),
            PayloadJson = payload != null ? Regex.Unescape(JsonSerializer.Serialize(payload, JsonSerializerOptions)) : null,
            From = nodeFrom,
            To = nodeTo,
            MqttServer = origin,
            MqttTopic = topics?.Take(topics.Length - 1).JoinString("/")
        };

        Context.Add(packet);
        await Context.SaveChangesAsync();
        
        Logger.LogInformation("Add new packet #{id}|{idPacket} of type {type} from {from} to {to} by {gateway}. Encrypted : {encrypted}", packet.Id, meshPacket.Id, packet.PortNum, nodeFrom, nodeTo, nodeGateway, packet.Encrypted);
        Logger.LogDebug("New packet #{id} of type {type} {payload}", meshPacket.Id, meshPacket.Decoded?.Portnum, meshPacket.GetPayload());
        
        switch (meshPacket.Decoded?.Portnum)
        {
            case PortNum.MapReportApp:
                await DoMapReportingPacket(nodeFrom, meshPacket.GetPayload<MapReport>());
                break;
            case PortNum.NodeinfoApp:
                await DoNodeInfoPacket(nodeFrom, meshPacket.GetPayload<User>());
                break;
            case PortNum.PositionApp:
                await DoPositionPacket(nodeFrom, packet, meshPacket.GetPayload<Meshtastic.Protobufs.Position>());
                break;
            case PortNum.TelemetryApp:
                await DoTelemetryPacket(nodeFrom, packet, meshPacket.GetPayload<Meshtastic.Protobufs.Telemetry>());
                break;
            case PortNum.NeighborinfoApp:
                await DoNeighborInfoPacket(nodeFrom, packet, meshPacket.GetPayload<NeighborInfo>());
                break;
            case PortNum.TextMessageApp:
                await DoTextMessagePacket(nodeFrom, nodeTo, packet, meshPacket.GetPayload<string>());
                break;
            case PortNum.WaypointApp:
                await DoWaypointPacket(nodeFrom, packet, meshPacket.GetPayload<Waypoint>());
                break;
            case PortNum.TracerouteApp:
                await DoTraceroutePacket(nodeFrom, nodeTo, packet, meshPacket.GetPayload<RouteDiscovery>());
                break;
        }

        return (packet, meshPacket);
    }
    
    private async Task DoMapReportingPacket(Node nodeFrom, MapReport? mapReport)
    {
        if (mapReport == null)
        {
            return;
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
        await UpdatePosition(nodeFrom, mapReport.LatitudeI, mapReport.LongitudeI, mapReport.Altitude, null);
    }
    
    public async Task DoPositionPacket(Node nodeFrom, Packet packet, Meshtastic.Protobufs.Position? positionPayload)
    {
        if (positionPayload == null || packet.WantResponse == true)
        {
            return;
        }

        var position = await UpdatePosition(nodeFrom, positionPayload.LatitudeI, positionPayload.LongitudeI, positionPayload.Altitude, packet);

        if (packet.GatewayPosition != null && position != null)
        {
            packet.Position = position;
            packet.GatewayDistanceKm = Utils.CalculateDistance(position.Latitude, position.Longitude,
                packet.GatewayPosition.Latitude, packet.GatewayPosition.Longitude);

            Context.Update(packet);
            await Context.SaveChangesAsync();
        }
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
            Voltage = telemetryPayload.DeviceMetrics?.Voltage,
            ChannelUtilization = telemetryPayload.DeviceMetrics?.ChannelUtilization,
            AirUtilTx = telemetryPayload.DeviceMetrics?.AirUtilTx,
            Uptime = telemetryPayload.DeviceMetrics?.UptimeSeconds > 0 ? TimeSpan.FromSeconds(telemetryPayload.DeviceMetrics.UptimeSeconds) : null,
            Temperature = telemetryPayload.EnvironmentMetrics?.Temperature,
            RelativeHumidity = telemetryPayload.EnvironmentMetrics?.RelativeHumidity,
            BarometricPressure = telemetryPayload.EnvironmentMetrics?.BarometricPressure,
            Channel1Voltage = telemetryPayload.PowerMetrics?.Ch1Voltage,
            Channel2Voltage = telemetryPayload.PowerMetrics?.Ch2Voltage,
            Channel3Voltage = telemetryPayload.PowerMetrics?.Ch3Voltage,
            Channel1Current = telemetryPayload.PowerMetrics?.Ch1Current,
            Channel2Current = telemetryPayload.PowerMetrics?.Ch2Current,
            Channel3Current = telemetryPayload.PowerMetrics?.Ch3Current,
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

            await SetNeighbor(MeshtasticMqttExplorer.Context.Entities.NeighborInfo.Source.Neighbor, packet, nodeFrom, neighborNode, neighbor.Snr, nodePosition, neighborPosition);
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
                           ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(payload.Expire).DateTime,
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
            waypoint.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(payload.Expire).DateTime;
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

        if (payload.Route.Count == 0)
        {
            return;
        }

        var hop = 0;
        var lastNode = nodeFrom;
        foreach (var nodeId in payload.Route)
        {
            var node = await Context.Nodes
                .Include(n => n.Positions.OrderByDescending(p => p.UpdatedAt).Take(1))
                .FindByNodeIdAsync(nodeId) ?? new Node
            {
                NodeId = nodeId
            };
            if (node.Id == 0)
            {
                Context.Add(node);
                Logger.LogInformation("New node (traceroute) created {node}", node);
                await Context.SaveChangesAsync();
            }
            
            var traceroute = new Traceroute
            {
                From = nodeFrom,
                To = nodeTo,
                Packet = packet,
                Node = node,
                Hop = hop
            };

            Context.Add(traceroute);
            
            await SetNeighbor(MeshtasticMqttExplorer.Context.Entities.NeighborInfo.Source.Traceroute, packet, lastNode, node, hop == 0 ? packet.RxSnr ?? 0 : 0, lastNode.Positions.FirstOrDefault(), node.Positions.FirstOrDefault());

            lastNode = node;
            hop++;
        }
        
        await SetNeighbor(MeshtasticMqttExplorer.Context.Entities.NeighborInfo.Source.Traceroute, packet, lastNode, nodeTo, 0, lastNode.Positions.FirstOrDefault(), nodeTo.Positions.FirstOrDefault());
        await Context.SaveChangesAsync();
    }

    public async Task<Position?> UpdatePosition(Node node, int latitude, int longitude, int altitude, Packet? packet)
    {
        if (latitude == 0 && longitude == 0)
        {
            Logger.LogWarning("Position given for {node} is incorrect : 0", node);
            
            return null;
        }
        
        node.Latitude = latitude * 0.0000001;
        node.Longitude = longitude * 0.0000001;
        node.Altitude = altitude;

        var position = await Context.Positions
            .OrderByDescending(a => a.UpdatedAt)
            .FindByIdAsync(node.Id);

        if (position == null 
            || position.Latitude != node.Latitude 
            || position.Longitude != node.Longitude 
            || position.Altitude != node.Altitude)
        {
            position = new Position
            {
                Latitude = node.Latitude.Value,
                Longitude = node.Longitude.Value,
                Altitude = node.Altitude,
                Node = node,
                Packet = packet
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
            if (node.RegionCode != null && source == RegionCodeAndModemPresetSource.Mqtt)
            {
                return;
            }
            
            if (node.RegionCode != regionCode)
            {
                
                Logger.LogDebug("Node {node} does not have the same RegionCode ({oldRegionCode}) so we set it from {source} to {regionCode}", node, node.RegionCode, source, regionCode);
                node.RegionCode = regionCode;
            }
        }
        
        if (modemPreset.HasValue)
        {
            if (node.ModemPreset != null && source == RegionCodeAndModemPresetSource.Mqtt)
            {
                return;
            }
            
            if (node.ModemPreset != modemPreset)
            {
                Logger.LogDebug("Node {node} does not have the same RegionCode ({oldModemPreset}) so we set it from {source} to {modemPreset}", node, node.ModemPreset, source, modemPreset);
                node.ModemPreset = modemPreset;
            }
        }

        Context.Update(node);
        await Context.SaveChangesAsync();
    }

    public async Task<Context.Entities.NeighborInfo?> SetNeighbor(Context.Entities.NeighborInfo.Source source, Packet packet, Node nodeFrom, Node neighborNode, double snr, Position? nodePosition, Position? neighborPosition)
    {
        if (nodeFrom == neighborNode 
            || (source == MeshtasticMqttExplorer.Context.Entities.NeighborInfo.Source.Gateway && snr == 0)
            || NodesIgnored.Contains(nodeFrom.NodeId) 
            || NodesIgnored.Contains(neighborNode.NodeId)
            || packet.ViaMqtt == true
        )
        {
            return null;
        }

        double? distance = null;
            
        if (nodePosition != null && neighborPosition != null)
        {
            distance = Utils.CalculateDistance(nodePosition.Latitude, nodePosition.Longitude,
                neighborPosition.Latitude, neighborPosition.Longitude);
        }
            
        var neighborInfo = await Context.NeighborInfos.FirstOrDefaultAsync(n => n.Node == nodeFrom && n.Neighbor == neighborNode && n.DataSource == source) ?? new MeshtasticMqttExplorer.Context.Entities.NeighborInfo
        {
            Node = nodeFrom,
            NodePosition = nodePosition,
            Packet = packet,
            Neighbor = neighborNode,
            NeighborPosition = neighborPosition,
            Snr = snr,
            Distance = distance,
            DataSource = source
        };
        if (neighborInfo.Id == 0)
        {
            Logger.LogInformation("Add neighbor {neighbor} to {node}", neighborNode, nodeFrom);
            Context.Add(neighborInfo);
        }
        else
        {
            Logger.LogInformation("Update neighbor {neighbor} to {node}", neighborNode, nodeFrom);
            neighborInfo.UpdatedAt = packet.CreatedAt;
            neighborInfo.Packet = packet;
            neighborInfo.Snr = snr;
            neighborInfo.NodePosition = nodePosition;
            neighborInfo.NeighborPosition = neighborPosition;
            neighborInfo.Distance = distance;
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
    
    private byte[] CreateNonce(ulong packetId, uint fromNode)
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