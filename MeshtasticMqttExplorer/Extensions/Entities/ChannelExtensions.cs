using MeshtasticMqttExplorer.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeshtasticMqttExplorer.Extensions.Entities;

public static class ChannelExtensions
{
    public static Task<Channel?> FindByNameAsync(this IQueryable<Channel> channels, string name)
    {
        return string.IsNullOrWhiteSpace(name) ? null : channels.FirstOrDefaultAsync(n => n.Name == name);
    }
}