using Common.Context.Entities;
using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace Common.Extensions.Entities;

public static class PacketExtensions
{
    public static async Task<Packet?> FindByPacketIdAsync(this IQueryable<Packet> packets, uint packetId)
    {
        return packetId > 0 ? await packets.OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(n => n.PacketId == packetId) : null;
    }

    public static List<PacketsHop> GetAllPacketForPacketIdGroupedByHops(this IQueryable<Packet> packets, Packet packet)
    {
        return packets
            .Include(p => p.Gateway)
            .Include(p => p.MqttServer)
            .Include(p => p.RelayNodeNode)
            .Where(a => a.PacketId == packet.PacketId && a.FromId == packet.FromId && a.ToId == packet.ToId && a.FromId != a.GatewayId)
            .OrderByDescending(p => p.HopLimit)
            .ThenByDescending(p => p.RxSnr)
            .AsEnumerable()
            .GroupBy(a => a.HopStart - a.HopLimit ?? 0, (hop, list) => new PacketsHop
            {
                Hop = hop,
                Packets = list.OrderByDescending(l => l.RxSnr).ToList()
            })
            .OrderBy(a => a.Hop)
            .ToList();
    }
}