using MeshtasticMqttExplorer.Context;
using MeshtasticMqttExplorer.Context.Entities;

namespace MeshtasticMqttExplorer.GraphQl;

public class Query
{
    [UseOffsetPaging]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Node> GetNodes(DataContext context)
    {
        return context.Nodes;
    }
}