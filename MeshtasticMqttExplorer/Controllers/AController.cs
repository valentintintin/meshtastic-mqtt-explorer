using Microsoft.AspNetCore.Mvc;

namespace MeshtasticMqttExplorer.Controllers;

public abstract class AController(ILogger<AController> logger) : Controller
{
    protected readonly ILogger<AController> Logger = logger;
}