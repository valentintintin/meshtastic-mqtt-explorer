namespace Common.Context.Entities;

public class Channel : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public required string Name { get; set; }

    public ICollection<TextMessage> TextMessages { get; set; } = [];
    public ICollection<Packet> Packets { get; set; } = [];

    public override string ToString()
    {
        return $"#{Id} -> {Name}";
    }
}