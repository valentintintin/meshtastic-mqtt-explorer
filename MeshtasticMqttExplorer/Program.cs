using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AntDesign;
using Blazored.LocalStorage;
using Google.Protobuf;
using Meshtastic.Extensions;
using Meshtastic.Protobufs;
using MeshtasticMqttExplorer;
using MeshtasticMqttExplorer.Components;
using MeshtasticMqttExplorer.Context;
using MeshtasticMqttExplorer.Extensions;
using MeshtasticMqttExplorer.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using NetDaemon.Extensions.Scheduler;
using NLog;
using NLog.Web;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

Console.WriteLine("Starting");

var logger = ConfigureNlogFile();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Trace);
    builder.Host.UseNLog();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddAntDesign();
    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    builder.Services.AddResponseCompression();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddNetDaemonScheduler();
    builder.Services.AddBlazoredLocalStorage();

    builder.Services.Configure<HostOptions>(hostOptions =>
    {
        hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    });

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

    app.UseMiddleware<PerformanceAndCultureMiddleware>();

    app.UseStaticFiles(new StaticFileOptions
    {
        ServeUnknownFileTypes = true
    });
    app.UseAntiforgery();

    app.MapControllers();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    LocaleProvider.DefaultLanguage = "fr-FR";
    LocaleProvider.SetLocale(LocaleProvider.DefaultLanguage);

    var culture = CultureInfo.GetCultureInfo(LocaleProvider.DefaultLanguage);

    CultureInfo.DefaultThreadCurrentCulture = culture;
    CultureInfo.DefaultThreadCurrentUICulture = culture;
    CultureInfo.CurrentCulture = culture;
    Thread.CurrentThread.CurrentCulture = culture;
    Thread.CurrentThread.CurrentUICulture = culture;

    var context = await app.Services.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContextAsync();
    await context.Database.MigrateAsync();

    MeshtasticService.NodesIgnored.AddRange(app.Services.GetRequiredService<IConfiguration>().GetSection("NodesIgnored").Get<List<uint>>() ?? []);
    Console.WriteLine($"Nodes ignored : {MeshtasticService.NodesIgnored.Select(a => a.ToString()).JoinString()}");

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

Logger ConfigureNlogFile()
{
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    var configFile = "nlog.config";

    if (File.Exists($"nlog.{environment}.config"))
    {
        configFile = $"nlog.{environment}.config";
    }

    if (string.IsNullOrWhiteSpace(environment))
    { // Null == Production
        configFile = "nlog.Production.config";
    }

    Console.WriteLine($"Logger avec le fichier {configFile}");
    
    return LogManager.Setup().LoadConfigurationFromFile(configFile).GetCurrentClassLogger();
}