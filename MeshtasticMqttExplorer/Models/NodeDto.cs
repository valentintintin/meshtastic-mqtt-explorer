using Meshtastic.Protobufs;

namespace MeshtasticMqttExplorer.Models;

public class NodeDto
{
    public required long Id { get; set; }
    public required DateTime? LastSeen { get; set; }

    public required uint NodeId { get; set; }
    public required string? NodeIdString { get; set; }
    public required string? LongName { get; set; }
    public required string? ShortName { get; set; }
    public required string? AllNames { get; set; }

    public required Config.Types.DeviceConfig.Types.Role? Role { get; set; }
    
    public required string Link { get; set; }
}