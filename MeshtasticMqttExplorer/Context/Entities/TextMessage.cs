namespace MeshtasticMqttExplorer.Context.Entities;

public class TextMessage : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public long FromId { get; set; }
    public virtual Node From { get; set; } = null!;
    
    public long? ToId { get; set; }
    public virtual Node? To { get; set; }
    
    public long PacketId { get; set; }
    public virtual Packet Packet { get; set; } = null!;
    
    public long ChannelId { get; set; }
    public virtual Channel Channel { get; set; } = null!;
    
    public required string Message { get; set; }
}