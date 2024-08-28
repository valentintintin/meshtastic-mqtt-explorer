using MeshtasticMqttExplorer.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeshtasticMqttExplorer.Extensions.Entities;

public static class PacketExtensions
{
    public static async Task<Packet?> FindByPacketIdAsync(this IQueryable<Packet> packets, uint packetId)
    {
        return packetId > 0 ? await packets.FirstOrDefaultAsync(n => n.PacketId == packetId) : null;
    }
}