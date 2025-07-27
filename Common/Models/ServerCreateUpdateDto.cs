using System.ComponentModel.DataAnnotations;
using Common.Context.Entities;

namespace Common.Models;

public class ServerCreateUpdateDto
{
    [MinLength(1)]
    public string Name { get; set; } = null!;

    public MqttServer.ServerType Type { get; set; } = MqttServer.ServerType.MqttClient;
    
    [MinLength(1)]
    public string Host { get; set; } = null!;

    public int Port { get; set; } = 1883;
    
    public string? Username { get; set; }
    
    public string? Password { get; set; }

    public string? Topics { get; set; } = "msh/#";
    
    public bool Enabled { get; set; } = true;
    
    public MqttServer.RelayType? IsARelayType { get; set; }
    
    public uint? RelayPositionPrecision { get; set; } = 32;

    public bool UseWorker { get; set; }

    public bool IsNotHighLoad { get; set; } = true;

    public bool IsHighLoad
    {
        get => !IsNotHighLoad;
        set => IsNotHighLoad = !value;
    }
    
    public bool MqttPostJson { get; set; } = true;
    
    public bool ShouldBeRelayed { get; set; } = true;
}