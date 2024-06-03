// ReSharper disable InconsistentNaming
namespace MeshtasticMqttExplorer.Models;

public class ChartData<T>
{
    public required string type { get; init; }
    
    public required T value { get; init; }
}