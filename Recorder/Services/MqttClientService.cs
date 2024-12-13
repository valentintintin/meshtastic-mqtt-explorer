using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Common.Context;
using Common.Context.Entities;
using Common.Extensions;
using Common.Services;
using Google.Protobuf;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using User = Meshtastic.Protobufs.User;
using Waypoint = Meshtastic.Protobufs.Waypoint;

namespace Recorder.Services;

public class MqttClientService : BackgroundService
{
    public static readonly List<MqttClientAndConfiguration> MqttClientAndConfigurations  = [];
    private readonly ILogger<MqttClientService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDbContextFactory<DataContext> _contextFactory;
    private readonly IHostEnvironment _environment;
    private readonly IServiceProvider _serviceProvider;

    public MqttClientService(ILogger<MqttClientService> logger, IConfiguration configuration, IDbContextFactory<DataContext> contextFactory, IHostEnvironment environment, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _contextFactory = contextFactory;
        _environment = environment;
        _serviceProvider = serviceProvider;
        
        MqttClientAndConfigurations.AddRange(
            contextFactory.CreateDbContext().MqttServers
            .Where(a => a.Enabled)
            .ToList()
            .Select(a => new MqttClientAndConfiguration
            {
                MqttServer = a,
                // Client = new MqttClientFactory().CreateMqttClient()
                Client = new MqttFactory().CreateMqttClient()
            })
        );
        
        foreach (var mqttClientAndConfiguration in MqttClientAndConfigurations)
        {
            mqttClientAndConfiguration.Client.ConnectedAsync += async _ =>
            {
                _logger.LogInformation("Connection successful to MQTT {name}", mqttClientAndConfiguration.MqttServer.Name);

                foreach (var topic in mqttClientAndConfiguration.MqttServer.Topics.Distinct())
                {
                    await mqttClientAndConfiguration.Client.SubscribeAsync(
                        new MqttTopicFilterBuilder()
                            .WithTopic(topic)
                            .WithRetainAsPublished(false)
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                            .Build());
                    
                    _logger.LogDebug("Subscription MQTT {name} to {topic}", mqttClientAndConfiguration.MqttServer.Name, topic);
                }
            };
            
            mqttClientAndConfiguration.Client.ApplicationMessageReceivedAsync += async e =>
            {
                var guid = Guid.NewGuid();
                var topic = e.ApplicationMessage.Topic;
                _logger.LogInformation("Received from {name} on {topic} with id {guid}", mqttClientAndConfiguration.MqttServer.Name, topic, guid);

                if (topic.Contains("json"))
                {
                    return;
                }

                if (topic.Contains("paho")) // Bad guys on Meshtastic MQTT server
                {
                    return;
                }

                if (topic.Contains("stat"))
                {
                    return;
                }

                if (topic.Contains("json"))
                {
                    return;
                }

                if (topic.Contains("RxCanaux")) // Gaulix filtre de Nivek
                {
                    return;
                }

                mqttClientAndConfiguration.NbPacket++;
                mqttClientAndConfiguration.LastPacketReceivedDate = DateTime.UtcNow;

                var mqttService = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<MqttService>();
                // var packet = await mqttService.DoReceive(topic, e.ApplicationMessage.Payload, mqttClientAndConfiguration.MqttServer);
                var packet = await mqttService.DoReceive(topic, new ReadOnlySequence<byte>(e.ApplicationMessage.Payload), mqttClientAndConfiguration.MqttServer);

                _logger.LogInformation("Received frame#{packetId} from {name} on {topic} with id {guid} done. Frame time {frameTime}", packet?.packet.Id, mqttClientAndConfiguration.MqttServer.Name, topic, guid, DateTimeOffset.FromUnixTimeSeconds(packet?.meshPacket.RxTime ?? 0));
            };

            mqttClientAndConfiguration.Client.DisconnectedAsync += async args =>
            {
                logger.LogWarning(args.Exception, "MQTT {name} disconnected, reason : {reason}", mqttClientAndConfiguration.MqttServer.Name, args.ReasonString);

                await Task.Delay(TimeSpan.FromSeconds(5));
                await ConnectMqtt();
            };
        }
    }
    
    public async Task PublishMessage(PublishMessageDto dto)
    {
        var mqttConfiguration = string.IsNullOrWhiteSpace(dto.MqttServer) ? MqttClientAndConfigurations.First() : 
            MqttClientAndConfigurations.First(a => a.MqttServer.Name == dto.MqttServer);
        
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
            case PublishMessageDto.MessageType.Raw:
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
    }

    private async Task ConnectMqtt()
    {
        foreach (var mqttClientAndConfiguration in MqttClientAndConfigurations.Where(m => !m.Client.IsConnected))
        {
            _logger.LogInformation("Run connection to MQTT {name}", mqttClientAndConfiguration.MqttServer.Name);

            var mqttClientOptionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(mqttClientAndConfiguration.MqttServer.Host, mqttClientAndConfiguration.MqttServer.Port)
                    .WithClientId($"MeshtasticMqttExplorer_ValentinF4HVV_{_environment.EnvironmentName}")
                    .WithCleanSession()
                ;
            
            if (!string.IsNullOrWhiteSpace(mqttClientAndConfiguration.MqttServer.Username))
            {
                mqttClientOptionsBuilder = mqttClientOptionsBuilder
                    .WithCredentials(mqttClientAndConfiguration.MqttServer.Username, mqttClientAndConfiguration.MqttServer.Password);
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
            _logger.LogWarning("Disconnect from MQTT {name}", mqttClientAndConfiguration.MqttServer.Name);
            await mqttClientAndConfiguration.Client.DisconnectAsync(cancellationToken: stoppingToken);
        }
    }
    
    public class MqttClientAndConfiguration
    {
        public required IMqttClient Client { get; init; }
        public required MqttServer MqttServer { get; init; }
        public int NbPacket { get; set; }
        public DateTime? LastPacketReceivedDate { get; set; }
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
    
    public PortNum? PortNum { get; set; }
    
    [MinLength(1)]
    public string? RawBase64 { get; set; }
    
    public enum MessageType
    {
        Message,
        NodeInfo,
        Position,
        Waypoint,
        Raw
    }
}