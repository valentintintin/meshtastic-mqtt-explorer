using MeshtasticMqttExplorer.Context;
using Microsoft.EntityFrameworkCore;

namespace MeshtasticMqttExplorer.Services;

public abstract class AService(ILogger<AService> logger, IDbContextFactory<DataContext> contextFactory)
{
    protected readonly ILogger<AService> Logger = logger;
    protected readonly IDbContextFactory<DataContext> DbContextFactory = contextFactory;
    protected readonly DataContext Context = contextFactory.CreateDbContext();
}