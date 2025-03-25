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
    public static readonly List<MqttClientAndConfiguration> MqttClientAndConfigurations  = [];
    private readonly bool _isWorker = Assembly.GetEntryAssembly()?.GetName().Name?.Contains("Worker") ?? false;
    private readonly bool _isRecorder = Assembly.GetEntryAssembly()?.GetName().Name?.Contains("Recorder") ?? false;
    private readonly bool _isFront = Assembly.GetEntryAssembly()?.GetName().Name?.Contains("Explorer") ?? false;
    
    public async Task<(Packet packet, ServiceEnvelope serviceEnveloppe)?> DoReceive(string topic, byte[] payload, MqttServer mqttServer)
    {
        var services = serviceProvider.CreateScope().ServiceProvider;
        var mqttService = services.GetRequiredService<MqttService>();

        var mqttClient = MqttClientAndConfigurations.FirstOrDefault(a => a.MqttServer == mqttServer)?.Client;
        var packet = await mqttService.DoReceive(topic, payload, mqttServer, mqttClient != null ? async message => await mqttClient.PublishAsync(message) : null);

        return packet;
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
                if (mqttClientAndConfiguration.MqttServer.IsHighLoad && JobStorage.Current.GetMonitoringApi().EnqueuedCount("packet-2") > 20)
                {
                    return;
                }
                
                var topic = e.ApplicationMessage.Topic;

                if (topic.Contains("paho") || topic.Contains("stat") || topic.Contains("json"))
                {
                    return;
                }

                var guid = Guid.NewGuid();

                logger.LogInformation("Received from {name} on {topic} with id {guid}", mqttClientAndConfiguration.MqttServer.Name, topic, guid);

                mqttClientAndConfiguration.NbPacket++;
                mqttClientAndConfiguration.LastPacketReceivedDate = DateTime.UtcNow;

                if (configuration.GetValue("UseWorker", false) || mqttClientAndConfiguration.MqttServer.IsHighLoad)
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