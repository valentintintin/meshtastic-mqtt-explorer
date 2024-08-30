using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using MeshtasticMqttExplorer.Context;
using MeshtasticMqttExplorer.Context.Entities;
using MeshtasticMqttExplorer.Models;
using Microsoft.EntityFrameworkCore;

namespace MeshtasticMqttExplorer.Services;

public class NotificationService(ILogger<AService> logger, IDbContextFactory<DataContext> contextFactory, IConfiguration configuration) : AService(logger, contextFactory)
{
    public async Task SendNotification(Packet packet, MeshPacket meshPacket)
    {
        var notificationsCanalToSend = (configuration.GetSection("Notifications").Get<List<NotificationConfiguration>>() ?? [])
            .Where(n => n.Enabled)
            .Where(n => !n.PortNum.HasValue || packet.PortNum == n.PortNum)
            .Where(n => !n.To.HasValue || packet.To.NodeId == n.To)
            .Where(n => !n.From.HasValue || packet.From.NodeId == n.From)
            .Where(n => string.IsNullOrWhiteSpace(n.Channel) || packet.Channel.Name == n.Channel)
            .ToList();

        var message = $"""
                       [{packet.Channel.Name}] {packet.From.AllNames}
                       
                       {(packet.To.NodeId != MeshtasticService.NodeBroadcast ? $"Pour : {packet.To.AllNames}" : "En broadcast")}

                       {(packet.Gateway != packet.From ? $"Via {packet.Gateway.AllNames} ({packet.GatewayDistanceKm} Km, SNR {packet.RxSnr})" : "Via lui-même")}

                       > {meshPacket.GetPayload<string>()}
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