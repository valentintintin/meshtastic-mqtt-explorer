namespace Common.Exceptions;

public class LoginException(string message) : MqttMeshtasticException(message);