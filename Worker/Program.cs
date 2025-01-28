using Common.Context;
using Common.Jobs;
using Common.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

Console.WriteLine("Starting");

var logger = ConfigureNlogFile();

try {
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Trace);
    builder.Logging.AddNLog();
    
    builder.Services.AddDbContextFactory<DataContext>(option =>
    {
        option.UseNpgsql(
            builder.Configuration.GetConnectionString("Default")
        );
        option.EnableSensitiveDataLogging();
    });
    
    builder.Services.AddSingleton<NotificationService>();
    builder.Services.AddSingleton<MqttClientService>();
    builder.Services.AddSingleton<IRecurringJobManager, RecurringJobManager>();
    builder.Services.AddScoped<MeshtasticService>();
    builder.Services.AddScoped<MqttService>();
    
    builder.Services.AddHostedService(p => p.GetRequiredService<MqttClientService>());

    builder.Services.AddHangfire(action =>
    {
        action
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => { options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Default")); },
                new PostgreSqlStorageOptions
                {
                    SchemaName = "hangfire_worker",
                    QueuePollInterval = TimeSpan.FromSeconds(2)
                });
    });
    builder.Services.AddHangfireServer(action =>
    {
        action.ServerName = "Meshtastic Explorer Worker";
        action.SchedulePollingInterval = TimeSpan.FromSeconds(2);
        action.WorkerCount = 1;
        action.Queues = ["packet", "default"];
    });
    
    var app = builder.Build();

    var context = await app.Services.GetRequiredService<IDbContextFactory<DataContext>>()
        .CreateDbContextAsync();
    await context.Database.MigrateAsync();
    
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<PurgeJob>("purgeJob", (a) => a.RunPurge(), Cron.Hourly);

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
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
    var configFile = $"nlog.{environment}.config";

    if (!File.Exists(configFile))
    {
        configFile = "nlog.config";
    }

    Console.WriteLine($"Logger configured with file: {configFile}");

    return LogManager.Setup().LoadConfigurationFromFile(configFile).GetCurrentClassLogger();
}