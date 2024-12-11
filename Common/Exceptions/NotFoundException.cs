namespace Common.Exceptions;

public class NotFoundException : MqttMeshtasticException
{
    public NotFoundException(string typeEntity, object? key) : base($"Entity {typeEntity} with key {(key ?? "null")} not found") { }

    public NotFoundException(string message) : base(message) { }
}

public class NotFoundException<T>(object? key = null) : NotFoundException(typeof(T).Name, key);