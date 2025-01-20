using Microsoft.AspNetCore.Mvc;

namespace MeshtasticMqttExplorer.Controllers;

[ApiController]
[Route("action")]
public class ActionController(ILogger<ActionController> logger) : AController(logger)
{
}