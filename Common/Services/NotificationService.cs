using Common.Context;
using Common.Context.Entities;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Common.Services;

public class NotificationService(ILogger<AService> logger, IDbContextFactory<DataContext> contextFactory) : AService(logger, contextFactory)
{
    public async Task SendNotification(Packet packet)
    {
        var notificationsCanalToSend = Context.Webhooks
            .Where(n => n.Enabled)
            .Where(n => !n.PortNum.HasValue || packet.PortNum == n.PortNum)
            .Where(n => !n.From.HasValue || packet.From.NodeId == n.From)
            .Where(n => !n.To.HasValue || packet.To.NodeId == n.To)
            .Where(n => !n.Gateway.HasValue || packet.Gateway.NodeId == n.Gateway)
            .Where(n => !n.FromOrTo.HasValue 
                        || packet.From.NodeId == n.FromOrTo 
                        || packet.To.NodeId == n.FromOrTo)
            .Where(n => !n.MqttServerId.HasValue 
                        || packet.MqttServerId == n.MqttServerId || packet.From.MqttServerId == n.MqttServerId
                        || (packet.PacketDuplicated != null && packet.PacketDuplicated.MqttServerId == n.MqttServerId)
            )
            .Where(n => string.IsNullOrWhiteSpace(n.Channel) || packet.Channel.Name == n.Channel)
            .Where(n => n.AllowByHimSelf || packet.FromId != packet.GatewayId)
            .Where(n => packet.PacketDuplicated == null || n.AllowDuplication)
            .AsEnumerable()
            .DistinctBy(n => n.Url)
            .ToList();

        var isText = packet.PortNum is PortNum.TextMessageApp;
        
        var message = $"""
                       [{packet.Channel.Name}] {packet.From.AllNames}
                       
                       {(packet.To.NodeId != MeshtasticService.NodeBroadcast ? $"Pour : {packet.To.AllNames}" : "En broadcast")}

                       {(packet.Gateway != packet.From ? $"Via {packet.Gateway.AllNames} ({packet.GatewayDistanceKm} Km, SNR {packet.RxSnr}, RSSI {packet.RxRssi}, {packet.HopStart - packet.HopLimit} sauts, {(packet is { HopStart: not null, HopLimit: not null } && packet.HopLimit == packet.HopStart ? " reçu en direct": "")})" : "Via lui-même")}

                       > {(isText ? "" : $"{packet.PortNum} :\n")} {packet.PayloadJson?.Trim('"')}
                       """;
        
        foreach (var notificationConfiguration in notificationsCanalToSend)
        {
            await MakeRequest(notificationConfiguration, message, packet);
        }
    }
    
    private async Task<string?> MakeRequest(Webhook webhook, string message, Packet packet) 
    {
        Logger.LogTrace("Send notification to {name} for packet #{packetId}", webhook.Name, packet.Id);
        
        using var client = new HttpClient();

        var encodedMessage = Uri.EscapeDataString(message);
        var requestUrl = webhook.Url.Replace("{{message}}", encodedMessage);

        try
        {
            var response = await client.GetAsync(requestUrl);

            response.EnsureSuccessStatusCode();

            Logger.LogInformation("Send notification to {name} for packet #{packetId} OK", webhook.Name, packet.Id);

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Send notification to {name} for packet #{packetId} KO", webhook.Name, packet.Id);
            return default;
        }
    }
}