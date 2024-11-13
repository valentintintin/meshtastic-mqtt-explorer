using System.Text.Json.Serialization;

namespace Common.Models;

public class MqttConfiguration
{
    public required string Name { get; set; }
    
    public required string Host { get; set; }
    
    [JsonIgnore]
    public int Port { get; set; } = 1883;
    
    [JsonIgnore]
    public string? Username { get; set; }
    
    [JsonIgnore]
    public string? Password { get; set; }
    public List<string> Topics { get; set; } = [];
    public bool Enabled { get; set; } = true;
    
    public uint NbPacket { get; set; }
    public DateTime? LastPacketDate { get; set; }
}