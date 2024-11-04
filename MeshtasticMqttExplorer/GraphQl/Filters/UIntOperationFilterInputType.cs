using HotChocolate.Data.Filters;
using MeshtasticMqttExplorer.GraphQl.Types;

namespace MeshtasticMqttExplorer.GraphQl.Filters;

public class UIntOperationFilterInputType
    : ComparableOperationFilterInputType<UIntType>
{
    protected override void Configure(IFilterInputTypeDescriptor descriptor)
    {
        descriptor.Name("UIntOperationFilterInput");
        base.Configure(descriptor);
    }
}
