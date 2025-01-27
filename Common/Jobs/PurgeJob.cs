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
        var minDate = DateTime.UtcNow.Date.AddDays(-configuration.GetValue("PurgeDays", 3));
        
        Logger.LogTrace("Delete old data if they are too old < {date}", minDate);

        Context.RemoveRange(Context.Positions.Where(a => a.UpdatedAt < minDate));
        Context.RemoveRange(Context.NeighborInfos.Where(a => a.UpdatedAt < minDate));
        Context.RemoveRange(Context.SignalHistories.Where(a => a.CreatedAt < minDate));
        Context.RemoveRange(Context.Telemetries.Where(a => a.CreatedAt < minDate));
        Context.RemoveRange(Context.TextMessages.Where(a => a.CreatedAt < minDate));
        Context.RemoveRange(Context.Waypoints.Where(a => a.UpdatedAt < minDate));
        await Context.SaveChangesAsync();

        Context.RemoveRange(Context.Packets.Where(a => a.CreatedAt < minDate));
        await Context.SaveChangesAsync();

        var nodesToDelete = Context.Nodes.Where(a => a.LastSeen < minDate).ToList();
        var nodesIdToDelete = nodesToDelete.Select(a => a.Id).ToList();
        Context.RemoveRange(Context.TextMessages.Where(a => nodesIdToDelete.Contains(a.ToId) || nodesIdToDelete.Contains(a.FromId)));
        Context.RemoveRange(Context.Packets.Where(a => nodesIdToDelete.Contains(a.ToId) || nodesIdToDelete.Contains(a.FromId) || nodesIdToDelete.Contains(a.GatewayId)));
        Context.RemoveRange(nodesToDelete);
        await Context.SaveChangesAsync();
        
        var channelsToDelete = Context.Channels.Where(a => a.UpdatedAt < minDate).ToList();
        var channelsIdToDelete = nodesToDelete.Select(a => a.Id).ToList();
        Context.RemoveRange(Context.TextMessages.Where(a => channelsIdToDelete.Contains(a.ChannelId)));
        Context.RemoveRange(Context.Packets.Where(a => channelsIdToDelete.Contains(a.ToId)));
        Context.RemoveRange(channelsToDelete);
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