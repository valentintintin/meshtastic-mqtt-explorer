using MeshtasticMqttExplorer.Context.Entities;

namespace MeshtasticMqttExplorer.GraphQl.Types;

public class NodeType : ObjectType<Node>
{
    protected override void Configure(IObjectTypeDescriptor<Node> descriptor)
    {
        base.Configure(descriptor);
    }
}