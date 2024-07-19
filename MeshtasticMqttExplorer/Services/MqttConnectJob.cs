namespace MeshtasticMqttExplorer.Services;

public class MqttConnectJob(MqttService mqttService, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (configuration.GetValue("ConnectToMqtt", false))
        {
            await mqttService.ConnectMqtt();
        }
    }
}