namespace MeshtasticMqttExplorer.Services;

public class MqttConnectJob(MqttService mqttService, IHostEnvironment environment) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (environment.IsProduction())
        {
            await mqttService.ConnectMqtt();
        }
    }
}