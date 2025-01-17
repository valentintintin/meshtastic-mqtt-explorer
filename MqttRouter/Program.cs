using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Common.Context;
using Common.Context.Entities;
using Common.Context.Entities.Router;
using Common.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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
        o.ListenAnyIP(1883, l => l.UseMqtt());
        o.ListenAnyIP(5192); // Default HTTP pipeline
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
    
    builder.Services.AddCors(option =>
    {
        option.AddDefaultPolicy(corsPolicyBuilder => corsPolicyBuilder
            .WithOrigins(builder.Configuration.GetSection("CorsHosts").Value?.Split(",") ?? Array.Empty<string>())
            .AllowAnyHeader()
            .AllowAnyMethod()
        );
    });

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });

    builder.Services.AddScoped<UserService>();
    builder.Services.AddScoped<MqttService>();
    builder.Services.AddSingleton<NotificationService>();
    builder.Services.AddSingleton<PurgeService>();
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
    
    builder.Services.AddAuthorization();
    
    var tokenSettings = builder.Configuration.GetSection("Authentication")
        .GetSection("Schemes").GetSection("Bearer");
    var jwtKey = tokenSettings.GetValue<string?>("IssuerSigningKey");

    if (!string.IsNullOrEmpty(jwtKey))
    {
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters.ValidateAudience = false;
            options.TokenValidationParameters.ValidateIssuer = false;
            options.TokenValidationParameters.IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        });
    }

    var app = builder.Build();

    app.UseCors();
    app.UseForwardedHeaders();

    if (!builder.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/error", createScopeForErrors: true);
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    
    app.UseMqttServer(
        server =>
        {
            var serviceProvider = app.Services.CreateScope().ServiceProvider;
            
            var mqttController = new MqttServerController(serviceProvider);

            server.ValidatingConnectionAsync += mqttController.OnConnection;
            server.InterceptingPublishAsync += mqttController.OnNewPacket;
            server.InterceptingClientEnqueueAsync += mqttController.BeforeSend;
            server.ClientDisconnectedAsync += mqttController.OnDisconnection;
        });

    var context = await app.Services.GetRequiredService<IDbContextFactory<DataContext>>()
        .CreateDbContextAsync();
    await context.Database.MigrateAsync();
    
    foreach (var nodeConfiguration in context.NodeConfigurations.Where(a => a.MqttId != null))
    {
        nodeConfiguration.MqttId = null;
        context.Update(nodeConfiguration);
    }
    await context.SaveChangesAsync();
    
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