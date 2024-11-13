using System.Text.Json.Serialization;
using Common.Context;
using Common.Extensions;
using Common.Services;
using Meshtastic.Protobufs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.Scheduler;
using NLog;
using NLog.Web;
using Recorder.Services;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

Console.WriteLine("Starting");

var logger = ConfigureNlogFile();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Trace);
    builder.Host.UseNLog();
    
    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    builder.Services.AddResponseCompression();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddNetDaemonScheduler();
    
    builder.Services.AddDbContextFactory<DataContext>(option =>
    {
        option.UseNpgsql(
            builder.Configuration.GetConnectionString("Default")
        );
    });

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });
    
    builder.Services.AddScoped<MeshtasticService>();
    builder.Services.AddScoped<NotificationService>();
    builder.Services.AddSingleton<MqttService>();
    builder.Services.AddHostedService(p => p.GetRequiredService<MqttService>());
    
    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddSeq(builder.Configuration.GetSection("Logging").GetSection("Seq"));
        });
    }

    var app = builder.Build();

    if (!builder.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/error", createScopeForErrors: true);
    }
    
    app.MapControllers();

    var context = await app.Services.GetRequiredService<IDbContextFactory<DataContext>>()
        .CreateDbContextAsync();
    await context.Database.MigrateAsync();

    MeshtasticService.NodesIgnored.AddRange(app.Services.GetRequiredService<IConfiguration>()
        .GetSection("NodesIgnored").Get<List<uint>>() ?? new List<uint>());
    Console.WriteLine($"Nodes ignored: {string.Join(", ", MeshtasticService.NodesIgnored.Select(a => a.ToHexString()))}");

    if (false && app.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
    {
        var n1 = context.Nodes.Find(2090);
        var n2 = context.Nodes.Find(2807);
        using var scope = app.Services.CreateScope();
        var meshtasticService = scope.ServiceProvider.GetRequiredService<MeshtasticService>();
        meshtasticService.SimplifyNeighborForNode(n1, n2);

        var packets = context.Packets
            .Include(a => a.From)
            .ThenInclude(n => n.Positions.OrderByDescending(p => p.UpdatedAt).Take(1))
            .Include(a => a.To)
            .ThenInclude(n => n.Positions.OrderByDescending(p => p.UpdatedAt).Take(1))
            .Include(a => a.Gateway)
            .ThenInclude(n => n.Positions.OrderByDescending(p => p.UpdatedAt).Take(1))
            .Where(a => a.PacketDuplicated == null)
            .Where(a => a.PortNum == PortNum.TracerouteApp && a.RequestId > 0)
            .ToList();

        int i = 0;
        foreach (var packet in packets)
        {
            try
            {
                await meshtasticService.DoReceive(packet);
                i++;

                if (i % 10 == 0)
                {
                    Console.WriteLine($"\n\nProcessed packet {i} with ID #{packet.Id}\n\n");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error processing packet #{packet.Id}: {e}");
            }
        }
    }

    Console.WriteLine("Started");

    await app.RunAsync();
}
catch (Exception exception)
{
    // Pour les commandes dotnet ef
    var type = exception.GetType().Name;
    var isMigrationCommand = type.StartsWith("HostAbortedException");

    if (!isMigrationCommand)
    {
        // NLog : capture les erreurs d'initialisation
        logger.Error(exception, "Stopped program because of exception");
        throw;
    }
}
finally
{
    // Assurez-vous de vider et d'arrêter les timers/threads internes avant la sortie de l'application (pour éviter une erreur de segmentation sur Linux)
    LogManager.Shutdown();
}

Console.WriteLine("Stopped");

Logger ConfigureNlogFile()
{
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    var configFile = $"nlog.{environment}.config";

    if (!File.Exists(configFile))
    {
        configFile = "nlog.config";
    }

    Console.WriteLine($"Logger configured with file: {configFile}");

    return LogManager.Setup().LoadConfigurationFromFile(configFile).GetCurrentClassLogger();
}