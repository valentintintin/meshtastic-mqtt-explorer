namespace Common.Context.Entities;

public class NeighborInfo : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public long NodeReceiverId { get; set; }
    public virtual Node NodeReceiver { get; set; } = null!;
    
    public long? NodeReceiverPositionId { get; set; }
    public virtual Position? NodeReceiverPosition { get; set; }
    
    public long? PacketId { get; set; }
    public virtual Packet? Packet { get; set; }
    
    public long NodeHeardId { get; set; }
    public virtual Node NodeHeard { get; set; } = null!;
    
    public long? NodeHeardPositionId { get; set; }
    public virtual Position? NodeHeardPosition { get; set; }
    
    public float Snr { get; set; }
    public float? Rssi { get; set; }
    
    public double? Distance { get; set; }

    public Source DataSource { get; set; } = Source.Neighbor;
    
    public enum Source
    {
        Neighbor,
        Gateway,
        Traceroute,
        Unknown,
        Relay,
        NextHop,
    }
}