using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Common.Context;
using Common.Context.Entities;
using Common.Exceptions;
using Common.Extensions;
using Common.Jobs;
using Common.Models;
using Google.Protobuf;
using Hangfire;
using Meshtastic.Connections;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using DeviceConnection = Common.Models.DeviceConnection;
using Position = Meshtastic.Protobufs.Position;
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
    private readonly bool _isWorker = Assembly.GetEntryAssembly()?.GetName().Name?.Contains("Worker") ?? false;
    private readonly bool _isRecorder = Assembly.GetEntryAssembly()?.GetName().Name?.Contains("Recorder") ?? false;
    private readonly bool _isFront = Assembly.GetEntryAssembly()?.GetName().Name?.Contains("Explorer") ?? false;
    
    public async Task<(Packet packet, ServiceEnvelope serviceEnveloppe)?> DoReceive(string topic, byte[] payload, MqttServer mqttServer)
    {
        var services = serviceProvider.CreateScope().ServiceProvider;
        
        var mqttService = services.GetRequiredService<MqttService>();
        var packet = await mqttService.DoReceive(topic, payload, mqttServer);

        if (packet is { packet: not null, serviceEnveloppe: not null, packet.PacketDuplicatedId: null } && !mqttServer.IsHighLoad)
        {
            try
            {
                await RelayToAnotherMqttServer(topic, packet.Value.packet, packet.Value.serviceEnveloppe, mqttServer);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Relay error for Packet#{packetId} on topic {topic}", packet.Value.serviceEnveloppe.Packet.Id, topic);
            }
        }

        return packet;
    }

    private async Task RelayToAnotherMqttServer(string topic, Packet packet, ServiceEnvelope serviceEnvelope, MqttServer mqttServer)
    {
        var meshPacket = serviceEnvelope.Packet;
        
        foreach (var mqttClientAndConfiguration in MqttClientAndConfigurations.Where(a => a.Client.IsConnected && a.MqttServer != mqttServer && a.MqttServer.IsARelayType != null))
        {
            if (mqttClientAndConfiguration.MqttServer.IsARelayType == MqttServer.RelayType.All
                || mqttClientAndConfiguration.MqttServer.IsARelayType == MqttServer.RelayType.MapReport && meshPacket.Decoded?.Portnum == PortNum.MapReportApp
                || mqttClientAndConfiguration.MqttServer.IsARelayType == MqttServer.RelayType.NodeInfoAndPosition && meshPacket.Decoded?.Portnum is PortNum.MapReportApp or PortNum.NodeinfoApp or PortNum.PositionApp
                || mqttClientAndConfiguration.MqttServer.IsARelayType == MqttServer.RelayType.UseFull && meshPacket.Decoded?.Portnum is PortNum.MapReportApp or PortNum.NodeinfoApp or PortNum.PositionApp or PortNum.NeighborinfoApp or PortNum.TracerouteApp
               )
            {
                logger.LogInformation("Relay from MqttServer {name}, frame#{packetId} from {from} of type {portNum} to MqttServer {relayMqttName} topic {topic}", mqttServer.Name, packet.Id, packet.From, meshPacket.Decoded?.Portnum, mqttClientAndConfiguration.MqttServer.Name, topic);

                if (meshPacket.Decoded != null)
                {
                    try
                    {
                        var positionPrecision = mqttClientAndConfiguration.MqttServer.RelayPositionPrecision ?? 32;
                        var dataApp = new Data();
                        dataApp.MergeFrom(meshPacket.Decoded.Payload);

                        switch (meshPacket.Decoded?.Portnum)
                        {
                            case PortNum.PositionApp:
                            {
                                var position = new Position();
                                position.MergeFrom(meshPacket.Decoded.Payload);

                                if (position.PrecisionBits > positionPrecision)
                                {
                                    logger.LogDebug(
                                        "Relay frame#{packetId} change precision from {currentPrecision} to {newPrecision} for {portNum} and node #{node}",
                                        meshPacket.Id, position.PrecisionBits, positionPrecision, meshPacket.Decoded.Portnum, packet.From);

                                    position.LatitudeI =
                                        (int)(position.LatitudeI & (uint.MaxValue << (int)(32 - positionPrecision)));
                                    position.LongitudeI =
                                        (int)(position.LongitudeI & (uint.MaxValue << (int)(32 - positionPrecision)));
                                    position.LatitudeI += 1 << (int)(31 - positionPrecision);
                                    position.LongitudeI += 1 << (int)(31 - positionPrecision);
                                    position.PrecisionBits = positionPrecision;
                                    
                                    meshPacket.Decoded.Payload = position.ToByteString();
                                }

                                break;
                            }
                            case PortNum.MapReportApp:
                            {
                                var mapReport = new MapReport();
                                mapReport.MergeFrom(meshPacket.Decoded.Payload);

                                if (mapReport.PositionPrecision > positionPrecision)
                                {
                                    logger.LogDebug(
                                        "Relay frame#{packetId} change precision from {currentPrecision} to {newPrecision} for {portNum} and node #{node}",
                                        meshPacket.Id, mapReport.PositionPrecision, positionPrecision, meshPacket.Decoded.Portnum, packet.From);

                                    mapReport.LatitudeI =
                                        (int)(mapReport.LatitudeI & (uint.MaxValue << (int)(32 - positionPrecision)));
                                    mapReport.LongitudeI =
                                        (int)(mapReport.LongitudeI & (uint.MaxValue << (int)(32 - positionPrecision)));
                                    mapReport.LatitudeI += 1 << (int)(31 - positionPrecision);
                                    mapReport.LongitudeI += 1 << (int)(31 - positionPrecision);
                                    mapReport.PositionPrecision = positionPrecision;
                                    
                                    meshPacket.Decoded.Payload = mapReport.ToByteString();
                                }

                                break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Frame#{packetId} of type {portNum} impossible to change ProtoBuf", meshPacket.Id, meshPacket.Decoded!.Portnum);
                    }
                }

                var result = await mqttClientAndConfiguration.Client.PublishBinaryAsync(topic, serviceEnvelope.ToByteArray());

                if (!result.IsSuccess)
                {
                    logger.LogWarning("Relay from MqttServer {name}, frame#{packetId} from {from} of type {portNum} to MqttServer {relayMqttName} topic {topic} KO : {code}, {error}", mqttServer.Name, packet.Id, packet.From, meshPacket.Decoded?.Portnum, mqttClientAndConfiguration.MqttServer.Name, topic, result.ReasonCode, result.ReasonString);
                }
            }
        }
    }

    public async Task PublishMessage(SentPacketDto dto)
    {
        var mqttConfiguration = MqttClientAndConfigurations.FirstOrDefault(a => a.MqttServer.Id == dto.MqttServerId);

        if (mqttConfiguration == null)
        {
            throw new NotFoundException<MqttServer>(dto.MqttServerId);
        }
        
        var meshtasticService = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<MeshtasticService>();
        
        var topic = (string) dto.RootTopic.Clone();
        
        var packet = new ServiceEnvelope
        {
            ChannelId = dto.Channel,
            GatewayId = dto.NodeFromId,
            Packet = meshtasticService.CreateMeshPacketFromDto(dto)
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

        await meshtasticService.DoReceive(packet.Packet.From, dto.Channel, packet.Packet);
    }

    private async Task ConnectMqtt(MqttClientAndConfiguration mqttClientAndConfiguration)
    {
        if (mqttClientAndConfiguration.Client.IsConnected)
        {
            return;
        }

        logger.LogInformation("Run connection to MQTT {name}", mqttClientAndConfiguration.MqttServer.Name);

        var mqttClientOptionsBuilder = new MqttClientOptionsBuilder().WithTcpServer(mqttClientAndConfiguration.MqttServer.Host, mqttClientAndConfiguration.MqttServer.Port)
            .WithClientId($"MeshtasticMqttExplorer_{Assembly.GetEntryAssembly()?.GetName().Name}_{environment.EnvironmentName}_{mqttClientAndConfiguration.MqttServer.Name}#{mqttClientAndConfiguration.MqttServer.Id}")
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
            await Task.Delay(TimeSpan.FromSeconds(5));
            await ConnectMqtt(mqttClientAndConfiguration);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MqttClientAndConfigurations.AddRange(
            (await contextFactory.CreateDbContextAsync(stoppingToken)).MqttServers
            .Where(a => a.Enabled)
            .Where(a => a.Type == MqttServer.ServerType.Mqtt)
            .AsEnumerable()
            .Where(a => (_isRecorder && a.Topics.Count > 0) || (_isWorker && a.IsARelayType != null) || _isFront)
            .ToList()
            .Select(a => new MqttClientAndConfiguration
            {
                MqttServer = a,
                Client = new MqttClientFactory().CreateMqttClient()
            })
        );

        if (MqttClientAndConfigurations.Count == 0)
        {
            return;
        }
        
        foreach (var mqttClientAndConfiguration in MqttClientAndConfigurations)
        {
            mqttClientAndConfiguration.Client.ConnectedAsync += async _ =>
            {
                logger.LogInformation("Connection successful to MQTT {name}", mqttClientAndConfiguration.MqttServer.Name);

                if (_isRecorder)
                {
                    foreach (var topic in mqttClientAndConfiguration.MqttServer.Topics.Distinct())
                    {
                        await mqttClientAndConfiguration.Client.SubscribeAsync(
                            new MqttTopicFilterBuilder()
                                .WithTopic(topic)
                                .WithRetainAsPublished(false)
                                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                .Build(), cancellationToken: stoppingToken);

                        logger.LogDebug("Subscription MQTT {name} to {topic}",
                            mqttClientAndConfiguration.MqttServer.Name, topic);
                    }
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
                    var queue = mqttClientAndConfiguration.MqttServer.IsHighLoad ? "packet-2" : "packet";
                    backgroundJobClient.Enqueue<MqttReceiveJob>(queue, a => a.ExecuteAsync(topic, e.ApplicationMessage.Payload.ToArray(), mqttClientAndConfiguration.MqttServer.Id, guid));
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
                await ConnectMqtt(mqttClientAndConfiguration);
            };
        }

        Task.WaitAll(MqttClientAndConfigurations
            .Where(m => !m.Client.IsConnected)
            .Select(ConnectMqtt)
            .ToArray(), stoppingToken);
    }

    public void DisconnectAsync()
    {
        Task.WaitAll(MqttClientAndConfigurations
            .Where(a => a.Client.IsConnected)
            .Select(a => a.Client.DisconnectAsync())
            .ToArray());
    }
    
    public class MqttClientAndConfiguration : DeviceConnection
    {
        public required IMqttClient Client { get; init; }
    }
}