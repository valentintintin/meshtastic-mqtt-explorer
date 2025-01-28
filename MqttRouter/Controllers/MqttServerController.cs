using System.Buffers;
using Common;
using Common.Context;
using Common.Context.Entities;
using Common.Context.Entities.Router;
using Common.Exceptions;
using Common.Extensions;
using Common.Extensions.Entities;
using Common.Services;
using Google.Protobuf;
using Meshtastic.Protobufs;
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

    private readonly List<MessagePublishInfo> _messagesToPublish = [];
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

        try
        {
            if (await userService.HasClaim(user, SecurityConstants.Claim.ReceiveEveryPackets))
            {
                _logger.LogInformation(
                    "Client {username}#{userId} identified with id {clientId} has claim ReceiveEveryPackets",
                    eventArgs.UserName, user.Id, eventArgs.ClientId);
                _clientIdsWithClaimEveryPacket.Add(eventArgs.ClientId);
            }
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Error occured while trying to get claim ReceiveEveryPackets");
        }

        _logger.LogInformation("Client {username}#{userId} identified with id {clientId}", eventArgs.UserName, user.Id,
            eventArgs.ClientId);
    }

    public async Task OnNewPacket(InterceptingPublishEventArgs eventArgs)
    {
        PurgeOldPublishInfos();
        
        if (!eventArgs.ApplicationMessage.Topic.Contains("msh/"))
        {
            return;
        }
        
        var services = serviceProvider.CreateScope().ServiceProvider;
        var context = services.GetRequiredService<DataContext>();
        var routingService = services.GetRequiredService<RoutingService>();
        var mqttService = services.GetRequiredService<MqttService>();

        var publishInfo = new MessagePublishInfo
        {
            ClientId = eventArgs.ClientId
        };

        routingService.SetDbContext(context);
        mqttService.SetDbContext(context);
        _mqttServer ??= await context.MqttServers.FirstAsync(a =>
            a.Name == serviceProvider.GetRequiredService<IConfiguration>().GetValue<string>("MqttServerName"));

        _logger.LogTrace("Client {clientId} send packet {guid} on {topic}", eventArgs.ClientId, publishInfo.Guid,
            eventArgs.ApplicationMessage.Topic);

        User? user = null;
        try
        {
            user = await GetAndUpdateUserFromClientId(eventArgs.ClientId, context);
            _logger.LogTrace("Client {clientId} send packet {guid} on {topic}. User #{userId}", eventArgs.ClientId, publishInfo.Guid,
                eventArgs.ApplicationMessage.Topic, user?.Id);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Client {clientId} send packet {guid} on {topic} KO. User issue", eventArgs.ClientId, publishInfo.Guid,
                eventArgs.ApplicationMessage.Topic);
        }

        NodeConfiguration? nodeConfiguration = null;
            
        try
        {
            nodeConfiguration = await GetAndUpdateNodeConfiguration(eventArgs.ClientId,
                eventArgs.ApplicationMessage.Topic, context, user);
            _logger.LogTrace("Client {clientId} send packet {guid} on {topic}. Node configuration #{nodeConfigurationId}", eventArgs.ClientId, publishInfo.Guid,
                eventArgs.ApplicationMessage.Topic, nodeConfiguration?.Id);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Client {clientId} send packet {guid} on {topic} KO. Node configuration issue", eventArgs.ClientId, publishInfo.Guid,
                eventArgs.ApplicationMessage.Topic);
        }
        
        if (eventArgs.ApplicationMessage.Topic.Contains("stat") || eventArgs.ApplicationMessage.Topic.Contains("json"))
        {
            eventArgs.ProcessPublish = false;
            return;
        }

        (Packet packet, MeshPacket meshPacket)? packetAndMeshPacket;
            
        try
        {
            packetAndMeshPacket = await mqttService.DoReceive(eventArgs.ApplicationMessage.Topic,
                eventArgs.ApplicationMessage.Payload.ToArray(), _mqttServer!);
            
            if (packetAndMeshPacket?.packet == null)
            {
                throw new MqttMeshtasticException("Packet could not be processed --> null returned");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Client {clientId} and user#{userId} send packet {guid} on {topic}. KO packet error", eventArgs.ClientId,
                user?.Id, publishInfo.Guid, eventArgs.ApplicationMessage.Topic);

            eventArgs.ProcessPublish = false;
            
            return;
        }
        
        _logger.LogInformation("Client {clientId} of user#{userId} from node configuration #{nodeConfigurationId} send packet#{id}|{packetId}|{guid} on {topic}", eventArgs.ClientId, user?.Id, nodeConfiguration?.Id,
            packetAndMeshPacket.Value.packet.Id, packetAndMeshPacket.Value.meshPacket.Id, publishInfo.Guid, eventArgs.ApplicationMessage.Topic);
        
        publishInfo.Packet = packetAndMeshPacket.Value.packet;
        publishInfo.MeshPacket = packetAndMeshPacket.Value.meshPacket;

        if (publishInfo.Packet.PacketDuplicated == null)
        {
            publishInfo.PacketActivity = await routingService.Route(eventArgs.ClientId, user?.Id, packetAndMeshPacket.Value.packet, packetAndMeshPacket.Value.meshPacket);

            if (publishInfo.PacketActivity.Accepted)
            {
                if (publishInfo.PacketActivity.ReceiverIds.Count != 0)
                {
                    publishInfo.ReceiverClientIds.AddRange(publishInfo.PacketActivity.ReceiverIds);

                    _logger.LogInformation(
                        "Client {clientId} and user#{userId} send packet {guid} on {topic} accepted for {nbReceivers} : {comment}",
                        eventArgs.ClientId, user?.Id, publishInfo.Guid, eventArgs.ApplicationMessage.Topic,
                        publishInfo.PacketActivity.ReceiverIds.Count, publishInfo.PacketActivity.Comment);
                }
                else
                {
                    _logger.LogInformation(
                        "Client {clientId} and user#{userId} send packet {guid} on {topic} accepted : {comment}",
                        eventArgs.ClientId, user?.Id, publishInfo.Guid, eventArgs.ApplicationMessage.Topic,
                        publishInfo.PacketActivity.Comment);

                    await Task.Delay(TimeSpan.FromSeconds((long)(5 + (packetAndMeshPacket.Value.packet.HopStart - packetAndMeshPacket.Value.packet.HopLimit * 2.5))!)); // Trouver un meilleur endroit ?
                }
            }
            else
            {
                _logger.LogInformation(
                    "Client {clientId} and user#{userId} send packet {guid} on {topic} refused : {comment}",
                    eventArgs.ClientId, user?.Id, publishInfo.Guid, eventArgs.ApplicationMessage.Topic,
                    publishInfo.PacketActivity.Comment);
            }
        }
        else
        {
            _logger.LogInformation(
                "Client {clientId} and user#{userId} send packet {guid} on {topic} refused because packet#{packetId} duplicated",
                eventArgs.ClientId, user?.Id, publishInfo.Guid, eventArgs.ApplicationMessage.Topic,
                publishInfo.Packet.Id);
        }

        _logger.LogInformation("Client {clientId} of user#{userId} from node configuration #{nodeConfigurationId} send packet#{id}|{packetId}|{guid} on {topic}. Packet allowed. Nb receivers : {nbReceivers}", eventArgs.ClientId, user?.Id, nodeConfiguration?.Id,
            packetAndMeshPacket.Value.packet.Id, packetAndMeshPacket.Value.meshPacket.Id, publishInfo.Guid, eventArgs.ApplicationMessage.Topic, publishInfo.ReceiverClientIds.Count);
        _logger.LogTrace("Client {clientId} of user#{userId} from node configuration #{nodeConfigurationId} send packet#{id}|{packetId}|{guid} on {topic}. Receivers : {receivers}", eventArgs.ClientId, user?.Id, nodeConfiguration?.Id,
            packetAndMeshPacket.Value.packet.Id, packetAndMeshPacket.Value.meshPacket.Id, publishInfo.Guid, eventArgs.ApplicationMessage.Topic, publishInfo.ReceiverClientIds.JoinString());
        
        _messagesToPublish.Add(publishInfo);
    }

    private void PurgeOldPublishInfos()
    {
        _messagesToPublish.RemoveAll(a => DateTime.UtcNow - a.Packet.CreatedAt >= TimeSpan.FromMinutes(1));
    }

    public Task BeforeSend(InterceptingClientApplicationMessageEnqueueEventArgs eventArgs)
    {
        if (!eventArgs.ApplicationMessage.Topic.Contains("msh/"))
        {
            return Task.CompletedTask;
        }

        var meshPacket = new ServiceEnvelope();
        
        try
        {
            meshPacket.MergeFrom(eventArgs.ApplicationMessage.Payload.ToArray());
        }
        catch (Exception e)
        {
            _logger.LogWarning(e,
                "Impossible to decode ProtoBuf the packet from client {senderId} on {topic}",
                eventArgs.SenderClientId, eventArgs.ApplicationMessage.Topic);
            
            eventArgs.AcceptEnqueue = false;
            
            return Task.CompletedTask;
        }

        var publishInfo = _messagesToPublish.FirstOrDefault(a => a.ClientId == eventArgs.SenderClientId && a.MeshPacket.Id == meshPacket.Packet.Id);

        if (publishInfo == null)
        {
            eventArgs.AcceptEnqueue = false;
            
            _logger.LogWarning(
                "Packet#{id} from client {senderId} on {topic} not sent to {receiverId} because we do not have publish info",
                meshPacket.Packet.Id, eventArgs.SenderClientId, eventArgs.ApplicationMessage.Topic, eventArgs.ReceiverClientId);
            
            return Task.CompletedTask;
        }

        if (publishInfo.ReceiverClientIds.Count > 0 && !publishInfo.ReceiverClientIds.Contains(eventArgs.ReceiverClientId))
        {
            eventArgs.AcceptEnqueue = false;
            
            _logger.LogDebug("Packet#{id} {guid} from client {senderId} on {topic} not sent to {receiverId} because not in list",
                meshPacket.Packet.Id, publishInfo.Guid, eventArgs.SenderClientId, eventArgs.ApplicationMessage.Topic, eventArgs.ReceiverClientId);
            
            return Task.CompletedTask;
        }

        var hopLimit = publishInfo.MeshPacket.HopLimit;
        var hopStart = publishInfo.MeshPacket.HopStart;

        if (_clientIdsWithClaimEveryPacket.Contains(eventArgs.ReceiverClientId))
        {
            _logger.LogDebug(
                "Packet#{id} {guid} from client {senderId} on {topic} sent to {receiverId} because he has claim ReceiveEveryPackets",
                meshPacket.Packet.Id, publishInfo.Guid, eventArgs.SenderClientId,
                eventArgs.ApplicationMessage.Topic,
                eventArgs.ReceiverClientId);
        }
        else if (publishInfo.PacketActivity != null)
        {
            if (!publishInfo.PacketActivity.Accepted)
            {
                eventArgs.AcceptEnqueue = false;

                _logger.LogDebug(
                    "Packet#{id} {guid} from client {senderId} on {topic} not sent to {receiverId} because refused",
                    meshPacket.Packet.Id, publishInfo.Guid, eventArgs.SenderClientId,
                    eventArgs.ApplicationMessage.Topic, eventArgs.ReceiverClientId);
                
                return Task.CompletedTask;
            }

            hopLimit = hopStart = (uint)publishInfo.PacketActivity.HopLimit;

            _logger.LogDebug(
                "Packet#{id} {guid} from client {senderId} on {topic} sent to {receiverId} with hop set to {hopRequired}",
                meshPacket.Packet.Id, publishInfo.Guid, eventArgs.SenderClientId,
                eventArgs.ApplicationMessage.Topic,
                eventArgs.ReceiverClientId, hopLimit);
        }

        try
        {
            meshPacket.Packet.HopLimit = hopLimit;
            meshPacket.Packet.HopStart = hopStart;
            eventArgs.ApplicationMessage.Payload = new ReadOnlySequence<byte>(meshPacket.ToByteArray());
        }
        catch (Exception e)
        {
            _logger.LogWarning(e,
                "Impossible to change hop to {hopLimit}/{hopStart} for the packet#{id} from client {senderId} on {topic}",
                hopLimit, hopStart, meshPacket.Packet.Id, eventArgs.SenderClientId, eventArgs.ApplicationMessage.Topic);
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
            .Include(a => a.Node)
            .FirstOrDefaultAsync(a => a.Node.NodeId == connectedNodeId);
        
        if (nodeConfiguration != null)
        {
            nodeConfiguration.User = user;
            nodeConfiguration.MqttId = clientId;
            nodeConfiguration.LastSeenOnMqtt = DateTime.UtcNow;
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
    
    private class MessagePublishInfo
    {
        public Guid Guid { get; }= Guid.NewGuid();
        
        public required string ClientId { get; set; }
        
        public List<string> ReceiverClientIds { get; set; } = [];
        
        public Packet Packet { get; set; } = null!;

        public MeshPacket MeshPacket { get; set; } = null!;
        
        public PacketActivity? PacketActivity { get; set; }
    }
}