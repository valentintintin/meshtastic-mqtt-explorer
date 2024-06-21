using System.ComponentModel.DataAnnotations;
using Meshtastic.Protobufs;

namespace MeshtasticMqttExplorer.Context.Entities;

public class Node : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public required uint NodeId { get; set; }
    public string? NodeIdString { get; set; }
    public string? LongName { get; set; }
    public string? ShortName { get; set; }
    public string? AllNames { get; set; }

    public Config.Types.DeviceConfig.Types.Role? Role { get; set; }
    public HardwareModel? HardwareModel { get; set; }
    public string? FirmwareVersion { get; set; }
    public Config.Types.LoRaConfig.Types.RegionCode? RegionCode { get; set; }
    public Config.Types.LoRaConfig.Types.ModemPreset? ModemPreset { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? Altitude { get; set; }

    public int? NumOnlineLocalNodes { get; set; }

    public DateTime? LastSeen { get; set; }
    
    public bool? HasDefaultChannel { get; set; }
    
    public bool? IsMqttGateway { get; set; }

    public virtual ICollection<Packet> PacketsFrom { get; set; } = [];
    public virtual ICollection<Packet> PacketsTo { get; set; } = [];
    public virtual ICollection<Position> Positions { get; set; } = [];
    public virtual ICollection<Waypoint> Waypoints { get; set; } = [];
    public virtual ICollection<Telemetry> Telemetries { get; set; } = [];
    public virtual ICollection<NeighborInfo> MyNeighbors { get; set; } = [];
    public virtual ICollection<NeighborInfo> NeighborsFor { get; set; } = [];
    public virtual ICollection<TextMessage> TextMessagesFrom { get; set; } = [];
    public virtual ICollection<TextMessage> TextMessagesTo { get; set; } = [];

    public string NodeIdAsString() => $"!{NodeId.ToString("X").ToLower()}";
    public string Name() => LongName != null && ShortName != null ? $"{LongName} | {ShortName}" : NodeIdAsString();
    public string FullName() => LongName != null && ShortName != null ? $"{Name()} - {NodeIdAsString()}" : NodeIdAsString();
    public string OneName(bool onlyShort = false) => (onlyShort ? null : LongName) ?? ShortName ?? NodeIdAsString();

    public override string ToString()
    {
        return $"#{Id} -> {FullName()}";
    }
}