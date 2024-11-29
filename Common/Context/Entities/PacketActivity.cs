namespace Common.Context.Entities;

public class PacketActivity : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public long NodeConfigurationId { get; set; }
    public virtual NodeConfiguration NodeConfiguration { get; set; } = null!;
}