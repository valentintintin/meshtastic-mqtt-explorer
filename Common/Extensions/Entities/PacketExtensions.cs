using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace Common.Extensions.Entities;

public static class PacketExtensions
{
    public static async Task<Packet?> FindByPacketIdAsync(this IQueryable<Packet> packets, uint packetId)
    {
        return packetId > 0 ? await packets.OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(n => n.PacketId == packetId) : null;
    }
}