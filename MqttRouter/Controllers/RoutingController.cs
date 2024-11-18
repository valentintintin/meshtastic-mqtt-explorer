using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MqttRouter.Models;
using MqttRouter.Services;
using Recorder.Controllers;

namespace MqttRouter.Controllers;

[ApiController]
[Route("routing")]
public class RoutingController(ILogger<AController> logger, RoutingService routingService) : AController(logger)
{
    [HttpPost("")]
    public async Task<bool> Route(RoutingDto dto)
    {
        return await routingService.Route(dto);
    }
}