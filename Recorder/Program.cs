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
    
    builder.Services.AddScoped<MeshtasticService>();
    builder.Services.AddScoped<NotificationService>();
    builder.Services.AddScoped<MqttService>();
    builder.Services.AddSingleton<MqttClientService>();
    builder.Services.AddSingleton<PurgeService>();
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

    app.Services.GetRequiredService<PurgeService>(); // Instance to schedules

    if (false && app.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var meshtasticService = scope.ServiceProvider.GetRequiredService<MeshtasticService>();

        var allDatas = context.NeighborInfos
            .Include(a => a.Packet)
            .ThenInclude(a => a.Gateway)
            .Include(a => a.Packet)
            .ThenInclude(a => a.From)
            .Where(a => a.DataSource == NeighborInfo.Source.Gateway)
            .Where(a => a.Packet != null)
            .ToList();

        int i = 0;
        foreach (var data in allDatas)
        {
            try
            {
                data.Node = data.Packet!.Gateway;
                data.Neighbor = data.Packet.From;
                context.Update(data);
                i++;

                if (i % 10 == 0)
                {
                    Console.WriteLine($"Processed data {i} with ID #{data.Id}");
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error processing data #{data.Id}: {e}");
            }
        }
        
        await context.SaveChangesAsync();
        Console.WriteLine("Ended");
        return;
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