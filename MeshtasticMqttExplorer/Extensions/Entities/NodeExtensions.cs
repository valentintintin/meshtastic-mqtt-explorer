using MeshtasticMqttExplorer.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeshtasticMqttExplorer.Extensions.Entities;

public static class NodeExtensions
{
    public static Task<Node?> FindByNodeIdAsync(this IQueryable<Node> nodes, uint nodeId)
    {
        return nodeId > 0 ? nodes.FirstOrDefaultAsync(n => n.NodeId == nodeId) : null;
    }
    
    public static Task<Node?> FindByNodeIdStringAsync(this IQueryable<Node> nodes, string nodeIdString)
    {
        return string.IsNullOrWhiteSpace(nodeIdString) ? null : nodes.FirstOrDefaultAsync(n => n.NodeIdString != null && n.NodeIdString.ToLower().TrimStart('!') == nodeIdString.ToLower().TrimStart('!'));
    }
}