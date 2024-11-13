namespace Common.Context.Entities;

public class Position : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public long NodeId { get; set; }
    public virtual Node Node { get; set; } = null!;
    
    public long? PacketId { get; set; }
    public virtual Packet? Packet { get; set; }
    
    public required double Latitude { get; set; }
    public required double Longitude { get; set; }
    public int? Altitude { get; set; }
}