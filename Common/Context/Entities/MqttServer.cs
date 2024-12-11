namespace Common.Context.Entities;

public class MqttServer : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public required string Name { get; set; }
    
    public required string Host { get; set; }
    
    public int Port { get; set; } = 1883;
    
    public string? Username { get; set; }
    
    public string? Password { get; set; }
    
    public List<string> Topics { get; set; } = [];
    
    public bool Enabled { get; set; } = true;
    
    public virtual ICollection<Packet> Packets { get; set; } = [];
    public virtual ICollection<Node> Nodes { get; set; } = [];
}