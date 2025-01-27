using Common.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace Common.Extensions.Entities;

public static class PacketExtensions
{
    public static async Task<Packet?> FindByPacketIdAsync(this IQueryable<Packet> packets, uint packetId)
    {
        return packetId > 0 ? await packets.OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(n => n.PacketId == packetId) : null;
    }

    public static List<IGrouping<int, Packet>> GetAllPacketForPacketIdGroupedByHops(this IQueryable<Packet> packets, Packet packet)
    {
        return packets
            .Include(p => p.Gateway)
            .Include(p => p.MqttServer)
            .Where(a => a.PacketId == packet.PacketId && a.FromId == packet.FromId && a.ToId == packet.ToId && a.FromId != a.GatewayId)
            .ToList()
            .OrderByDescending(p => p.HopLimit)
            .ThenByDescending(p => p.RxSnr)
            .ToList()
            .GroupBy(a => a.HopStart - a.HopLimit ?? 0)
            .OrderBy(a => a.Key)
            .ToList();
    }
}