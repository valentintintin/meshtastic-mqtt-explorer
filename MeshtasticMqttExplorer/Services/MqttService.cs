using System.Globalization;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Google.Protobuf;
using Meshtastic.Protobufs;
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

namespace MeshtasticMqttExplorer.Services;

public class MqttService : AService, IAsyncDisposable
{
    public static readonly uint NodeBroadcast = 4294967295;
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

                await DoReceive(rootPacket, mqttClientAndConfiguration, e.ApplicationMessage.Topic);
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
        var node = await mqtt.Context.Nodes.FindByNodeIdAsync(nodeId) ?? new Node
        {
            NodeId = nodeId,
            IsMqttGateway = status == "online"
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
            node.IsMqttGateway = status == "online";
            node.LastSeen = DateTime.UtcNow;
            mqtt.Context.Update(node);
        }
        await mqtt.Context.SaveChangesAsync();
    }

    private async Task DoReceive(ServiceEnvelope rootPacket, MqttClientAndConfiguration mqtt, string topic)
    {
        var nodeGatewayId = uint.Parse(rootPacket.GatewayId[1..], NumberStyles.HexNumber);
        var nodeGateway = await mqtt.Context.Nodes.FindByNodeIdAsync(nodeGatewayId) ?? new Node
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
        
        var nodeFrom = await mqtt.Context.Nodes.FindByNodeIdAsync(rootPacket.Packet.From) ?? new Node
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
            nodeFrom.ModemPreset ??= nodeGateway.ModemPreset;
            nodeFrom.RegionCode ??= nodeGateway.RegionCode;
            mqtt.Context.Update(nodeFrom);
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
            await mqtt.Context.SaveChangesAsync();
        }
        else if (nodeTo.NodeId != NodeBroadcast)
        {
            nodeTo.ModemPreset ??= nodeGateway.ModemPreset;
            nodeTo.RegionCode ??= nodeGateway.RegionCode;
            mqtt.Context.Update(nodeTo);
        }
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
        
        // todo : Dechiffrer avec AQ== si chiffré : https://github.com/liamcottle/meshtastic-map/blob/1fb6a27ab035a708e674314c82b71ffe801f1f61/src/mqtt.js#L239

        var payload = rootPacket.Packet.GetPayload();
        
        var packet = new Packet
        {
            Channel = channel,
            Gateway = nodeGateway,
            Encrypted = rootPacket.Packet.Decoded?.Portnum == null,
            Priority = rootPacket.Packet.Priority,
            PacketId = rootPacket.Packet.Id,
            WantAck = rootPacket.Packet.WantAck,
            RxSnr = rootPacket.Packet.RxSnr,
            RxRssi = rootPacket.Packet.RxRssi,
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
            MqttTopic = topic
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
        }
        
        NewPacket.OnNext(packet);
    }

    public async Task ConnectMqtt()
    {
        foreach (var mqttClientAndConfiguration in _mqttClientAndConfigurations.Where(m => !m.Client.IsConnected))
        {
            Logger.LogInformation("Run connection to MQTT {name}", mqttClientAndConfiguration.Configuration.Name);
            
            await mqttClientAndConfiguration.Client.ConnectAsync(new MqttClientOptionsBuilder()
                .WithTcpServer(mqttClientAndConfiguration.Configuration.Host, mqttClientAndConfiguration.Configuration.Port)
                .WithCredentials(mqttClientAndConfiguration.Configuration.Username, mqttClientAndConfiguration.Configuration.Password)
                .WithClientId($"MeshtasticMqttExplorer_ValentinF4HVV_{_environment.EnvironmentName}")
                .Build());   
        }
    }

    private async Task DoMapReportingPacket(MqttClientAndConfiguration mqtt, Node nodeFrom, MapReport? mapReport)
    {
        if (mapReport == null)
        {
            return;
        }
        
        await KeepNbPacketsTypeForNode(nodeFrom, PortNum.MapReportApp, 10);

        nodeFrom.ShortName = mapReport.ShortName;
        nodeFrom.LongName = mapReport.LongName;
        nodeFrom.Latitude = mapReport.LatitudeI * 0.0000001;
        nodeFrom.Longitude = mapReport.LongitudeI * 0.0000001;
        nodeFrom.Altitude = mapReport.Altitude;
        nodeFrom.Role = mapReport.Role;
        nodeFrom.HardwareModel = mapReport.HwModel;
        nodeFrom.ModemPreset = mapReport.ModemPreset;
        nodeFrom.RegionCode = mapReport.Region;
        nodeFrom.NumOnlineLocalNodes = (int?)mapReport.NumOnlineLocalNodes;
        nodeFrom.FirmwareVersion = mapReport.FirmwareVersion;
        nodeFrom.HasDefaultChannel = mapReport.HasDefaultChannel;
        
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

        var position = new Position
        {
            Node = nodeFrom,
            Packet = packet,
            Latitude =  positionPayload.LatitudeI * 0.0000001,
            Longitude =  positionPayload.LongitudeI * 0.0000001,
            Altitude = positionPayload.Altitude
        };
        
        mqtt.Context.Add(position);

        nodeFrom.Latitude = position.Latitude;
        nodeFrom.Longitude = position.Longitude;
        nodeFrom.Altitude = position.Altitude;
        
        Logger.LogInformation("Update {node} with new Position", nodeFrom);
        
        mqtt.Context.Update(nodeFrom);
        await mqtt.Context.SaveChangesAsync();
        NewPosition.OnNext(position);
        NewNode.OnNext(nodeFrom);
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
            var neighborNode = await mqtt.Context.Nodes.FindByNodeIdAsync(neighbor.NodeId) ?? new Node
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
                neighborNode.RegionCode ??= nodeFrom.RegionCode;
                neighborNode.ModemPreset ??= nodeFrom.ModemPreset;
                mqtt.Context.Update(neighborNode);
            }
            NewNode.OnNext(neighborNode);
            await mqtt.Context.SaveChangesAsync();
            
            var neighborInfo = await mqtt.Context.NeighborInfos.FirstOrDefaultAsync(n => n.Node == nodeFrom && n.Neighbor == neighborNode) ?? new MeshtasticMqttExplorer.Context.Entities.NeighborInfo
            {
                Node = nodeFrom,
                Packet = packet,
                Neighbor = neighborNode,
                Snr = neighbor.Snr
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
        
        Context.RemoveRange(packets);
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