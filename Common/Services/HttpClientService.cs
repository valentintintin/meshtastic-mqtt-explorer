using System.Reflection;
using System.Text.Json;
using Common.Context;
using Common.Context.Entities;
using Common.Exceptions;
using Common.Extensions.Entities;
using Common.Jobs;
using Common.Models;
using Google.Protobuf;
using Hangfire;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Channel = Meshtastic.Protobufs.Channel;

namespace Common.Services;

public class HttpClientService(
    ILogger<HttpClientService> logger,
    IDbContextFactory<DataContext> contextFactory,
    IServiceProvider serviceProvider) : 
    BackgroundService
{
    public readonly List<HttpClientAndConfiguration> HttpClientAndConfigurations  = [];
    
    private readonly bool _isFront = Assembly.GetEntryAssembly()?.GetName().Name?.Contains("Explorer") ?? false;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        
        HttpClientAndConfigurations.AddRange(
            (await contextFactory.CreateDbContextAsync(stoppingToken)).MqttServers
                .Where(a => a.Type == MqttServer.ServerType.NodeHttp)
                .ToList()
                .Select(a => new HttpClientAndConfiguration
                {
                    MqttServer = a,
                    Client = new HttpClient
                    {
                        BaseAddress = new Uri($"{a.Host}:{a.Port}")
                    }
                })
        );

        if (HttpClientAndConfigurations.Count == 0)
        {
            return;
        }

        if (_isFront)
        {
            return;
        }
        
        var context = await contextFactory.CreateDbContextAsync(stoppingToken);
        var meshtasticService = serviceProvider.CreateAsyncScope().ServiceProvider.GetRequiredService<MeshtasticService>();
        meshtasticService.SetDbContext(context);

        Task.WaitAll(HttpClientAndConfigurations
            .Where(a => a.MqttServer.Enabled)
            .Select(httpClientAndConfiguration => SendPacket(httpClientAndConfiguration, new ToRadio
            {
                WantConfigId = (uint)Random.Shared.NextInt64()
            }, stoppingToken))
            .ToList(), stoppingToken);

        while (true)
        {
            foreach (var httpClientAndConfiguration in HttpClientAndConfigurations.Where(a => a.MqttServer.Enabled))
            {
                var fromRadio = await RetrievePacket(httpClientAndConfiguration, stoppingToken);

                if (fromRadio != null)
                {
                    logger.LogDebug("Receive from {name} payload {payloadType}",
                        httpClientAndConfiguration.MqttServer.Name, fromRadio.PayloadVariantCase);

                    switch (fromRadio.PayloadVariantCase)
                    {
                        case FromRadio.PayloadVariantOneofCase.None:
                            if (!httpClientAndConfiguration.InitDone)
                            {
                                httpClientAndConfiguration.InitDone = true;
                                logger.LogInformation("{name} init done",
                                    httpClientAndConfiguration.MqttServer.Name);
                            }
                            else if (DateTime.UtcNow - httpClientAndConfiguration.LastHeartbeat >=
                                     TimeSpan.FromMinutes(1))
                            {
                                await SendPacket(httpClientAndConfiguration, new ToRadio
                                {
                                    Heartbeat = new Heartbeat()
                                }, stoppingToken);
                                httpClientAndConfiguration.LastHeartbeat = DateTime.UtcNow;
                            }

                            break;
                        case FromRadio.PayloadVariantOneofCase.MyInfo:
                            httpClientAndConfiguration.Gateway =
                                context.Nodes.FindByNodeId(fromRadio.MyInfo.MyNodeNum) ?? new Node
                                {
                                    NodeId = fromRadio.MyInfo.MyNodeNum,
                                    IsMqttGateway = true
                                };
                            httpClientAndConfiguration.Gateway.LastSeen = DateTime.UtcNow;
                            if (httpClientAndConfiguration.Gateway.Id == 0)
                            {
                                context.Add(httpClientAndConfiguration.Gateway);
                                await context.SaveChangesAsync(stoppingToken);
                                logger.LogInformation("New node (gateway) created {node}",
                                    httpClientAndConfiguration.Gateway);
                            }
                            else
                            {
                                httpClientAndConfiguration.Gateway.IsMqttGateway = true;
                                context.Update(httpClientAndConfiguration.Gateway);
                                await context.SaveChangesAsync(stoppingToken);
                            }

                            break;
                        case FromRadio.PayloadVariantOneofCase.Config:
                            if (httpClientAndConfiguration.Gateway != null)
                            {
                                switch (fromRadio.Config.PayloadVariantCase)
                                {
                                    case Config.PayloadVariantOneofCase.Lora:
                                        await meshtasticService.UpdateRegionCodeAndModemPreset(
                                            httpClientAndConfiguration.Gateway, fromRadio.Config.Lora.Region,
                                            fromRadio.Config.Lora.ModemPreset,
                                            MeshtasticService.RegionCodeAndModemPresetSource.NodeInfo);
                                        break;
                                    case Config.PayloadVariantOneofCase.Device:
                                        httpClientAndConfiguration.Gateway.Role = fromRadio.Config.Device.Role;
                                        context.Update(httpClientAndConfiguration.Gateway);
                                        await context.SaveChangesAsync(stoppingToken);
                                        break;
                                }
                            }

                            break;
                        case FromRadio.PayloadVariantOneofCase.NodeInfo:
                            var node = context.Nodes.FindByNodeId(fromRadio.NodeInfo.Num) ?? new Node
                            {
                                NodeId = fromRadio.NodeInfo.Num,
                            };
                            if (node.Id == 0)
                            {
                                context.Add(node);
                                await context.SaveChangesAsync(stoppingToken);
                                logger.LogInformation("New node (NodeInfo) created {node}", node);
                            }

                            await meshtasticService.DoNodeInfoPacket(node, fromRadio.NodeInfo.User);

                            if (fromRadio.NodeInfo.Position != null)
                            {
                                await meshtasticService.UpdatePosition(node, fromRadio.NodeInfo.Position.LatitudeI,
                                    fromRadio.NodeInfo.Position.LongitudeI, fromRadio.NodeInfo.Position.Altitude, null);
                            }

                            if (httpClientAndConfiguration.Gateway != null)
                            {
                                await meshtasticService.UpdateRegionCodeAndModemPreset(node,
                                    httpClientAndConfiguration.Gateway.RegionCode,
                                    httpClientAndConfiguration.Gateway.ModemPreset,
                                    MeshtasticService.RegionCodeAndModemPresetSource.NodeInfo);
                            }

                            break;
                        case FromRadio.PayloadVariantOneofCase.Channel:
                            httpClientAndConfiguration.Channels.Add(fromRadio.Channel.Index, fromRadio.Channel);
                            break;
                        case FromRadio.PayloadVariantOneofCase.Packet:
                            
                            if (httpClientAndConfiguration is { InitDone: true, Gateway: not null } &&
                                httpClientAndConfiguration.Channels.ContainsKey((int)fromRadio.Packet.Channel))
                            {
                                httpClientAndConfiguration.NbPacket++;
                                httpClientAndConfiguration.LastPacketReceivedDate = DateTime.UtcNow;
                                
                                var packetAndMeshPacket = await meshtasticService.DoReceive(
                                    httpClientAndConfiguration.Gateway!.NodeId,
                                    httpClientAndConfiguration.Channels[(int)fromRadio.Packet.Channel].Settings.Name,
                                    fromRadio.Packet);
                                if (packetAndMeshPacket != null)
                                {
                                    var packet = packetAndMeshPacket.Value.packet;

                                    packet.MqttServerId = httpClientAndConfiguration.MqttServer.Id;
                                    context.Update(packet);

                                    if (packet.From == packet.Gateway || packet.From.MqttServer == null)
                                    {
                                        logger.LogInformation("Change MQTT Server for node #{node} to {mqttServer}",
                                            packet.From, packet.MqttServer?.Name);

                                        packet.From.MqttServerId = packet.MqttServerId;
                                        context.Update(packet.From);
                                    }

                                    await context.SaveChangesAsync(stoppingToken);

                                    await meshtasticService.UpdateRegionCodeAndModemPreset(packet.From,
                                        httpClientAndConfiguration.Gateway.RegionCode,
                                        httpClientAndConfiguration.Gateway.ModemPreset,
                                        MeshtasticService.RegionCodeAndModemPresetSource.Mqtt);
                                }
                            }
                            break;
                    }
                }
            }
            
            await Task.Delay(HttpClientAndConfigurations.All(a => a.InitDone) ? 2000 : 50, stoppingToken); // Move for each httpConfig
        }
    }

    public async Task PublishMessage(SentPacketDto dto)
    {
        var httpConfiguration = HttpClientAndConfigurations.FirstOrDefault(a => a.MqttServer.Id == dto.MqttServerId);

        if (httpConfiguration == null)
        {
            throw new NotFoundException<MqttServer>(dto.MqttServerId);
        }
        
        var meshtasticService = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<MeshtasticService>();
        
        var packet = new ToRadio
        {
            Packet = meshtasticService.CreateMeshPacketFromDto(dto)
        };

        if (packet.Packet.HopLimit == 0 && httpConfiguration.Gateway?.HopStart > 0)
        {
            packet.Packet.HopLimit = (uint)httpConfiguration.Gateway.HopStart.Value;
        }
        
        var packetBytes = packet.ToByteArray();
        
        logger.LogInformation("Send packet to HTTP {packet} with data {data} | {base64}", JsonSerializer.Serialize(packet), JsonSerializer.Serialize(packet.Packet.GetPayload()), Convert.ToBase64String(packetBytes));

        await httpConfiguration.Client.PutAsync("/api/v1/toradio", new ByteArrayContent(packetBytes));
        
        // dto.channel quand HTTP est un nombre donc pas bon
        await meshtasticService.DoReceive(packet.Packet.From, dto.Channel, packet.Packet);
    }

    private async Task<FromRadio?> RetrievePacket(HttpClientAndConfiguration httpClientAndConfiguration, CancellationToken stoppingToken)
    {
        try
        {
            var proto = await httpClientAndConfiguration.Client.GetByteArrayAsync("/api/v1/fromradio?all=false",
                stoppingToken);
            var fromRadio = new FromRadio();
            fromRadio.MergeFrom(proto);
            return fromRadio;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Http {name} fromRadio error", httpClientAndConfiguration.MqttServer.Name);
            return null;
        }
    }

    private async Task SendPacket(HttpClientAndConfiguration httpClientAndConfiguration, ToRadio toRadio, CancellationToken stoppingToken)
    {
        try
        {
            await httpClientAndConfiguration.Client.PutAsync("/api/v1/toradio",
                new ByteArrayContent(toRadio.ToByteArray()), stoppingToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Http {name} toRadio error", httpClientAndConfiguration.MqttServer.Name);
        }
    }
    
    public class HttpClientAndConfiguration : DeviceConnection
    {
        public required HttpClient Client { get; init; }
        public DateTime LastHeartbeat { get; set; } = DateTime.MinValue;
        public Node? Gateway { get; set; }

        public readonly Dictionary<int, Channel> Channels = new();
        
        public bool InitDone { get; set; }
    }
}