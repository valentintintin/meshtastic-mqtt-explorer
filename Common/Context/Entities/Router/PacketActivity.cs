namespace Common.Context.Entities.Router;

public class PacketActivity : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public long PacketId { get; set; }
    public virtual Packet Packet { get; set; } = null!;
    
    public bool Accepted { get; set; }

    public List<string> ReceiverIds { get; set; } = [];
    
    public string? Comment { get; set; }
}