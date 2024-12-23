using System.ComponentModel.DataAnnotations;

namespace MqttRouter.Models;

public class UserUpdateDto
{
    [MinLength(1)]
    public required string Username { get; set; }
    
    [MinLength(1)]
    public required string Email { get; set; }
}