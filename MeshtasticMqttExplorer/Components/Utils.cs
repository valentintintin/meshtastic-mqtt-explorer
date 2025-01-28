using AntDesign;
using AntDesign.Charts;
using Common;
using Common.Context.Entities;
using Common.Extensions;
using Meshtastic.Protobufs;
using MeshtasticMqttExplorer.Models;
using Color = System.Drawing.Color;
using Waypoint = Common.Context.Entities.Waypoint;

namespace MeshtasticMqttExplorer.Components;

public static class Utils
{
    public static readonly string Red = Color.DarkRed.Name.ToLower();
    public static readonly string GreenLight = Color.LimeGreen.Name.ToLower();
    public static readonly string Green = Color.DarkGreen.Name.ToLower();
    public static readonly string Gold = Color.Goldenrod.Name.ToLower();
    public static readonly string Blue = Color.DodgerBlue.Name.ToLower();
    public static readonly string Gray = Color.DarkGray.Name.ToLower();
    public static readonly string Orange = Color.OrangeRed.Name.ToLower();

    public static readonly TableFilter<Config.Types.LoRaConfig.Types.RegionCode?>[] RegionCodeFilters = ((Config.Types.LoRaConfig.Types.RegionCode[])Enum.GetValues(typeof(Config.Types.LoRaConfig.Types.RegionCode)))
        .Select(p => new TableFilter<Config.Types.LoRaConfig.Types.RegionCode?>
        {
            Text = p.ToString(),
            Value = p
        })
        .OrderBy(p =>
        {
            return p.Value switch
            {
                Config.Types.LoRaConfig.Types.RegionCode.Eu433 => "1",
                Config.Types.LoRaConfig.Types.RegionCode.Eu868 => "0",
                _ => p.Text
            };
        })
        .ToArray();
    public static readonly TableFilter<Config.Types.LoRaConfig.Types.ModemPreset?>[] ModemPresetFilters = ((Config.Types.LoRaConfig.Types.ModemPreset[])Enum.GetValues(typeof(Config.Types.LoRaConfig.Types.ModemPreset)))
        .Select(p => new TableFilter<Config.Types.LoRaConfig.Types.ModemPreset?>
        {
            Text = p.ToString(),
            Value = p
        })
        .OrderBy(p =>
        {
            return p.Value switch
            {
                Config.Types.LoRaConfig.Types.ModemPreset.LongFast => "1",
                Config.Types.LoRaConfig.Types.ModemPreset.LongModerate => "0",
                _ => p.Text
            };
        })
        .ToArray();
    public static readonly TableFilter<HardwareModel?>[] HardwareModelFilters = ((HardwareModel[])Enum.GetValues(typeof(HardwareModel)))
        .Select(p => new TableFilter<HardwareModel?>
        {
            Text = p.ToString(),
            Value = p
        })
        .OrderBy(p => p.Text)
        .ToArray();
    public static readonly TableFilter<Config.Types.DeviceConfig.Types.Role?>[] RoleFilters = ((Config.Types.DeviceConfig.Types.Role[])Enum.GetValues(typeof(Config.Types.DeviceConfig.Types.Role)))
        .Select(p => new TableFilter<Config.Types.DeviceConfig.Types.Role?>
        {
            Text = p.ToString(),
            Value = p
        })
        .OrderBy(p => p.Text)
        .ToArray();
    public static readonly TableFilter<PortNum?>[] PortNumFilters = ((PortNum[])[PortNum.NeighborinfoApp, PortNum.TelemetryApp, PortNum.NodeinfoApp, PortNum.RangeTestApp, PortNum.SerialApp, PortNum.AdminApp, PortNum.PositionApp, PortNum.MapReportApp, PortNum.WaypointApp, PortNum.RoutingApp, PortNum.TracerouteApp, PortNum.TextMessageApp]).OrderBy(num => num)
        .Select(p => new TableFilter<PortNum?>
        {
            Text = p.ToString(),
            Value = p
        })
        .Concat([new TableFilter<PortNum?> { Text = "Chiffrée", Value = null }])
        .OrderBy(p => p.Text)
        .ToArray();
    public static readonly List<TableFilter<long?>> MqttServerFilters = [];

    public static readonly TableLocale TableLocale = new()
    {
        FilterTitle = "Menu de filtre",
        FilterConfirm = "OK",
        FilterReset = "Réinitialiser",
        FilterEmptyText = "",
        SelectAll = "Sélectionner la page actuelle",
        SelectInvert = "Inverser la page actuelle",
        SelectionAll = "Sélectionner toutes les données",
        SortTitle = "Trier",
        Expand = "Développer la ligne",
        Collapse = "Réduire la ligne",
        TriggerDesc = "Cliquez pour trier par ordre décroissant",
        TriggerAsc = "Cliquez pour trier par ordre croissant",
        CancelSort = "Cliquez pour annuler le tri",
        FilterOptions = new FilterOptionsLocale
        {
            True = "Vrai",
            False = "Faux",
            And = "Et",
            Or = "Ou",
            Equals = "Égal",
            NotEquals = "Pas égal",
            Contains = "Contient",
            NotContains = "Ne contient pas",
            StartsWith = "Commence par",
            EndsWith = "Finit par",
            GreaterThan = "Plus grand que",
            LessThan = "Moins que",
            GreaterThanOrEquals = "Plus grand ou égal",
            LessThanOrEquals = "Moins ou égal",
            IsNull = "Est nul",
            IsNotNull = "N'est pas nul",
            TheSameDateWith = "La même date que"
        }
    };

    public static LineConfig GetLineConfig(string yAxisText, double? min = null, double? max = null, bool legend = false)
    {
        return new LineConfig
        {
            Padding = "auto",
            AutoFit = true,
            XField = nameof(DateChartData<object>.date),
            YField = nameof(DateChartData<object>.value),
            YAxis = new ValueAxis
            {
                Label = new BaseAxisLabel
                {
                    Visible = true
                },
                Title = new BaseAxisTitle
                {
                    Text = yAxisText,
                    Visible = true
                },
                Min = min,
                Max = max,
                Visible = true,
            },
            Legend = new Legend
            {
                Visible = legend
            },
            SeriesField = nameof(DateChartData<object>.type),
            Color = new [] { Blue, Red, Orange, GreenLight, Gray }
        };
    }

    public static string GetNeighborLinePopupHtml(Node node, Common.Context.Entities.NeighborInfo neighborInfo, Common.Context.Entities.NeighborInfo? reverseNeighborInfo = null)
    {
        if (reverseNeighborInfo != null)
        {
            return $"<p>" +
                   $"<a href=\"/node/{node.Id}\" target=\"_blank\" rel=\"nofollow\">" +
                   $"<b>{node.AllNames}</b>" +
                   $"</a>" +
                   $"</p>" +
                   $"<p>" +
                   $"SNR : <b>{neighborInfo.Snr}</b> | Date : <b>{neighborInfo.UpdatedAt.ToFrench()}</b>" +
                   $"</p>" +
                   $"<p>" +
                   $"<a href=\"/packet/{neighborInfo.PacketId}\" target=\"_blank\" rel=\"nofollow\">" +
                   $"<i>Voir la trame ({neighborInfo.DataSource})</i>" +
                   $"</a>" +
                   $"</p>" +
                   $"<p>" +
                   $"<a href=\"/node/{reverseNeighborInfo.NodeReceiver.Id}\" target=\"_blank\" rel=\"nofollow\">" +
                   $"<b>{reverseNeighborInfo.NodeReceiver.AllNames}</b>" +
                   $"</a>" +
                   $"</p>" +
                   $"<p>" +
                   $"SNR : <b>{reverseNeighborInfo.Snr}</b> | Date : <b>{reverseNeighborInfo.UpdatedAt.ToFrench()}</b>" +
                   $"</p>" +
                   $"<p>" +
                   $"<a href=\"/packet/{reverseNeighborInfo.PacketId}\" target=\"_blank\" rel=\"nofollow\">" +
                   $"<i>Voir la trame ({reverseNeighborInfo.DataSource})</i>" +
                   $"</a>" +
                   $"</p>" + 
                   $"<p>" +
                   $"Distance : <b>{(neighborInfo.Distance.HasValue ? Math.Round(neighborInfo.Distance.Value, 2) : "-")}</b> Km" +
                   $"</p>" + 
                   $"<p>" +
                   $"<a href=\"/signal-plotter/{node.Id}/{reverseNeighborInfo.NodeReceiverId}\" target=\"_blank\" rel=\"nofollow\">Comparer les signaux</a>" +
                   $"</p>";
        }
        
        var neighbor = neighborInfo.NodeHeard == node ? neighborInfo.NodeReceiver : neighborInfo.NodeHeard;

        return $"<p>" +
               $"<a href=\"/node/{node.Id}\" target=\"_blank\" rel=\"nofollow\">" +
               $"<b>{node.AllNames}</b>" +
               $"</a>" +
               $"</p>" +
               $"<p>" +
               $"Entend ({neighborInfo.DataSource}) : " +
               $"<a href=\"/node/{neighbor.Id}\" target=\"_blank\">" +
               $"<b>{neighbor.AllNames}</b>" +
               $"</a>" +
               $"</p>" +
               $"<p>" +
               $"Distance : <b>{(neighborInfo.Distance.HasValue ? Math.Round(neighborInfo.Distance.Value, 2) : "-")}</b> Km" +
               $"</p>" +
               $"<p>" +
               $"SNR : <b>{neighborInfo.Snr}</b> | Date : <b>{neighborInfo.UpdatedAt.ToFrench()}</b>" +
               $"</p>" +
               $"<p>" +
               $"<a href=\"/packet/{neighborInfo.PacketId}\" target=\"_blank\" rel=\"nofollow\">" +
               $"<i>Voir la trame</i>" +
               $"</a>" +
               $"</p>";
    }

    public static string GetWaypointLinePopupHtml(Node node, Waypoint waypoint)
    {
        return $"<p>" +
               $"<a href=\"/node/{node.Id}\" target=\"_blank\" rel=\"nofollow\">" +
               $"<b>{node.AllNames}</b>" +
               $"</a>" +
               $"</p>" +
               $"<p>" +
               $"Point d'intérêt : <b>{waypoint.Name}</b>" +
               $"</p>" +
               $"<p>" +
               $"Ajouté le <b>{waypoint.CreatedAt.ToFrench()}</b>" +
               $"<br />{waypoint.Description}" +
               $"</p>" +
               $"<p>" +
               $"Distance : {MeshtasticUtils.CalculateDistance(node.Latitude!.Value, node.Longitude!.Value, waypoint.Latitude, waypoint.Longitude)} Km" +
               $"</p>";
    }
}