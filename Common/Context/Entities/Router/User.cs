using Microsoft.AspNetCore.Identity;

namespace Common.Context.Entities.Router;

public class User : IdentityUser<long>, IEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public DateTime? ConnectedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    
    public string? ExternalId { get; set; }
    
    public string? Ip { get; set; }
    
    public string? TempBP { get; set; }

    public virtual ICollection<NodeConfiguration>? NodeConfigurations { get; set; } = [];
}