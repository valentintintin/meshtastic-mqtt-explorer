using Common.Context;
using Common.Context.Entities;
using Common.Extensions;
using Common.Models;
using Common.Services;
using Meshtastic.Protobufs;
using Microsoft.AspNetCore.Identity;
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
    private readonly MqttService _mqttService = serviceProvider.GetRequiredService<MqttService>();
    private readonly UserService _userService = serviceProvider.GetRequiredService<UserService>();
    private readonly RoutingService _routingService = serviceProvider.GetRequiredService<RoutingService>();
    private readonly Dictionary<string, List<string>> _clientIdReceivers = new();
    private MqttServer? _mqttServer;

    public async Task OnValidateConnection(ValidatingConnectionEventArgs eventArgs)
    {
        _logger.LogInformation("Client {username} try to join with id {clientId}", eventArgs.UserName, eventArgs.ClientId);

        if (string.IsNullOrWhiteSpace(eventArgs.UserName))
        {
            // TODO not authorized
            _logger.LogWarning("Client {username} identified with id {clientId} as guest", eventArgs.UserName, eventArgs.ClientId);
            return;
        }

        var user = await _userService.Login(eventArgs.UserName, eventArgs.Password, eventArgs.Endpoint);

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
        if (_clientIdReceivers.ContainsKey(eventArgs.ClientId))
        {
            _clientIdReceivers.Remove(eventArgs.ClientId);
        }

        _logger.LogInformation("Client {clientId} send packet on {topic}", eventArgs.ClientId, eventArgs.ApplicationMessage.Topic);

        _mqttServer ??= serviceProvider.GetRequiredService<DataContext>().MqttServers.First(a =>
            a.Name == serviceProvider.GetRequiredService<IConfiguration>().GetValue<string>("MqttServerName"));

        var packetAndMeshPacket = await _mqttService.DoReceive(eventArgs.ApplicationMessage.Topic, eventArgs.ApplicationMessage.Payload, _mqttServer);

        if (packetAndMeshPacket?.packet != null)
        {
            var (shouldPublish, receivers) = await _routingService.Route(eventArgs.ClientId, packetAndMeshPacket.Value.packet);
            eventArgs.ProcessPublish = shouldPublish;

            if (receivers.Count != 0)
            {
                _clientIdReceivers.Add(eventArgs.ClientId, receivers);
            }
        }
        else
        {
            eventArgs.ProcessPublish = false;
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

    private long? GetUserId(List<MqttUserProperty> userProperties)
    {
        return userProperties.FirstOrDefault(k => k.Name == "userId")?.Value?.ToLongNullable();
    }
}