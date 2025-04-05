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

        Context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        
        await Context.Positions.Where(a => a.UpdatedAt < minDate).ExecuteDeleteAsync();
        await Context.NeighborInfos.Where(a => a.UpdatedAt < minDate).ExecuteDeleteAsync();
        await Context.SignalHistories.Where(a => a.CreatedAt < minDate).ExecuteDeleteAsync();
        await Context.Telemetries.Where(a => a.CreatedAt < minDate).ExecuteDeleteAsync();
        await Context.TextMessages.Where(a => a.CreatedAt < minDate).ExecuteDeleteAsync();
        await Context.Waypoints.Where(a => a.UpdatedAt < minDate).ExecuteDeleteAsync();
        var deleteResult = await Context.Packets.Where(a => a.CreatedAt < minDate).ExecuteDeleteAsync();

        Logger.LogInformation("Old packets deleted {result}", deleteResult);

        var nodesIdToDelete = Context.Nodes.Where(a => a.UpdatedAt < minDate).Select(a => a.Id).ToList();
        await Context.TextMessages.Where(a => nodesIdToDelete.Contains(a.ToId) || nodesIdToDelete.Contains(a.FromId)).ExecuteDeleteAsync();
        await Context.Packets.Where(a => nodesIdToDelete.Contains(a.ToId) || nodesIdToDelete.Contains(a.FromId) || nodesIdToDelete.Contains(a.GatewayId) || (a.NextHopId.HasValue && nodesIdToDelete.Contains(a.NextHopId.Value)) || (a.RelayNodeId.HasValue && nodesIdToDelete.Contains(a.RelayNodeId.Value))).ExecuteDeleteAsync();
        await Context.NeighborInfos.Where(a => nodesIdToDelete.Contains(a.NodeHeardId) || nodesIdToDelete.Contains(a.NodeReceiverId)).ExecuteDeleteAsync();
        deleteResult = await Context.Nodes.Where(a => nodesIdToDelete.Contains(a.Id)).ExecuteDeleteAsync();
        
        Logger.LogInformation("Old nodes deleted {result}", deleteResult);

        var channelsIdToDelete = Context.Channels.Where(a => a.UpdatedAt < minDate).Select(a => a.Id).ToList();
        await Context.TextMessages.Where(a => channelsIdToDelete.Contains(a.ChannelId)).ExecuteDeleteAsync();
        await Context.Packets.Where(a => channelsIdToDelete.Contains(a.ToId)).ExecuteDeleteAsync();
        deleteResult = await Context.Channels.Where(a => channelsIdToDelete.Contains(a.Id)).ExecuteDeleteAsync();
        
        Logger.LogInformation("Old channels deleted {result}", deleteResult);
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