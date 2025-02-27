using Common.Models;
using Common.Services;
using Microsoft.AspNetCore.Mvc;

namespace MeshtasticMqttExplorer.Controllers;

[ApiController]
[Route("api/send")]
public class SendController(ILogger<ActionController> logger, HttpClientService httpClientService) : AController(logger)
{
    [HttpPost]
    public async Task SendToServer([FromBody] SentPacketDto dto)
    {
        await httpClientService.PublishMessage(dto);
    }
}