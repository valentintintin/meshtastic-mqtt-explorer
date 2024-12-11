namespace Common.Exceptions;

public class MissingConfigurationException(string key) : Exception($"Missing key in configuration : {key}");