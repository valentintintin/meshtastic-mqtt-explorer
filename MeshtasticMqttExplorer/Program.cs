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
using MeshtasticMqttExplorer.Extensions.Entities;
using MeshtasticMqttExplorer.Services;
using Microsoft.EntityFrameworkCore;
using Node = MeshtasticMqttExplorer.Context.Entities.Node;

Console.WriteLine("Starting");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddServerSideBlazor();

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
    /*
    var nodes = context.Nodes;
    await foreach (var node in nodes)
    {
        node.NodeIdString = node.NodeIdAsString();
        node.AllNames = node.FullName();
    }

    await context.SaveChangesAsync();
      */  
    /*
    var packets = context.Packets.Include(n => n.From).Where(n => n.PortNum == PortNum.NeighborinfoApp).ToList();
    foreach (var packet in packets)
    {
        NeighborInfo neighborInfoPayload = new();
        neighborInfoPayload.MergeFrom(packet.Payload);

        foreach (var neighbor in neighborInfoPayload.Neighbors)
        {
            var neighborNode = await context.Nodes.FindByNodeIdAsync(neighbor.NodeId) ?? new Node
            {
                NodeId = neighbor.NodeId,
                ModemPreset = packet.From.ModemPreset,
                RegionCode = packet.From.RegionCode
            };
            if (neighborNode.Id == 0)
            {
                context.Add(neighborNode);
            }
            else
            {
                neighborNode.RegionCode ??= packet.From.RegionCode;
                neighborNode.ModemPreset ??= packet.From.ModemPreset;
                context.Update(neighborNode);
            }

            await context.SaveChangesAsync();

            var neighborInfo =
                await context.NeighborInfos.FirstOrDefaultAsync(
                    n => n.Node == packet.From && n.Neighbor == neighborNode) ??
                new MeshtasticMqttExplorer.Context.Entities.NeighborInfo
                {
                    Node = packet.From,
                    Packet = packet,
                    Neighbor = neighborNode,
                    Snr = neighbor.Snr
                };
            if (neighborInfo.Id == 0)
            {
                context.Add(neighborInfo);
            }
            else
            {
                neighborInfo.CreatedAt = packet.CreatedAt;
                neighborInfo.UpdatedAt = packet.CreatedAt;
                context.Update(neighborInfo);
            }
        }

        await context.SaveChangesAsync();
    }
    */
}

Console.WriteLine("Started");

await app.RunAsync();

Console.WriteLine("Stopped");