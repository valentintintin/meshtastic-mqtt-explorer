using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AntDesign;
using Google.Protobuf;
using Meshtastic.Protobufs;
using MeshtasticMqttExplorer;
using MeshtasticMqttExplorer.Components;
using MeshtasticMqttExplorer.Components.Shared;
using MeshtasticMqttExplorer.Context;
using MeshtasticMqttExplorer.Extensions;
using MeshtasticMqttExplorer.Extensions.Entities;
using MeshtasticMqttExplorer.Services;
using Microsoft.EntityFrameworkCore;
using Node = MeshtasticMqttExplorer.Context.Entities.Node;

Console.WriteLine("Starting");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddAntDesign();

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

builder.Services.AddSingleton<MqttService>();
builder.Services.AddHostedService<MqttConnectJob>();
builder.Services.AddHostedService<PurgeJob>();

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

app.UseStaticFiles();
app.UseAntiforgery();

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

var context = app.Services.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContext();
context.Database.Migrate();

if (false)
{
    var mqttService = app.Services.GetRequiredService<MqttService>();
    
    var nodes = context.Nodes
        .Include(a => a.PacketsFrom.Where(p => p.PortNum == PortNum.MapReportApp || p.PortNum == PortNum.PositionApp).OrderByDescending(p => p.UpdatedAt).Take(1))
        .Where(n => n.PacketsFrom.Any(p => p.PortNum == PortNum.MapReportApp || p.PortNum == PortNum.PositionApp))
        .ToList();
    var i = 0;
    foreach (var node in nodes)
    {
        var packet = node.PacketsFrom.First();
        
        var rootPacket = new ServiceEnvelope();
        rootPacket.MergeFrom(packet.Payload);

        switch (rootPacket.Packet.Decoded.Portnum)
        {
            case PortNum.MapReportApp:
            {
                var data = rootPacket.Packet.GetPayload<MapReport>();
            
                if (data != null)
                {
                    node.ModemPreset = data.ModemPreset;
                    node.RegionCode = data.Region;
                
                    await mqttService.UpdatePosition(node, data.LatitudeI, data.LongitudeI, data.Altitude, null, context);
                }

                break;
            }
            case PortNum.PositionApp:
            {
                var data = rootPacket.Packet.GetPayload<Position>();

                if (data != null)
                {
                    await mqttService.UpdatePosition(node, data.LatitudeI, data.LongitudeI, data.Altitude, null, context);
                }

                break;
            }
        }

        await context.SaveChangesAsync();
        
        Console.WriteLine($"#{node.Id} | {++i}/{nodes.Count}");
    }
}

Console.WriteLine("Started");

await app.RunAsync();

Console.WriteLine("Stopped");