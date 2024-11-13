using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Recorder.Models;
using Recorder.Services;

namespace Recorder.Controllers;

[ApiController]
[Route("mqtt")]
public class MqttController(ILogger<AController> logger) : AController(logger)
{
    [HttpGet("list")]
    public IEnumerable<MqttConfiguration> List()
    {
        return MqttService.MqttClientAndConfigurations.Select(c => c.Configuration);
    }

    [HttpPost("send/{server}")]
    public bool Send(string server, [FromBody] string dataBase64)
    {
        throw new NotImplementedException();
    }
}