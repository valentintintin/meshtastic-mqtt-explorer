using Common;
using Common.Jobs;
using Common.Services;
using Hangfire;
using NLog;

Console.WriteLine("Starting");

var logger = Init.ConfigureNlogFile();

try {
    var builder = Host.CreateApplicationBuilder(args);

    builder.Configure();
    
    builder.Services.AddHostedService(p => p.GetRequiredService<MqttClientService>());
    builder.Services.AddHostedService(p => p.GetRequiredService<HttpClientService>());
    
    builder.Services.AddHangfireServer(action =>
    {
        action.ServerName = "Meshtastic Explorer Worker 1";
        action.SchedulePollingInterval = TimeSpan.FromSeconds(1);
        action.WorkerCount = 2;
        action.Queues = ["packet", "default"];
    });
    
    builder.Services.AddHangfireServer(action =>
    {
        action.ServerName = "Meshtastic Explorer Worker 2";
        action.WorkerCount = 1;
        action.Queues = ["packet-2", "default-2"];
    });
    
    var app = builder.Build();

    await app.Configure();
    
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