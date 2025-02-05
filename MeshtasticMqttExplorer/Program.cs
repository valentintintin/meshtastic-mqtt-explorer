using System.Globalization;
using System.Text.Json.Serialization;
using AntDesign;
using Blazored.LocalStorage;
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
using NetDaemon.Extensions.Scheduler;
using NLog;
using NLog.Web;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using NotificationService = Common.Services.NotificationService;

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
    builder.Services.AddHangfire(action =>
    {
        action
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
            {
                options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Default"));
            }, new PostgreSqlStorageOptions
            {
                SchemaName = "hangfire_worker"
            });
    });
    
    builder.Services.AddIdentity<Common.Context.Entities.Router.User, IdentityRole<long>>(options =>
        {
            options.Lockout.AllowedForNewUsers = false;
            
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 1;
            options.Password.RequiredUniqueChars = 1;

            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<DataContext>()
        .AddDefaultTokenProviders(); // token for reset password etc

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
    builder.Services.AddSingleton<RecorderService>();
    builder.Services.AddScoped<MeshtasticService>();
    builder.Services.AddScoped<MqttService>();
    builder.Services.AddScoped<UserService>();
    
    builder.Services.AddHostedService(p => p.GetRequiredService<MqttClientService>());

    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddSeq(builder.Configuration.GetSection("Logging").GetSection("Seq"));
        });
    }
    
    builder.Services.AddAuthorization();
    builder.Services.AddAuthentication("Bearer").AddJwtBearer();

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
    
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapControllers();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.UseHangfireDashboard("/worker", new DashboardOptions
    {
        DashboardTitle = "Meshtastic Explorer Worker Dashboard",
        Authorization = [],
        IgnoreAntiforgeryToken = true
    });

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