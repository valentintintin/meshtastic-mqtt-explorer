using System.ComponentModel.DataAnnotations;
using System.Reactive.Concurrency;
using System.Text;
using System.Text.Json;
using AntDesign;
using Google.Protobuf;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using MeshtasticMqttExplorer.Components;
using MeshtasticMqttExplorer.Context;
using MeshtasticMqttExplorer.Context.Entities;
using MeshtasticMqttExplorer.Extensions;
using Microsoft.EntityFrameworkCore;
using MqttConfiguration = MeshtasticMqttExplorer.Models.MqttConfiguration;
using Waypoint = Meshtastic.Protobufs.Waypoint;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace MeshtasticMqttExplorer.Services;

public class MqttService : BackgroundService
{
    public static readonly List<MqttClientAndConfiguration> MqttClientAndConfigurations  = [];
    private readonly ILogger<MqttService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDbContextFactory<DataContext> _contextFactory;
    private readonly IHostEnvironment _environment;
    private readonly IServiceProvider _serviceProvider;

    public MqttService(ILogger<MqttService> logger, IConfiguration configuration, IDbContextFactory<DataContext> contextFactory, IHostEnvironment environment, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _contextFactory = contextFactory;
        _environment = environment;
        _serviceProvider = serviceProvider;

        logger.LogInformation("Purge des données");
        PurgeOldData().ConfigureAwait(true).GetAwaiter().GetResult();
        PurgeOldPackets().ConfigureAwait(true).GetAwaiter().GetResult();;
        logger.LogInformation("Purge des données OK");

        _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IScheduler>().SchedulePeriodic(TimeSpan.FromHours(1), async () =>
        {
            await PurgeOldData();
            await PurgeOldPackets();
        });
        
        MqttClientAndConfigurations.AddRange(
            (configuration.GetSection("Mqtt").Get<List<MqttConfiguration>>() ?? throw new KeyNotFoundException("Mqtt"))
            .Where(a => a.Enabled)
            .Select(a => new MqttClientAndConfiguration
            {
                Configuration = a,
                Client = new MqttFactory().CreateMqttClient()
            })
            .ToList()
        );
        
        foreach (var mqttClientAndConfiguration in MqttClientAndConfigurations)
        {
            Utils.MqttServerFilters.Add(new TableFilter<string?> { Text = mqttClientAndConfiguration.Configuration.Name, Value = mqttClientAndConfiguration.Configuration.Name });
            
            mqttClientAndConfiguration.Client.ConnectedAsync += async _ =>
            {
                _logger.LogInformation("Connection successful to MQTT {name}", mqttClientAndConfiguration.Configuration.Name);

                foreach (var topic in mqttClientAndConfiguration.Configuration.Topics.Distinct())
                {
                    await mqttClientAndConfiguration.Client.SubscribeAsync(
                        new MqttTopicFilterBuilder()
                            .WithTopic(topic)
                            .WithRetainAsPublished(false)
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                            .Build());
                    
                    _logger.LogDebug("Subscription MQTT {name} to {topic}", mqttClientAndConfiguration.Configuration.Name, topic);
                }
            };
            
            mqttClientAndConfiguration.Client.ApplicationMessageReceivedAsync += async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                _logger.LogTrace("Received from {name} on {topic}", mqttClientAndConfiguration.Configuration.Name, topic);

                if (topic.Contains("json"))
                {
                    return;
                }

                if (topic.Contains("paho"))
                {
                    return;
                }

                if (topic.Contains("stat"))
                {
                    return;
                }

                mqttClientAndConfiguration.NbPacket++;
                mqttClientAndConfiguration.LastPacketDate = DateTime.UtcNow;

                await DoReceive(topic, e.ApplicationMessage.PayloadSegment.ToArray(), mqttClientAndConfiguration.Configuration);
            };

            mqttClientAndConfiguration.Client.DisconnectedAsync += async args =>
            {
                logger.LogWarning(args.Exception, "MQTT {name} disconnected, reason : {reason}", mqttClientAndConfiguration.Configuration.Name, args.ReasonString);

                await Task.Delay(TimeSpan.FromSeconds(5));
                await ConnectMqtt();
            };
        }
    }

    private async Task DoReceive(string topic, byte[]? data, MqttConfiguration mqttConfiguration)
    {
        var topicSegments = topic.Split("/");

        if (data == null || data.Length == 0)
        {
            _logger.LogWarning("Received from {name} on {topic} without data so ignored",
                mqttConfiguration.Name, topic);

            return;
        }

        var rootPacket = new ServiceEnvelope();

        try
        {
            rootPacket.MergeFrom(data);

            if (rootPacket.Packet == null)
            {
                _logger.LogWarning("Received from {name} on {topic} but packet null", mqttConfiguration.Name, topic);
                return;
            }

            await DoReceivePacket(rootPacket, mqttConfiguration, topicSegments);
        }
        catch (InvalidProtocolBufferException ex)
        {
            _logger.LogWarning(ex,
                "Received from {name} on {topic} but packet incorrect. Packet Raw : {packetRaw} | {rawString}",
                mqttConfiguration.Name, topic, Convert.ToBase64String(data), Encoding.UTF8.GetString(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error for a received MQTT message from {name} on {topic}. Packet : {packet}. Packet Raw : {packetRaw} | {rawString}",
                mqttConfiguration.Name, topic, JsonSerializer.Serialize(rootPacket),
                Convert.ToBase64String(data), Encoding.UTF8.GetString(data));
        }
    }

    private async Task DoReceivePacket(ServiceEnvelope rootPacket, MqttConfiguration mqtt, string[] topics)
    {
        if (!rootPacket.GatewayId.StartsWith('!'))
        {
            _logger.LogWarning("Node (gateway) incorrect : {nodeId}", rootPacket.GatewayId);
            return;
        }

        var nodeGatewayId = rootPacket.GatewayId.ToInteger();

        var meshtasticService = _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<MeshtasticService>();
        var packet = await meshtasticService.DoReceive(nodeGatewayId, rootPacket.ChannelId, rootPacket.Packet, mqtt.Name, topics);

        if (packet != null)
        {
            await KeepNbPacketsTypeForNode(packet.Value.packet.From, PortNum.MapReportApp, 10);
        }
    }

    public async Task PublishMessage(PublishMessageDto dto)
    {
        var mqttConfiguration = string.IsNullOrWhiteSpace(dto.MqttServer) ? MqttClientAndConfigurations.First() : 
            MqttClientAndConfigurations.First(a => a.Configuration.Name == dto.MqttServer);
        
        var meshtasticService = _serviceProvider.CreateAsyncScope().ServiceProvider.GetRequiredService<MeshtasticService>();

        var nodeFromId = dto.NodeFromId.ToInteger();
        var nodeToId = string.IsNullOrWhiteSpace(dto.NodeToId) ? MeshtasticService.NodeBroadcast : dto.NodeToId.ToInteger();
        var topic = (string) dto.RootTopic.Clone();

        var data = new Data();
        
        switch (Enum.Parse<PublishMessageDto.MessageType>(dto.Type))
        {
            case PublishMessageDto.MessageType.Message:
                if (string.IsNullOrWhiteSpace(dto.Message))
                {
                    throw new ValidationException("Le message est vide");
                }
                
                data.Portnum = PortNum.TextMessageApp;
                data.Payload = ByteString.CopyFrom(Encoding.UTF8.GetBytes(dto.Message!));
                break;
            case PublishMessageDto.MessageType.NodeInfo:
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
            case PublishMessageDto.MessageType.Position:
                data.Portnum = PortNum.PositionApp;
                data.Payload = ByteString.CopyFrom(new Meshtastic.Protobufs.Position
                {
                    LongitudeI = (int) (dto.Longitude / 0.0000001),
                    LatitudeI = (int) (dto.Latitude / 0.0000001),
                    Altitude = dto.Altitude
                }.ToByteArray());
                break;
            case PublishMessageDto.MessageType.Waypoint:
                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    throw new ValidationException("Le nom est vide");
                }
                
                data.Portnum = PortNum.WaypointApp;
                data.Payload = ByteString.CopyFrom(new Waypoint
                {
                    Id = dto.Id ?? (uint) Random.Shared.NextInt64(),
                    Name = dto.Name,
                    Description = dto.Description,
                    Expire = (uint)new DateTimeOffset(dto.Expires).ToUnixTimeSeconds(),
                    LongitudeI = (int) (dto.Longitude / 0.0000001),
                    LatitudeI = (int) (dto.Latitude / 0.0000001)
                }.ToByteArray());
                break;
        }
        
        var packet = new ServiceEnvelope
        {
            ChannelId = dto.Channel,
            GatewayId = dto.NodeFromId,
            Packet = meshtasticService.CreateMeshPacket(nodeToId, nodeFromId, dto.Channel, data, dto.HopLimit, dto.Key)
        };
        
        if (packet.Packet.Decoded != null)
        {
            topic += "e";
        }
        else
        {
            topic += "c";
        }

        topic += $"/{dto.Channel}/{dto.NodeFromId}";
        
        var packetBytes = packet.ToByteArray();
        
        _logger.LogInformation("Send packet to MQTT topic {topic} : {packet} with data {data} | {base64}", topic, JsonSerializer.Serialize(packet), JsonSerializer.Serialize(packet.Packet.GetPayload()), Convert.ToBase64String(packetBytes));

        await mqttConfiguration.Client.PublishBinaryAsync(topic, packetBytes);
        await DoReceive(topic, packetBytes, mqttConfiguration.Configuration);
    }

    public async Task PurgePacketsForNode(Node node)
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-_configuration.GetValue("PurgeDays", 3));
        var context = await _contextFactory.CreateDbContextAsync();
        
        _logger.LogTrace("Delete packets for node {node} if they are too old < {date}", node, minDate);
        
        context.RemoveRange(context.Packets.Where(a => a.CreatedAt < minDate && a.From == node));
        
        await context.SaveChangesAsync();
    }

    public async Task PurgeEncryptedPacketsForNode(Node node)
    {
        var threeDays = DateTime.UtcNow.Date.AddDays(-3);
        var context = await _contextFactory.CreateDbContextAsync();
        
        _logger.LogTrace("Delete packets for node {node} if they are too old < {date} and encrypted", node, threeDays);
        
        context.RemoveRange(context.Packets.Where(a => a.CreatedAt < threeDays && a.From == node && a.Encrypted && string.IsNullOrWhiteSpace(a.PayloadJson)));
        
        await context.SaveChangesAsync();
    }

    public async Task PurgeOldPackets()
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-_configuration.GetValue("PurgeDays", 3));
        var context = await _contextFactory.CreateDbContextAsync();
        
        _logger.LogTrace("Delete packets if they are too old < {date}", minDate);
        
        context.RemoveRange(context.Packets.Where(a => a.CreatedAt < minDate));
        
        await context.SaveChangesAsync();
    }

    public async Task PurgeDataForNode(Node node)
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-_configuration.GetValue("PurgeDays", 3));
        var context = await _contextFactory.CreateDbContextAsync();
        
        _logger.LogTrace("Delete old data for node {node} if they are too old < {date}", node, minDate);
        
        context.RemoveRange(context.Telemetries.Where(a => a.CreatedAt < minDate && a.Node == node));
        context.RemoveRange(context.Positions.Where(a => a.CreatedAt < minDate && a.Node == node));
        context.RemoveRange(context.Telemetries.Where(a => a.CreatedAt < minDate && a.Node == node));

        await context.SaveChangesAsync();
    }

    public async Task PurgeOldData()
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-_configuration.GetValue("PurgeDays", 3));
        var context = await _contextFactory.CreateDbContextAsync();
        
        _logger.LogTrace("Delete old data if they are too old < {date}", minDate);
        
        context.RemoveRange(context.Telemetries.Where(a => a.CreatedAt < minDate));
        context.RemoveRange(context.Positions.Where(a => a.CreatedAt < minDate));
        context.RemoveRange(context.Telemetries.Where(a => a.CreatedAt < minDate));

        await context.SaveChangesAsync();
    }

    private async Task KeepNbPacketsTypeForNode(Node node, PortNum portNum, int nbToKeep)
    {
        _logger.LogTrace("Delete {type} packets for node #{node} if there are > {nbToKeep}", portNum, node.Id, nbToKeep);
        var context = await _contextFactory.CreateDbContextAsync();
        
        context.RemoveRange(context.Packets
            .Where(a => a.From == node && a.PortNum == portNum)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(nbToKeep));

        await context.SaveChangesAsync();
    }

    private async Task KeepDatedPacketsTypeForNode(Node node, PortNum portNum, int nbDays)
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-nbDays);
        var context = await _contextFactory.CreateDbContextAsync();
        
        _logger.LogTrace("Delete {type} packets for node #{node} if they are too old < {date}", portNum, node.Id, minDate);
        
        context.RemoveRange(context.Packets
            .Where(a => a.From == node && a.PortNum == portNum && a.CreatedAt < minDate)
        );
        
        await context.SaveChangesAsync();
    }

    private async Task ConnectMqtt()
    {
        foreach (var mqttClientAndConfiguration in MqttClientAndConfigurations.Where(m => !m.Client.IsConnected))
        {
            _logger.LogInformation("Run connection to MQTT {name}", mqttClientAndConfiguration.Configuration.Name);

            var mqttClientOptionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(mqttClientAndConfiguration.Configuration.Host, mqttClientAndConfiguration.Configuration.Port)
                    .WithClientId($"MeshtasticMqttExplorer_ValentinF4HVV_{_environment.EnvironmentName}")
                ;
            
            if (!string.IsNullOrWhiteSpace(mqttClientAndConfiguration.Configuration.Username))
            {
                mqttClientOptionsBuilder = mqttClientOptionsBuilder
                    .WithCredentials(mqttClientAndConfiguration.Configuration.Username, mqttClientAndConfiguration.Configuration.Password);
            }

            await mqttClientAndConfiguration.Client.ConnectAsync(mqttClientOptionsBuilder.Build());   
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("ConnectToMqtt", true))
        {
            _logger.LogWarning("MQTT disabled");
            
            return;
        }

        await ConnectMqtt();

        stoppingToken.WaitHandle.WaitOne();
        
        foreach (var mqttClientAndConfiguration in MqttClientAndConfigurations.Where(m => m.Client.IsConnected))
        {
            _logger.LogWarning("Disconnect from MQTT {name}", mqttClientAndConfiguration.Configuration.Name);
            await mqttClientAndConfiguration.Client.DisconnectAsync();
        }
    }
    
    public class MqttClientAndConfiguration
    {
        public required IMqttClient Client { get; init; }
        public required MqttConfiguration Configuration { get; init; }
        public uint NbPacket { get; set; }
        public DateTime? LastPacketDate { get; set; }
    }
}

public class PublishMessageDto
{
    public string MqttServer { get; set; } = null!;

    [Required]
    [Length(2, 9)]
    public string NodeFromId { get; set; } = "!1000001";
    
    [Required]
    [MinLength(1)]
    public string Channel { get; set; } = "LongFast";
    
    [Length(2, 9)]
    public string? NodeToId { get; set; }
    
    [Range(0, 7)]
    public uint HopLimit { get; set; } = 3;
    
    [Required]
    [MinLength(4)]
    public string RootTopic { get; set; } = "msh/EU_868/2/";

    [MinLength(4)]
    public string? Key { get; set; }// = "AQ==";
    
    public string Type { get; set; } = MessageType.Message.ToString();
    
    [Length(1, 200)]
    public string? Message { get; set; }
    
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Altitude { get; set; }
    
    [Length(4, 4)]
    public string? ShortName { get; set; }
    
    [Length(1, 37)]
    public string? Name { get; set; }
    
    [MaxLength(100)]
    public string? Description { get; set; }
    
    public uint? Id { get; set; }
    
    public DateTime Expires { get; set; } = DateTime.UtcNow.AddDays(1);
    
    public enum MessageType
    {
        Message,
        NodeInfo,
        Position,
        Waypoint
    }
}