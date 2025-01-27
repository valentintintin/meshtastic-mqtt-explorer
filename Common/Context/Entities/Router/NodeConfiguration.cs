namespace Common.Context.Entities.Router;

public class NodeConfiguration : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public string? MqttId { get; set; }
    
    public DateTime? LastSeenOnMqtt { get; set; }

    public bool Forbidden { get; set; }
    
    public long NodeId { get; set; }
    public virtual Node Node { get; set; } = null!;
    
    public long? UserId { get; set; }
    public virtual User? User { get; set; }
    
    public string? Department { get; set; }
}