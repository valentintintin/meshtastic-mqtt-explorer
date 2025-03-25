using Meshtastic.Protobufs;

namespace MeshtasticMqttExplorer.Models;

public class PacketNodeType
{
    public long? MqttServerId { get; set; }
    public string? MqttServerName { get; set; }
    public Config.Types.LoRaConfig.Types.RegionCode? RegionCode { get; set; }
    public required long NodeId { get; set; }
    public required string NodeName { get; set; }
    public PortNum? PortNum { get; set; }
    public required int Nb { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}