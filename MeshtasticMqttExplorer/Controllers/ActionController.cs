using Common;
using Common.Context;
using Common.Services;
using MeshtasticMqttExplorer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DateTimeOffset = System.DateTimeOffset;
using NeighborInfo = Common.Context.Entities.NeighborInfo;

namespace MeshtasticMqttExplorer.Controllers;

[ApiController]
[Route("action")]
public class ActionController(ILogger<ActionController> logger, PurgeService purgeService) : AController(logger)
{
    [HttpGet]
    public async Task Purge()
    {
        Logger.LogInformation("Purge data");

        await purgeService.RunPurge();
    }
}