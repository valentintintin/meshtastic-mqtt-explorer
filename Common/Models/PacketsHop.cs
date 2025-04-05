using Common.Context.Entities;

namespace Common.Models;

public class PacketsHop
{
    public required int Hop {get;set;}
    public required List<Packet> Packets { get; set; }
}