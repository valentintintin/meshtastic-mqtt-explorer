﻿using System.Text.Json.Serialization;
using Common.Context;
using Common.Context.Entities;
using Common.Context.Entities.Router;
using Common.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet.AspNetCore;
using MqttRouter.Controllers;
using MqttRouter.Models;
using MqttRouter.Services;
using NetDaemon.Extensions.Scheduler;
using NLog;
using NLog.Web;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

Console.WriteLine("Starting");

var logger = ConfigureNlogFile();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Trace);
    builder.Host.UseNLog();

    builder.WebHost.UseKestrel(o =>
    {
        o.ListenAnyIP(1884, l => l.UseMqtt());
    });
    
    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    builder.Services.AddResponseCompression();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddNetDaemonScheduler();
    
    builder.Services.AddIdentity<User, IdentityRole<long>>(options =>
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

    builder.Services.AddScoped<UserService>();
    builder.Services.AddScoped<MqttService>();
    builder.Services.AddScoped<NotificationService>();
    builder.Services.AddScoped<MeshtasticService>();
    builder.Services.AddScoped<RoutingService>();

    builder.Services.AddHostedMqttServer(options => options.WithDefaultEndpoint());
    builder.Services.AddMqttConnectionHandler();
    
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
    
    app.UseMqttServer(
        server =>
        {
            var serviceProvider = app.Services.CreateScope().ServiceProvider;
            
            var mqttController = new MqttServerController(serviceProvider);

            server.ValidatingConnectionAsync += mqttController.OnValidateConnection;
            server.InterceptingPublishAsync += mqttController.OnInterceptingInbound;
            server.InterceptingClientEnqueueAsync += mqttController.OnInterceptingPublish;
            server.ClientDisconnectedAsync += mqttController.OnDisconnected;
        });

    var context = await app.Services.GetRequiredService<IDbContextFactory<DataContext>>()
        .CreateDbContextAsync();
    await context.Database.MigrateAsync();

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