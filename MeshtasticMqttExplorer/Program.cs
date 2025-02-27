using System.Globalization;
using System.Text.Json.Serialization;
using AntDesign;
using Blazored.LocalStorage;
using Common;
using Common.Context;
using Common.Services;
using Hangfire;
using Hangfire.PostgreSql;
using MeshtasticMqttExplorer;
using MeshtasticMqttExplorer.Components;
using MeshtasticMqttExplorer.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using NotificationService = Common.Services.NotificationService;

Console.WriteLine("Starting");

var logger = Init.ConfigureNlogFile();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configure();

    builder.Services.AddRazorComponents().AddInteractiveServerComponents();
    builder.Services.AddAntDesign();
    builder.Services.AddBlazoredLocalStorage();
    
    builder.Services.AddSingleton<RecorderService>();
    builder.Services.AddHostedService(p => p.GetRequiredService<MqttClientService>());
    builder.Services.AddHostedService(p => p.GetRequiredService<HttpClientService>());
    
    var app = builder.Build();
    
    await app.Configure();

    app.UseMiddleware<PerformanceAndCultureMiddleware>();

    app.UseStaticFiles(new StaticFileOptions
    {
        ServeUnknownFileTypes = true
    });
    
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.UseHangfireDashboard("/worker", new DashboardOptions
    {
        DashboardTitle = "Meshtastic Explorer Worker Dashboard",
        Authorization = [],
        IgnoreAntiforgeryToken = true
    });

    LocaleProvider.DefaultLanguage = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
    LocaleProvider.SetLocale(LocaleProvider.DefaultLanguage);
    
    Utils.MqttServerFilters.AddRange(
        await (await app.Services.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContextAsync()).MqttServers
            .Select(c => new TableFilter<long?> { Text = c.Name, Value = c.Id })
            .ToListAsync()
    );

    Console.WriteLine("Started");

    await app.RunAsync();
}
catch (Exception exception)
{
    // For dotnet ef commands
    var type = exception.GetType().Name;
    var isMigrationCommand = type.StartsWith("HostAbortedException");

    if (!isMigrationCommand)
    {
        // NLog: catch setup errors
        logger.Error(exception, "Stopped program because of exception");
        throw;
    }
}
finally
{
    // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
    LogManager.Shutdown();
}

Console.WriteLine("Stopped");