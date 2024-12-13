using Common.Context;
using Common.Context.Entities;
using Common.Context.Entities.Router;
using Common.Extensions;
using Common.Extensions.Entities;
using Common.Models;
using Common.Services;
using Meshtastic.Protobufs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet.Packets;
using MQTTnet.Server;
using MqttRouter.Services;
using MqttServer = Common.Context.Entities.MqttServer;

namespace MqttRouter.Controllers;

public class MqttServerController(IServiceProvider serviceProvider)
{
    private readonly ILogger<MqttServerController> _logger = serviceProvider.GetRequiredService<ILogger<MqttServerController>>();
    private readonly Dictionary<string, List<string>> _clientIdReceivers = new();
    private MqttServer? _mqttServer;

    public async Task OnValidateConnection(ValidatingConnectionEventArgs eventArgs)
    {
        var services = serviceProvider.CreateScope().ServiceProvider;
        
        _logger.LogInformation("Client {username} try to join with id {clientId}", eventArgs.UserName, eventArgs.ClientId);

        if (string.IsNullOrWhiteSpace(eventArgs.UserName))
        {
            // TODO not authorized
            _logger.LogWarning("Client {username} identified with id {clientId} as guest", eventArgs.UserName, eventArgs.ClientId);
            return;
        }

        var user = await services.GetRequiredService<UserService>().Login(eventArgs.UserName, eventArgs.Password, eventArgs.Endpoint);

        if (user == null)
        {
            _logger.LogWarning("Client {username} not identified", eventArgs.UserName);
            throw new UnauthorizedAccessException();
        }

        eventArgs.SessionItems.Add("userId", user.Id);
        
        _logger.LogInformation("Client {username}#{userId} identified with id {clientId}", eventArgs.UserName, user.Id, eventArgs.ClientId);
    }

    public async Task OnInterceptingInbound(InterceptingPublishEventArgs eventArgs)
    {
        var services = serviceProvider.CreateScope().ServiceProvider;
        var context = services.GetRequiredService<DataContext>();

        if (_mqttServer == null)
        {
            _mqttServer = await context.MqttServers.FirstAsync(a => a.Name == serviceProvider.GetRequiredService<IConfiguration>().GetValue<string>("MqttServerName"));
        }
        
        var guid = Guid.NewGuid();
        
        _logger.LogInformation("Client {clientId} send packet {guid} on {topic}", eventArgs.ClientId, guid, eventArgs.ApplicationMessage.Topic);
        
        if (_clientIdReceivers.ContainsKey(eventArgs.ClientId))
        {
            _clientIdReceivers.Remove(eventArgs.ClientId);
        }

        eventArgs.ApplicationMessage.UserProperties ??= [];
        
        var userId = GetUserId(eventArgs.ApplicationMessage.UserProperties);

        if (userId.HasValue)
        {
            var user = await context.Users.FindByIdAsync(userId);
            if (user == null)
            {
                eventArgs.ProcessPublish = false;
                eventArgs.CloseConnection = true;
                
                _logger.LogError("Client {clientId} and user#{userId} unknown. Refused packet", eventArgs.ClientId, userId);
                return;
            }

            if (!await serviceProvider.GetRequiredService<UserService>().IsAuthorized(user.Id))
            {
                eventArgs.ProcessPublish = false;
                eventArgs.CloseConnection = true;
                
                _logger.LogError("Client {clientId} and user#{userId} locked. Refused packet", eventArgs.ClientId, userId);
                return;   
            }
            
            user.LastSeenAt = DateTime.UtcNow;
            context.Update(user);
            await context.SaveChangesAsync();
        }
        
        _logger.LogInformation("Client {clientId} and user#{userId} send packet {guid} on {topic}", eventArgs.ClientId, userId, guid, eventArgs.ApplicationMessage.Topic);

        if (eventArgs.ApplicationMessage.Topic.Contains("stat") || eventArgs.ApplicationMessage.Topic.Contains("json"))
        {
            _logger.LogInformation("Client {clientId} and user#{userId} send packet {guid} on {topic} ignored", eventArgs.ClientId, userId, guid, eventArgs.ApplicationMessage.Topic);
            eventArgs.ProcessPublish = false;
            return;
        }

        var mqttService = services.GetRequiredService<MqttService>();
        mqttService.SetDbContext(context);
        var packetAndMeshPacket = await mqttService.DoReceive(eventArgs.ApplicationMessage.Topic, eventArgs.ApplicationMessage.Payload, _mqttServer!);

        if (packetAndMeshPacket?.packet is { PacketDuplicated: null })
        {
            var routingService = services.GetRequiredService<RoutingService>();
            routingService.SetDbContext(context);

            var lastTopic = eventArgs.ApplicationMessage.Topic.Split('/').Last();
            uint? connectedNodeId = null;
            try
            {
                connectedNodeId = lastTopic.ToInteger();
            }
            catch
            {
                // Ignored
            }

            var packetActivity = await routingService.Route(eventArgs.ClientId, userId, connectedNodeId, packetAndMeshPacket.Value.packet);
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

    public Task OnInterceptingPublish(InterceptingClientApplicationMessageEnqueueEventArgs eventArgs)
    {
        if (!_clientIdReceivers.TryGetValue(eventArgs.SenderClientId, out var receivers) || receivers.Contains(eventArgs.ReceiverClientId))
        {
            _logger.LogInformation("Packet from client {senderId} on {topic} sent to {receiverId}", eventArgs.SenderClientId, eventArgs.ApplicationMessage.Topic, eventArgs.ReceiverClientId);
        }
        else
        {
            _logger.LogTrace("Packet from client {senderId} on {topic} not sent to {receiverId} because not in list", eventArgs.SenderClientId, eventArgs.ApplicationMessage.Topic, eventArgs.ReceiverClientId);
        }
        
        return Task.CompletedTask;   
    }
    
    public Task OnDisconnected(ClientDisconnectedEventArgs eventArgs)
    {
        _logger.LogInformation("Client {clientId} for user{userId} disconnected for reason {reason}", eventArgs.ClientId, GetUserId(eventArgs.UserProperties), eventArgs.ReasonString);
        return Task.CompletedTask;
    }

    private long? GetUserId(List<MqttUserProperty>? userProperties)
    {
        return userProperties?.FirstOrDefault(k => k.Name == "userId")?.Value?.ToLongNullable();
    }
}