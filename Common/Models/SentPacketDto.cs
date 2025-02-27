using System.ComponentModel.DataAnnotations;
using Common.Extensions;
using Common.Services;
using Meshtastic.Protobufs;

namespace Common.Models;

public class SentPacketDto
{
    [Required]
    public long MqttServerId { get; set; }

    [Required]
    [Length(2, 9)]
    public string NodeFromId { get; set; } = "!";
    
    [Required]
    [MinLength(1)]
    public string Channel { get; set; } = "LongFast";

    [Required]
    [Length(2, 9)]
    public string NodeToId { get; set; } = MeshtasticService.NodeBroadcast.ToHexString();
    
    [Range(0, 7)]
    public uint HopLimit { get; set; }

    public bool WantAck { get; set; }
    
    [Required]
    [MinLength(4)]
    public string RootTopic { get; set; } = "msh/EU_868/2/";

    [MinLength(4)]
    public string? Key { get; set; } // = "AQ==";
    
    public string Type { get; set; } = MessageType.Message.ToString();
    
    [Length(1, 200)]
    public string? Message { get; set; }
    
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Altitude { get; set; }
    
    [Length(4, 4)]
    public string? ShortName { get; set; }
    
    [Length(1, 37)]
    public string? Name { get; set; }
    
    [MaxLength(100)]
    public string? Description { get; set; }
    
    public uint? Id { get; set; }
    
    public DateTime Expires { get; set; } = DateTime.UtcNow.AddDays(1);
    
    public PortNum? PortNum { get; set; }
    
    [MinLength(1)]
    public string? RawBase64 { get; set; }

    public enum MessageType
    {
        Message,
        NodeInfo,
        Position,
        Waypoint,
        Raw
    }
}