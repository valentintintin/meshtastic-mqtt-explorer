using System.Text.Json;
using System.Text.RegularExpressions;
using Meshtastic.Protobufs;

namespace MeshtasticMqttExplorer.Extensions;

public static class ServiceEnvelopeExtensions
{
    private static bool IsValidMeshPacket(MeshPacket? packet)
    {
        return packet?.PayloadVariantCase == MeshPacket.PayloadVariantOneofCase.Decoded &&
               packet.Decoded?.Payload != null;
    }

    public static TResult? GetPayload<TResult>(this MeshPacket? packet) where TResult : class
    {
        if (!IsValidMeshPacket(packet))
            return null;

        try
        {
            if (typeof(TResult) == typeof(AdminMessage) && packet?.Decoded?.Portnum == PortNum.AdminApp)
                return AdminMessage.Parser.ParseFrom(packet.Decoded?.Payload) as TResult;
            if (typeof(TResult) == typeof(RouteDiscovery) && packet?.Decoded?.Portnum == PortNum.TracerouteApp)
                return RouteDiscovery.Parser.ParseFrom(packet.Decoded?.Payload) as TResult;
            if (typeof(TResult) == typeof(NeighborInfo) && packet?.Decoded?.Portnum == PortNum.NeighborinfoApp)
                return NeighborInfo.Parser.ParseFrom(packet.Decoded?.Payload) as TResult;
            if (typeof(TResult) == typeof(Routing) && packet?.Decoded?.Portnum == PortNum.RoutingApp)
                return Routing.Parser.ParseFrom(packet.Decoded?.Payload) as TResult;
            if (typeof(TResult) == typeof(Position) && packet?.Decoded?.Portnum == PortNum.PositionApp)
                return Position.Parser.ParseFrom(packet.Decoded?.Payload) as TResult;
            if (typeof(TResult) == typeof(Telemetry) && packet?.Decoded?.Portnum == PortNum.TelemetryApp)
                return Telemetry.Parser.ParseFrom(packet.Decoded?.Payload) as TResult;
            if (typeof(TResult) == typeof(NodeInfo) && packet?.Decoded?.Portnum == PortNum.NodeinfoApp)
                return NodeInfo.Parser.ParseFrom(packet.Decoded?.Payload) as TResult;
            if (typeof(TResult) == typeof(User) && packet?.Decoded?.Portnum == PortNum.NodeinfoApp)
                return User.Parser.ParseFrom(packet.Decoded?.Payload) as TResult;
            if (typeof(TResult) == typeof(Waypoint) && packet?.Decoded?.Portnum == PortNum.WaypointApp)
                return Waypoint.Parser.ParseFrom(packet.Decoded?.Payload) as TResult;
            if (typeof(TResult) == typeof(MapReport) && packet?.Decoded?.Portnum == PortNum.MapReportApp)
                return MapReport.Parser.ParseFrom(packet.Decoded?.Payload) as TResult;
            if (typeof(TResult) == typeof(StoreAndForward) && packet?.Decoded?.Portnum == PortNum.StoreForwardApp)
                return StoreAndForward.Parser.ParseFrom(packet.Decoded?.Payload) as TResult;
            if (typeof(TResult) == typeof(string) && packet?.Decoded?.Portnum == PortNum.TextMessageApp)
                return packet.Decoded?.Payload.ToStringUtf8() as TResult;
            if (typeof(TResult) == typeof(string) && packet?.Decoded?.Portnum == PortNum.SerialApp)
                return packet.Decoded?.Payload.ToStringUtf8() as TResult;
            if (typeof(TResult) == typeof(string) && packet?.Decoded?.Portnum == PortNum.RangeTestApp)
                return packet.Decoded?.Payload.ToStringUtf8() as TResult;

            return null;
        }
        catch (Exception)
        {
            // todo log
            return null;
        }
    }

    public static object? GetPayload(this MeshPacket? packet)
    {
        if (!IsValidMeshPacket(packet))
            return null;

        try
        {
            return packet?.Decoded?.Portnum switch
            {
                PortNum.AdminApp => AdminMessage.Parser.ParseFrom(packet.Decoded?.Payload),
                PortNum.TracerouteApp => RouteDiscovery.Parser.ParseFrom(packet.Decoded?.Payload),
                PortNum.RoutingApp => Routing.Parser.ParseFrom(packet.Decoded?.Payload),
                PortNum.PositionApp => Position.Parser.ParseFrom(packet.Decoded?.Payload),
                PortNum.TelemetryApp => Telemetry.Parser.ParseFrom(packet.Decoded?.Payload),
                PortNum.NodeinfoApp => User.Parser.ParseFrom(packet.Decoded?.Payload),
                PortNum.WaypointApp => Waypoint.Parser.ParseFrom(packet.Decoded?.Payload),
                PortNum.NeighborinfoApp => NeighborInfo.Parser.ParseFrom(packet.Decoded?.Payload),
                PortNum.StoreForwardApp => StoreAndForward.Parser.ParseFrom(packet.Decoded?.Payload),
                PortNum.MapReportApp => MapReport.Parser.ParseFrom(packet.Decoded?.Payload),
                PortNum.TextMessageApp => packet.Decoded?.Payload.ToStringUtf8(),
                PortNum.SerialApp => packet.Decoded?.Payload.ToStringUtf8(),
                PortNum.RangeTestApp => packet.Decoded?.Payload.ToStringUtf8(),
                _ => packet?.Decoded?.Payload
            };
        }
        catch (Exception) 
        {
            // todo log 
            return JsonSerializer.Serialize(packet?.Decoded?.Payload);
        }
    }
}