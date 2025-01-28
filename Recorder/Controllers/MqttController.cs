using Common.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Recorder.Models;

namespace Recorder.Controllers;

[ApiController]
[Route("mqtt")]
public class MqttController(ILogger<AController> logger, MqttClientService clientService) : AController(logger)
{
    [HttpGet("list")]
    public IEnumerable<MqttClientDto> List()
    {
        return clientService.MqttClientAndConfigurations.Select(c => new MqttClientDto
        {
            Id = c.MqttServer.Id,
            Name = c.MqttServer.Name,
            NbPacket = c.NbPacket,
            LastPacketReceivedDate = c.LastPacketReceivedDate,
        });
    }
}