using System.Reflection;
using Common.Context.Configurations;
using Common.Context.Entities;
using Common.Context.Entities.Router;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Channel = Common.Context.Entities.Channel;
using NeighborInfo = Common.Context.Entities.NeighborInfo;
using NodeConfiguration = Common.Context.Entities.Router.NodeConfiguration;
using Position = Common.Context.Entities.Position;
using Telemetry = Common.Context.Entities.Telemetry;
using Waypoint = Common.Context.Entities.Waypoint;

namespace Common.Context;

public class DataContext(DbContextOptions<DataContext> options, ILogger<DataContext> logger) : 
    IdentityDbContext<User, IdentityRole<long>, long>(options)
{
    public required DbSet<Packet> Packets { get; set; }
    public required DbSet<Node> Nodes { get; set; }
    public required DbSet<Position> Positions { get; set; }
    public required DbSet<Telemetry> Telemetries { get; set; }
    public required DbSet<NeighborInfo> NeighborInfos { get; set; }
    public required DbSet<Channel> Channels { get; set; }
    public required DbSet<TextMessage> TextMessages { get; set; }
    public required DbSet<Waypoint> Waypoints { get; set; }
    public required DbSet<SignalHistory> SignalHistories { get; set; }
    
    public required DbSet<MqttServer> MqttServers { get; set; }
    public required DbSet<Webhook> Webhooks { get; set; }
    public required DbSet<WebhookHistory> WebhooksHistories { get; set; }
    
    public required DbSet<NodeConfiguration> NodeConfigurations { get; set; }
    public required DbSet<PacketActivity> PacketActivities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetAssembly(typeof(PacketConfiguration))!);

        modelBuilder.Entity<IdentityRole<long>>().ToTable("Roles", "router");
        modelBuilder.Entity<IdentityUserClaim<long>>().ToTable("UserClaims", "router");
        modelBuilder.Entity<IdentityUserRole<long>>().ToTable("UserRoles", "router");
        modelBuilder.Entity<IdentityUserLogin<long>>().ToTable("UserLogins", "router");
        modelBuilder.Entity<IdentityUserToken<long>>().ToTable("UserTokens", "router");
        modelBuilder.Entity<IdentityRoleClaim<long>>().ToTable("RoleClaims", "router");
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
            else if (entityEntry.Entity is PacketActivity packetActivity)
            {
                packetActivity.IsBroadcast = packetActivity.ReceiverIds.Count == 0;
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