using System.Buffers;
using System.Text;
using System.Text.Json;
using Common.Context;
using Common.Context.Entities;
using Common.Extensions;
using Common.Extensions.Entities;
using Common.Models;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Common.Services;

public class MqttService(ILogger<MqttService> logger, IDbContextFactory<DataContext> contextFactory, MeshtasticService meshtasticService, NotificationService notificationService) : AService(logger, contextFactory)
{
    public async Task<(Packet packet, MeshPacket meshPacket)?> DoReceive(string topic, byte[] data, MqttServer mqttServer)
    {
        var topicSegments = topic.Split("/");

        if (data.Length == 0)
        {
            logger.LogWarning("Received from {name} on {topic} without data so ignored", mqttServer.Name, topic);
            return null;
        }

        var rootPacket = new ServiceEnvelope();

        try
        {
            rootPacket.MergeFrom(data);

            if (rootPacket.Packet == null)
            {
                logger.LogWarning("Received from {name} on {topic} but packet null", mqttServer.Name, topic);
                return null;
            }

            var packetAndMeshPacket = await DoReceivePacket(rootPacket, mqttServer.Id, topicSegments);

            if (packetAndMeshPacket != null)
            {
                await notificationService.SendNotification(packetAndMeshPacket.Value.packet);
            }

            return packetAndMeshPacket;
        }
        catch (InvalidProtocolBufferException ex)
        {
            logger.LogWarning(ex,
                "Received from {name} on {topic} but packet incorrect. Packet Raw : {packetRaw} | {rawString}",
                mqttServer.Name, topic, Convert.ToBase64String(data), Encoding.UTF8.GetString(data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error for a received MQTT message from {name} on {topic}. Packet : {packet}. Packet Raw : {packetRaw} | {rawString}",
                mqttServer.Name, topic, JsonSerializer.Serialize(rootPacket),
                Convert.ToBase64String(data), Encoding.UTF8.GetString(data));
        }

        return null;
    }

    private async Task<(Packet packet, MeshPacket meshPacket)?> DoReceivePacket(ServiceEnvelope rootPacket, long mqttServerId, string[] topics)
    {
        if (!rootPacket.GatewayId.StartsWith('!'))
        {
            logger.LogWarning("Node (gateway) incorrect : {nodeId}", rootPacket.GatewayId);
            return null;
        }

        var nodeGatewayId = rootPacket.GatewayId.ToInteger();

        meshtasticService.SetDbContext(Context);
        var packetAndMeshPacket = await meshtasticService.DoReceive(nodeGatewayId, rootPacket.ChannelId, rootPacket.Packet);

        if (packetAndMeshPacket != null)
        {
            var packet = packetAndMeshPacket.Value.packet;
            
            packet.MqttServer = await Context.MqttServers.FindAsync(mqttServerId);
            packet.MqttTopic = topics.Take(topics.Length - 1).JoinString("/");
            Context.Update(packet);
            await Context.SaveChangesAsync();
            
            if (packet.From == packet.Gateway || packet.From.MqttServer == null)
            {
                packet.From.MqttServer = packet.MqttServer;
                Context.Update(packet.From);
                await Context.SaveChangesAsync();
            }

            Config.Types.LoRaConfig.Types.RegionCode? regionCode = null;
            Config.Types.LoRaConfig.Types.ModemPreset? modemPreset = null;
            
            if (topics.Contains("EU_868"))
            {
                regionCode = Config.Types.LoRaConfig.Types.RegionCode.Eu868;
            } 
            else if (topics.Contains("EU_433"))
            {
                regionCode = Config.Types.LoRaConfig.Types.RegionCode.Eu433;
            }
            
            if (topics.Contains("LongFast"))
            {
                modemPreset = Config.Types.LoRaConfig.Types.ModemPreset.LongFast;
            } 
            else if (topics.Contains("LongMod"))
            {
                modemPreset = Config.Types.LoRaConfig.Types.ModemPreset.LongModerate;
            }
            else if (topics.Contains("LongSlow"))
            {
                modemPreset = Config.Types.LoRaConfig.Types.ModemPreset.LongSlow;
            }
            
            await meshtasticService.UpdateRegionCodeAndModemPreset(packet.From, regionCode, modemPreset, MeshtasticService.RegionCodeAndModemPresetSource.Mqtt);
            await meshtasticService.UpdateRegionCodeAndModemPreset(packet.Gateway, regionCode, modemPreset, MeshtasticService.RegionCodeAndModemPresetSource.Mqtt);
        }

        return packetAndMeshPacket;
    }
}