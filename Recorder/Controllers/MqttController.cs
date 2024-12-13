using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Recorder.Models;
using Recorder.Services;

namespace Recorder.Controllers;

[ApiController]
[Route("mqtt")]
public class MqttController(ILogger<AController> logger) : AController(logger)
{
    [HttpGet("list")]
    public IEnumerable<MqttClientDto> List()
    {
        return MqttClientService.MqttClientAndConfigurations.Select(c => new MqttClientDto
        {
            Id = c.MqttServer.Id,
            Name = c.MqttServer.Name,
            NbPacket = c.NbPacket,
            LastPacketReceivedDate = c.LastPacketReceivedDate,
        });
    }

    [HttpPost("send/{server}")]
    public bool Send(string server, [FromBody] string dataBase64)
    {
        throw new NotImplementedException();
    }
}