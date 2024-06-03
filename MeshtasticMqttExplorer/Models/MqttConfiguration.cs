namespace MeshtasticMqttExplorer.Models;

public class MqttConfiguration
{
    public required string Name { get; set; }
    public required string Host { get; set; }
    public int Port { get; set; } = 1883;
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required List<string> Topics { get; set; }
    public bool Enabled { get; set; } = true;
}