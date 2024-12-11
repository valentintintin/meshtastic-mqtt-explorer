using Common.Context;
using Common.Context.Entities;
using Common.Exceptions;
using Common.Models;
using Common.Services;
using Microsoft.EntityFrameworkCore;

namespace MeshtasticMqttExplorer.Services;

public class RecorderService(ILogger<AService> logger, IDbContextFactory<DataContext> contextFactory, IConfiguration configuration) : AService(logger, contextFactory)
{
}