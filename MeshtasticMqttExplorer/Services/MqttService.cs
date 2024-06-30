using System.Globalization;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CS_AES_CTR;
using Google.Protobuf;
using Meshtastic.Protobufs;
using MeshtasticMqttExplorer.Components;
using MeshtasticMqttExplorer.Context;
using MeshtasticMqttExplorer.Context.Entities;
using MeshtasticMqttExplorer.Extensions;
using MeshtasticMqttExplorer.Extensions.Entities;
using MeshtasticMqttExplorer.Models;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;
using Channel = MeshtasticMqttExplorer.Context.Entities.Channel;
using NeighborInfo = Meshtastic.Protobufs.NeighborInfo;
using Position = MeshtasticMqttExplorer.Context.Entities.Position;
using Telemetry = MeshtasticMqttExplorer.Context.Entities.Telemetry;
using Waypoint = Meshtastic.Protobufs.Waypoint;

namespace MeshtasticMqttExplorer.Services;

public class MqttService : AService, IAsyncDisposable
{
    public static readonly uint NodeBroadcast = 0xFFFFFFFF;
    public static readonly uint[] NodesAvoided = [NodeBroadcast, 0x1, 0x10];
    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter()
        },
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public readonly Subject<Packet> NewPacket = new();
    public readonly Subject<Channel> NewChannel = new();
    public readonly Subject<Node> NewNode = new();
    public readonly Subject<Telemetry> NewTelemetry = new();
    public readonly Subject<Position> NewPosition = new();
    public readonly Subject<MeshtasticMqttExplorer.Context.Entities.NeighborInfo> NewNeighborInfoMessage = new();
    public readonly Subject<TextMessage> NewTextMessage = new();
    public readonly Subject<Waypoint> NewWaypoint = new();

    private readonly List<MqttClientAndConfiguration> _mqttClientAndConfigurations;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public MqttService(ILogger<MqttService> logger, IConfiguration configuration, IDbContextFactory<DataContext> contextFactory, IHostEnvironment environment) : base(logger, contextFactory)
    {
        _configuration = configuration;
        _environment = environment;
        
        _mqttClientAndConfigurations = (configuration.GetSection("Mqtt").Get<List<MqttConfiguration>>() ?? throw new KeyNotFoundException("Mqtt"))
            .Where(c => c.Enabled)
            .Select(async c => new MqttClientAndConfiguration
            {
                Client = new MqttFactory().CreateMqttClient(),
                Configuration = c,
                Context = await contextFactory.CreateDbContextAsync()
            })
            .Select(a => a.Result)
            .ToList();

        foreach (var mqttClientAndConfiguration in _mqttClientAndConfigurations)
        {
            mqttClientAndConfiguration.Client.ConnectedAsync += async _ =>
            {
                Logger.LogInformation("Connection successful to MQTT {name}", mqttClientAndConfiguration.Configuration.Name);

                foreach (var topic in mqttClientAndConfiguration.Configuration.Topics.Distinct())
                {
                    await mqttClientAndConfiguration.Client.SubscribeAsync(
                        new MqttTopicFilterBuilder()
                            .WithTopic(topic)
                            .Build());
                    
                    Logger.LogDebug("Susbcription MQTT {name} to {topic}", mqttClientAndConfiguration.Configuration.Name, topic);
                }
            };

            mqttClientAndConfiguration.Client.ApplicationMessageReceivedAsync += async e =>
            {
                logger.LogDebug("Received from {name} on {topic}", mqttClientAndConfiguration.Configuration.Name, e.ApplicationMessage.Topic);
                
                var data = e.ApplicationMessage.PayloadSegment.ToArray().ToArray();

                var topicSegments = e.ApplicationMessage.Topic.Split("/");
                if (topicSegments.Contains("stat"))
                {
                    await DoReceiveStatus(topicSegments.Last(), Encoding.Default.GetString(data), mqttClientAndConfiguration);
                    return;
                }
                
                var rootPacket = new ServiceEnvelope();
                rootPacket.MergeFrom(data);

                if (rootPacket.Packet.ViaMqtt)
                {
                    logger.LogTrace("Packet #{packetId} gated via MQTT so ignored", rootPacket.Packet.Id);
                    return;
                }

                await DoReceive(rootPacket, mqttClientAndConfiguration, topicSegments);
            };

            mqttClientAndConfiguration.Client.DisconnectedAsync += async args =>
            {
                logger.LogWarning(args.Exception, "MQTT {name} disconnected", mqttClientAndConfiguration.Configuration.Name);

                await Task.Delay(TimeSpan.FromSeconds(5));
                await ConnectMqtt();
            };
        }
    }

    private async Task DoReceiveStatus(string nodeIdString, string status, MqttClientAndConfiguration mqtt)
    {
        var nodeId = uint.Parse(nodeIdString[1..], NumberStyles.HexNumber);
        var isMqttGateway = status == "online";
        var node = await mqtt.Context.Nodes.FindByNodeIdAsync(nodeId) ?? new Node
        {
            NodeId = nodeId,
            IsMqttGateway = isMqttGateway
        };
        if (node.Id == 0)
        {
            if (node.IsMqttGateway == true)
            {
                Logger.LogInformation("New node (status) created {node}", node);
                mqtt.Context.Add(node);
            }
        }
        else if (node.NodeId != NodeBroadcast)
        {
            if (node.IsMqttGateway != isMqttGateway)
            {
                node.IsMqttGateway = isMqttGateway;
                if (node.IsMqttGateway == true)
                {
                    node.LastSeen = DateTime.UtcNow;
                }

                mqtt.Context.Update(node);
            }
        }
        await mqtt.Context.SaveChangesAsync();
    }

    private async Task DoReceive(ServiceEnvelope rootPacket, MqttClientAndConfiguration mqtt, string[] topics)
    {
        var nodeGatewayId = uint.Parse(rootPacket.GatewayId[1..], NumberStyles.HexNumber);
        var nodeGateway = await mqtt.Context.Nodes
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
            mqtt.Context.Add(nodeGateway);
        }
        else if (nodeGateway.NodeId != NodeBroadcast)
        {
            nodeGateway.LastSeen = DateTime.UtcNow;
            nodeGateway.IsMqttGateway = true;
            mqtt.Context.Update(nodeGateway);
        }
        await mqtt.Context.SaveChangesAsync();
        NewNode.OnNext(nodeGateway);
        
        var nodeFrom = await mqtt.Context.Nodes
            .Include(n => n.Positions.OrderByDescending(a => a.UpdatedAt).Take(1))
            .FindByNodeIdAsync(rootPacket.Packet.From) ?? new Node
        {
            NodeId = rootPacket.Packet.From,
            LastSeen = DateTime.UtcNow,
            ModemPreset = nodeGateway.ModemPreset,
            RegionCode = nodeGateway.RegionCode
        };
        if (nodeFrom.Id == 0)
        {
            mqtt.Context.Add(nodeFrom);
            Logger.LogInformation("New node (from) created {node}", nodeFrom);
        }
        else if (nodeFrom.NodeId != NodeBroadcast)
        {
            nodeFrom.LastSeen = DateTime.UtcNow;
            mqtt.Context.Update(nodeFrom);
            await UpdateRegionCodeAndModemPreset(nodeFrom, nodeGateway.RegionCode, nodeGateway.ModemPreset, $"Gateway-{nodeGateway}", mqtt.Context);
        }
        await mqtt.Context.SaveChangesAsync();
        NewNode.OnNext(nodeFrom);

        var nodeTo = await mqtt.Context.Nodes.FindByNodeIdAsync(rootPacket.Packet.To) ?? new Node
        {
            NodeId = rootPacket.Packet.To,
            ModemPreset = nodeGateway.ModemPreset,
            RegionCode = nodeGateway.RegionCode
        };
        if (nodeTo.Id == 0)
        {
            mqtt.Context.Add(nodeTo);
            Logger.LogInformation("New node (to) created {node}", nodeTo);
        }
        else if (nodeTo.NodeId != NodeBroadcast)
        {
            // nodeTo.ModemPreset ??= nodeGateway.ModemPreset;
            // nodeTo.RegionCode ??= nodeGateway.RegionCode;
            mqtt.Context.Update(nodeTo);
        }
        await mqtt.Context.SaveChangesAsync();
        NewNode.OnNext(nodeTo);
        
        var channel = await mqtt.Context.Channels.FirstOrDefaultAsync(c => c.Name == rootPacket.ChannelId) ?? new Channel
        {
            Name = rootPacket.ChannelId
        };
        if (channel.Id == 0)
        {
            mqtt.Context.Add(channel);
            await mqtt.Context.SaveChangesAsync();
            NewChannel.OnNext(channel);
            Logger.LogInformation("New channel created {channel}", channel);
        }

        if (await mqtt.Context.Packets.AnyAsync(a => a.PacketId == rootPacket.Packet.Id && a.FromId == rootPacket.Packet.From && (DateTime.UtcNow - a.CreatedAt).TotalMinutes < 1))
        {
            Logger.LogWarning("Packet #{packetId} from {from} to {to} by {gateway} duplicated, ignored", rootPacket.Packet.Id, nodeFrom, nodeTo, nodeGateway);
            
            return;
        }

        var isEncrypted = rootPacket.Packet.Decoded == null;
        rootPacket.Packet.Decoded ??= Decrypt(rootPacket.Packet.Encrypted.ToByteArray(), "1PG7OiApB1nwvP+rz05pAQ==", rootPacket.Packet.Id, rootPacket.Packet.From);
        
        var payload = rootPacket.Packet.GetPayload();
        
        var nodeGatewayPosition = nodeGateway.Positions.FirstOrDefault();
        var nodeFromPosition =nodeFrom.Positions.FirstOrDefault();
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
            Priority = rootPacket.Packet.Priority,
            PacketId = rootPacket.Packet.Id,
            WantAck = rootPacket.Packet.WantAck,
            RxSnr = rootPacket.Packet.RxSnr,
            RxRssi = rootPacket.Packet.RxRssi,
            HopStart = rootPacket.Packet.HopStart,
            HopLimit = rootPacket.Packet.HopLimit,
            ChannelIndex = rootPacket.Packet.Channel,
            RxTime = rootPacket.Packet.RxTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(rootPacket.Packet.RxTime) : null,
            PortNum = rootPacket.Packet.Decoded?.Portnum,
            WantResponse = rootPacket.Packet.Decoded?.WantResponse,
            ReplyId = rootPacket.Packet.Decoded?.ReplyId > 0 ? rootPacket.Packet.Decoded?.ReplyId : null,
            Payload = rootPacket.ToByteArray(),
            PayloadJson = payload != null ? Regex.Unescape(JsonSerializer.Serialize(payload, JsonSerializerOptions)) : null,
            From = nodeFrom,
            To = nodeTo,
            MqttServer = mqtt.Configuration.Name,
            MqttTopic = topics.Take(topics.Length - 1).JoinString("/")
        };

        mqtt.Context.Add(packet);
        await mqtt.Context.SaveChangesAsync();
        
        Logger.LogInformation("Add new packet #{id} of type {type} from {from} to {to} by {gateway}. Encrypted : {encrypted}", packet.Id, packet.PortNum, nodeFrom, nodeTo, nodeGateway, packet.Encrypted);
        Logger.LogDebug("New packet {packet} {payload}", rootPacket, rootPacket.Packet.GetPayload());
        
        switch (rootPacket.Packet.Decoded?.Portnum)
        {
            case PortNum.MapReportApp:
                await DoMapReportingPacket(mqtt, nodeFrom, rootPacket.Packet.GetPayload<MapReport>()); 
                break;
            case PortNum.NodeinfoApp:
                await DoNodeInfoPacket(mqtt, nodeFrom, packet, rootPacket.Packet.GetPayload<User>());
                break;
            case PortNum.PositionApp:
                await DoPositionPacket(mqtt, nodeFrom, packet, rootPacket.Packet.GetPayload<Meshtastic.Protobufs.Position>());
                break;
            case PortNum.TelemetryApp:
                await DoTelemetryPacket(mqtt, nodeFrom, packet, rootPacket.Packet.GetPayload<Meshtastic.Protobufs.Telemetry>());
                break;
            case PortNum.NeighborinfoApp:
                await DoNeighborInfoPacket(mqtt, nodeFrom, packet, rootPacket.Packet.GetPayload<NeighborInfo>());
                break;
            case PortNum.TextMessageApp:
                await DoTextMessagePacket(mqtt, nodeFrom, nodeTo, packet, rootPacket.Packet.GetPayload<string>());
                break;
            case PortNum.WaypointApp:
                await DoWaypointPacket(mqtt, nodeFrom, nodeTo, packet, rootPacket.Packet.GetPayload<Waypoint>());
                break;
        }
        
        NewPacket.OnNext(packet);
    }

    public async Task ConnectMqtt()
    {
        foreach (var mqttClientAndConfiguration in _mqttClientAndConfigurations.Where(m => !m.Client.IsConnected))
        {
            Logger.LogInformation("Run connection to MQTT {name}", mqttClientAndConfiguration.Configuration.Name);

            var mqttClientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttClientAndConfiguration.Configuration.Host, mqttClientAndConfiguration.Configuration.Port)
                .WithClientId($"MeshtasticMqttExplorer_ValentinF4HVV_{_environment.EnvironmentName}");

            if (!string.IsNullOrWhiteSpace(mqttClientAndConfiguration.Configuration.Username))
            {
                mqttClientOptionsBuilder = mqttClientOptionsBuilder
                    .WithCredentials(mqttClientAndConfiguration.Configuration.Username, mqttClientAndConfiguration.Configuration.Password);
            }

            await mqttClientAndConfiguration.Client.ConnectAsync(mqttClientOptionsBuilder.Build());   
        }
    }

    private async Task DoMapReportingPacket(MqttClientAndConfiguration mqtt, Node nodeFrom, MapReport? mapReport)
    {
        if (mapReport == null)
        {
            return;
        }
        
        await KeepNbPacketsTypeForNode(nodeFrom, PortNum.MapReportApp, 10);

        await UpdateRegionCodeAndModemPreset(nodeFrom, mapReport.Region, mapReport.ModemPreset, $"MapReport", mqtt.Context);
        
        nodeFrom.ShortName = mapReport.ShortName;
        nodeFrom.LongName = mapReport.LongName;
        nodeFrom.Role = mapReport.Role;
        nodeFrom.HardwareModel = mapReport.HwModel;
        nodeFrom.NumOnlineLocalNodes = (int?)mapReport.NumOnlineLocalNodes;
        nodeFrom.FirmwareVersion = mapReport.FirmwareVersion;
        nodeFrom.HasDefaultChannel = mapReport.HasDefaultChannel;
        await UpdatePosition(nodeFrom, mapReport.LatitudeI, mapReport.LongitudeI, mapReport.Altitude, null, mqtt.Context);

        Logger.LogInformation("Update {node} with MapReport", nodeFrom);
        
        mqtt.Context.Update(nodeFrom);
        await mqtt.Context.SaveChangesAsync();
        NewNode.OnNext(nodeFrom);
    }

    private async Task DoPositionPacket(MqttClientAndConfiguration mqtt, Node nodeFrom, Packet packet,
        Meshtastic.Protobufs.Position? positionPayload)
    {
        if (positionPayload == null)
        {
            return;
        }

        await UpdatePosition(nodeFrom, positionPayload.LatitudeI, positionPayload.LongitudeI, positionPayload.Altitude, packet, mqtt.Context);
    }

    private async Task DoNodeInfoPacket(MqttClientAndConfiguration mqtt, Node nodeFrom, Packet packet,
        User? userPayload)
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
        
        mqtt.Context.Update(nodeFrom);
        await mqtt.Context.SaveChangesAsync();
        NewNode.OnNext(nodeFrom);
    }

    private async Task DoTelemetryPacket(MqttClientAndConfiguration mqtt, Node nodeFrom, Packet packet,
        Meshtastic.Protobufs.Telemetry? telemetryPayload)
    {
        if (telemetryPayload == null)
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
        };
        
        Logger.LogInformation("Update {node} with new Telemetry {type}", nodeFrom, telemetry.Type);
        
        mqtt.Context.Add(telemetry);
        await mqtt.Context.SaveChangesAsync();
        NewTelemetry.OnNext(telemetry);
    }
    
    private async Task DoNeighborInfoPacket(MqttClientAndConfiguration mqtt, Node nodeFrom, Packet packet, NeighborInfo? neighborInfoPayload)
    {
        if (neighborInfoPayload == null)
        {
            return;
        }

        foreach (var neighbor in neighborInfoPayload.Neighbors)
        {
            var neighborNode = await mqtt.Context.Nodes
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
                mqtt.Context.Add(neighborNode);
            }
            else
            {
                await UpdateRegionCodeAndModemPreset(neighborNode, nodeFrom.RegionCode, nodeFrom.ModemPreset,
                    $"Neighbor-{nodeFrom}", mqtt.Context);
            }
            NewNode.OnNext(neighborNode);
            await mqtt.Context.SaveChangesAsync();

            var nodePosition = nodeFrom.Positions.FirstOrDefault();
            var neighborPosition = neighborNode.Positions.FirstOrDefault();
            double? distance = null;
            
            if (nodePosition != null && neighborPosition != null)
            {
                distance = Utils.CalculateDistance(nodePosition.Latitude, nodePosition.Longitude,
                    neighborPosition.Latitude, neighborPosition.Longitude);
            }
            
            var neighborInfo = await mqtt.Context.NeighborInfos.FirstOrDefaultAsync(n => n.Node == nodeFrom && n.Neighbor == neighborNode) ?? new MeshtasticMqttExplorer.Context.Entities.NeighborInfo
            {
                Node = nodeFrom,
                NodePosition = nodePosition,
                Packet = packet,
                Neighbor = neighborNode,
                NeighborPosition = neighborPosition,
                Snr = neighbor.Snr,
                Distance = distance
            };
            if (neighborInfo.Id == 0)
            {
                Logger.LogInformation("Add neighbor {neighbor} to {node}", neighborNode, nodeFrom);
                mqtt.Context.Add(neighborInfo);
            }
            else
            {
                Logger.LogInformation("Update neighbor {neighbor} to {node}", neighborNode, nodeFrom);
                neighborInfo.UpdatedAt = packet.CreatedAt;
                neighborInfo.Packet = packet;
                neighborInfo.Snr = neighbor.Snr;
                neighborInfo.NodePosition = nodePosition;
                neighborInfo.NeighborPosition = neighborPosition;
                neighborInfo.Distance = distance;
                mqtt.Context.Update(neighborInfo);
            }

            NewNeighborInfoMessage.OnNext(neighborInfo);
        }

        await mqtt.Context.SaveChangesAsync();
    }

    private async Task DoTextMessagePacket(MqttClientAndConfiguration mqtt, Node nodeFrom, Node nodeTo, Packet packet,
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
        mqtt.Context.Add(textMessage);
        await mqtt.Context.SaveChangesAsync();
        NewTextMessage.OnNext(textMessage);
    }

    private async Task DoWaypointPacket(MqttClientAndConfiguration mqtt, Node nodeFrom, Node nodeTo, Packet packet, Waypoint? payload)
    {
        if (payload == null)
        {
            return;
        }

        var waypoint = await mqtt.Context.Waypoints.FirstOrDefaultAsync(a => a.WaypointId == payload.Id) ??
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
            mqtt.Context.Add(waypoint);
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
            
            mqtt.Context.Update(waypoint);
        }
        
        await mqtt.Context.SaveChangesAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        foreach (var mqttClientAndConfiguration in _mqttClientAndConfigurations.Where(m => m.Client.IsConnected))
        {
            Logger.LogWarning("Disconnect from MQTT {name}", mqttClientAndConfiguration.Configuration.Name);
            await mqttClientAndConfiguration.Client.DisconnectAsync();
            await mqttClientAndConfiguration.Context.DisposeAsync();
        }
        
        await base.DisposeAsync();
    }

    public async Task<Position?> UpdatePosition(Node node, int latitude, int longitude, int altitude, Packet? packet, DataContext context)
    {
        if (latitude == 0 && longitude == 0)
        {
            Logger.LogWarning("Position given for {node} is incorrect", node);
            
            return null;
        }
        
        node.Latitude = latitude * 0.0000001;
        node.Longitude = longitude * 0.0000001;
        node.Altitude = altitude;

        var position = context.Positions
            .OrderByDescending(a => a.UpdatedAt)
            .FirstOrDefault(a => a.Node == node);

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
            
            context.Add(position);
        
            Logger.LogInformation("Update {node} with new Position", node);
        }
        else
        {
            position.UpdatedAt = DateTime.UtcNow;
            
            if (packet != null)
            {
                position.Packet = packet;
            }

            context.Update(position);
        }

        context.Update(node);
        await context.SaveChangesAsync();
        
        NewNode.OnNext(node);
        NewPosition.OnNext(position);

        return position;
    }

    private async Task UpdateRegionCodeAndModemPreset(Node node, Config.Types.LoRaConfig.Types.RegionCode? regionCode, Config.Types.LoRaConfig.Types.ModemPreset? modemPreset, string source, DataContext context)
    {
        if (!node.RegionCode.HasValue || !node.ModemPreset.HasValue)
        {
            Logger.LogDebug("Node {node} does not have RegionCode or ModemPreset so we set it from {source} to {regionCode} {modemPreset}", node, source, regionCode, modemPreset);
        }
        else if (node.RegionCode != regionCode || node.ModemPreset != modemPreset)
        {
            Logger.LogDebug("Node {node} does not have the same RegionCode ({oldRegionCode}) or ModemPreset ({oldModemPreset}) so we set it from {source} to {regionCode} {modemPreset}", node, node.RegionCode, node.ModemPreset, source, regionCode, modemPreset);
        }
        else
        {
            return;
        }
        
        node.RegionCode = regionCode;
        node.ModemPreset = modemPreset;

        context.Update(node);

        await context.SaveChangesAsync();
    }
    
    private Data? Decrypt(byte[] input, string key, ulong packetId, uint nodeFromId)
    {
        var nonce = CreateNonce(packetId, nodeFromId);

        var forDecrypting = new AES_CTR(Convert.FromBase64String(key), nonce);
        var decryptedContent = new byte[input.Length];
        forDecrypting.DecryptBytes(decryptedContent, input);
        
        try
        {
            var packet = new Data();
            packet.MergeFrom(decryptedContent);

            Logger.LogInformation("Decrypt packet {packetId} from {nodeId} with key {key} OK", packetId, nodeFromId, key);

            return packet;
        }
        catch
        {
            Logger.LogWarning("Decrypt packet {packetId} from {nodeId} with key {key} KO", packetId, nodeFromId, key);

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
    
    private class MqttClientAndConfiguration
    {
        public required IMqttClient Client { get; set; }
        public required MqttConfiguration Configuration { get; set; }
        public required DataContext Context { get; set; }
    }

    public async Task PurgePackets()
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-_configuration.GetValue("PurgeDays", 3));
        var context = await DbContextFactory.CreateDbContextAsync();
        
        var packets = context.Packets.Where(a => a.CreatedAt < minDate).ToList();

        if (packets.Count == 0)
        {
            return;
        }

        Logger.LogInformation("Delete {nbPackets} packets because they are too old < {date}", packets.Count, minDate);
        
        context.RemoveRange(packets);
        await context.SaveChangesAsync();
    }

    public async Task PurgeEncryptedPackets()
    {
        var threeDays = DateTime.UtcNow.Date.AddDays(-3);
        var context = await DbContextFactory.CreateDbContextAsync();
        
        var packets = context.Packets.Where(a => a.CreatedAt < threeDays && a.Encrypted && string.IsNullOrWhiteSpace(a.PayloadJson)).ToList();

        if (packets.Count == 0)
        {
            return;
        }

        Logger.LogInformation("Delete {nbPackets} packets because they are too old < {date} and encrypted", packets.Count, threeDays);
        
        context.RemoveRange(packets);
        await context.SaveChangesAsync();
    }

    public async Task PurgeData()
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-_configuration.GetValue("PurgeDays", 3));
        var context = await DbContextFactory.CreateDbContextAsync();
        
        var telemetries = context.Telemetries.Where(a => a.CreatedAt < minDate).ToList();

        if (telemetries.Count > 0)
        {
            Logger.LogInformation("Delete {nbData} telemetries because they are too old < {date}", telemetries.Count,
                minDate);

            context.RemoveRange(telemetries);
        }

        var positions = context.Positions.Where(a => a.CreatedAt < minDate).ToList();

        if (positions.Count > 0)
        {
            Logger.LogInformation("Delete {nbData} telemetries because they are too old < {date}", positions.Count,
                minDate);

            context.RemoveRange(positions);
        }

        var neighbors = context.Telemetries.Where(a => a.CreatedAt < minDate).ToList();

        if (neighbors.Count > 0)
        {
            Logger.LogInformation("Delete {nbData} telemetries because they are too old < {date}", neighbors.Count,
                minDate);

            context.RemoveRange(neighbors);
        }

        await context.SaveChangesAsync();
    }

    private async Task KeepNbPacketsTypeForNode(Node node, PortNum portNum, int nbToKeep)
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        
        var packets = context.Packets
            .Where(a => a.From == node && a.PortNum == portNum)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(nbToKeep)
            .ToList();

        if (packets.Count == 0)
        {
            return;
        }

        Logger.LogInformation("Delete {nbPackets} of {type} packets for node #{node} because there are already {nbKeep} of them", packets.Count, portNum, node.Id, nbToKeep);
        
        context.RemoveRange(packets);
        await context.SaveChangesAsync();
    }

    private async Task KeepDatedPacketsTypeForNode(Node node, PortNum portNum, int nbDays)
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-nbDays);
        var context = await DbContextFactory.CreateDbContextAsync();
        
        var packets = context.Packets
            .Where(a => a.From == node && a.PortNum == portNum && a.CreatedAt < minDate)
            .ToList();

        if (packets.Count == 0)
        {
            return;
        }

        Logger.LogInformation("Delete {nbPackets} of {type} packets for node #{node} because they are too old < {date}", packets.Count, portNum, node.Id, minDate);
        
        context.RemoveRange(packets);
        await context.SaveChangesAsync();
    }
}