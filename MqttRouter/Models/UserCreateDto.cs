using System.ComponentModel.DataAnnotations;

namespace MqttRouter.Models;

public class UserCreateDto
{
    [MinLength(1)]
    public required string Username { get; set; }
    
    [MinLength(1)]
    public required string Email { get; set; }
    
    [MinLength(1)]
    public required string Password { get; set; }
    
    public string? ExternalId { get; set; }
}