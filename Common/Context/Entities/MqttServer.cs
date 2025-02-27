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

    public RelayType? IsARelayType { get; set; }

    public uint? RelayPositionPrecision { get; set; } = 32;

    public ServerType Type { get; set; } = ServerType.Mqtt;

    public bool IsHighLoad { get; set; }

    public virtual ICollection<Packet> Packets { get; set; } = [];
    public virtual ICollection<Node> Nodes { get; set; } = [];
    
    public enum ServerType
    {
        Mqtt,
        NodeHttp
    }
    
    public enum RelayType
    {
        MapReport,
        NodeInfoAndPosition,
        UseFull,
        All
    }
}