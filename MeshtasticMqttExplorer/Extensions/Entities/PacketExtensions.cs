using MeshtasticMqttExplorer.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeshtasticMqttExplorer.Extensions.Entities;

public static class PacketExtensions
{
    public static Task<Packet?> FindByPacketIdAsync(this IQueryable<Packet> packets, uint packetId)
    {
        return packetId > 0 ? packets.FirstOrDefaultAsync(n => n.PacketId == packetId) : null;
    }
}