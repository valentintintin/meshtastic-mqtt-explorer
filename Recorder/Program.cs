using System.Text.Json.Serialization;
using Common.Context;
using Common.Extensions;
using Common.Extensions.Entities;
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
using NeighborInfo = Common.Context.Entities.NeighborInfo;

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
        option.EnableSensitiveDataLogging();
    });

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });
    
    builder.Services.AddSingleton<NotificationService>();
    builder.Services.AddSingleton<MqttClientService>();
    builder.Services.AddSingleton<PurgeService>();
    builder.Services.AddScoped<MeshtasticService>();
    builder.Services.AddScoped<MqttService>();
    
    builder.Services.AddHostedService(p => p.GetRequiredService<MqttClientService>());
    
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

    if (false && app.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var meshtasticService = scope.ServiceProvider.GetRequiredService<MeshtasticService>();
        meshtasticService.SetDbContext(context);

        var allDatas = context.Packets
            .Where(a => a.PortNum == PortNum.TracerouteApp && a.CreatedAt >= DateTime.UtcNow.AddDays(-3) && a.MqttServerId != 3)
            .GroupBy(a => new { a.FromId, a.ToId }, (u, packets) => packets.OrderByDescending(a => a.CreatedAt).First())
            .ToList();
        
        Console.WriteLine($"Count {allDatas.Count}");

        var i = 0;
        foreach (var dataId in allDatas)
        {
            var data = context.Packets
                .Include(n => n.Channel)
                .Include(n => n.MqttServer)
                .Include(n => n.From)
                .Include(n => n.To)
                .Include(n => n.Gateway)
                .Include(a => a.PacketDuplicated)
                .Include(a => a.Position)
                .FindById(dataId.Id);
            
            try
            {
                await meshtasticService.DoReceive(data);
                i++;

                // if (i % 10 == 0)
                // {
                    Console.WriteLine($"Processed data {i}/{allDatas.Count} with ID #{data.Id}");
                    // await context.SaveChangesAsync();
                // }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error processing data #{data.Id}: {e} {e.StackTrace}");
            }
        }
        
        await context.SaveChangesAsync();
        Console.WriteLine("Ended");
        return;
    }
    
    app.Services.GetRequiredService<PurgeService>(); // Run cron

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