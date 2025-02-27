using Common.Context.Entities;

namespace Common.Models;

public class DeviceConnection
{
    public required MqttServer MqttServer { get; init; }
    public int NbPacket { get; set; }
    public DateTime? LastPacketReceivedDate { get; set; }
}