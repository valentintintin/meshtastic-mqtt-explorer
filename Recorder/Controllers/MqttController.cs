using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Recorder.Controllers;

[ApiController]
[Route("mqtt")]
public class MqttController(ILogger<AController> logger) : AController(logger)
{
    [HttpPost("send/{server}")]
    public bool Send(string server, [FromBody] string dataBase64)
    {
        throw new NotImplementedException();
    }
}