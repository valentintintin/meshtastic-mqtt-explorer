using System.Reflection;
using Common.Context.Configurations;
using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MqttRouter.Context;

public class DataRouterContext(DbContextOptions<DataRouterContext> options, ILogger<DataRouterContext> logger) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetAssembly(typeof(PacketConfiguration))!);
    }
    
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ComputeEntitiesBeforeSaveChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ComputeEntitiesBeforeSaveChanges();
        
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ComputeEntitiesBeforeSaveChanges()
    {
        foreach (var entityEntry in ChangeTracker.Entries())
        {
            if (entityEntry.Entity is IEntity entity)
            {
                switch (entityEntry.State)
                {
                    case EntityState.Added:
                        if (entity.CreatedAt == DateTime.MinValue)
                        {
                            entity.CreatedAt = DateTime.UtcNow;
                        }

                        if (entity.UpdatedAt == DateTime.MinValue)
                        {
                            entity.UpdatedAt = DateTime.UtcNow;
                        }
                        break;
                    case EntityState.Modified:
                        entity.UpdatedAt = DateTime.UtcNow;
                        break;
                }
            }
        }
    }
}