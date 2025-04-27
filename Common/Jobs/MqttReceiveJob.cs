using System.Buffers;
using System.Text;
using Common.Context;
using Common.Context.Entities;
using Common.Exceptions;
using Common.Services;
using Meshtastic.Protobufs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;

namespace Common.Jobs;

public class MqttReceiveJob(ILogger<MqttReceiveJob> logger, IDbContextFactory<DataContext> contextFactory, IServiceProvider serviceProvider) : AJob(logger, contextFactory, serviceProvider)
{
    public async Task ExecuteAsync(string topic, byte[] payload, long mqttServerId, Guid guid)
    {
        var mqttServer = await (await ContextFactory.CreateDbContextAsync()).MqttServers.FindAsync(mqttServerId);

        if (mqttServer == null)
        {
            throw new NotFoundException<MqttServer>(mqttServerId);
        }

        var services = ServiceProvider.CreateScope().ServiceProvider;
        var mqttService = services.GetRequiredService<MqttService>();

        var mqttClient = MqttClientService.MqttClientAndConfigurations.FirstOrDefault(a => a.MqttServer.Id == mqttServer.Id)?.Client;
        var packet = await mqttService.DoReceive(topic, payload, mqttServer, mqttClient != null ? async message => await mqttClient.PublishAsync(message) : null);
        
        Logger.LogInformation("Received frame#{packetId} from {name} on {topic} with id {guid} done. Frame time {frameTime}", packet?.packet.Id, mqttServer.Name, topic, guid, DateTimeOffset.FromUnixTimeSeconds(packet?.serviceEnveloppe.Packet.RxTime ?? 0));
    }
}