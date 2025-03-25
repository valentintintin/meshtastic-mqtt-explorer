using System.Text;
using System.Text.Json.Serialization;
using Common;
using Common.Context;
using Common.Context.Entities.Router;
using Common.Services;
using Hangfire;
using Hangfire.PostgreSql;
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
using MqttRouter.Services;
using NLog;
using NLog.Web;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

Console.WriteLine("Starting");

var logger = Init.ConfigureNlogFile();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    builder.Configure();

    builder.WebHost.UseKestrel(o =>
    {
        o.ListenAnyIP(1883, l => l.UseMqtt());
        o.ListenAnyIP(5192); // Default HTTP pipeline
    });

    builder.Services.AddScoped<RoutingService>();
    builder.Services.AddHostedMqttServer(options => options.WithDefaultEndpoint());
    builder.Services.AddMqttConnectionHandler();
    
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

    await app.Configure();
    
    app.UseMqttServer(
        server =>
        {
            var serviceProvider = app.Services.CreateScope().ServiceProvider;
            
            var mqttController = new MqttServerController(serviceProvider, server);

            server.ValidatingConnectionAsync += mqttController.OnConnection;
            server.InterceptingPublishAsync += mqttController.OnNewPacket;
            server.InterceptingClientEnqueueAsync += mqttController.BeforeSend;
            server.ClientDisconnectedAsync += mqttController.OnDisconnection;
        });
    
    var context = await app.Services.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContextAsync();
    
    foreach (var nodeConfiguration in context.NodeConfigurations.Include(a => a.Node).Where(a => a.IsConnected))
    {
        nodeConfiguration.IsConnected = false;
        nodeConfiguration.Node.IsMqttGateway = false;
        context.Update(nodeConfiguration);
        context.Update(nodeConfiguration.Node);
    }
    await context.SaveChangesAsync();
    
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