namespace Common.Context.Entities;

public class Traceroute : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public long NodeFromId { get; set; }
    public virtual Node From { get; set; } = null!;
    
    public long NodeToId { get; set; }
    public virtual Node To { get; set; } = null!;
    
    public long NodeId { get; set; }
    public virtual Node Node { get; set; } = null!;
    
    public required int Hop { get; set; }
    
    public double? Snr { get; set; }
    
    public long? PacketId { get; set; }
    public virtual Packet? Packet { get; set; }
}