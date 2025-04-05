using Common.Context;
using Common.Extensions.Entities;
using MeshtasticMqttExplorer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MeshtasticMqttExplorer.Controllers;

[ApiController]
[Route("api/node")]
public class NodeController(ILogger<ActionController> logger, IDbContextFactory<DataContext> contextFactory, IConfiguration configuration) : AController(logger)
{
    [HttpGet("search")]
    public async Task<NodeDto?> SearchNode(string value)
    {
        var context = await contextFactory.CreateDbContextAsync();
        var node = await context.Nodes.Search(value);

        if (node == null)
        {
            return null;
        }

        return new NodeDto
        {
            Id = node.Id,
            NodeId = node.NodeId,
            LastSeen = node.LastSeen,
            NodeIdString = node.NodeIdString,
            LongName = node.LongName,
            ShortName = node.ShortName,
            AllNames = node.AllNames,
            Role = node.Role,
            Link = $"{configuration.GetValue<string>("FrontUrl")}/node/{node.Id}"
        };
    }
}