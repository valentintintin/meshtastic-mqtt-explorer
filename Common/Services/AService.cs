using Common.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Common.Services;

public abstract class AService(ILogger<AService> logger, IDbContextFactory<DataContext> contextFactory)
{
    protected readonly ILogger<AService> Logger = logger;
    protected readonly IDbContextFactory<DataContext> ContextFactory = contextFactory;
    protected DataContext Context = contextFactory.CreateDbContext();
    
    public void SetDbContext(DataContext context) => Context = context;
}