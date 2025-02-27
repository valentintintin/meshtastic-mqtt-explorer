using Microsoft.AspNetCore.Mvc;

namespace MeshtasticMqttExplorer.Controllers;

[ApiController]
[Route("api/action")]
public class ActionController(ILogger<ActionController> logger) : AController(logger)
{
}