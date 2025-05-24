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
    public async Task SendNotification(Packet packet)
    {
        var context = await ContextFactory.CreateDbContextAsync();

        var notificationsCanalToSend = context.Webhooks
            .Where(n => n.Enabled)
            .Where(n => !n.PortNum.HasValue || packet.PortNum == n.PortNum)
            .Where(n => packet.PortNum != PortNum.MapReportApp)
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
                                                              || (packet.PacketDuplicated != null &&
                                                                  packet.PacketDuplicated.MqttServerId ==
                                                                  n.MqttServerId)
                        )
                        || (n.OnlyWhenDifferentMqttServer && packet.From.MqttServerId != n.MqttServerId &&
                            packet.FromId != packet.GatewayId)
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
                    && MeshtasticUtils.CalculateDistance(n.Latitude.Value, n.Longitude.Value,
                        packet.GatewayPosition.Latitude, packet.GatewayPosition.Longitude) <=
                    n.DistanceAroundPositionKm
                )
            )
            .Union(
                context.WebhooksHistories
                    .Where(nn => nn.Packet.PacketId == packet.PacketId)
                    .Select(n => n.Webhook)
                    .Where(nn => !string.IsNullOrWhiteSpace(nn.UrlToEditMessage))
                    .Where(nn => nn.Enabled)
            )
            .DistinctBy(n => n.Url)
            .ToList();

        if (notificationsCanalToSend.Count == 0)
        {
            return;
        }

        foreach (var notificationConfiguration in notificationsCanalToSend)
        {
            var message = GetPacketMessageToSend(packet, context, notificationConfiguration.IncludeHopsDetails, notificationConfiguration.IncludePayloadDetails, notificationConfiguration.IncludeStats);
            await MakeRequest(notificationConfiguration, message, packet, context);
        }
    }

    public string GetPacketMessageToSend(Packet packet, DataContext context, bool includeHopsDetails = true, bool includePayloadDetails = true, bool includeStats = true)
    {
        var isText = packet.PortNum is PortNum.TextMessageApp;

        var allPackets = context.Packets.GetAllPacketForPacketIdGroupedByHops(packet);

        var message = $"[{packet.Channel.Name}] <b>{packet.From.AllNames}</b>";

        if (packet.To.NodeId != MeshtasticService.NodeBroadcast)
        {
            message += "" + '\n' + $"Pour: <b>{packet.To.AllNames}</b>";
        }

        if (includeHopsDetails)
        {
            if (allPackets.Count != 0)
            {
                message += "" + '\n';
            }

            foreach (var aPacketData in allPackets)
            {
                message += $"(<b>{aPacketData.Hop}</b>/{packet.HopStart}) ";
                
                foreach (var aPacket in aPacketData.Packets)
                {
                    if (aPacket.RelayNodeNode != null)
                    {
                        message += $"<b>{aPacket.RelayNodeNode.OneName(true)}</b>>";
                    }
                    else if (aPacket.RelayNode > 0)
                    {
                        message += $"<b>0x{aPacket.RelayNode}</b>>";
                    }
                    
                    message += $"<b>{aPacket.Gateway.OneName(true)}</b> ({aPacket.RxSnr}) ; ";
                }

                message += "" + '\n';
            }
        }

        message = message.TrimEnd()
                  + '\n'
                  + $"> <b>{(isText ? "" : $"{packet.PortNum}")}";
        
        if (includePayloadDetails)
        {
            if (!isText)
            {
                message += "" + '\n';
            }

            message += packet.PayloadJson?.Trim('"');
        }
        else if (isText)
        {
            message += packet.PayloadJson?.Trim('"');
        }
        
        message = message.TrimEnd()
                  + "</b>"
                  + '\n'
                  + $"{configuration.GetValue<string>("FrontUrl")}/packet/{packet.PacketDuplicatedId ?? packet.Id}";

        if (includeStats)
        {
            var packetsStats = context.Packets.Where(a => a.PortNum == packet.PortNum && a.From == packet.From && a.To == packet.To && a.PacketDuplicated == null);
            var countToday = packetsStats.Count(a => a.CreatedAt.Date == packet.CreatedAt.Date); 
            var lastPacketDate = packetsStats.OrderByDescending(a => a.CreatedAt).FirstOrDefault(a => a.PacketId != packet.PacketId)?.CreatedAt;

            message += '\n' + $"Dernier : <b>{(int?) (DateTime.UtcNow - lastPacketDate)?.TotalMinutes ?? 0}</b> min | Aujourd'hui : <b>{countToday}</b>";
        }
        
        return message;
    }

    private async Task<WebhookHistory?> MakeRequest(Webhook webhook, string message, Packet packet, DataContext context) 
    {
        Logger.LogTrace("Send notification to {name} for packet #{packetId}", webhook.Name, packet.Id);
        
        using var client = new HttpClient();

        var encodedMessage = Uri.EscapeDataString(message);
        var requestUrl = webhook.Url;

        var messageHistory = await context.WebhooksHistories.FirstOrDefaultAsync(a => a.WebhookId == webhook.Id && a.Packet.PacketId == packet.PacketId) ?? new WebhookHistory
        {
            WebhookId = webhook.Id
        };

        messageHistory.PacketId = packet.Id;
        
        if (!string.IsNullOrWhiteSpace(webhook.UrlToEditMessage) && !string.IsNullOrWhiteSpace(messageHistory.MessageId))
        {
            Logger.LogDebug("Send notification to {name} for packet #{packetId} with edit url and messageId {messageId} found", webhook.Name, packet.Id, messageHistory.MessageId);
            
            if (!webhook.IncludeHopsDetails)
            {
                Logger.LogDebug("Send notification to {name} for packet #{packetId} ignored because no hop details but edit url", webhook.Name, packet.Id);

                return null;
            }
            
            requestUrl = webhook.UrlToEditMessage.Replace("{{messageId}}", messageHistory.MessageId);
        }

        requestUrl = requestUrl.Replace("{{message}}", encodedMessage);

        try
        {
            var response = await client.GetAsync(requestUrl);

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("Send notification to {name} for packet #{packetId} KO : {content}", webhook.Name, packet.Id, responseContent);
                return null;
            }

            Logger.LogInformation("Send notification to {name} for packet #{packetId} OK", webhook.Name, packet.Id);
            
            if (requestUrl.StartsWith("https://api.telegram.org/"))
            {
                messageHistory.MessageId = GetMessageIdFromTelegramResponse(responseContent);
            }

            if (messageHistory.Id == 0)
            {
                context.Add(messageHistory);
            }
            else
            {
                context.Update(messageHistory);
            }

            await context.SaveChangesAsync();
            
            return messageHistory;
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Send notification to {name} for packet #{packetId} KO", webhook.Name, packet.Id);
        }

        return null;
    }
    
    private string? GetMessageIdFromTelegramResponse(string responseContent)
    {
        return JsonSerializer.Deserialize<TelegramAddEditMessageResponseDto>(responseContent)?.Result.MessageId.ToString();
    }
}