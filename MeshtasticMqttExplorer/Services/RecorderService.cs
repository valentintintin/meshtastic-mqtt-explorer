using Common.Context;
using Common.Models;
using Common.Services;
using MeshtasticMqttExplorer.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace MeshtasticMqttExplorer.Services;

public class RecorderService(ILogger<AService> logger, IDbContextFactory<DataContext> contextFactory, IConfiguration configuration) : AService(logger, contextFactory)
{
    public async Task<IEnumerable<MqttConfiguration>> GetMqttConfigurations()
    {
        var recorderUrl = configuration.GetValue<string>("RecorderUrl") ?? throw new MissingConfigurationException("RecorderUrl");

        Logger.LogInformation("Get MqttConfigurations from Recorder {url}", recorderUrl);

        var client = new HttpClient
        {
            BaseAddress = new Uri(recorderUrl)
        };

        try
        {
            var response = await client.GetFromJsonAsync<IEnumerable<MqttConfiguration>>("mqtt/list");
            Logger.LogInformation("Got {nb} MqttConfigurations from Recorder", response?.Count());
            return response ?? [];
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during Get MqttConfigurations from Recorder");
            return [];
        }
    }
}