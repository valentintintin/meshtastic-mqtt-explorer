using System.Reflection;
using Meshtastic.Protobufs;
using MeshtasticMqttExplorer.Context.Configurations;
using MeshtasticMqttExplorer.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Channel = MeshtasticMqttExplorer.Context.Entities.Channel;
using NeighborInfo = MeshtasticMqttExplorer.Context.Entities.NeighborInfo;
using Position = MeshtasticMqttExplorer.Context.Entities.Position;
using Telemetry = MeshtasticMqttExplorer.Context.Entities.Telemetry;
using Waypoint = MeshtasticMqttExplorer.Context.Entities.Waypoint;

namespace MeshtasticMqttExplorer.Context;

public class DataContext(DbContextOptions<DataContext> options, ILogger<DataContext> logger) : DbContext(options)
{
    public required DbSet<Packet> Packets { get; set; }
    public required DbSet<Node> Nodes { get; set; }
    public required DbSet<Position> Positions { get; set; }
    public required DbSet<Telemetry> Telemetries { get; set; }
    public required DbSet<NeighborInfo> NeighborInfos { get; set; }
    public required DbSet<Channel> Channels { get; set; }
    public required DbSet<TextMessage> TextMessages { get; set; }
    public required DbSet<Waypoint> Waypoints { get; set; }
    public required DbSet<Traceroute> Traceroutes { get; set; }

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
            if (entityEntry.Entity is Node node)
            {
                try
                {
                    node.NodeIdString = node.NodeIdAsString();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error while saving nodeIdString in SaveChanges for node #{node}", node.Id);
                }

                try 
                {
                    node.AllNames = node.FullName();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error while saving allNames in SaveChanges for node #{node}", node.Id);
                }
            }
            
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