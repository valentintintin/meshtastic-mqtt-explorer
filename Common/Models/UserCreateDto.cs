using System.ComponentModel.DataAnnotations;

namespace Common.Models;

public class UserCreateDto
{
    [MinLength(1)]
    public string? Username { get; set; }
    
    [MinLength(1)]
    public string? Email { get; set; }
    
    [MinLength(1)]
    public string? Password { get; set; }
    
    public string? ExternalId { get; set; }
        
    public bool CanReceiveEverything { get; set; }
}