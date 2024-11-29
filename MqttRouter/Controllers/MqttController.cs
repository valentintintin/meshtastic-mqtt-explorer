using System.Text;
using MQTTnet;
using MQTTnet.Server;

namespace MqttRouter.Controllers;

public class MqttController
{
    public Task OnClientConnected(ClientConnectedEventArgs eventArgs)
    {
        Console.WriteLine($"Client '{eventArgs.ClientId}' connected.");
        return Task.CompletedTask;
    }
    
    public Task ValidateConnection(ValidatingConnectionEventArgs eventArgs)
    {
        Console.WriteLine($"Client '{eventArgs.ClientId}' wants to connect. Accepting!");
        return Task.CompletedTask;
    }

    public Task InterceptingPublish(InterceptingClientApplicationMessageEnqueueEventArgs eventArgs)
    {
        Console.WriteLine($"Client '{eventArgs.SenderClientId}' published.");
        var s = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload);
        if (s.Contains("test"))
        {
            Console.WriteLine($"Client '{eventArgs.SenderClientId}' ignored test.");
            eventArgs.AcceptEnqueue = false;
        }
        else if (s.Contains("try"))
        {
            Console.WriteLine($"Client '{eventArgs.SenderClientId}' only try for one client.");
        }
        return Task.CompletedTask;
    }
}