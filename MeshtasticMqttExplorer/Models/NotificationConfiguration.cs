using Meshtastic.Protobufs;

namespace MeshtasticMqttExplorer.Models;

public class NotificationConfiguration
{
    public required string Name { get; set; }
    public required string Url { set; get; }

    public bool Enabled { get; set; } = true;
    
    public bool AllowDuplication { get; set; } = false;
    
    public PortNum? PortNum { get; set; }
    public uint? From { get; set; }
    public uint? To { get; set; }
    public uint? Gateway { get; set; }
    public uint? FromOrTo { get; set; }
    public string? Channel { get; set; }
}