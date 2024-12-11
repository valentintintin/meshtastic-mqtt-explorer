namespace Common.Exceptions;

public class MqttMeshtasticException(string message, Exception? innerException = null)
    : Exception(message, innerException);