using Common;
using Common.Context;
using Common.Services;
using MeshtasticMqttExplorer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DateTimeOffset = System.DateTimeOffset;
using NeighborInfo = Common.Context.Entities.NeighborInfo;

namespace MeshtasticMqttExplorer.Controllers;

[ApiController]
[Route("api/graph")]
public class GraphController(ILogger<GraphController> logger, IDbContextFactory<DataContext> contextFactory) : AController(logger)
{
    [HttpGet]
    public async Task<GraphDto> GetGraph()
    {
        Logger.LogInformation("Get nodes graph");
        
        var context = await contextFactory.CreateDbContextAsync();

        var aDay = DateTime.UtcNow.AddHours(-24);
        
        var links = await context.NeighborInfos
            .Include(a => a.NodeReceiver)
            .ThenInclude(a => a.Telemetries.OrderByDescending(t => t.UpdatedAt).Take(10))
            .Include(a => a.NodeHeard)
            .Where(a => a.DataSource != NeighborInfo.Source.Unknown && a.DataSource != NeighborInfo.Source.NextHop)
            .Where(n => !MeshtasticService.NodesIgnored.Contains(n.NodeReceiver.NodeId) && !MeshtasticService.NodesIgnored.Contains(n.NodeHeard.NodeId))
            .Where(a => a.Distance > 0 && a.Distance < MeshtasticUtils.DefaultDistanceAllowed)
            .Where(a => a.UpdatedAt >= aDay)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync();

        return new GraphDto
        {
            Nodes = links
                .GroupBy(n => n.NodeReceiver, (key, values) => key)
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
                .Union(
                    links
                        .GroupBy(n => n.NodeHeard, (key, values) => key)
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
                )
                .DistinctBy(n => n.NodeId)
                .OrderBy(n => n.LongName)
                .ToList(),
            Links = links.Select(l => new GraphDto.LinkDto
            {
                NeighborId = l.Id,
                NodeSourceId = l.NodeReceiver.NodeId,
                NodeTargetId = l.NodeHeard.NodeId,
                Date = l.UpdatedAt,
                Snr = l.Snr
            }).ToList()
        };
    }
}