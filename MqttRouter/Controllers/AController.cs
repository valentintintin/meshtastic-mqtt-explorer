using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Recorder.Controllers;

public abstract class AController(ILogger<AController> logger) : Controller
{
    protected readonly ILogger<AController> Logger = logger;
}