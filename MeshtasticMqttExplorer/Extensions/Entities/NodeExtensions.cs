using MeshtasticMqttExplorer.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeshtasticMqttExplorer.Extensions.Entities;

public static class NodeExtensions
{
    public static Task<Node?> FindByNodeIdAsync(this IQueryable<Node> nodes, uint nodeId)
    {
        return nodeId == 0 ? null : nodes.FirstOrDefaultAsync(n => n.NodeId == nodeId);
    }
}