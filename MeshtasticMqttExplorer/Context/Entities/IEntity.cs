namespace MeshtasticMqttExplorer.Context.Entities;

public interface IEntity
{
    public long Id { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}