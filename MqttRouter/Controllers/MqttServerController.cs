using Common.Context;
using Common.Context.Entities.Router;
using Common.Extensions;
using Common.Extensions.Entities;
using Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet.Server;
using MqttRouter.Services;
using MqttServer = Common.Context.Entities.MqttServer;
using User = Common.Context.Entities.Router.User;

namespace MqttRouter.Controllers;

public class MqttServerController(IServiceProvider serviceProvider)
{
    private readonly ILogger<MqttServerController> _logger = serviceProvider.GetRequiredService<ILogger<MqttServerController>>();
    private readonly Dictionary<string, List<string>> _clientIdReceivers = new();
    private readonly Dictionary<string, long> _userIds = new();
    private readonly Dictionary<string, long> _nodeConfigurationIds = new();
    private MqttServer? _mqttServer;

    public async Task OnConnection(ValidatingConnectionEventArgs eventArgs)
    {
        var services = serviceProvider.CreateScope().ServiceProvider;

        _logger.LogInformation("Client {username} try to join with id {clientId}", eventArgs.UserName, eventArgs.ClientId);

        if (string.IsNullOrWhiteSpace(eventArgs.UserName))
        {
            _logger.LogWarning("Client {username} identified with id {clientId} as guest", eventArgs.UserName, eventArgs.ClientId);
            throw new UnauthorizedAccessException();
        }

        var user = await services.GetRequiredService<UserService>().Login(eventArgs.UserName, eventArgs.Password, eventArgs.Endpoint);

        if (user == null)
        {
            _logger.LogWarning("Client {username} not identified", eventArgs.UserName);
            throw new UnauthorizedAccessException();
        }
        
        _userIds.Add(eventArgs.ClientId, user.Id);
        
        _logger.LogInformation("Client {username}#{userId} identified with id {clientId}", eventArgs.UserName, user.Id, eventArgs.ClientId);
    }

    public async Task OnNewPacket(InterceptingPublishEventArgs eventArgs)
    {
        var services = serviceProvider.CreateScope().ServiceProvider;
        var context = services.GetRequiredService<DataContext>();
        var routingService = services.GetRequiredService<RoutingService>();
        var mqttService = services.GetRequiredService<MqttService>();

        var guid = Guid.NewGuid();

        routingService.SetDbContext(context);
        mqttService.SetDbContext(context);
        _mqttServer ??= await context.MqttServers.FirstAsync(a => a.Name == serviceProvider.GetRequiredService<IConfiguration>().GetValue<string>("MqttServerName"));

        _logger.LogInformation("Client {clientId} send packet {guid} on {topic}", eventArgs.ClientId, guid, eventArgs.ApplicationMessage.Topic);
        
        if (_clientIdReceivers.ContainsKey(eventArgs.ClientId))
        {
            _clientIdReceivers.Remove(eventArgs.ClientId);
        }

        var userId = GetUserId(eventArgs.ClientId);
        User? user = null;

        if (userId.HasValue)
        {
            user = await context.Users.FindByIdAsync(userId);
            if (user == null)
            {
                eventArgs.ProcessPublish = false;
                
                _logger.LogError("Client {clientId} and user#{userId} unknown. Refused packet", eventArgs.ClientId, userId);
                return;
            }

            if (!await serviceProvider.GetRequiredService<UserService>().IsAuthorized(user.Id))
            {
                eventArgs.ProcessPublish = false;
                
                _logger.LogError("Client {clientId} and user#{userId} locked. Refused packet", eventArgs.ClientId, userId);
                return;
            }
            
            user.LastSeenAt = DateTime.UtcNow;
            context.Update(user);
            await context.SaveChangesAsync();
        }
        
        var connectedNodeId = GetConnectedNodeId(eventArgs.ApplicationMessage.Topic);
        var nodeConfiguration = await context.NodeConfigurations.FirstOrDefaultAsync(a => a.Node.NodeId == connectedNodeId);

        if (connectedNodeId.HasValue)
        {
            if (nodeConfiguration != null)
            {
                if (nodeConfiguration.Forbidden)
                {
                    eventArgs.ProcessPublish = false;

                    _logger.LogError("Client {clientId} and user#{userId} for node#{nodeId} locked. Refused packet",
                        eventArgs.ClientId, userId, nodeConfiguration.Id);
                    return;
                }

                nodeConfiguration.User = user;
                nodeConfiguration.MqttId = eventArgs.ClientId;
                context.Update(nodeConfiguration);
                await context.SaveChangesAsync();
            }
            else
            {
                var node = await context.Nodes.FindByNodeIdAsync(connectedNodeId.Value);

                if (node != null)
                {
                    nodeConfiguration = new NodeConfiguration
                    {
                        Node = node,
                        User = user,
                        MqttId = eventArgs.ClientId
                    };
                    context.Add(nodeConfiguration);
                    await context.SaveChangesAsync();
                }
            }
        }

        if (nodeConfiguration != null && !_nodeConfigurationIds.ContainsKey(eventArgs.ClientId))
        {
            _nodeConfigurationIds.Add(eventArgs.ClientId, nodeConfiguration.Id);
        }

        _logger.LogInformation("Client {clientId} and user#{userId} send packet {guid} on {topic}", eventArgs.ClientId, userId, guid, eventArgs.ApplicationMessage.Topic);

        if (eventArgs.ApplicationMessage.Topic.Contains("stat") || eventArgs.ApplicationMessage.Topic.Contains("json"))
        {
            _logger.LogInformation("Client {clientId} and user#{userId} send packet {guid} on {topic} ignored", eventArgs.ClientId, userId, guid, eventArgs.ApplicationMessage.Topic);
            eventArgs.ProcessPublish = false;
            return;
        }
        
        var packetAndMeshPacket = await mqttService.DoReceive(eventArgs.ApplicationMessage.Topic, eventArgs.ApplicationMessage.Payload, _mqttServer!);

        if (packetAndMeshPacket?.packet != null)
        {
            if (packetAndMeshPacket.Value.packet.PacketDuplicated != null)
            {
                eventArgs.ProcessPublish = false;
                _logger.LogInformation("Client {clientId} and user#{userId} send packet {guid} on {topic} refused because packet#{packetId} duplicated", eventArgs.ClientId, userId, guid, eventArgs.ApplicationMessage.Topic, packetAndMeshPacket.Value.packet.Id);
                return;
            }
            
            var packetActivity = await routingService.Route(eventArgs.ClientId, userId, packetAndMeshPacket.Value.packet);
            eventArgs.ProcessPublish = packetActivity.Accepted;

            if (packetActivity.Accepted)
            {
                if (packetActivity.ReceiverIds.Count != 0)
                {
                    _clientIdReceivers.Add(eventArgs.ClientId, packetActivity.ReceiverIds);
                    _logger.LogInformation("Client {clientId} and user#{userId} send packet {guid} on {topic} accepted for {nbReceivers} : {comment}", eventArgs.ClientId, userId, guid, eventArgs.ApplicationMessage.Topic, packetActivity.ReceiverIds.Count, packetActivity.Comment);
                    _logger.LogTrace("Receivers for packet {guid} : {receivers}", guid, packetActivity.ReceiverIds.JoinString());
                }
                else
                {
                    _logger.LogInformation("Client {clientId} and user#{userId} send packet {guid} on {topic} accepted : {comment}", eventArgs.ClientId, userId, guid, eventArgs.ApplicationMessage.Topic, packetActivity.Comment);
                }
            }
            else
            {
                _logger.LogInformation("Client {clientId} and user#{userId} send packet {guid} on {topic} refused : {comment}", eventArgs.ClientId, userId, guid, eventArgs.ApplicationMessage.Topic, packetActivity.Comment);
            }
        }
        else
        {
            eventArgs.ProcessPublish = false;
            _logger.LogWarning("Client {clientId} and user#{userId} send packet {guid} on {topic} refused because packet error", eventArgs.ClientId, userId, guid, eventArgs.ApplicationMessage.Topic);
        }
    }

    private uint? GetConnectedNodeId(string topic)
    {
        var lastTopic = topic.Split('/').Last();
        uint? connectedNodeId = null;
        try
        {
            connectedNodeId = lastTopic.ToInteger();
        }
        catch
        {
            // Ignored
        }

        return connectedNodeId;
    }

    public Task BeforeSend(InterceptingClientApplicationMessageEnqueueEventArgs eventArgs)
    {
        if (!_clientIdReceivers.TryGetValue(eventArgs.SenderClientId, out var receivers) || receivers.Contains(eventArgs.ReceiverClientId))
        {
            _logger.LogInformation("Packet from client {senderId} on {topic} sent to {receiverId}", eventArgs.SenderClientId, eventArgs.ApplicationMessage.Topic, eventArgs.ReceiverClientId);
        }
        else
        {
            eventArgs.AcceptEnqueue = false;
            _logger.LogTrace("Packet from client {senderId} on {topic} not sent to {receiverId} because not in list", eventArgs.SenderClientId, eventArgs.ApplicationMessage.Topic, eventArgs.ReceiverClientId);
        }
        
        return Task.CompletedTask;   
    }
    
    public async Task OnDisconnection(ClientDisconnectedEventArgs eventArgs)
    {
        if (_nodeConfigurationIds.ContainsKey(eventArgs.ClientId))
        {
            _nodeConfigurationIds.Remove(eventArgs.ClientId);
        }
        if (_userIds.ContainsKey(eventArgs.ClientId))
        {
            _userIds.Remove(eventArgs.ClientId);
        }
        
        var services = serviceProvider.CreateScope().ServiceProvider;
        var context = services.GetRequiredService<DataContext>();
        
        var nodeConfigurationId = GetNodeConfigurationId(eventArgs.ClientId);
        if (nodeConfigurationId.HasValue)
        {
            var nodeConfiguration = await context.NodeConfigurations.FindByIdAsync(nodeConfigurationId.Value);
            if (nodeConfiguration != null)
            {
                nodeConfiguration.MqttId = null;
                context.Update(nodeConfiguration);
                await context.SaveChangesAsync();
            }
        }

        _logger.LogInformation("Client {clientId} for user{userId} and node configuration #{nodeConfigurationId} disconnected for reason {reason}", eventArgs.ClientId, GetUserId(eventArgs.ClientId), GetNodeConfigurationId(eventArgs.ClientId), eventArgs.ReasonString);
    }

    private long? GetUserId(string clientId)
    {
        return _userIds.TryGetValue(clientId, out var userId) ? userId : null;
    }

    private long? GetNodeConfigurationId(string clientId)
    {
        return _nodeConfigurationIds.TryGetValue(clientId, out var nodeConfigurationId) ? nodeConfigurationId : null;
    }
}