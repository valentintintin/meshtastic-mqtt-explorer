using System.Text.Json.Serialization;
using Common;
using Common.Context;
using Common.Extensions.Entities;
using Common.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Meshtastic.Protobufs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

Console.WriteLine("Starting");

var logger = Init.ConfigureNlogFile();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configure();
    
    builder.Services.AddHostedService(p => p.GetRequiredService<MqttClientService>());
    builder.Services.AddHostedService(p => p.GetRequiredService<HttpClientService>());

    var app = builder.Build();

    await app.Configure();
    
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