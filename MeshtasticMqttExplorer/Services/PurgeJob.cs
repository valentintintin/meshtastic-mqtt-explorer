namespace MeshtasticMqttExplorer.Services;

public class PurgeJob(MqttService mqttService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await mqttService.PurgePackets();

            await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
        }
    }
}