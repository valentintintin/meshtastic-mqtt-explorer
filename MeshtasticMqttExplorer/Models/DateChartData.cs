// ReSharper disable InconsistentNaming
namespace MeshtasticMqttExplorer.Models;

public class DateChartData<T> : ChartData<T>
{
    public required string date { get; init; }
}