using Microsoft.AspNetCore.Identity;

namespace Common.Context.Entities;

public class User : IdentityUser<long>, IEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public string? Ip { get; set; }

    public bool Forbidden { get; set; }

    public virtual ICollection<NodeConfiguration>? NodeConfigurations { get; set; } = [];
}