using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace Common.Extensions.Entities;

public static class NodeExtensions
{
    public static Node? FindByNodeId(this IEnumerable<Node> nodes, uint nodeId)
    {
        return nodes.OrderBy(n => n.CreatedAt).FirstOrDefault(n => n.NodeId == nodeId);
    }
    
    public static async Task<Node?> FindByNodeIdAsync(this IQueryable<Node> nodes, uint nodeId)
    {
        return await nodes.OrderBy(n => n.CreatedAt).FirstOrDefaultAsync(n => n.NodeId == nodeId);
    }
    
    public static async Task<Node?> FindByNodeIdStringAsync(this IQueryable<Node> nodes, string nodeIdString)
    {
        return string.IsNullOrWhiteSpace(nodeIdString) ? null : await nodes.OrderBy(n => n.CreatedAt).FirstOrDefaultAsync(n => n.NodeIdString != null && n.NodeIdString.ToLower().TrimStart('!') == nodeIdString.ToLower().TrimStart('!'));
    }
}