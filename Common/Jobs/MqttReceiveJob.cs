using Common.Context;
using Common.Context.Entities;
using Common.Exceptions;
using Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        var services = serviceProvider.CreateScope().ServiceProvider;
        var packet = await services.GetRequiredService<MqttClientService>().DoReceive(topic, payload, mqttServer);

        Logger.LogInformation("Received frame#{packetId} from {name} on {topic} with id {guid} done. Frame time {frameTime}", packet?.packet.Id, mqttServer.Name, topic, guid, DateTimeOffset.FromUnixTimeSeconds(packet?.meshPacket.RxTime ?? 0));
    }
}