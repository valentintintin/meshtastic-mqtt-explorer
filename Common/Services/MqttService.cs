using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Common.Context;
using Common.Context.Entities;
using Common.Extensions;
using Google.Protobuf;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Position = Meshtastic.Protobufs.Position;

namespace Common.Services;

public class MqttService(ILogger<MqttService> logger, IDbContextFactory<DataContext> contextFactory, 
    MeshtasticService meshtasticService, NotificationService notificationService) : AService(logger, contextFactory)
{
    public async Task<(Packet packet, ServiceEnvelope serviceEnveloppe)?> DoReceive(string topic, byte[] data, MqttServer mqttServer, Func<MqttApplicationMessage, Task>? sendAsJsonPayload = null)
    {
        var topicSegments = topic.Split("/");

        if (data.Length == 0)
        {
            logger.LogWarning("Received from {name} on {topic} without data so ignored", mqttServer.Name, topic);
            return null;
        }

        var serviceEnveloppe = new ServiceEnvelope();

        try
        {
            serviceEnveloppe.MergeFrom(data);

            if (serviceEnveloppe.Packet == null)
            {
                logger.LogWarning("Received from {name} on {topic} but packet null", mqttServer.Name, topic);
                return null;
            }

            var packetAndMeshPacket = await DoReceivePacket(serviceEnveloppe, mqttServer.Id, topicSegments);

            if (packetAndMeshPacket?.packet != null)
            {
                var packet = packetAndMeshPacket.Value.packet;
                await notificationService.SendNotification(packet);

                if (packet.PacketDuplicatedId == null && mqttServer.ShouldBeRelayed)
                {
                    try
                    {
                        await RelayToAnotherMqttServer(topic, packet, serviceEnveloppe, mqttServer);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Relay error for Packet#{packetId} on topic {topic}", serviceEnveloppe.Packet.Id, topic);
                    }
                }
                
                if (sendAsJsonPayload != null && mqttServer.MqttPostJson)
                {
                    try
                    {
                        await sendAsJsonPayload(GetMqttApplicationMessageAsJson(topic, serviceEnveloppe, packetAndMeshPacket));
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning(e, "Impossible to resend json packet on MQTT");
                    }
                }
                
                return (packet, serviceEnveloppe);
            }
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
                mqttServer.Name, topic, JsonSerializer.Serialize(serviceEnveloppe),
                Convert.ToBase64String(data), Encoding.UTF8.GetString(data));
        }

        return null;
    }

    private static MqttApplicationMessage GetMqttApplicationMessageAsJson(string topic, ServiceEnvelope serviceEnvelope, [DisallowNull] (Packet packet, MeshPacket meshPacket)? packetAndMeshPacket)
    {
        return new MqttApplicationMessage
        {
            Topic = topic.Replace("msh", "msh-json"),
            Payload = new ReadOnlySequence<byte>(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(new {
                        payloadDecoded =  serviceEnvelope.Packet.GetPayload(),
                        from = new
                        {
                            id = packetAndMeshPacket.Value.packet.From.NodeIdString,  
                            longName = packetAndMeshPacket.Value.packet.From.LongName,  
                            shortName = packetAndMeshPacket.Value.packet.From.ShortName,
                            latitude = packetAndMeshPacket.Value.packet.From.Latitude,
                            longitude = packetAndMeshPacket.Value.packet.From.Longitude,
                            role = packetAndMeshPacket.Value.packet.From.Role,
                        },
                        to = new
                        {
                            id = packetAndMeshPacket.Value.packet.To.NodeIdString,  
                            longName = packetAndMeshPacket.Value.packet.To.LongName,  
                            shortName = packetAndMeshPacket.Value.packet.To.ShortName,
                            latitude = packetAndMeshPacket.Value.packet.To.Latitude,
                            longitude = packetAndMeshPacket.Value.packet.To.Longitude,
                            role = packetAndMeshPacket.Value.packet.To.Role,
                        },
                        gateway = new
                        {
                            id = packetAndMeshPacket.Value.packet.Gateway.NodeIdString,  
                            longName = packetAndMeshPacket.Value.packet.Gateway.LongName,  
                            shortName = packetAndMeshPacket.Value.packet.Gateway.ShortName,
                            latitude = packetAndMeshPacket.Value.packet.Gateway.Latitude,
                            longitude = packetAndMeshPacket.Value.packet.Gateway.Longitude,
                            role = packetAndMeshPacket.Value.packet.Gateway.Role,
                        },
                        relay = packetAndMeshPacket.Value.packet.RelayNodeNode != null ? new {
                            id = packetAndMeshPacket.Value.packet.RelayNodeNode.NodeIdString,  
                            longName = packetAndMeshPacket.Value.packet.RelayNodeNode.LongName,  
                            shortName = packetAndMeshPacket.Value.packet.RelayNodeNode.ShortName,
                            latitude = packetAndMeshPacket.Value.packet.RelayNodeNode.Latitude,
                            longitude = packetAndMeshPacket.Value.packet.RelayNodeNode.Longitude,
                            role = packetAndMeshPacket.Value.packet.RelayNodeNode.Role,
                        } : null,
                        packetEnveloppe = packetAndMeshPacket.Value.meshPacket
                    }, MeshtasticService.JsonSerializerOptions)
                )
            )
        };
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
            
            if (packet.From == packet.Gateway && packet.PortNum != PortNum.MapReportApp || packet.From.MqttServer == null)
            {
                logger.LogInformation("Change MQTT Server for node #{node} to {mqttServer}", packet.From, packet.MqttServer?.Name);
                
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

    private async Task RelayToAnotherMqttServer(string topic, Packet packet, ServiceEnvelope serviceEnvelope, MqttServer mqttServer)
    {
        var meshPacket = serviceEnvelope.Packet;
        
        foreach (var mqttClientAndConfiguration in MqttClientService.MqttClientAndConfigurations.Where(a => a.Client.IsConnected && a.MqttServer != mqttServer && a.MqttServer.IsARelayType != null))
        {
            if (mqttClientAndConfiguration.MqttServer.IsARelayType == MqttServer.RelayType.All
                || mqttClientAndConfiguration.MqttServer.IsARelayType == MqttServer.RelayType.MapReport && meshPacket.Decoded?.Portnum == PortNum.MapReportApp
                || mqttClientAndConfiguration.MqttServer.IsARelayType == MqttServer.RelayType.NodeInfoAndPosition && meshPacket.Decoded?.Portnum is PortNum.MapReportApp or PortNum.NodeinfoApp or PortNum.PositionApp
                || mqttClientAndConfiguration.MqttServer.IsARelayType == MqttServer.RelayType.UseFull && meshPacket.Decoded?.Portnum is PortNum.MapReportApp or PortNum.NodeinfoApp or PortNum.PositionApp or PortNum.NeighborinfoApp or PortNum.TracerouteApp
               )
            {
                logger.LogInformation("Relay from MqttServer {name}, frame#{packetId} from {from} of type {portNum} to MqttServer {relayMqttName} topic {topic}", mqttServer.Name, packet.Id, packet.From, meshPacket.Decoded?.Portnum, mqttClientAndConfiguration.MqttServer.Name, topic);

                if (meshPacket.Decoded != null)
                {
                    try
                    {
                        var positionPrecision = mqttClientAndConfiguration.MqttServer.RelayPositionPrecision ?? 32;
                        var dataApp = new Data();
                        dataApp.MergeFrom(meshPacket.Decoded.Payload);

                        switch (meshPacket.Decoded?.Portnum)
                        {
                            case PortNum.PositionApp:
                            {
                                var position = new Position();
                                position.MergeFrom(meshPacket.Decoded.Payload);

                                if (position.PrecisionBits > positionPrecision)
                                {
                                    logger.LogDebug(
                                        "Relay frame#{packetId} change precision from {currentPrecision} to {newPrecision} for {portNum} and node #{node}",
                                        meshPacket.Id, position.PrecisionBits, positionPrecision, meshPacket.Decoded.Portnum, packet.From);

                                    position.LatitudeI =
                                        (int)(position.LatitudeI & (uint.MaxValue << (int)(32 - positionPrecision)));
                                    position.LongitudeI =
                                        (int)(position.LongitudeI & (uint.MaxValue << (int)(32 - positionPrecision)));
                                    position.LatitudeI += 1 << (int)(31 - positionPrecision);
                                    position.LongitudeI += 1 << (int)(31 - positionPrecision);
                                    position.PrecisionBits = positionPrecision;
                                    
                                    meshPacket.Decoded.Payload = position.ToByteString();
                                }

                                break;
                            }
                            case PortNum.MapReportApp:
                            {
                                var mapReport = new MapReport();
                                mapReport.MergeFrom(meshPacket.Decoded.Payload);

                                if (mapReport.PositionPrecision > positionPrecision)
                                {
                                    logger.LogDebug(
                                        "Relay frame#{packetId} change precision from {currentPrecision} to {newPrecision} for {portNum} and node #{node}",
                                        meshPacket.Id, mapReport.PositionPrecision, positionPrecision, meshPacket.Decoded.Portnum, packet.From);

                                    mapReport.LatitudeI =
                                        (int)(mapReport.LatitudeI & (uint.MaxValue << (int)(32 - positionPrecision)));
                                    mapReport.LongitudeI =
                                        (int)(mapReport.LongitudeI & (uint.MaxValue << (int)(32 - positionPrecision)));
                                    mapReport.LatitudeI += 1 << (int)(31 - positionPrecision);
                                    mapReport.LongitudeI += 1 << (int)(31 - positionPrecision);
                                    mapReport.PositionPrecision = positionPrecision;
                                    
                                    meshPacket.Decoded.Payload = mapReport.ToByteString();
                                }

                                break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Frame#{packetId} of type {portNum} impossible to change ProtoBuf", meshPacket.Id, meshPacket.Decoded!.Portnum);
                    }
                }

                var result = await mqttClientAndConfiguration.Client.PublishBinaryAsync(topic, serviceEnvelope.ToByteArray());

                if (!result.IsSuccess)
                {
                    logger.LogWarning("Relay from MqttServer {name}, frame#{packetId} from {from} of type {portNum} to MqttServer {relayMqttName} topic {topic} KO : {code}, {error}", mqttServer.Name, packet.Id, packet.From, meshPacket.Decoded?.Portnum, mqttClientAndConfiguration.MqttServer.Name, topic, result.ReasonCode, result.ReasonString);
                }
            }
        }
    }
}