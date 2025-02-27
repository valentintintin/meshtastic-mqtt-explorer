using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace Common.Extensions.Entities;

public static class ChannelExtensions
{
    public static async Task<Channel?> FindByNameAsync(this IQueryable<Channel> channels, string name)
    {
        return string.IsNullOrWhiteSpace(name) ? null : await channels.FirstOrDefaultAsync(n => n.Name == name);
    }
    
    public static Channel? FindByName(this IQueryable<Channel> channels, string name)
    {
        return string.IsNullOrWhiteSpace(name) ? null : channels.FirstOrDefault(n => n.Name == name);
    }
}