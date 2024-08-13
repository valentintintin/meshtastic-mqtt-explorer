namespace MeshtasticMqttExplorer.Context.Entities;

public class NeighborInfo : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public long NodeId { get; set; }
    public virtual Node Node { get; set; } = null!;
    
    public long? NodePositionId { get; set; }
    public virtual Position? NodePosition { get; set; }
    
    public long? PacketId { get; set; }
    public virtual Packet? Packet { get; set; }
    
    public long NeighborId { get; set; }
    public virtual Node Neighbor { get; set; } = null!;
    
    public long? NeighborPositionId { get; set; }
    public virtual Position? NeighborPosition { get; set; }
    
    public double Snr { get; set; }
    
    public double? Distance { get; set; }

    public Source DataSource { get; set; } = Source.Neighbor;
    
    public enum Source
    {
        Neighbor,
        Gateway,
        Traceroute
    }
}