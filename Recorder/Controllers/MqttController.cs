using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Recorder.Models;

namespace Recorder.Controllers;

[ApiController]
[Route("mqtt")]
public class MqttController(ILogger<AController> logger, IConfiguration configuration) : AController(logger)
{
    [HttpGet("list")]
    public IEnumerable<MqttConfiguration> List()
    {
        return (configuration.GetSection("Mqtt").Get<List<MqttConfiguration>>() ?? throw new KeyNotFoundException("Mqtt"))
            .Where(a => a.Enabled);
    }

    [HttpPost("send/{server}")]
    public bool Send(string server, [FromBody] string dataBase64)
    {
        throw new NotImplementedException();
    }
}