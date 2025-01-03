using System.Reactive.Concurrency;
using Common.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Common.Services;

public class PurgeService : AService
{
    private readonly IConfiguration _configuration;

    public PurgeService(ILogger<PurgeService> logger, IDbContextFactory<DataContext> contextFactory, IConfiguration configuration, IServiceProvider serviceProvider) : base(logger, contextFactory)
    {
        _configuration = configuration;

        var scheduler = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IScheduler>();
        
        scheduler.Schedule(TimeSpan.FromSeconds(15), async () =>
        {
            await RunPurge();
        });
        
        scheduler.SchedulePeriodic(TimeSpan.FromHours(1), async () =>
        {
            await RunPurge();
        });
    }

    public async Task RunPurge()
    {
        Logger.LogTrace("Purge des données");
        await PurgeOldData();
        await PurgeOldPackets();
        await RefreshNodesGateway();
        Logger.LogInformation("Purge des données OK");
    }

    private async Task PurgeOldPackets()
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-_configuration.GetValue("PurgeDays", 3));
        
        Logger.LogTrace("Delete packets if they are too old < {date}", minDate);
        
        Context.RemoveRange(Context.Packets.Where(a => a.CreatedAt < minDate));
        
        await Context.SaveChangesAsync();
    }

    private async Task PurgeOldData()
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-_configuration.GetValue("PurgeDays", 3));
        
        Logger.LogTrace("Delete old data if they are too old < {date}", minDate);
        
        Context.RemoveRange(Context.Telemetries.Where(a => a.CreatedAt < minDate));
        Context.RemoveRange(Context.Positions.Where(a => a.CreatedAt < minDate));
        Context.RemoveRange(Context.Telemetries.Where(a => a.CreatedAt < minDate));
        Context.RemoveRange(Context.NeighborInfos.Where(a => a.CreatedAt < minDate));
        Context.RemoveRange(Context.SignalHistories.Where(a => a.CreatedAt < minDate));
        Context.RemoveRange(Context.TextMessages.Where(a => a.CreatedAt < minDate));

        await Context.SaveChangesAsync();
    }

    private async Task RefreshNodesGateway()
    {
        var minDate = DateTime.UtcNow.AddDays(-1);
        
        Logger.LogTrace("Make gateway offline if they are too old < {date}", minDate);

        foreach (var node in Context.Nodes
                     .Include(a => a.PacketsGateway.OrderByDescending(b => b.CreatedAt).Take(1))
                     .Where(a => a.IsMqttGateway == true && a.PacketsGateway.Any()))
        {
            if (node.PacketsGateway.First().CreatedAt < minDate)
            {
                node.IsMqttGateway = false;
                Context.Update(node);
            }
        }

        await Context.SaveChangesAsync();
    }
}