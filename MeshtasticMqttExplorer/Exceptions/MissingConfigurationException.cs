namespace MeshtasticMqttExplorer.Exceptions;

public class MissingConfigurationException(string key) : MeshtasticMqttExplorerException($"Missing key in configuration : {key}");