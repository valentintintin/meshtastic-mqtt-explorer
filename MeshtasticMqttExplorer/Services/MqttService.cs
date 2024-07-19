using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
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
    public static readonly List<uint> NodesIgnored = [NodeBroadcast, 0x1, 0x10];
    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter()
        },
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
    
    private readonly List<MqttClientAndConfiguration> _mqttClientAndConfigurations;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly Subject<(MqttApplicationMessage message, MqttConfiguration mqttConfiguration)> _mqttReceived = new();

    public MqttService(ILogger<MqttService> logger, IConfiguration configuration, IDbContextFactory<DataContext> contextFactory, IHostEnvironment environment, IServiceProvider serviceProvider) : base(logger, contextFactory)
    {
        _configuration = configuration;
        _environment = environment;
        var scheduler = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IScheduler>();

        var shouldPurge = true;
        
        NodesIgnored.AddRange(_configuration.GetValue<List<uint>>("NodesIgnored", [])!);

        _mqttClientAndConfigurations = (configuration.GetSection("Mqtt").Get<List<MqttConfiguration>>() ?? throw new KeyNotFoundException("Mqtt"))
            .Where(c => c.Enabled)
            .Select(a => new MqttClientAndConfiguration
            {
                Configuration = a,
                Client = new MqttFactory().CreateMqttClient()
            })
            .ToList();
        
        foreach (var mqttConfiguration in _mqttClientAndConfigurations)
        {
            mqttConfiguration.Client.ConnectedAsync += async _ =>
            {
                Logger.LogInformation("Connection successful to MQTT {name}", mqttConfiguration.Configuration.Name);

                foreach (var topic in mqttConfiguration.Configuration.Topics.Distinct())
                {
                    await mqttConfiguration.Client.SubscribeAsync(
                        new MqttTopicFilterBuilder()
                            .WithTopic(topic)
                            .Build());
                    
                    Logger.LogDebug("Susbcription MQTT {name} to {topic}", mqttConfiguration.Configuration.Name, topic);
                }
            };

            mqttConfiguration.Client.ApplicationMessageReceivedAsync += e =>
            {
                var topic = e.ApplicationMessage.Topic;
                logger.LogTrace("Received from {name} on {topic}", mqttConfiguration.Configuration.Name, topic);

                if (topic.Contains("json"))
                {
                    logger.LogTrace("Received from {name} on {topic} which is JSON so ignored", mqttConfiguration.Configuration.Name, topic);

                    return Task.CompletedTask;
                }

                if (topic.Contains("paho"))
                {
                    logger.LogTrace("Received from {name} on {topic} so ignored", mqttConfiguration.Configuration.Name, topic);

                    return Task.CompletedTask;
                }
                
                _mqttReceived.OnNext((e.ApplicationMessage, mqttConfiguration.Configuration));
                return Task.CompletedTask;
            };

            mqttConfiguration.Client.DisconnectedAsync += async args =>
            {
                logger.LogWarning(args.Exception, "MQTT {name} disconnected, reason : {reason}", mqttConfiguration.Configuration.Name, args.ReasonString);

                await Task.Delay(TimeSpan.FromSeconds(5));
                await ConnectMqtt();
            };
        }

        _mqttReceived.SubscribeAsync(async data =>
        {
            if (shouldPurge)
            {
                Logger.LogInformation("Purge is needed");
                await PurgeData();
                await PurgeEncryptedPackets();
                await PurgePackets();
                shouldPurge = false;
            }
            
            var rootPacket = new ServiceEnvelope();
                
            try
            {
                var topicSegments = data.message.Topic.Split("/");
                var dataSegments = data.message.PayloadSegment;

                if (dataSegments.Count == 0)
                {
                    logger.LogWarning("Received from {name} on {topic} without data so ignored", data.mqttConfiguration.Name, data.message.Topic);
                        
                    return;
                }
                    
                var firstDataArray = dataSegments.ToArray();
                    
                if (topicSegments.Contains("stat"))
                {
                    await DoReceiveStatus(topicSegments.Last(), Encoding.UTF8.GetString(firstDataArray));
                    return;
                }
                    
                rootPacket.MergeFrom(firstDataArray.ToArray());

                if (rootPacket.Packet == null)
                {
                    logger.LogWarning("Received from {name} on {topic} but packet null", data.mqttConfiguration.Name, data.message.Topic);

                    return;
                }
                    
                await DoReceive(rootPacket, data.mqttConfiguration, topicSegments);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error for a received MQTT message from {name} on {topic}. Packet : {packet}. Packet Raw : {packetRaw}", data.mqttConfiguration.Name, data.message.Topic, JsonSerializer.Serialize(rootPacket), JsonSerializer.Serialize(data.message.PayloadSegment));
            }
        });

        scheduler.SchedulePeriodic(TimeSpan.FromHours(1), () =>
        {
            shouldPurge = true;
        });
    }

    private async Task DoReceiveStatus(string nodeIdString, string status)
    {
        if (!nodeIdString.StartsWith('!'))
        {
            Logger.LogWarning("Node (status) incorrect : {nodeId}", nodeIdString);
            return;
        }
        
        var nodeId = uint.Parse(nodeIdString[1..], NumberStyles.HexNumber);

        if (NodesIgnored.Contains(nodeId))
        {
            Logger.LogInformation("Node (status) ignored : {node}", nodeIdString);
            return;
        }
        
        var isMqttGateway = status == "online";
        var node = await Context.Nodes.FindByNodeIdAsync(nodeId) ?? new Node
        {
            NodeId = nodeId,
            IsMqttGateway = isMqttGateway
        };
        if (node.Id == 0)
        {
            if (node.IsMqttGateway == true)
            {
                Logger.LogInformation("New node (status) created {node}", node);
                Context.Add(node);
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

                Context.Update(node);
            }
        }
        await Context.SaveChangesAsync();
    }

    private async Task DoReceive(ServiceEnvelope rootPacket, MqttConfiguration mqtt, string[] topics)
    {
        if (!rootPacket.GatewayId.StartsWith('!'))
        {
            Logger.LogWarning("Node (gateway) incorrect : {nodeId}", rootPacket.GatewayId);
            return;
        }

        var nodeGatewayId = uint.Parse(rootPacket.GatewayId[1..], NumberStyles.HexNumber);

        if (NodesIgnored.Contains(nodeGatewayId))
        {
            Logger.LogInformation("Node (gateway) ignored : {node}", rootPacket.GatewayId);
            return;
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
        else if (nodeGateway.NodeId != NodeBroadcast)
        {
            nodeGateway.LastSeen = DateTime.UtcNow;
            nodeGateway.IsMqttGateway = true;
            Context.Update(nodeGateway);
        }
        await Context.SaveChangesAsync();

        if (NodesIgnored.Contains(rootPacket.Packet.From))
        {
            Logger.LogInformation("Node (from) ignored : {node}", rootPacket.Packet.From.ToHexString());
            return;
        }
        
        var nodeFrom = await Context.Nodes
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
            Context.Add(nodeFrom);
            Logger.LogInformation("New node (from) created {node}", nodeFrom);
        }
        else if (nodeFrom.NodeId != NodeBroadcast)
        {
            nodeFrom.LastSeen = DateTime.UtcNow;
            Context.Update(nodeFrom);
            await UpdateRegionCodeAndModemPreset(nodeFrom, nodeGateway.RegionCode, nodeGateway.ModemPreset, $"Gateway-{nodeGateway}", Context);
        }
        await Context.SaveChangesAsync();

        var nodeTo = await Context.Nodes.FindByNodeIdAsync(rootPacket.Packet.To) ?? new Node
        {
            NodeId = rootPacket.Packet.To,
            ModemPreset = nodeGateway.ModemPreset,
            RegionCode = nodeGateway.RegionCode
        };
        if (nodeTo.Id == 0)
        {
            Context.Add(nodeTo);
            Logger.LogInformation("New node (to) created {node}", nodeTo);
        }
        else if (nodeTo.NodeId != NodeBroadcast)
        {
            Context.Update(nodeTo);
        }
        await Context.SaveChangesAsync();
        
        var channel = await Context.Channels.FirstOrDefaultAsync(c => c.Name == rootPacket.ChannelId) ?? new Channel
        {
            Name = rootPacket.ChannelId
        };
        if (channel.Id == 0)
        {
            Context.Add(channel);
            await Context.SaveChangesAsync();
            Logger.LogInformation("New channel created {channel}", channel);
        }

        if (await Context.Packets.AnyAsync(a => a.PacketId == rootPacket.Packet.Id && a.FromId == rootPacket.Packet.From && (DateTime.UtcNow - a.CreatedAt).TotalMinutes < 1))
        {
            Logger.LogWarning("Packet #{packetId} from {from} to {to} by {gateway} duplicated, ignored", rootPacket.Packet.Id, nodeFrom, nodeTo, nodeGateway);
            
            return;
        }

        var isEncrypted = rootPacket.Packet.Decoded == null;
        rootPacket.Packet.Decoded ??= Decrypt(rootPacket.Packet.Encrypted.ToByteArray(), "AQ==", rootPacket.Packet.Id, rootPacket.Packet.From);
        
        var payload = rootPacket.Packet.GetPayload();
        
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
            MqttServer = mqtt.Name,
            MqttTopic = topics.Take(topics.Length - 1).JoinString("/")
        };

        Context.Add(packet);
        await Context.SaveChangesAsync();
        
        Logger.LogInformation("Add new packet #{id} of type {type} from {from} to {to} by {gateway}. Encrypted : {encrypted}", packet.Id, packet.PortNum, nodeFrom, nodeTo, nodeGateway, packet.Encrypted);
        Logger.LogDebug("New packet {packet} {payload}", rootPacket, rootPacket.Packet.GetPayload());
        
        switch (rootPacket.Packet.Decoded?.Portnum)
        {
            case PortNum.MapReportApp:
                await DoMapReportingPacket(nodeFrom, rootPacket.Packet.GetPayload<MapReport>()); 
                break;
            case PortNum.NodeinfoApp:
                await DoNodeInfoPacket(nodeFrom, rootPacket.Packet.GetPayload<User>());
                break;
            case PortNum.PositionApp:
                await DoPositionPacket(nodeFrom, packet, rootPacket.Packet.GetPayload<Meshtastic.Protobufs.Position>());
                break;
            case PortNum.TelemetryApp:
                await DoTelemetryPacket(nodeFrom, packet, rootPacket.Packet.GetPayload<Meshtastic.Protobufs.Telemetry>());
                break;
            case PortNum.NeighborinfoApp:
                await DoNeighborInfoPacket(nodeFrom, packet, rootPacket.Packet.GetPayload<NeighborInfo>());
                break;
            case PortNum.TextMessageApp:
                await DoTextMessagePacket(nodeFrom, nodeTo, packet, rootPacket.Packet.GetPayload<string>());
                break;
            case PortNum.WaypointApp:
                await DoWaypointPacket(nodeFrom, packet, rootPacket.Packet.GetPayload<Waypoint>());
                break;
        }
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

    public async Task PublishMessage(PublishMessageDto dto)
    {
        var mqttConfiguration = string.IsNullOrWhiteSpace(dto.MqttServer) ? _mqttClientAndConfigurations.First() : 
            _mqttClientAndConfigurations.First(a => a.Configuration.Name == dto.MqttServer);
        
        var packetId = (uint)Random.Shared.Next();
        var nodeFromId = uint.Parse(dto.NodeFromId[1..], NumberStyles.HexNumber);
        var nodeToId = string.IsNullOrWhiteSpace(dto.NodeToId) ? NodeBroadcast : uint.Parse(dto.NodeToId[1..], NumberStyles.HexNumber);
        var topic = $"{dto.RootTopic}";

        var data = new Data
        {
            Portnum = PortNum.TextMessageApp,
            Payload = ByteString.CopyFrom(Encoding.UTF8.GetBytes(dto.Message))
        };
        
        var packet = new ServiceEnvelope
        {
            ChannelId = dto.Channel,
            GatewayId = dto.NodeFromId,
            Packet = new MeshPacket
            {
                Id = packetId,
                To = nodeToId,
                From = nodeFromId,
                Channel = GenerateHash(dto.Channel, dto.Key),
                WantAck = false,
                HopLimit = dto.HopLimit
            }
        };

        if (string.IsNullOrWhiteSpace(dto.Key))
        {
            packet.Packet.Decoded = data;
            topic += "e";
        }
        else
        {
            packet.Packet.Encrypted = ByteString.CopyFrom(Encrypt(data.ToByteArray(), dto.Key!, packetId, nodeFromId));
            topic += "c";
        }

        topic += $"/{dto.Channel}/{dto.NodeFromId}";
        Logger.LogInformation("Send packet to MQTT topic {topic} : {packet} with data {data}", topic, JsonSerializer.Serialize(packet), JsonSerializer.Serialize(packet.Packet.GetPayload()));

        // await mqttConfiguration.Client.PublishBinaryAsync(topic, packet.ToByteArray());
    }
    
    private async Task DoMapReportingPacket(Node nodeFrom, MapReport? mapReport)
    {
        if (mapReport == null)
        {
            return;
        }
        
        await KeepNbPacketsTypeForNode(nodeFrom, PortNum.MapReportApp, 10);

        await UpdateRegionCodeAndModemPreset(nodeFrom, mapReport.Region, mapReport.ModemPreset, $"MapReport", Context);
        
        nodeFrom.ShortName = mapReport.ShortName;
        nodeFrom.LongName = mapReport.LongName;
        nodeFrom.Role = mapReport.Role;
        nodeFrom.HardwareModel = mapReport.HwModel;
        nodeFrom.NumOnlineLocalNodes = (int?)mapReport.NumOnlineLocalNodes;
        nodeFrom.FirmwareVersion = mapReport.FirmwareVersion;
        nodeFrom.HasDefaultChannel = mapReport.HasDefaultChannel;
        await UpdatePosition(nodeFrom, mapReport.LatitudeI, mapReport.LongitudeI, mapReport.Altitude, null, Context);

        Logger.LogInformation("Update {node} with MapReport", nodeFrom);
        
        Context.Update(nodeFrom);
        await Context.SaveChangesAsync();
    }

    private async Task DoPositionPacket(Node nodeFrom, Packet packet, Meshtastic.Protobufs.Position? positionPayload)
    {
        if (positionPayload == null)
        {
            return;
        }

        var position = await UpdatePosition(nodeFrom, positionPayload.LatitudeI, positionPayload.LongitudeI, positionPayload.Altitude, packet, Context);

        if (packet.GatewayPosition != null && position != null)
        {
            packet.Position = position;
            packet.GatewayDistanceKm = Utils.CalculateDistance(position.Latitude, position.Longitude,
                packet.GatewayPosition.Latitude, packet.GatewayPosition.Longitude);

            Context.Update(packet);
            await Context.SaveChangesAsync();
        }
    }

    private async Task DoNodeInfoPacket(Node nodeFrom, User? userPayload)
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

    private async Task DoTelemetryPacket(Node nodeFrom, Packet packet,
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
        
        Context.Add(telemetry);
        await Context.SaveChangesAsync();
    }
    
    private async Task DoNeighborInfoPacket(Node nodeFrom, Packet packet, NeighborInfo? neighborInfoPayload)
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
                await UpdateRegionCodeAndModemPreset(neighborNode, nodeFrom.RegionCode, nodeFrom.ModemPreset,
                    $"Neighbor-{nodeFrom}", Context);
            }
            await Context.SaveChangesAsync();

            var nodePosition = nodeFrom.Positions.FirstOrDefault();
            var neighborPosition = neighborNode.Positions.FirstOrDefault();
            double? distance = null;
            
            if (nodePosition != null && neighborPosition != null)
            {
                distance = Utils.CalculateDistance(nodePosition.Latitude, nodePosition.Longitude,
                    neighborPosition.Latitude, neighborPosition.Longitude);
            }
            
            var neighborInfo = await Context.NeighborInfos.FirstOrDefaultAsync(n => n.Node == nodeFrom && n.Neighbor == neighborNode) ?? new MeshtasticMqttExplorer.Context.Entities.NeighborInfo
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
                Context.Add(neighborInfo);
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
                Context.Update(neighborInfo);
            }
        }

        await Context.SaveChangesAsync();
    }

    private async Task DoTextMessagePacket(Node nodeFrom, Node nodeTo, Packet packet,
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

    private async Task DoWaypointPacket(Node nodeFrom, Packet packet, Waypoint? payload)
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

    public new async ValueTask DisposeAsync()
    {
        foreach (var mqttClientAndConfiguration in _mqttClientAndConfigurations.Where(m => m.Client.IsConnected))
        {
            Logger.LogWarning("Disconnect from MQTT {name}", mqttClientAndConfiguration.Configuration.Name);
            await mqttClientAndConfiguration.Client.DisconnectAsync();
        }
        
        await base.DisposeAsync();
    }

    public async Task<Position?> UpdatePosition(Node node, int latitude, int longitude, int altitude, Packet? packet, DataContext context)
    {
        if (latitude == 0 && longitude == 0)
        {
            Logger.LogWarning("Position given for {node} is incorrect : 0", node);
            
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
            Logger.LogTrace("Decrypt packet {packetId} from {nodeId} with key {key} KO", packetId, nodeFromId.ToHexString(), key);

            return null;
        }
    }
    
    private byte[]? Encrypt(byte[] input, string key, ulong packetId, uint nodeFromId)
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
    
    private class MqttClientAndConfiguration
    {
        public required IMqttClient Client { get; set; }
        public required MqttConfiguration Configuration { get; set; }
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
        
        var packets = Context.Packets.Where(a => a.CreatedAt < threeDays && a.Encrypted && string.IsNullOrWhiteSpace(a.PayloadJson)).ToList();

        if (packets.Count == 0)
        {
            return;
        }

        Logger.LogInformation("Delete {nbPackets} packets because they are too old < {date} and encrypted", packets.Count, threeDays);
        
        Context.RemoveRange(packets);
        await Context.SaveChangesAsync();
    }

    public async Task PurgeData()
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-_configuration.GetValue("PurgeDays", 3));
        
        var telemetries = Context.Telemetries.Where(a => a.CreatedAt < minDate).ToList();

        if (telemetries.Count > 0)
        {
            Logger.LogInformation("Delete {nbData} telemetries because they are too old < {date}", telemetries.Count,
                minDate);

            Context.RemoveRange(telemetries);
        }

        var positions = Context.Positions.Where(a => a.CreatedAt < minDate).ToList();

        if (positions.Count > 0)
        {
            Logger.LogInformation("Delete {nbData} telemetries because they are too old < {date}", positions.Count,
                minDate);

            Context.RemoveRange(positions);
        }

        var neighbors = Context.Telemetries.Where(a => a.CreatedAt < minDate).ToList();

        if (neighbors.Count > 0)
        {
            Logger.LogInformation("Delete {nbData} telemetries because they are too old < {date}", neighbors.Count,
                minDate);

            Context.RemoveRange(neighbors);
        }

        await Context.SaveChangesAsync();
    }

    private async Task KeepNbPacketsTypeForNode(Node node, PortNum portNum, int nbToKeep)
    {
        var packets = Context.Packets
            .Where(a => a.From == node && a.PortNum == portNum)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(nbToKeep)
            .ToList();

        if (packets.Count == 0)
        {
            return;
        }

        if (packets.Count > 1)
        {
            Logger.LogInformation(
                "Delete {nbPackets} of {type} packets for node #{node} because there are already {nbKeep} of them",
                packets.Count, portNum, node.Id, nbToKeep);
        }

        Context.RemoveRange(packets);
        await Context.SaveChangesAsync();
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

public class PublishMessageDto
{
    public string? MqttServer { get; set; }

    [Required]
    public string NodeFromId { get; set; } = "!1000001";
    
    [Required]
    public string Channel { get; set; } = "LongFast";
    
    [Required]
    public string Message { get; set; } = "Test";
    
    public string? NodeToId { get; set; }
    public uint HopLimit { get; set; } = 3;
    
    [Required]
    public string RootTopic { get; set; } = "msh/EU_868/2/";

    public string? Key { get; set; }// = "AQ==";
}