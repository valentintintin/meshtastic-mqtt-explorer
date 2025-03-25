namespace Common.Context.Entities;

public class WebhookHistory : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public long PacketId { get; set; }
    public virtual Packet Packet { get; set; } = null!;
    
    public long WebhookId { get; set; }
    public virtual Webhook Webhook { get; set; } = null!;
    
    public string? MessageId { get; set; }
}