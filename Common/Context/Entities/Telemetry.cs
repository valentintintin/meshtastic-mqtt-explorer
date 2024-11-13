namespace Common.Context.Entities;

public class Telemetry : IEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public long NodeId { get; set; }
    public virtual Node Node { get; set; } = null!;
    
    public long? PacketId { get; set; }
    public virtual Packet? Packet { get; set; }
    
    public uint? BatteryLevel { get; set; }
    public float? Voltage { get; set; }
    public float? ChannelUtilization { get; set; }
    public float? AirUtilTx { get; set; }
    public TimeSpan? Uptime { get; set; }
    
    public float? Temperature { get; set; }
    public float? RelativeHumidity { get; set; }
    public float? BarometricPressure { get; set; }
    
    public float? Channel1Voltage { get; set; }
    public float? Channel1Current { get; set; }
    
    public float? Channel2Voltage { get; set; }
    public float? Channel2Current { get; set; }
    
    public float? Channel3Voltage { get; set; }
    public float? Channel3Current { get; set; }

    public required Meshtastic.Protobufs.Telemetry.VariantOneofCase Type;
}