using HotChocolate.Data.Filters;
using MeshtasticMqttExplorer.GraphQl.Filters;

namespace MeshtasticMqttExplorer.GraphQl;

public class CustomFilterConvention : FilterConvention
{
    protected override void Configure(IFilterConventionDescriptor descriptor)
    {
        descriptor.AddDefaults();
        
        descriptor.BindRuntimeType<uint?, UIntOperationFilterInputType>();
        descriptor.BindRuntimeType<uint, UIntOperationFilterInputType>();
        descriptor.BindRuntimeType<ulong?, ULongOperationFilterInputType>();
        descriptor.BindRuntimeType<ulong, ULongOperationFilterInputType>();
    }
}
