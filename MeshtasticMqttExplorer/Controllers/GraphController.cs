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

        var links = await context.NeighborInfos
            .Include(a => a.Node)
            .ThenInclude(a => a.Telemetries.OrderByDescending(t => t.UpdatedAt).Take(10))
            .Include(a => a.Neighbor)
            .Where(a => a.DataSource != NeighborInfo.Source.Unknown)
            .Where(n => !MeshtasticService.NodesIgnored.Contains(n.Node.NodeId) && !MeshtasticService.NodesIgnored.Contains(n.Neighbor.NodeId))
            .Where(a => a.Distance > 0 && a.Distance < Utils.DefaultDistanceAllowed)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync()
            ;

        return new GraphDto
        {
            Nodes = links
                .GroupBy(n => n.Node, (key, values) => key)
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
                        .GroupBy(n => n.Neighbor, (key, values) => key)
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
                NodeSourceId = l.Node.NodeId,
                NodeTargetId = l.Neighbor.NodeId,
                Date = l.UpdatedAt,
                Snr = l.Snr
            }).ToList()
        };
    }
}