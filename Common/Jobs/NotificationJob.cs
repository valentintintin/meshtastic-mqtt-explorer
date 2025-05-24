using Common.Context;
using Common.Context.Entities;
using Common.Exceptions;
using Common.Extensions.Entities;
using Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Common.Jobs;

public class NotificationJob(ILogger<NotificationJob> logger, IDbContextFactory<DataContext> contextFactory, IServiceProvider serviceProvider) : AJob(logger, contextFactory, serviceProvider)
{
    public async Task ExecuteAsync(long packetId)
    {
        var services = serviceProvider.CreateScope().ServiceProvider;

        var packet = await Context.Packets
            .Include(n => n.Channel)
            .Include(n => n.MqttServer)
            .Include(n => n.From)
            .Include(n => n.To)
            .Include(n => n.Gateway)
            .Include(n => n.GatewayPosition)
            .Include(n => n.PacketDuplicated)
            .FindByIdAsync(packetId);

        if (packet == null)
        {
            throw new NotFoundException<Packet>(packetId);
        }
        
        await services.GetRequiredService<NotificationService>().SendNotification(packet);

        Logger.LogInformation("Notification for frame#{id}/{packetId} done", packet.Id, packet.PacketId);
    }
}