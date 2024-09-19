using Meshtastic.Protobufs;
using MeshtasticMqttExplorer.Components;
using MeshtasticMqttExplorer.Context;
using MeshtasticMqttExplorer.Models;
using MeshtasticMqttExplorer.Services;
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
            .Where(n => !MeshtasticService.NodesIgnored.Contains(n.NodeId))
            .Include(n => n.MyNeighbors
                .OrderByDescending(t => t.UpdatedAt)
                .Where(t => t.UpdatedAt >= minDate)
                .Where(a => a.Distance > 0 && a.Distance < Utils.DefaultDistanceAllowed)
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
                .Include(a => a.Telemetries.OrderByDescending(aa => aa.UpdatedAt).Take(10))
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
                    BatteryLevel = n.Telemetries.Any(a => a.BatteryLevel != null) ? n.Telemetries.First(a => a.BatteryLevel != null).BatteryLevel : null,
                    Voltage = n.Telemetries.Any(a => a.Voltage != null) ? n.Telemetries.First(a => a.Voltage != null).Voltage : null,
                    AirUtilTx = n.Telemetries.Any(a => a.AirUtilTx != null) ? n.Telemetries.First(a => a.AirUtilTx != null).AirUtilTx : null,
                    ChannelUtilization = n.Telemetries.Any(a => a.ChannelUtilization != null) ? n.Telemetries.First(a => a.BatteryLevel != null).ChannelUtilization : null,
                    Temperature = n.Telemetries.Any(a => a.Temperature != null) ? n.Telemetries.First(a => a.Temperature != null).Temperature : null,
                    Humidity = n.Telemetries.Any(a => a.RelativeHumidity != null) ? n.Telemetries.First(a => a.RelativeHumidity != null).RelativeHumidity : null,
                    Pressure = n.Telemetries.Any(a => a.BarometricPressure != null) ? n.Telemetries.First(a => a.BarometricPressure != null).BarometricPressure : null,
                })
                .OrderBy(n => n.LongName)
                .ToListAsync(),
            Links = links
        };
    }
}