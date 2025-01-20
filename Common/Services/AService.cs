using Common.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Common.Services;

public abstract class AService(ILogger<AService> logger, IDbContextFactory<DataContext> contextFactory)
{
    public ILogger<AService> Logger { get; } = logger;
    public IDbContextFactory<DataContext> ContextFactory { get; } = contextFactory;
    protected DataContext Context = contextFactory.CreateDbContext();
    
    public void SetDbContext(DataContext context) => Context = context;
}