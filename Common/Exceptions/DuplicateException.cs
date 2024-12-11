namespace Common.Exceptions;

public class DuplicateException(string what) : MqttMeshtasticException($"Duplicate entity {what}");

public class DuplicateEmailException() : DuplicateException("Email is already used");