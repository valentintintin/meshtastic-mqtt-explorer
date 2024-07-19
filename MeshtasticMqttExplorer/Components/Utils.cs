using AntDesign;
using AntDesign.Charts;
using Meshtastic.Protobufs;
using MeshtasticMqttExplorer.Context.Entities;
using MeshtasticMqttExplorer.Models;
using Color = System.Drawing.Color;

namespace MeshtasticMqttExplorer.Components;

public static class Utils
{
    public static readonly string Red = Color.DarkRed.Name.ToLower();
    public static readonly string Green = Color.ForestGreen.Name.ToLower();
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
    public static readonly TableFilter<PortNum?>[] PortNumFilters = ((PortNum[])Enum.GetValues(typeof(PortNum)))
        .Select(p => new TableFilter<PortNum?>
        {
            Text = p.ToString(),
            Value = p
        })
        .Concat([new TableFilter<PortNum?> { Text = "Chiffrée", Value = null }])
        .OrderBy(p => p.Text)
        .ToArray();
    public static readonly TableFilter<string?>[] MqttServerFilters =
    [
        new TableFilter<string?> { Text = "Meshtastic", Value = "Meshtastic" },
        new TableFilter<string?> { Text = "Gaulix", Value = "Gaulix" },
    ];

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
    
    public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371; // Rayon de la Terre en kilomètres
        var lat1Rad = DegreesToRadians(lat1);
        var lon1Rad = DegreesToRadians(lon1);
        var lat2Rad = DegreesToRadians(lat2);
        var lon2Rad = DegreesToRadians(lon2);

        var dlat = lat2Rad - lat1Rad;
        var dlon = lon2Rad - lon1Rad;

        var a = Math.Sin(dlat / 2) * Math.Sin(dlat / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(dlon / 2) * Math.Sin(dlon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var distance = r * c;

        return Math.Round(distance, 2);
    }

    public static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    public static LineConfig GetLineConfig(string yAxisText, double? min = null, double? max = null)
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
                Visible = false
            },
            SeriesField = nameof(DateChartData<object>.type),
            Color = new [] { Blue, Red }
        };
    }
}