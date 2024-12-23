using Common;
using Common.Context;
using Common.Context.Entities.Router;
using Common.Exceptions;
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
    private readonly ILogger<MqttServerController> _logger =
        serviceProvider.GetRequiredService<ILogger<MqttServerController>>();

    private readonly Dictionary<string, List<string>> _clientIdReceivers = new();
    private readonly Dictionary<string, long> _userIds = new();
    private readonly List<string> _clientIdsWithClaimEveryPacket = [];
    private MqttServer? _mqttServer;

    public async Task OnConnection(ValidatingConnectionEventArgs eventArgs)
    {
        var services = serviceProvider.CreateScope().ServiceProvider;

        _logger.LogInformation("Client {username} try to join with id {clientId}", eventArgs.UserName,
            eventArgs.ClientId);

        if (string.IsNullOrWhiteSpace(eventArgs.UserName))
        {
            _logger.LogWarning("Client {username} identified with id {clientId} as guest", eventArgs.UserName,
                eventArgs.ClientId);
            throw new UnauthorizedAccessException();
        }

        var userService = services.GetRequiredService<UserService>();
        var user = await userService.Login(eventArgs.UserName, eventArgs.Password, eventArgs.Endpoint);

        if (user == null)
        {
            _logger.LogWarning("Client {username} not identified", eventArgs.UserName);
            throw new UnauthorizedAccessException();
        }

        _userIds.Add(eventArgs.ClientId, user.Id);

        if (await userService.HasClaim(user, SecurityConstants.Claim.ReceiveEveryPackets))
        {
            _logger.LogInformation("Client {username}#{userId} identified with id {clientId} has claim ReceiveEveryPackets", eventArgs.UserName, user.Id, eventArgs.ClientId);            
            _clientIdsWithClaimEveryPacket.Add(eventArgs.ClientId);
        }

        _logger.LogInformation("Client {username}#{userId} identified with id {clientId}", eventArgs.UserName, user.Id,
            eventArgs.ClientId);
    }

    public async Task OnNewPacket(InterceptingPublishEventArgs eventArgs)
    {
        var services = serviceProvider.CreateScope().ServiceProvider;
        var context = services.GetRequiredService<DataContext>();
        var routingService = services.GetRequiredService<RoutingService>();
        var mqttService = services.GetRequiredService<MqttService>();

        var guid = Guid.NewGuid();
        var receiverIds = _clientIdsWithClaimEveryPacket.ToList();

        routingService.SetDbContext(context);
        mqttService.SetDbContext(context);
        _mqttServer ??= await context.MqttServers.FirstAsync(a =>
            a.Name == serviceProvider.GetRequiredService<IConfiguration>().GetValue<string>("MqttServerName"));

        _logger.LogInformation("Client {clientId} send packet {guid} on {topic}", eventArgs.ClientId, guid,
            eventArgs.ApplicationMessage.Topic);

        User? user;
        try
        {
            user = await GetAndUpdateUserFromClientId(eventArgs.ClientId, context);
            _logger.LogInformation("Client {clientId} send packet {guid} on {topic}. User #{userId}", eventArgs.ClientId, guid,
                eventArgs.ApplicationMessage.Topic, user?.Id);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Client {clientId} send packet {guid} on {topic} KO. User issue", eventArgs.ClientId, guid,
                eventArgs.ApplicationMessage.Topic);
            return;
        }
        
        NodeConfiguration? nodeConfiguration;
        try
        {
            nodeConfiguration = await GetAndUpdateNodeConfiguration(eventArgs.ClientId,
                eventArgs.ApplicationMessage.Topic, context, user);
            _logger.LogInformation("Client {clientId} send packet {guid} on {topic}. Node configuration #{nodeConfigurationId}", eventArgs.ClientId, guid,
                eventArgs.ApplicationMessage.Topic, nodeConfiguration?.Id);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Client {clientId} send packet {guid} on {topic} KO. Node configuration issue", eventArgs.ClientId, guid,
                eventArgs.ApplicationMessage.Topic);
            return;
        }
        
        if (eventArgs.ApplicationMessage.Topic.Contains("stat") || eventArgs.ApplicationMessage.Topic.Contains("json"))
        {
            eventArgs.ProcessPublish = false;
            _logger.LogInformation("Client {clientId} and user#{userId} send packet {guid} on {topic} ignored",
                eventArgs.ClientId, user?.Id, guid, eventArgs.ApplicationMessage.Topic);
            return;
        }
        
        _logger.LogInformation("Client {clientId} and user#{userId} send packet {guid} on {topic}", eventArgs.ClientId,
            user?.Id, guid, eventArgs.ApplicationMessage.Topic);
        
        var packetAndMeshPacket = await mqttService.DoReceive(eventArgs.ApplicationMessage.Topic,
            eventArgs.ApplicationMessage.Payload, _mqttServer!);

        if (packetAndMeshPacket?.packet == null)
        {
            eventArgs.ProcessPublish = false;
            _logger.LogWarning(
                "Client {clientId} and user#{userId} send packet {guid} on {topic} refused because packet error",
                eventArgs.ClientId, user?.Id, guid, eventArgs.ApplicationMessage.Topic);
            return;
        }

        if (packetAndMeshPacket.Value.packet.PacketDuplicated != null)
        {
            _clientIdReceivers.Add(eventArgs.ClientId, receiverIds);
            
            _logger.LogInformation(
                "Client {clientId} and user#{userId} send packet {guid} on {topic} refused because packet#{packetId} duplicated",
                eventArgs.ClientId, user?.Id, guid, eventArgs.ApplicationMessage.Topic,
                packetAndMeshPacket.Value.packet.Id);
            return;
        }

        var packetActivity =
            await routingService.Route(eventArgs.ClientId, user?.Id, packetAndMeshPacket.Value.packet);

        if (packetActivity.Accepted)
        {
            if (packetActivity.ReceiverIds.Count != 0)
            {
                receiverIds.AddRange(packetActivity.ReceiverIds);
                _logger.LogInformation(
                    "Client {clientId} and user#{userId} send packet {guid} on {topic} accepted for {nbReceivers} : {comment}",
                    eventArgs.ClientId, user?.Id, guid, eventArgs.ApplicationMessage.Topic,
                    packetActivity.ReceiverIds.Count, packetActivity.Comment);
                _logger.LogTrace("Receivers for packet {guid} : {receivers}", guid,
                    packetActivity.ReceiverIds.JoinString());
            }
            else
            {
                receiverIds.Clear();

                _logger.LogInformation(
                    "Client {clientId} and user#{userId} send packet {guid} on {topic} accepted : {comment}",
                    eventArgs.ClientId, user?.Id, guid, eventArgs.ApplicationMessage.Topic,
                    packetActivity.Comment);
            }
        }
        else
        {
            _logger.LogInformation(
                "Client {clientId} and user#{userId} send packet {guid} on {topic} refused : {comment}",
                eventArgs.ClientId, user?.Id, guid, eventArgs.ApplicationMessage.Topic,
                packetActivity.Comment);
        }

        if (receiverIds.Count > 0)
        {
            _clientIdReceivers.Add(eventArgs.ClientId, receiverIds);
        }
    }

    public Task BeforeSend(InterceptingClientApplicationMessageEnqueueEventArgs eventArgs)
    {
        if (!_clientIdReceivers.TryGetValue(eventArgs.SenderClientId, out var receivers) ||
            receivers.Contains(eventArgs.ReceiverClientId))
        {
            _logger.LogInformation("Packet from client {senderId} on {topic} sent to {receiverId}",
                eventArgs.SenderClientId, eventArgs.ApplicationMessage.Topic, eventArgs.ReceiverClientId);
        }
        else
        {
            eventArgs.AcceptEnqueue = false;
            _logger.LogTrace("Packet from client {senderId} on {topic} not sent to {receiverId} because not in list",
                eventArgs.SenderClientId, eventArgs.ApplicationMessage.Topic, eventArgs.ReceiverClientId);
        }

        return Task.CompletedTask;
    }

    public async Task OnDisconnection(ClientDisconnectedEventArgs eventArgs)
    {
        if (_userIds.ContainsKey(eventArgs.ClientId))
        {
            _userIds.Remove(eventArgs.ClientId);
        }

        if (_clientIdsWithClaimEveryPacket.Contains(eventArgs.ClientId))
        {
            _clientIdsWithClaimEveryPacket.Remove(eventArgs.ClientId);
        }

        var services = serviceProvider.CreateScope().ServiceProvider;
        var context = services.GetRequiredService<DataContext>();

        var nodeConfigurations = await context.NodeConfigurations
            .Include(a => a.Node)
            .Where(a => a.MqttId == eventArgs.ClientId)
            .ToListAsync();

        foreach (var nodeConfiguration in nodeConfigurations)
        {
            nodeConfiguration.MqttId = null;
            nodeConfiguration.Node.IsMqttGateway = false;
            context.Update(nodeConfiguration);
            context.Update(nodeConfiguration.Node);
        }

        await context.SaveChangesAsync();

        _logger.LogInformation(
            "Client {clientId} for user{userId} disconnected for reason {reason}", eventArgs.ClientId, GetUserId(eventArgs.ClientId), eventArgs.ReasonString);
    }

    private long? GetUserId(string clientId)
    {
        return _userIds.TryGetValue(clientId, out var userId) ? userId : null;
    }

    private async Task<NodeConfiguration?> GetAndUpdateNodeConfiguration(string clientId, string topic, DataContext context, 
        User? user)
    {
        var connectedNodeId = GetConnectedNodeId(topic);

        if (!connectedNodeId.HasValue)
        {
            return null;
        }
        
        var nodeConfiguration = await context.NodeConfigurations
            .Include(nodeConfiguration => nodeConfiguration.Node)
            .FirstOrDefaultAsync(a => a.Node.NodeId == connectedNodeId);
        
        if (nodeConfiguration != null)
        {
            nodeConfiguration.User = user;
            nodeConfiguration.MqttId = clientId;
            nodeConfiguration.Node.IsMqttGateway = !nodeConfiguration.Forbidden;
            context.Update(nodeConfiguration);
            context.Update(nodeConfiguration.Node);
            await context.SaveChangesAsync();

            if (!nodeConfiguration.Forbidden)
            {
                return nodeConfiguration;
            }
            
            _logger.LogError("Client {clientId} and user#{userId} for node#{nodeId} locked", clientId, user?.Id, nodeConfiguration.Id);
            throw new UnauthorizedAccessException("User locked");
        }

        var node = await context.Nodes.FindByNodeIdAsync(connectedNodeId.Value);

        if (node == null)
        {
            return null;
        }
            
        node.IsMqttGateway = true;
        context.Update(node);

        nodeConfiguration = new NodeConfiguration
        {
            Node = node,
            User = user,
            MqttId = clientId
        };

        context.Add(nodeConfiguration);
        await context.SaveChangesAsync();

        return nodeConfiguration;
    }

    private async Task<User?> GetAndUpdateUserFromClientId(string clientId, DataContext context)
    {
        var userId = GetUserId(clientId);

        if (!userId.HasValue)
        {
            return null;
        }
        
        var user = await context.Users.FindByIdAsync(userId);
        if (user != null)
        {
            if (!await serviceProvider.GetRequiredService<UserService>().IsAuthorized(user))
            {
                _logger.LogError("Client {clientId} and user#{userId} locked", clientId,
                    userId);
                throw new UnauthorizedAccessException("User locked");
            }

            user.LastSeenAt = DateTime.UtcNow;
            context.Update(user);
            await context.SaveChangesAsync();
            
            return user;
        }

        _logger.LogError("Client {clientId} and user#{userId} unknown", clientId, userId);
        throw new NotFoundException<User>(userId);
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
}