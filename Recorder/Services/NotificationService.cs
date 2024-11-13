using Common.Context;
using Common.Context.Entities;
using Common.Services;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Recorder.Models;

namespace Recorder.Services;

public class NotificationService(ILogger<AService> logger, IDbContextFactory<DataContext> contextFactory, IConfiguration configuration) : AService(logger, contextFactory)
{
    public async Task SendNotification(Packet packet, MeshPacket meshPacket)
    {
        var notificationsCanalToSend = (configuration.GetSection("Notifications").Get<List<NotificationConfiguration>>() ?? [])
            .Where(n => n.Enabled)
            .Where(n =>
            {
                if (packet.PacketDuplicated != null
                    // || packet.Value.packet.Gateway == packet.Value.packet.To
                    // || (packet.Value.packet is { HopStart: not null, HopLimit: not null } && packet.Value.packet.HopLimit == packet.Value.packet.HopStart)
                   )
                {
                    return n.AllowDuplication;
                }

                return true;
            })
            .Where(n => !n.PortNum.HasValue || packet.PortNum == n.PortNum)
            .Where(n => !n.From.HasValue || packet.From.NodeId == n.From)
            .Where(n => !n.To.HasValue || packet.To.NodeId == n.To)
            .Where(n => !n.Gateway.HasValue || packet.Gateway.NodeId == n.Gateway)
            .Where(n => !n.FromOrTo.HasValue || packet.From.NodeId == n.FromOrTo || packet.To.NodeId == n.FromOrTo)
            .Where(n => string.IsNullOrWhiteSpace(n.MqttServer) || packet.MqttServer == n.MqttServer)
            .Where(n => string.IsNullOrWhiteSpace(n.Channel) || packet.Channel.Name == n.Channel)
            .DistinctBy(n => n.Url)
            .ToList();

        var isText = meshPacket.Decoded?.Portnum is PortNum.TextMessageApp;
        
        var message = $"""
                       [{packet.Channel.Name}] {packet.From.AllNames}
                       
                       {(packet.To.NodeId != MeshtasticService.NodeBroadcast ? $"Pour : {packet.To.AllNames}" : "En broadcast")}

                       {(packet.Gateway != packet.From ? $"Via {packet.Gateway.AllNames} ({packet.GatewayDistanceKm} Km, SNR {packet.RxSnr}, {packet.HopStart - packet.HopLimit} sauts, {(packet is { HopStart: not null, HopLimit: not null } && packet.HopLimit == packet.HopStart ? " reçu en direct": "")})" : "Via lui-même")}

                       > {(isText ? "" : $"{meshPacket.Decoded?.Portnum} :\n")} {meshPacket.GetPayload()}
                       """;
        
        foreach (var notificationConfiguration in notificationsCanalToSend)
        {
            await MakeRequest(notificationConfiguration, message, packet);
        }
    }
    
    private async Task<string?> MakeRequest(NotificationConfiguration notificationConfiguration, string message, Packet packet) 
    {
        Logger.LogTrace("Send notification to {name} for packet #{packetId}", notificationConfiguration.Name, packet.Id);
        
        using var client = new HttpClient();

        var encodedMessage = Uri.EscapeDataString(message);
        var requestUrl = notificationConfiguration.Url.Replace("{{message}}", encodedMessage);

        try
        {
            var response = await client.GetAsync(requestUrl);

            response.EnsureSuccessStatusCode();

            Logger.LogInformation("Send notification to {name} for packet #{packetId} OK", notificationConfiguration.Name, packet.Id);

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Send notification to {name} for packet #{packetId} KO", notificationConfiguration.Name, packet.Id);
            return default;
        }
    }
}