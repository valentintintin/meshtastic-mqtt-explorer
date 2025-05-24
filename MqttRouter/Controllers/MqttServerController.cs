using System.Buffers;
using System.Text;
using System.Text.Json;
using Common;
using Common.Context;
using Common.Context.Entities;
using Common.Context.Entities.Router;
using Common.Exceptions;
using Common.Extensions;
using Common.Extensions.Entities;
using Common.Services;
using Google.Protobuf;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using MqttRouter.Services;
using MqttServer = Common.Context.Entities.MqttServer;
using User = Common.Context.Entities.Router.User;

namespace MqttRouter.Controllers;

public class MqttServerController(IServiceProvider serviceProvider, MQTTnet.Server.MqttServer server)
{
    private static readonly SemaphoreSlim Lock = new(1, 1);
    
    private readonly ILogger<MqttServerController> _logger =
        serviceProvider.GetRequiredService<ILogger<MqttServerController>>();

    private readonly List<MessagePublishInfo> _messagesToPublish = [];
    private readonly Dictionary<string, long> _userIdForClientIds = new();
    private readonly List<string> _clientIdsWithClaimEveryPacket = [];
    private MqttServer? _mqttServer;

    public async Task OnConnection(ValidatingConnectionEventArgs eventArgs)
    {
        try
        {
            var services = serviceProvider.CreateScope().ServiceProvider;

            _logger.LogInformation("Client {username} try to join with id {clientId}", eventArgs.UserName,
                eventArgs.ClientId);

            if (string.IsNullOrWhiteSpace(eventArgs.UserName))
            {
                _logger.LogWarning("Client {username} identified with id {clientId} as guest", eventArgs.UserName,
                    eventArgs.ClientId);
                eventArgs.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                throw new UnauthorizedAccessException();
            }

            var userService = services.GetRequiredService<UserService>();
            var user = await userService.Login(eventArgs.UserName, eventArgs.Password,
                eventArgs.RemoteEndPoint.ToString());

            if (user == null)
            {
                _logger.LogWarning("Client {username} not identified", eventArgs.UserName);
                eventArgs.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                throw new UnauthorizedAccessException();
            }

            _userIdForClientIds.Remove(eventArgs.ClientId);

            _userIdForClientIds.Add(eventArgs.ClientId, user.Id);

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

            _logger.LogInformation("Client {username}#{userId} identified with id {clientId}", eventArgs.UserName,
                user.Id,
                eventArgs.ClientId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error connect !!");
        }
    }

    public async Task OnNewPacket(InterceptingPublishEventArgs eventArgs)
    {
        try
        {
            // _logger.LogInformation("Begin lock");
            // await Lock.WaitAsync();
            // _logger.LogInformation("Locked !");
            
            PurgeOldPublishInfos();

            if (!eventArgs.ApplicationMessage.Topic.Contains("msh/"))
            {
                // Lock.Release();
                // _logger.LogInformation("Return unlock");
                return;
            }

            if (eventArgs.ApplicationMessage.Topic.Contains("stat") ||
                eventArgs.ApplicationMessage.Topic.Contains("json"))
            {
                // Lock.Release();
                // _logger.LogInformation("Return unlock");
                return;
            }

            var services = serviceProvider.CreateScope().ServiceProvider;
            var context = await services.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContextAsync();
            var routingService = services.GetRequiredService<RoutingService>();
            var mqttService = services.GetRequiredService<MqttService>();

            var publishInfo = new MessagePublishInfo
            {
                ClientId = eventArgs.ClientId
            };

            routingService.SetDbContext(context);
            mqttService.SetDbContext(context);
            _mqttServer ??= await context.MqttServers.FirstAsync(a => a.Type == MqttServer.ServerType.MqttServer);

            _logger.LogTrace("Client {clientId} send packet {guid} on {topic}", eventArgs.ClientId, publishInfo.Guid,
                eventArgs.ApplicationMessage.Topic);

            User? user = null;
            try
            {
                user = await GetAndUpdateUserFromClientId(eventArgs.ClientId, context);
                _logger.LogTrace("Client {clientId} send packet {guid} on {topic}. User #{userId}", eventArgs.ClientId,
                    publishInfo.Guid,
                    eventArgs.ApplicationMessage.Topic, user?.Id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Client {clientId} send packet {guid} on {topic} KO. User issue",
                    eventArgs.ClientId, publishInfo.Guid,
                    eventArgs.ApplicationMessage.Topic);
            }

            NodeConfiguration? nodeConfiguration = null;

            try
            {
                nodeConfiguration = await GetAndUpdateNodeConfiguration(eventArgs.ClientId,
                    eventArgs.ApplicationMessage.Topic, context, user);
                _logger.LogTrace(
                    "Client {clientId} send packet {guid} on {topic}. Node configuration #{nodeConfigurationId}",
                    eventArgs.ClientId, publishInfo.Guid,
                    eventArgs.ApplicationMessage.Topic, nodeConfiguration?.Id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Client {clientId} send packet {guid} on {topic} KO. Node configuration issue",
                    eventArgs.ClientId, publishInfo.Guid,
                    eventArgs.ApplicationMessage.Topic);
            }

            (Packet packet, ServiceEnvelope serviceEnveloppe)? packetAndMeshPacket;

            try
            {
                packetAndMeshPacket = await mqttService.DoReceive(eventArgs.ApplicationMessage.Topic,
                    eventArgs.ApplicationMessage.Payload.ToArray(), _mqttServer, async message => await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(message)));

                if (packetAndMeshPacket?.packet == null)
                {
                    throw new MqttMeshtasticException("Packet could not be processed --> null returned");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "Client {clientId} and user#{userId} send packet {guid} on {topic}. KO packet error",
                    eventArgs.ClientId,
                    user?.Id, publishInfo.Guid, eventArgs.ApplicationMessage.Topic);

                eventArgs.ProcessPublish = false;
                
                // Lock.Release();
                // _logger.LogInformation("Return unlock");

                return;
            }

            _logger.LogInformation(
                "Client {clientId} of user#{userId} from node configuration #{nodeConfigurationId} send packet#{id}|{packetId}|{guid} on {topic}",
                eventArgs.ClientId, user?.Id, nodeConfiguration?.Id,
                packetAndMeshPacket.Value.packet.Id, packetAndMeshPacket.Value.serviceEnveloppe.Packet.Id, publishInfo.Guid,
                eventArgs.ApplicationMessage.Topic);

            publishInfo.Packet = packetAndMeshPacket.Value.packet;
            publishInfo.MeshPacket = packetAndMeshPacket.Value.serviceEnveloppe.Packet;

            if (publishInfo.Packet.PacketDuplicated == null)
            {
                publishInfo.PacketActivity = await routingService.Route(eventArgs.ClientId, user?.Id,
                    packetAndMeshPacket.Value.packet, packetAndMeshPacket.Value.serviceEnveloppe.Packet);

                if (nodeConfiguration?.Forbidden == true)
                {
                    _logger.LogWarning("Packet #{packetId}|{guid} is from node forbidden", publishInfo.MeshPacket.Id, publishInfo.Guid);
                    publishInfo.PacketActivity.Accepted = false;
                    publishInfo.PacketActivity.Comment = "Noeud interdit en downlink";
                }
                
                if (publishInfo.PacketActivity.Accepted)
                {
                    if (publishInfo.MeshPacket.Decoded?.Portnum == PortNum.TextMessageApp)
                    {
                        _logger.LogInformation("Packet #{packetId}|{guid} wait for 5 seconds because it's a message. We want it to do its way by radio first of MQTT", publishInfo.MeshPacket.Id, publishInfo.Guid);
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                    
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

            _logger.LogInformation(
                "Client {clientId} of user#{userId} from node configuration #{nodeConfigurationId} send packet#{id}|{packetId}|{guid} on {topic}. Packet allowed. Nb receivers : {nbReceivers}",
                eventArgs.ClientId, user?.Id, nodeConfiguration?.Id,
                packetAndMeshPacket.Value.packet.Id, packetAndMeshPacket.Value.serviceEnveloppe.Packet.Id, publishInfo.Guid,
                eventArgs.ApplicationMessage.Topic, publishInfo.ReceiverClientIds.Count);
            _logger.LogTrace(
                "Client {clientId} of user#{userId} from node configuration #{nodeConfigurationId} send packet#{id}|{packetId}|{guid} on {topic}. Receivers : {receivers}",
                eventArgs.ClientId, user?.Id, nodeConfiguration?.Id,
                packetAndMeshPacket.Value.packet.Id, packetAndMeshPacket.Value.serviceEnveloppe.Packet.Id, publishInfo.Guid,
                eventArgs.ApplicationMessage.Topic, publishInfo.ReceiverClientIds.JoinString());

            _messagesToPublish.Add(publishInfo);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error before publish !!");
        }
        finally
        {
            // Lock.Release();
            // _logger.LogInformation("Finally unlock");
        }
    }

    private void PurgeOldPublishInfos()
    {
        _messagesToPublish.RemoveAll(a => DateTime.UtcNow - a.Packet.CreatedAt >= TimeSpan.FromMinutes(5));
    }

    public async Task BeforeSend(InterceptingClientApplicationMessageEnqueueEventArgs eventArgs)
    {
        try
        {
            if (!eventArgs.ApplicationMessage.Topic.Contains("msh/"))
            {
                return;
            }

            if (eventArgs.ApplicationMessage.Topic.Contains("stat") ||
                eventArgs.ApplicationMessage.Topic.Contains("json"))
            {
                return;
            }

            var meshPacket = new ServiceEnvelope();

            var data = eventArgs.ApplicationMessage.Payload.ToArray();
            try
            {
                meshPacket.MergeFrom(data);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e,
                    "Impossible to decode ProtoBuf the packet from client {senderId} on {topic}. Raw {base64}",
                    eventArgs.SenderClientId, eventArgs.ApplicationMessage.Topic, Convert.ToBase64String(data));

                eventArgs.AcceptEnqueue = false;

                return;
            }

            var publishInfo = _messagesToPublish.FirstOrDefault(a =>
                a.ClientId == eventArgs.SenderClientId && meshPacket.Packet != null && a.MeshPacket != null && a.MeshPacket.Id == meshPacket.Packet.Id);

            if (publishInfo == null)
            {
                eventArgs.AcceptEnqueue = false;

                _logger.LogWarning(
                    "Packet#{id} from client {senderId} on {topic} not sent to {receiverId} because we do not have publish info",
                    meshPacket.Packet.Id, eventArgs.SenderClientId, eventArgs.ApplicationMessage.Topic,
                    eventArgs.ReceiverClientId);

                return;
            }
            
            uint hopLimit = 0;
            uint hopStart = 0;

            if (_clientIdsWithClaimEveryPacket.Contains(eventArgs.ReceiverClientId))
            {
                _logger.LogDebug(
                    "Packet#{id} {guid} from client {senderId} on {topic} sent to {receiverId} because he has claim ReceiveEveryPackets",
                    meshPacket.Packet.Id, publishInfo.Guid, eventArgs.SenderClientId,
                    eventArgs.ApplicationMessage.Topic,
                    eventArgs.ReceiverClientId);

                hopLimit = publishInfo.MeshPacket.HopLimit;
                hopStart = publishInfo.MeshPacket.HopStart;
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

                    return;
                }
                
                hopLimit = hopStart = (uint)publishInfo.PacketActivity.HopLimit;
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
                    hopLimit, hopStart, meshPacket.Packet.Id, eventArgs.SenderClientId,
                    eventArgs.ApplicationMessage.Topic);
            }

            _logger.LogDebug(
                "Packet#{id} {guid} from client {senderId} on {topic} sent to {receiverId} with hop set to {hopRequired}",
                meshPacket.Packet.Id, publishInfo.Guid, eventArgs.SenderClientId,
                eventArgs.ApplicationMessage.Topic,
                eventArgs.ReceiverClientId, meshPacket.Packet.HopLimit);

            if (publishInfo.PacketActivity != null)
            {
                try
                {
                    var services = serviceProvider.CreateScope().ServiceProvider;
                    var context = await services.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContextAsync();
                    context.Attach(publishInfo.PacketActivity);

                    if (publishInfo.ReceiverClientIds.Contains(eventArgs.ReceiverClientId))
                    {
                        publishInfo.PacketActivity.ReceiverIds.Remove(eventArgs.ReceiverClientId);
                        publishInfo.PacketActivity.ReceiverIds.Add($"{eventArgs.ReceiverClientId}-OK");
                    }
                    else
                    {
                        publishInfo.PacketActivity.ReceiverIds.Add(eventArgs.ReceiverClientId);
                    }

                    await context.SaveChangesAsync();
                    context.Entry(publishInfo.PacketActivity).State = EntityState.Detached;
                }
                catch (Exception e)
                {
                    _logger.LogError(e,
                        "Packet#{id} {guid} from client {senderId} on {topic} sent to {receiverId}. Error add clientId to packetActivity",
                        meshPacket.Packet.Id, publishInfo.Guid, eventArgs.SenderClientId,
                        eventArgs.ApplicationMessage.Topic,
                        eventArgs.ReceiverClientId);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error before send !!");
        }
    }

    public async Task OnDisconnection(ClientDisconnectedEventArgs eventArgs)
    {
        try
        {
            if (_clientIdsWithClaimEveryPacket.Contains(eventArgs.ClientId))
            {
                _clientIdsWithClaimEveryPacket.Remove(eventArgs.ClientId);
            }

            var services = serviceProvider.CreateScope().ServiceProvider;
            var context = await services.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContextAsync();

            foreach (var nodeConfiguration in await context.NodeConfigurations
                         .Include(a => a.Node)
                         .Where(a => a.MqttId == eventArgs.ClientId)
                         .ToListAsync())
            {
                try
                {
                    nodeConfiguration.IsConnected = false;
                    nodeConfiguration.Node.IsMqttGateway = false;
                    context.Update(nodeConfiguration);
                    context.Update(nodeConfiguration.Node);
                    await context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error during set nodeConfiguration disconnected");
                }
            }

            _logger.LogInformation(
                "Client {clientId} for user#{userId} disconnected for reason {reason}", eventArgs.ClientId,
                GetUserId(eventArgs.ClientId), eventArgs.ReasonString);
            
            if (_userIdForClientIds.ContainsKey(eventArgs.ClientId))
            {
                _userIdForClientIds.Remove(eventArgs.ClientId);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error disconnect !!");
        }
    }

    private long? GetUserId(string clientId)
    {
        return _userIdForClientIds.TryGetValue(clientId, out var userId) ? userId : null;
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
            nodeConfiguration.IsConnected = true;
            nodeConfiguration.LastSeenOnMqtt = DateTime.UtcNow;
            nodeConfiguration.Node.IsMqttGateway = !nodeConfiguration.Forbidden;
            context.Update(nodeConfiguration);
            context.Update(nodeConfiguration.Node);
            await context.SaveChangesAsync();

            if (!nodeConfiguration.Forbidden)
            {
                return nodeConfiguration;
            }
            
            _logger.LogWarning("Client {clientId} and user#{userId} for node#{nodeId} locked", clientId, user?.Id, nodeConfiguration.Id);
            // throw new UnauthorizedAccessException("User locked");
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
            MqttId = clientId,
            IsConnected = true
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