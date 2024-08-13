using Meshtastic.Protobufs;
using MeshtasticMqttExplorer.Context;
using MeshtasticMqttExplorer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DateTimeOffset = System.DateTimeOffset;
using NeighborInfo = MeshtasticMqttExplorer.Context.Entities.NeighborInfo;

namespace MeshtasticMqttExplorer.Controllers;

[ApiController]
[Route("graph")]
public class GraphController(ILogger<AController> logger, IDbContextFactory<DataContext> contextFactory) : AController(logger)
{
    [HttpGet]
    public async Task<GraphDto> GetGraph()
    {
        Logger.LogInformation("Get nodes graph");
        
        var context = await contextFactory.CreateDbContextAsync();

        var minDate = DateTime.UtcNow.Date.AddDays(-1);

        var query = context.Nodes
            .Include(n => n.MyNeighbors
                .OrderByDescending(t => t.UpdatedAt)
                .Where(t => t.UpdatedAt >= minDate)
            )
            .ThenInclude(nn => nn.Neighbor);

        var links = await query
            .Where(n => n.LastSeen >= minDate)
            .Where(n => n.MyNeighbors.Count > 0)
            .Where(n => n.RegionCode == Config.Types.LoRaConfig.Types.RegionCode.Eu868 && n.ModemPreset == Config.Types.LoRaConfig.Types.ModemPreset.LongModerate)
            .OrderByDescending(n => n.LastSeen)
            .SelectMany(n => n.MyNeighbors
                .Select(nn => new GraphDto.LinkDto
            {
                NodeSourceId = n.NodeId,
                NodeTargetId = nn.Neighbor.NodeId,
                Date = nn.UpdatedAt,
                Snr = nn.Snr
            }))
            .GroupBy(a => new
            {
                a.NodeSourceId,
                a.NodeTargetId
            }, (_, links) => links.OrderByDescending(a => a.Date).First())
            .ToListAsync();
        
        var nodeIds = links.Select(l => l.NodeSourceId).Concat(links.Select(l => l.NodeTargetId)).Distinct().ToList();
        
        return new GraphDto
        {
            Nodes = await query
                .Where(n => nodeIds.Contains(n.NodeId))
                .Select(n => new GraphDto.NodeDto
                {
                    NodeId = n.NodeId,
                    LongName = n.LongName,
                    ShortName = n.ShortName,
                    UpdatedAt = n.LastSeen.HasValue ? new DateTimeOffset(n.LastSeen.Value).ToUnixTimeSeconds() : null,
                    NeighboursUpdatedAt = n.MyNeighbors.Count > 0 ? new DateTimeOffset(n.MyNeighbors.First().UpdatedAt).ToUnixTimeSeconds() : 0,
                    Role = n.Role,
                    HardwareModel = n.HardwareModel,
                    // BatteryLevel = n.Telemetries.Count > 0 ? n.Telemetries.First().BatteryLevel : null,
                    // Voltage = n.Telemetries.Count > 0 ? n.Telemetries.First().Voltage : null,
                    // AirUtilTx = n.Telemetries.Count > 0 ? n.Telemetries.First().AirUtilTx : null,
                    // ChannelUtilization = n.Telemetries.Count > 0 ? n.Telemetries.First().ChannelUtilization : null,
                })
                .ToListAsync(),
            Links = links
        };
    }
}