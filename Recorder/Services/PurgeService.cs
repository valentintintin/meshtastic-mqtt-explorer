using System.Reactive.Concurrency;
using Common.Context;
using Common.Context.Entities;
using Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Recorder.Services;

public class PurgeService : AService
{
    private readonly IConfiguration _configuration;

    public PurgeService(ILogger<PurgeService> logger, IDbContextFactory<DataContext> contextFactory, IConfiguration configuration, IServiceProvider serviceProvider) : base(logger, contextFactory)
    {
        _configuration = configuration;
        logger.LogTrace("Purge des données");
        PurgeOldData().ConfigureAwait(true).GetAwaiter().GetResult();
        PurgeOldPackets().ConfigureAwait(true).GetAwaiter().GetResult();;
        logger.LogInformation("Purge des données OK");

        serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IScheduler>().SchedulePeriodic(TimeSpan.FromHours(1), async () =>
        {
            await PurgeOldData();
            await PurgeOldPackets();
        });
    }

    public async Task PurgeOldPackets()
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-_configuration.GetValue("PurgeDays", 3));
        
        Logger.LogTrace("Delete packets if they are too old < {date}", minDate);
        
        Context.RemoveRange(Context.Packets.Where(a => a.CreatedAt < minDate));
        
        await Context.SaveChangesAsync();
    }

    public async Task PurgeDataForNode(Node node)
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-_configuration.GetValue("PurgeDays", 3));
        
        Logger.LogTrace("Delete old data for node {node} if they are too old < {date}", node, minDate);
        
        Context.RemoveRange(Context.Telemetries.Where(a => a.CreatedAt < minDate && a.Node == node));
        Context.RemoveRange(Context.Positions.Where(a => a.CreatedAt < minDate && a.Node == node));
        Context.RemoveRange(Context.Telemetries.Where(a => a.CreatedAt < minDate && a.Node == node));
        Context.RemoveRange(Context.NeighborInfos.Where(a => a.CreatedAt < minDate && a.Node == node));

        await Context.SaveChangesAsync();
    }

    public async Task PurgeOldData()
    {
        var minDate = DateTime.UtcNow.Date.AddDays(-_configuration.GetValue("PurgeDays", 3));
        
        Logger.LogTrace("Delete old data if they are too old < {date}", minDate);
        
        Context.RemoveRange(Context.Telemetries.Where(a => a.CreatedAt < minDate));
        Context.RemoveRange(Context.Positions.Where(a => a.CreatedAt < minDate));
        Context.RemoveRange(Context.Telemetries.Where(a => a.CreatedAt < minDate));
        Context.RemoveRange(Context.NeighborInfos.Where(a => a.CreatedAt < minDate));

        await Context.SaveChangesAsync();
    }
}