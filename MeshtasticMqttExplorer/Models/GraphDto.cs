using System.Text.Json.Serialization;
using Meshtastic.Protobufs;

namespace MeshtasticMqttExplorer.Models;

public class GraphDto
{
    public int NbNodes => Nodes.Count;
    public int NbLinks => Links.Count;
    
    [JsonPropertyName("nodes")]
    public IList<NodeDto> Nodes { get; set; } = [];
    
    [JsonPropertyName("links")]
    public IList<LinkDto> Links { get; set; } = [];
    
    public class NodeDto
    {
        [JsonPropertyName("id")]
        public uint NodeId { get; set; }
        
        [JsonPropertyName("short_name")]
        public string? ShortName { get; set; }
        
        [JsonPropertyName("long_name")]
        public string? LongName { get; set; }
        
        [JsonPropertyName("updated_at")]
        public long? UpdatedAt { get; set; }
        
        [JsonPropertyName("neighbours_updated_at")]
        public long? NeighboursUpdatedAt { get; set; }
        
        [JsonPropertyName("role")]
        public Config.Types.DeviceConfig.Types.Role? Role { get; set; }
        
        // [JsonPropertyName("hardware_model")]
        // public HardwareModel? HardwareModel { get; set; }
        
        // [JsonPropertyName("battery_level")]
        // public double? BatteryLevel { get; set; }
        //
        // [JsonPropertyName("voltage")]
        // public double? Voltage { get; set; }
        //
        // [JsonPropertyName("air_util_tx")]
        // public double? AirUtilTx { get; set; }
        //
        // [JsonPropertyName("channel_utilization")]
        // public double? ChannelUtilization { get; set; }
        //
        // [JsonPropertyName("temperature")]
        // public double? Temperature { get; set; }
        //
        // [JsonPropertyName("relative_humidity")]
        // public double? Humidity { get; set; }
        //
        // [JsonPropertyName("barometric_pressure")]
        // public double? Pressure { get; set; }
    }
    
    public class LinkDto
    {
        [JsonPropertyName("source")]
        public uint NodeSourceId { get; set; }
        
        [JsonPropertyName("target")]
        public uint NodeTargetId { get; set; }
        
        [JsonPropertyName("snr")]
        public double Snr { get; set; }
        
        [JsonIgnore]
        public DateTime Date { get; set; }
    }
}