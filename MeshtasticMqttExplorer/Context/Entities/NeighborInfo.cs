namespace MeshtasticMqttExplorer.Context.Entities;

public class NeighborInfo : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public long NodeId { get; set; }
    public virtual Node Node { get; set; } = null!;
    
    public long? PacketId { get; set; }
    public virtual Packet? Packet { get; set; }
    
    public long NeighborId { get; set; }
    public virtual Node Neighbor { get; set; } = null!;
    
    public double Snr { get; set; }
}