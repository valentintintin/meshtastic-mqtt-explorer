using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Common.Context;
using Common.Context.Entities;
using Common.Extensions;
using Common.Jobs;
using Google.Protobuf;
using Hangfire;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using User = Meshtastic.Protobufs.User;
using Waypoint = Meshtastic.Protobufs.Waypoint;

namespace Common.Services;

public class MqttClientService(
    ILogger<MqttClientService> logger,
    IConfiguration configuration,
    IDbContextFactory<DataContext> contextFactory,
    IHostEnvironment environment,
    IServiceProvider serviceProvider,
    IBackgroundJobClient backgroundJobClient)
    : BackgroundService
{
    public readonly List<MqttClientAndConfiguration> MqttClientAndConfigurations  = [];

    public async Task<(Packet packet, MeshPacket meshPacket)?> DoReceive(string topic, byte[] payload, MqttServer mqttServer)
    {
        var services = serviceProvider.CreateScope().ServiceProvider;
        
        var mqttService = services.GetRequiredService<MqttService>();
        var packet = await mqttService.DoReceive(topic, payload, mqttServer);

        if (packet is { packet: not null, meshPacket: not null })
        {
            await RelayToAnotherMqttServer(topic, packet.Value.packet, packet.Value.meshPacket, mqttServer);
        }

        return packet;
    }

    private async Task RelayToAnotherMqttServer(string topic, Packet packet, MeshPacket meshPacket, MqttServer mqttServer)
    {
        List<Task> tasks = [];
        
        foreach (var mqttClientAndConfiguration in MqttClientAndConfigurations.Where(a => a.MqttServer != mqttServer && a.MqttServer.IsARelayType != null))
        {
            if (mqttClientAndConfiguration.MqttServer.IsARelayType == MqttServer.RelayType.All
                || mqttClientAndConfiguration.MqttServer.IsARelayType == MqttServer.RelayType.MapReport && meshPacket!.Decoded?.Portnum == PortNum.MapReportApp
                || mqttClientAndConfiguration.MqttServer.IsARelayType == MqttServer.RelayType.NodeInfoAndPosition && (meshPacket!.Decoded?.Portnum is PortNum.MapReportApp or PortNum.NodeinfoApp or PortNum.PositionApp)
                || mqttClientAndConfiguration.MqttServer.IsARelayType == MqttServer.RelayType.UseFull && (meshPacket!.Decoded?.Portnum is PortNum.MapReportApp or PortNum.NodeinfoApp or PortNum.PositionApp or PortNum.NeighborinfoApp or PortNum.TracerouteApp)
            )
            {
                logger.LogInformation("Relay from MqttServer {name}, frame#{packetId} from {from} of type {portNum} to MqttServer {relayMqttName} topic {topic}", mqttServer.Name, packet.Id, packet.From, meshPacket.Decoded?.Portnum, mqttClientAndConfiguration.MqttServer.Name, topic);

                tasks.Add(mqttClientAndConfiguration.Client.PublishBinaryAsync(topic, meshPacket.ToByteArray()));
            }
        }
        
        await Task.WhenAll(tasks);
    }

    public async Task PublishMessage(PublishMessageDto dto)
    {
        var mqttConfiguration = string.IsNullOrWhiteSpace(dto.MqttServer) ? MqttClientAndConfigurations.First() : 
            MqttClientAndConfigurations.First(a => a.MqttServer.Name == dto.MqttServer);
        
        var meshtasticService = serviceProvider.CreateAsyncScope().ServiceProvider.GetRequiredService<MeshtasticService>();

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
        
        logger.LogInformation("Send packet to MQTT topic {topic} : {packet} with data {data} | {base64}", topic, JsonSerializer.Serialize(packet), JsonSerializer.Serialize(packet.Packet.GetPayload()), Convert.ToBase64String(packetBytes));

        await mqttConfiguration.Client.PublishBinaryAsync(topic, packetBytes);
    }

    private async Task ConnectMqtt()
    {
        while (true)
        {
            var shouldRetry = false;

            foreach (var mqttClientAndConfiguration in MqttClientAndConfigurations.Where(m => !m.Client.IsConnected))
            {
                logger.LogInformation("Run connection to MQTT {name}", mqttClientAndConfiguration.MqttServer.Name);

                var mqttClientOptionsBuilder = new MqttClientOptionsBuilder().WithTcpServer(mqttClientAndConfiguration.MqttServer.Host, mqttClientAndConfiguration.MqttServer.Port)
                    .WithClientId($"MeshtasticMqttExplorer_ValentinF4HVV_{environment.EnvironmentName}")
                    .WithCleanSession();

                if (!string.IsNullOrWhiteSpace(mqttClientAndConfiguration.MqttServer.Username))
                {
                    mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithCredentials(mqttClientAndConfiguration.MqttServer.Username, mqttClientAndConfiguration.MqttServer.Password);
                }

                try
                {
                    await mqttClientAndConfiguration.Client.ConnectAsync(mqttClientOptionsBuilder.Build());
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Erreur during MQTT connection to {mqttServer}", mqttClientAndConfiguration.MqttServer.Name);
                    shouldRetry = true;
                }
            }

            if (shouldRetry)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                continue;
            }

            break;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("ConnectToMqtt", true))
        {
            logger.LogWarning("MQTT disabled");
            
            return;
        }
        
        MqttClientAndConfigurations.AddRange(
            (await contextFactory.CreateDbContextAsync(stoppingToken)).MqttServers
                .Where(a => a.Enabled)
                .ToList()
                .Select(a => new MqttClientAndConfiguration
                {
                    MqttServer = a,
                    Client = new MqttClientFactory().CreateMqttClient()
                })
        );
        
        foreach (var mqttClientAndConfiguration in MqttClientAndConfigurations)
        {
            mqttClientAndConfiguration.Client.ConnectedAsync += async _ =>
            {
                logger.LogInformation("Connection successful to MQTT {name}", mqttClientAndConfiguration.MqttServer.Name);

                foreach (var topic in mqttClientAndConfiguration.MqttServer.Topics.Distinct())
                {
                    await mqttClientAndConfiguration.Client.SubscribeAsync(
                        new MqttTopicFilterBuilder()
                            .WithTopic(topic)
                            .WithRetainAsPublished(false)
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                            .Build(), cancellationToken: stoppingToken);
                    
                    logger.LogDebug("Subscription MQTT {name} to {topic}", mqttClientAndConfiguration.MqttServer.Name, topic);
                }
            };
            
            mqttClientAndConfiguration.Client.ApplicationMessageReceivedAsync += async e =>
            {
                var guid = Guid.NewGuid();
                var topic = e.ApplicationMessage.Topic;

                if (topic.Contains("paho") || topic.Contains("stat") || topic.Contains("json"))
                {
                    return;
                }

                logger.LogInformation("Received from {name} on {topic} with id {guid}", mqttClientAndConfiguration.MqttServer.Name, topic, guid);

                mqttClientAndConfiguration.NbPacket++;
                mqttClientAndConfiguration.LastPacketReceivedDate = DateTime.UtcNow;

                if (configuration.GetValue("UseWorker", false))
                {
                    backgroundJobClient.Enqueue<MqttReceiveJob>("packet", a => a.ExecuteAsync(topic,
                        e.ApplicationMessage.Payload.ToArray(), mqttClientAndConfiguration.MqttServer.Id, guid));
                }
                else
                {
                    await DoReceive(topic, e.ApplicationMessage.Payload.ToArray(),
                        mqttClientAndConfiguration.MqttServer);
                }
            };

            mqttClientAndConfiguration.Client.DisconnectedAsync += async args =>
            {
                logger.LogWarning(args.Exception, "MQTT {name} disconnected, reason : {reason}", mqttClientAndConfiguration.MqttServer.Name, args.ReasonString);

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                await ConnectMqtt();
            };
        }

        await ConnectMqtt();

        stoppingToken.WaitHandle.WaitOne();
        
        foreach (var mqttClientAndConfiguration in MqttClientAndConfigurations.Where(m => m.Client.IsConnected))
        {
            logger.LogWarning("Disconnect from MQTT {name}", mqttClientAndConfiguration.MqttServer.Name);
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