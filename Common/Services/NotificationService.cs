using System.Text.Json;
using Common.Context;
using Common.Context.Entities;
using Common.Extensions.Entities;
using Common.Models;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Common.Services;

public class NotificationService(ILogger<AService> logger, IDbContextFactory<DataContext> contextFactory, IConfiguration configuration) : AService(logger, contextFactory)
{
    private Dictionary<(string url, uint packetId), (string? messageId, DateTime dateTime)> MessageIdForPacketId { get; } = new();
    
    public async Task SendNotification(Packet packet)
    {
        var context = await ContextFactory.CreateDbContextAsync();
        
        var notificationsCanalToSend = context.Webhooks
            .Where(n => n.Enabled)
            .Where(n => !n.PortNum.HasValue || packet.PortNum == n.PortNum)
            .Where(n => !n.From.HasValue || packet.From.NodeId == n.From)
            .Where(n => !n.To.HasValue || packet.To.NodeId == n.To)
            .Where(n => !n.Gateway.HasValue || packet.Gateway.NodeId == n.Gateway)
            .Where(n => !n.FromOrTo.HasValue 
                        || packet.From.NodeId == n.FromOrTo 
                        || packet.To.NodeId == n.FromOrTo)
            .Where(n => !n.MqttServerId.HasValue
                        || !n.OnlyWhenDifferentMqttServer && (packet.MqttServerId == n.MqttServerId 
                                                              || packet.From.MqttServerId == n.MqttServerId
                                                              || packet.Gateway.MqttServerId == n.MqttServerId
                                                              || (packet.PacketDuplicated != null && packet.PacketDuplicated.MqttServerId == n.MqttServerId)
                        )
                        || (n.OnlyWhenDifferentMqttServer && packet.From.MqttServerId != n.MqttServerId && packet.FromId != packet.GatewayId)
            )
            .Where(n => string.IsNullOrWhiteSpace(n.Channel) || packet.Channel.Name == n.Channel)
            .Where(n => n.AllowByHimSelf || packet.FromId != packet.GatewayId)
            // .Where(n => packet.PacketDuplicated == null || n.AllowDuplication)
            .AsEnumerable()
            .Where(n => 
                !n.DistanceAroundPositionKm.HasValue
                || (
                    n is { Latitude: not null, Longitude: not null } 
                    && packet.GatewayPosition != null 
                    && MeshtasticUtils.CalculateDistance(n.Latitude.Value, n.Longitude.Value, packet.GatewayPosition.Latitude, packet.GatewayPosition.Longitude) <= n.DistanceAroundPositionKm
                )
            )
            .DistinctBy(n => n.Url)
            .ToList();

        if (notificationsCanalToSend.Count == 0)
        {
            return;
        }
        
        foreach (var notificationConfiguration in notificationsCanalToSend)
        {
            var message = GetPacketMessageToSend(packet, context, notificationConfiguration.IncludeHopsDetails);
            await MakeRequest(notificationConfiguration, message, packet);
            
            PurgeMessageIds();
        }
    }

    private void PurgeMessageIds()
    {
        foreach (var (key, _) in MessageIdForPacketId.Where(a => DateTime.UtcNow - a.Value.dateTime >= TimeSpan.FromMinutes(5)))
        {
            MessageIdForPacketId.Remove(key);
        }
    }

    public string GetPacketMessageToSend(Packet packet, DataContext context, bool includeHopsDetails = true)
    {
        var isText = packet.PortNum is PortNum.TextMessageApp;

        var allPackets = context.Packets.GetAllPacketForPacketIdGroupedByHops(packet);

        var message = $"""
                       [{packet.Channel.Name}]
                       <b>{packet.From.AllNames}</b>
                       """;

        if (packet.To.NodeId != MeshtasticService.NodeBroadcast)
        {
            message += "" + '\n' + '\n' + $"Pour : <b>{packet.To.AllNames}</b>";
        }

        if (includeHopsDetails)
        {
            if (allPackets.Count != 0)
            {
                message += "" + '\n' + '\n';
            }

            foreach (var aPacketData in allPackets)
            {
                var nbHop = aPacketData.Key;

                message += "-- " + (nbHop == 0 ? "Reçu en <b>direct</b>" : $"Saut <b>{nbHop}</b>/{packet.HopStart}") + " --" +
                           '\n' + '\n';

                foreach (var aPacket in aPacketData)
                {
                    message +=
                        $"--> <b>{aPacket.Gateway.Name()}</b>\n{aPacket.GatewayDistanceKm} Km, SNR <b>{aPacket.RxSnr}</b>, RSSI <b>{aPacket.RxRssi}</b>, MQTT {aPacket.MqttServer?.Name}. {(packet.Id == aPacket.Id ? "Dernier reçu." : "")}"
                        + '\n' + '\n';
                }
            }
        }

        message = message.TrimEnd()
                  + '\n' + '\n' 
                  + $"> <b>{(isText ? "" : $"{packet.PortNum} :\n")}{packet.PayloadJson?.Trim('"')}</b>"
                  + '\n' + '\n'
                  + $"{configuration.GetValue<string>("FrontUrl")}/packet/{packet.PacketDuplicatedId ?? packet.Id}";
        
        return message;
    }

    private async Task MakeRequest(Webhook webhook, string message, Packet packet) 
    {
        Logger.LogTrace("Send notification to {name} for packet #{packetId}", webhook.Name, packet.Id);
        
        using var client = new HttpClient();

        var encodedMessage = Uri.EscapeDataString(message);
        var requestUrl = webhook.Url;

        var cacheMessageIdKey = (webhook.Url, packet.PacketId);
        if (!string.IsNullOrWhiteSpace(webhook.UrlToEditMessage) && MessageIdForPacketId.TryGetValue(cacheMessageIdKey, out var messageId) && !string.IsNullOrWhiteSpace(messageId.messageId))
        {
            Logger.LogDebug("Send notification to {name} for packet #{packetId} with edit url and messageId {messageId} found", webhook.Name, packet.Id, messageId.messageId);
            
            if (!webhook.IncludeHopsDetails)
            {
                Logger.LogDebug("Send notification to {name} for packet #{packetId} ignored because no hop details but edit url", webhook.Name, packet.Id);
                
                return;
            }
            
            requestUrl = webhook.UrlToEditMessage.Replace("{{messageId}}", messageId.messageId);
        }

        requestUrl = requestUrl.Replace("{{message}}", encodedMessage);

        try
        {
            var response = await client.GetAsync(requestUrl);

            response.EnsureSuccessStatusCode();

            Logger.LogInformation("Send notification to {name} for packet #{packetId} OK", webhook.Name, packet.Id);

            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (requestUrl.StartsWith("https://api.telegram.org/"))
            {
                MessageIdForPacketId.Remove(cacheMessageIdKey);
                MessageIdForPacketId.Add(cacheMessageIdKey, (GetMessageIdFromTelegramResponse(responseContent), DateTime.UtcNow));
            }
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Send notification to {name} for packet #{packetId} KO", webhook.Name, packet.Id);
        }
    }
    
    private string? GetMessageIdFromTelegramResponse(string responseContent)
    {
        return JsonSerializer.Deserialize<TelegramAddEditMessageResponseDto>(responseContent)?.Result.MessageId.ToString();
    }
}