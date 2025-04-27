using Common.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Common.Jobs;

public class PurgeJob(
    ILogger<PurgeJob> logger,
    IDbContextFactory<DataContext> contextFactory,
    IConfiguration configuration,
    IServiceProvider serviceProvider)
    : AJob(logger, contextFactory, serviceProvider)
{
    public async Task RunPurge()
    {
        Logger.LogTrace("Purge des données");
        await PurgeOldData();
        await RefreshNodesGateway();
        Logger.LogInformation("Purge des données OK");
    }

    private async Task PurgeOldData()
    {
        var minDate = DateTime.UtcNow.AddDays(-configuration.GetValue("PurgeDays", 3));
        
        Logger.LogTrace("Delete old data if they are too old < {date}", minDate);

        Context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));

        var deleteResult = await Context.Nodes.Where(a => a.UpdatedAt < minDate).ExecuteDeleteAsync();
        Logger.LogInformation("Old nodes deleted {result}", deleteResult);

        deleteResult = await Context.Channels.Where(a => a.UpdatedAt < minDate).ExecuteDeleteAsync();
        Logger.LogInformation("Old channels deleted {result}", deleteResult);
        
        await Context.Positions.Where(a => a.UpdatedAt < minDate).ExecuteDeleteAsync();
        await Context.NeighborInfos.Where(a => a.UpdatedAt < minDate).ExecuteDeleteAsync();
        await Context.SignalHistories.Where(a => a.CreatedAt < minDate).ExecuteDeleteAsync();
        await Context.Telemetries.Where(a => a.CreatedAt < minDate).ExecuteDeleteAsync();
        await Context.TextMessages.Where(a => a.CreatedAt < minDate).ExecuteDeleteAsync();
        await Context.Waypoints.Where(a => a.UpdatedAt < minDate).ExecuteDeleteAsync();
        await Context.PaxCounters.Where(a => a.UpdatedAt < minDate).ExecuteDeleteAsync();
        deleteResult = await Context.Packets.Where(a => a.CreatedAt < minDate).ExecuteDeleteAsync();
        Logger.LogInformation("Old packets deleted {result}", deleteResult);
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