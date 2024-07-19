using Google.Protobuf;
using Meshtastic.Protobufs;
using MeshtasticMqttExplorer.Context;
using Microsoft.EntityFrameworkCore;

namespace MeshtasticMqttExplorer.Services;

public class MeshtasticService(ILogger<MeshtasticService> logger, IDbContextFactory<DataContext> contextFactory) : AService(logger, contextFactory)
{
    public ChannelSet DecodeQrCodeUrl(string url)
    {
        Logger.LogTrace("Decoding of url {url}", url);

        try
        {
            var b64 = UrlSafeBase64Decode(url.Split("/#").Last());

            var packet = new ChannelSet();
            packet.MergeFrom(Convert.FromBase64String(b64));
            
            Logger.LogInformation("Decoding of url {url} OK. Main canal : {mainChanel}", url, packet.Settings.FirstOrDefault()?.Name);

            return packet;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error during url decode {url}", url);
            throw;
        }
    }
    
    private string UrlSafeBase64Decode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return base64;
    }
}