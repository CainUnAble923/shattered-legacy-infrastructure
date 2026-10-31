// cc-P708: the halls that moved onto the Siege of Sosaria land (Chase's picks of 2026-10-27 from P704; the places are
// Zigholtul's), and where each stood before. ClusterFGuildHallPlacement's place and clear take what a world still has at
// an old site away first: the hall's own sign, guild house and doors wherever they stand off its current site (and show
// again a stock sign one hid), and a guildmaster of the old type within a tile of the old post. Hand-written: the design
// file (design/guild-halls/halls.json) holds only where each hall is now.

using System;
using Server.Mobiles;

namespace Server;

public static class ClusterFGuildHallMoves
{
    public sealed record Moved(string Key, string FromTown, int MapIndex, Type NpcType, Point3D Npc);

    public static readonly Moved[] All =
    [
        new("league", "Britain (Castle British)", 1, typeof(LeagueRegistrar), new Point3D(1330, 1622, 50)),
        new("merchants", "Magincia", 1, typeof(MerchantGuildmaster), new Point3D(3711, 2179, 27)),
        new("healers", "Empath Abbey (Yew)", 1, typeof(HealerGuildmaster), new Point3D(637, 826, 0)),
        new("rangers", "Skara Brae", 1, typeof(OutridersGuildmaster), new Point3D(570, 2145, 0)),
        new("tinkers", "Vesper", 1, typeof(TinkerGuildmaster), new Point3D(2898, 740, 0)),
        new("custodians", "Britain (beside the First Bank)", 1, typeof(SanitationWarden), new Point3D(1456, 1710, 5))
    ];
}
