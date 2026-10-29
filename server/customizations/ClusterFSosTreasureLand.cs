namespace Server;

/// <summary>
/// Shattered Legacy (cc-P684, SoS swap S11). Where a treasure chest may not be buried on the Siege of Sosaria land, beyond
/// TreasureMap.ValidateLocation's own rules (no town, dungeon, champion spawn or house):
///
///   S14's unfinished land: the five closure boxes of shard-migration notes/cc-P673-sos-swap-plan.md section 2 (the
///     rooster island, the north-east snow isles, the bare land east of Haven, the cells round Echoback and Darkfathom,
///     and Glimmerwood's seam cells), which S14 fences until S19 finishes them.
///   Felucca's towns and dungeons: Felucca stands on the same SoS land as Trammel (cc-P675), but its regions stay
///     pinned's until S13 redraws them, so on Felucca a tile inside one of Trammel's town or dungeon regions is refused
///     too. On Trammel the map's own regions already say so.
///
/// TreasureMap.ValidateLocation (server/patches/TreasureMap.cs) asks Refused before its land checks, so every random
/// chest location of the revamp honours it. treasure.cfg's spots (server/data/treasure.cfg) were chosen by the same rules
/// (shard-migration notes/cc-P684-tools/treasure.py).
/// </summary>
public static class ClusterFSosTreasureLand
{
    /// <summary>P673 S14's closure boxes, inclusive (x1, y1, x2, y2).</summary>
    public static readonly (int X1, int Y1, int X2, int Y2)[] Closures =
    [
        (2840, 1324, 3039, 1523), // the rooster island
        (4224, 256, 4479, 767),   // the north-east snow isles
        (3072, 2304, 3199, 2559), // the bare land east of Haven
        (4096, 2560, 4223, 2815), // the cells round Echoback and Darkfathom
        (2688, 2688, 2943, 3071)  // Glimmerwood's seam cells
    ];

    public static bool InClosure(int x, int y)
    {
        foreach (var (x1, y1, x2, y2) in Closures)
        {
            if (x >= x1 && x <= x2 && y >= y1 && y <= y2)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when no chest may be buried at x, y on map for the reasons above (Trammel and Felucca only).</summary>
    public static bool Refused(int x, int y, int z, Map map)
    {
        if (map != Map.Trammel && map != Map.Felucca)
        {
            return false;
        }

        if (InClosure(x, y))
        {
            return true;
        }

        if (map == Map.Felucca)
        {
            var reg = Region.Find(new Point3D(x, y, z), Map.Trammel);
            return reg != null && (reg.IsPartOf<Regions.TownRegion>() || reg.IsPartOf<Regions.DungeonRegion>());
        }

        return false;
    }
}
