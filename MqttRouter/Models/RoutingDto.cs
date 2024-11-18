using System.ComponentModel.DataAnnotations;

namespace MqttRouter.Models;

public class RoutingDto
{
    [Required]
    [MinLength(1)]
    public required string Ip { get; set; }
    
    [Required]
    [MinLength(1)]
    public required string Topic { get; set; }
    
    [Required]
    [MinLength(1)]
    public required string Payload { get; set; }
}