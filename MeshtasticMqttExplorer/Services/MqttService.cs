using System.ComponentModel.DataAnnotations;
using System.Reactive.Concurrency;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using MeshtasticMqttExplorer.Context;
using MeshtasticMqttExplorer.Context.Entities;
using MeshtasticMqttExplorer.Extensions;
using MeshtasticMqttExplorer.Extensions.Entities;
using MeshtasticMqttExplorer.Models;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace MeshtasticMqttExplorer.Services;

public class MqttService : BackgroundService
{
    private readonly List<MqttClientAndConfiguration> _mqttClientAndConfigurations;
    private readonly ILogger<MqttService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDbContextFactory<DataContext> _contextFactory;
    private readonly IHostEnvironment _environment;
    private readonly IServiceProvider _serviceProvider;

    public static int NbMessages { get; set; }
    public static int NbMessagesToDo { get; set; }

    public MqttService(ILogger<MqttService> logger, IConfiguration configuration, IDbContextFactory<DataContext> contextFactory, IHostEnvironment environment, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _contextFactory = contextFactory;
        _environment = environment;
        _serviceProvider = serviceProvider;
        
        serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IScheduler>().SchedulePeriodic(TimeSpan.FromMinutes(15), GC.Collect);

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
                _logger.LogInformation("Connection successful to MQTT {name}", mqttConfiguration.Configuration.Name);

                foreach (var topic in mqttConfiguration.Configuration.Topics.Distinct())
                {
                    await mqttConfiguration.Client.SubscribeAsync(
                        new MqttTopicFilterBuilder()
                            .WithTopic(topic)
                            .Build());
                    
                    _logger.LogDebug("Subscription MQTT {name} to {topic}", mqttConfiguration.Configuration.Name, topic);
                }
            };

            mqttConfiguration.Client.ApplicationMessageReceivedAsync += async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                logger.LogTrace("Received from {name} on {topic}", mqttConfiguration.Configuration.Name, topic);

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
                
                NbMessages++;
                NbMessagesToDo++;

                await DoReceive(e.ApplicationMessage.Topic, e.ApplicationMessage.PayloadSegment.ToArray(), mqttConfiguration.Configuration);
                
                NbMessagesToDo--;
            };

            mqttConfiguration.Client.DisconnectedAsync += async args =>
            {
                logger.LogWarning(args.Exception, "MQTT {name} disconnected, reason : {reason}", mqttConfiguration.Configuration.Name, args.ReasonString);

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
            var node = packet.Value.packet.From;
            await PurgeDataForNode(node);
            await PurgeEncryptedPacketsForNode(node);
            await PurgePacketsForNode(node);
        }
    }

    public async Task PublishMessage(PublishMessageDto dto)
    {
        var mqttConfiguration = string.IsNullOrWhiteSpace(dto.MqttServer) ? _mqttClientAndConfigurations.First() : 
            _mqttClientAndConfigurations.First(a => a.Configuration.Name == dto.MqttServer);
        
        var meshtasticService = _serviceProvider.CreateAsyncScope().ServiceProvider.GetRequiredService<MeshtasticService>();

        var nodeFromId = dto.NodeFromId.ToInteger();
        var nodeToId = string.IsNullOrWhiteSpace(dto.NodeToId) ? MeshtasticService.NodeBroadcast : dto.NodeToId.ToInteger();
        var topic = dto.RootTopic.Clone();

        var data = new Data
        {
            Portnum = PortNum.TextMessageApp,
            Payload = ByteString.CopyFrom(Encoding.UTF8.GetBytes(dto.Message))
        };
        
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
        _logger.LogInformation("Send packet to MQTT topic {topic} : {packet} with data {data}", topic, JsonSerializer.Serialize(packet), JsonSerializer.Serialize(packet.Packet.GetPayload()));

        // await mqttConfiguration.Client.PublishBinaryAsync(topic, packet.ToByteArray());
    }
    
    private class MqttClientAndConfiguration
    {
        public required IMqttClient Client { get; set; }
        public required MqttConfiguration Configuration { get; set; }
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
        foreach (var mqttClientAndConfiguration in _mqttClientAndConfigurations.Where(m => !m.Client.IsConnected))
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
        
        foreach (var mqttClientAndConfiguration in _mqttClientAndConfigurations.Where(m => m.Client.IsConnected))
        {
            _logger.LogWarning("Disconnect from MQTT {name}", mqttClientAndConfiguration.Configuration.Name);
            await mqttClientAndConfiguration.Client.DisconnectAsync();
        }
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