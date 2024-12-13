using Common.Context.Entities;

namespace Recorder.Models;

public class MqttClientDto
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public int NbPacket { get; set; }
    public DateTime? LastPacketReceivedDate { get; set; }
}