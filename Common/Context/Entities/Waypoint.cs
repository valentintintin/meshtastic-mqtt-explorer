namespace Common.Context.Entities;

public class Waypoint : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public long NodeId { get; set; }
    public virtual Node Node { get; set; } = null!;
    
    public long? PacketId { get; set; }
    public virtual Packet? Packet { get; set; }
    
    public required uint WaypointId { get; set; }
    
    public required string Name { get; set; }
    public string? Description { get; set; }
    public uint? Icon { get; set; }
    
    public required DateTime? ExpiresAt { get; set; }
    
    public required double Latitude { get; set; }
    public required double Longitude { get; set; }
}