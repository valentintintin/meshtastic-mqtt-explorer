using MeshtasticMqttExplorer.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeshtasticMqttExplorer.Extensions.Entities;

public static class EntityExtensions
{
    public static T? FindById<T>(this IQueryable<T> entities, long? id) where T : IEntity
    {
        return id > 0 ? entities.SingleOrDefault(a => a.Id == id) : default;
    }
    
    public static async Task<T?> FindByIdAsync<T>(this IQueryable<T> entities, long? id) where T : IEntity
    {
        return id > 0 ? await entities.SingleOrDefaultAsync(a => a.Id == id) : default;
    }
    
    public static IQueryable<T> CreatedAfter<T>(this IQueryable<T> entities, DateTime afterDate) where T : IEntity
    {
        return entities.Where(a => a.CreatedAt >= afterDate);
    }
    
    public static IQueryable<T> UpdatedAfter<T>(this IQueryable<T> entities, DateTime afterDate) where T : IEntity
    {
        return entities.Where(a => a.UpdatedAt >= afterDate);
    }
}