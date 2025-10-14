using System.Globalization;
using System.Text.Json.Serialization;
using Common.Context;
using Common.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Common;

// TODO réfacto des Program.cs
public static class Init
{
    public static Logger ConfigureNlogFile()
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

    public static void Configure(this IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddNLog();
        
        if (!builder.Environment.IsDevelopment())
        {
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSeq(builder.Configuration.GetSection("Logging").GetSection("Seq"));
            });
        }
        
        builder.Services.AddDbContextFactory<DataContext>(option =>
        {
            option.UseNpgsql(
                builder.Configuration.GetConnectionString("Default")
            );
            option.EnableSensitiveDataLogging();
        });
        
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
                    SchemaName = "hangfire_worker",
                    QueuePollInterval = TimeSpan.FromSeconds(5)
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
        
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<MqttClientService>();
        builder.Services.AddSingleton<HttpClientService>();
        builder.Services.AddSingleton<IBackgroundJobClient, BackgroundJobClient>();
        builder.Services.AddSingleton<IRecurringJobManager, RecurringJobManager>();
        builder.Services.AddScoped<MeshtasticService>();
        builder.Services.AddScoped<MqttService>();
        builder.Services.AddScoped<UserService>();
        builder.Services.AddScoped<MqttServerService>();
        
        var culture = CultureInfo.GetCultureInfo("fr-FR");

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        if (builder is WebApplicationBuilder webApplicationBuilder)
        {
            webApplicationBuilder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
            
            webApplicationBuilder.Services.AddResponseCompression();
            webApplicationBuilder.Services.AddHttpContextAccessor();
            
            webApplicationBuilder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });
            
            webApplicationBuilder.Services.AddCors(option =>
            {
                option.AddDefaultPolicy(corsPolicyBuilder =>
                {
                    var corsHosts = webApplicationBuilder.Configuration.GetSection("CorsHosts").Value?.Split(";") ?? [];
                    
                    corsPolicyBuilder
                        .AllowAnyHeader()
                        .AllowAnyMethod();

                    if (corsHosts.Length != 0)
                    {
                        corsPolicyBuilder.WithOrigins(corsHosts);
                    }
                    else
                    {
                        corsPolicyBuilder.AllowAnyOrigin();
                    }
                });
            });
            
            webApplicationBuilder.Services.AddAuthorization();
        }
    }

    public static async Task Configure(this IHost host)
    {
        if (host is WebApplication webApplication)
        {
            if (!webApplication.Environment.IsDevelopment())
            {
                webApplication.UseExceptionHandler("/error", createScopeForErrors: true);
            }

            webApplication.UseCors();
            webApplication.UseForwardedHeaders();
            webApplication.UseAuthentication();
            webApplication.UseAuthorization();
            webApplication.MapControllers();
        }

        var context = await host.Services.GetRequiredService<IDbContextFactory<DataContext>>()
            .CreateDbContextAsync();
        await context.Database.MigrateAsync();
    }
}