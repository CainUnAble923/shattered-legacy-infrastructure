using System;
using System.Collections.Generic;

namespace Server;

/// <summary>
/// Shattered Legacy (cc-P681, SoS swap S9). Pinned's stock spawners on the Siege of Sosaria land, which Trammel and
/// Felucca both read on swap day (S2). Every stock spawner of the files the shard imports (post-uoml/** and shared/**,
/// 4,226 on the two facets) was graded on the SoS map files by shard-migration notes/cc-P681-tools/regrade.py:
/// its home on walkable ground (a floor within 5 Z for one that stands still), its range on land (or water, for sea
/// life), and still serving its purpose (a shopkeeper whose building is gone, a wild spawner in one of the author's new
/// towns or on changed habitat). Most stay. The ones that fail were searched for within 20 tiles (40 for a town's
/// banker, healer, smith or provisioner) and move (Moves), or nothing matched and they are left out (Drops). The ledger
/// with a reason per spawner is notes/cc-P681-tools/out/ledger.csv; the table is ClusterFSosSpawnPlacementsTable.cs.
///
/// The importer asks Place for every spawner it reads, before building it (server/patches/
/// ImportSpawners-sos-placements.patch): a moved one is built at its new home, as if the data said so; a dropped one is
/// not built. Spawners are found by their data guid and facet, never by coordinates (several share a tile). A fresh test
/// world is built on swap day (Chase 2026-10-27), so nothing here moves spawners an existing world already has.
/// </summary>
public static partial class ClusterFSosSpawnPlacements
{
    private static Dictionary<(Map, Guid), Point3D> _moves;
    private static HashSet<(Map, Guid)> _drops;

    private static Map FacetMap(string facet) => facet == "Felucca" ? Map.Felucca : Map.Trammel;

    private static void Build()
    {
        var moves = new Dictionary<(Map, Guid), Point3D>(Moves.Length);
        foreach (var (facet, guid, x, y, z) in Moves)
        {
            moves.Add((FacetMap(facet), Guid.Parse(guid)), new Point3D(x, y, z));
        }

        var drops = new HashSet<(Map, Guid)>(Drops.Length);
        foreach (var (facet, guid) in Drops)
        {
            drops.Add((FacetMap(facet), Guid.Parse(guid)));
        }

        _moves = moves;
        _drops = drops;
    }

    /// <summary>The new home of a stock spawner on that facet, or null when it stays.</summary>
    public static Point3D? NewHome(Map map, Guid guid)
    {
        if (_moves == null)
        {
            Build();
        }

        return _moves.TryGetValue((map, guid), out var p) ? p : null;
    }

    public static bool IsDropped(Map map, Guid guid)
    {
        if (_drops == null)
        {
            Build();
        }

        return _drops.Contains((map, guid));
    }

    /// <summary>
    /// Import hook: false when the spawner is left out on the SoS land; otherwise true, with location set to its new home
    /// when it moves. Only Trammel and Felucca, and only stock guids, are in the table.
    /// </summary>
    public static bool Place(Guid guid, Map map, ref Point3D location)
    {
        if (map != Map.Trammel && map != Map.Felucca)
        {
            return true;
        }

        if (IsDropped(map, guid))
        {
            return false;
        }

        if (NewHome(map, guid) is { } home)
        {
            location = home;
        }

        return true;
    }
}
