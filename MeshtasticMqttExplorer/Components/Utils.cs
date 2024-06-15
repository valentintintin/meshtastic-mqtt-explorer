using AntDesign;
using Meshtastic.Protobufs;
using Color = System.Drawing.Color;

namespace MeshtasticMqttExplorer.Components;

public static class Utils
{
    public static readonly string Red = Color.DarkRed.Name.ToLower();
    public static readonly string Green = Color.LimeGreen.Name.ToLower();
    public static readonly string Blue = Color.DodgerBlue.Name.ToLower();
    public static readonly string Gray = Color.DarkGray.Name.ToLower();
    
    public static readonly TableFilter<Config.Types.LoRaConfig.Types.RegionCode?>[] RegionCodeFilters = ((Config.Types.LoRaConfig.Types.RegionCode[])Enum.GetValues(typeof(Config.Types.LoRaConfig.Types.RegionCode)))
        .Select(p => new TableFilter<Config.Types.LoRaConfig.Types.RegionCode?>
        {
            Text = p.ToString(),
            Value = p
        })
        .OrderBy(p => p.Text)
        .ToArray();
    public static readonly TableFilter<Config.Types.LoRaConfig.Types.ModemPreset?>[] ModemPresetFilters = ((Config.Types.LoRaConfig.Types.ModemPreset[])Enum.GetValues(typeof(Config.Types.LoRaConfig.Types.ModemPreset)))
        .Select(p => new TableFilter<Config.Types.LoRaConfig.Types.ModemPreset?>
        {
            Text = p.ToString(),
            Value = p
        })
        .OrderBy(p => p.Text)
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
}