using Meshtastic.Protobufs;

namespace Common.Context.Entities;

public class Webhook : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public required string Name { get; set; }
    public required string Url { set; get; }
    public string? UrlToEditMessage { set; get; }

    public bool Enabled { get; set; } = true;
    
    public bool AllowDuplication { get; set; }
    public bool AllowByHimSelf { get; set; }
    public bool OnlyWhenDifferentMqttServer { get; set; }
    
    public PortNum? PortNum { get; set; }
    public uint? From { get; set; }
    public uint? To { get; set; }
    public uint? Gateway { get; set; }
    public uint? FromOrTo { get; set; }
    
    public long? MqttServerId { get; set; }
    public virtual MqttServer? MqttServer { get; set; }
    
    public string? Channel { get; set; }
 
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? DistanceAroundPositionKm { get; set; }
}