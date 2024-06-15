using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Google.Protobuf;
using Meshtastic.Protobufs;

namespace MeshtasticMqttExplorer.Context.Entities;

public class Packet : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public long ChannelId { get; set; }
    public virtual Channel Channel { get; set; } = null!;
    
    public required long PacketId { get; set; }
    
    public long GatewayId { get; set; }
    public virtual Node Gateway { get; set; } = null!;
    
    public long? GatewayPositionId { get; set; }
    public virtual Position? GatewayPosition { get; set; }
    
    public long FromId { get; set; }
    public virtual Node From { get; set; } = null!;

    public long ToId { get; set; }
    public virtual Node To { get; set; } = null!;

    public uint? ChannelIndex { get; set; }
    public required bool Encrypted { get; set; }
    
    public float? RxSnr { get; set; }
    public float? RxRssi { get; set; }
    public DateTimeOffset? RxTime { get; set; }
    
    public uint? HopStart { get; set; }
    public uint? HopLimit { get; set; }
    public bool? WantAck { get; set; }
    public MeshPacket.Types.Priority? Priority { get; set; }
    
    public PortNum? PortNum { get; set; }
    public byte[]? Payload { get; set; }
    public ByteString? ByteString => Payload != null ? ByteString.CopyFrom(Payload) : null;
    
    public string? PayloadJson { get; set; }
    public bool? WantResponse { get; set; }
    public long? ReplyId { get; set; }
    
    public string? MqttServer { get; set; }
    public string? MqttTopic { get; set; }
}