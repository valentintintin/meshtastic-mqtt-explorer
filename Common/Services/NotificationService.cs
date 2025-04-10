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
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public async Task SendNotification(Packet packet)
    {
        await _lock.WaitAsync();

        try
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
                var message = GetPacketMessageToSend(packet, context, notificationConfiguration.IncludeHopsDetails);
                await MakeRequest(notificationConfiguration, message, packet, context);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public string GetPacketMessageToSend(Packet packet, DataContext context, bool includeHopsDetails = true)
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
                var i = 0;

                message += $"(<b>{aPacketData.Hop}</b>/{packet.HopStart}) ";
                
                foreach (var aPacket in aPacketData.Packets)
                {
                    message += (aPacket.RelayNodeNode != null ? $"<b>{aPacket.RelayNodeNode.OneName(true)}</b>>" : "") 
                        + $"<b>{aPacket.Gateway.OneName(true)}</b> ({aPacket.RxSnr})"
                        + " ; ";
                }

                message += "" + '\n';
            }
        }

        message = message.TrimEnd()
                  + '\n'
                  + $"> <b>{(isText ? "" : $"{packet.PortNum} :\n")}{packet.PayloadJson?.Trim('"')}</b>"
                  + '\n'
                  + $"{configuration.GetValue<string>("FrontUrl")}/packet/{packet.PacketDuplicatedId ?? packet.Id}";
        
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

            response.EnsureSuccessStatusCode();

            Logger.LogInformation("Send notification to {name} for packet #{packetId} OK", webhook.Name, packet.Id);

            var responseContent = await response.Content.ReadAsStringAsync();
            
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