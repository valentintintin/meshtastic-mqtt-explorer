namespace Common.Context.Entities;

public class SignalHistory : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public long NodeReceiverId { get; set; }
    public virtual Node NodeReceiver { get; set; } = null!;

    public long NodeHeardId { get; set; }
    public virtual Node NodeHeard { get; set; } = null!;
    
    public float Snr { get; set; }
    public float? Rssi { get; set; }
    
    public long? PacketId { get; set; }
    public virtual Packet? Packet { get; set; }
}