using Common.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Common.Jobs;

public abstract class AJob(ILogger<AJob> logger, IDbContextFactory<DataContext> contextFactory, IServiceProvider serviceProvider)
{
    public ILogger<AJob> Logger { get; } = logger;
    public IDbContextFactory<DataContext> ContextFactory { get; } = contextFactory;
    public IServiceProvider ServiceProvider { get; } = serviceProvider;
    
    protected DataContext Context = contextFactory.CreateDbContext();
}