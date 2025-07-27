namespace Common.Exceptions;

public class NotificationIgnoredException(string reason) : MqttMeshtasticException($"We should ignore this message because {reason}")
{
    
}