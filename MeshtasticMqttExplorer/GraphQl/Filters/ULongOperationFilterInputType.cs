using HotChocolate.Data.Filters;
using MeshtasticMqttExplorer.GraphQl.Types;

namespace MeshtasticMqttExplorer.GraphQl.Filters;

public class ULongOperationFilterInputType
    : ComparableOperationFilterInputType<ULongType>
{
    protected override void Configure(IFilterInputTypeDescriptor descriptor)
    {
        descriptor.Name("ULongOperationFilterInput");
        base.Configure(descriptor);
    }
}
