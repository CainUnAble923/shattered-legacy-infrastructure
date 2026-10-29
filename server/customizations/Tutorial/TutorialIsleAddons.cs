// cc-P724 (Chase 10-28 12:02): Tutorial Island dressed with addons from the new downloads, all stock art (no client
// change). Ported to ModernUO from the ServUO scripts kept for reference in D:\UO\third-party\code (credits and terms
// in Tutorial/IsleAddons-SOURCE.txt):
//   - the wreck on the wake-up beach: Celestial's "Shipwrecks! East, West and North facing", the West one (its bow
//     points out to sea on the isle's west tip; Chase 10-28 12:18);
//   - the lighthouse on the point north of the dock: Milva's "New Lighthouse";
//   - the frame round the dock's moongate: Carl Hamilton's "MoongateStructure ADDON". The gate (TutorialTravelGate)
//     stands on the frame's dais, where the original leaves a blue tile for a moongate;
//   - a brass fountain on the practice bank's square: Lagatha's "New Fountions".
// Seeded with the isle's lessons (TutorialCorePlacement.SeedOn), so the Fold and every copy get them, a copy's reset
// removes them and P728's load top-up adds them to copies seeded before. Notes: shard-migration notes/cc-P724-isle-addons.md.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Tutorial;

public static class TutorialIsleAddons
{
    /// <summary>The wreck's centre on the west tip's waterline, a few tiles west of the wake-up spot (6638,3993).</summary>
    public static readonly Point3D WreckSpot = new(6627, 3995, -3);

    /// <summary>The lighthouse on the grass-and-sand point north of the dock, clear of the road (y 3989-3991).</summary>
    public static readonly Point3D LighthouseSpot = new(6815, 3984, 0);

    /// <summary>The fountain on the bank's square, east of the walk from the road to the bank door (x 6762).</summary>
    public static readonly Point3D FountainSpot = new(6768, 3984, 0);

    /// <summary>The frame's centre is the gate's spot; its outer ring is level with the dock's landing.</summary>
    public static Point3D FrameSpot(Map map) => TutorialWorld.Ground(map, TutorialWorld.Moongate);

    /// <summary>How far above the frame's base its dais top (the blue tile the gate stands on) is.</summary>
    public const int DaisHeight = 10;

    public static Point3D GateOnFrame(Map map) => new(TutorialWorld.Moongate.X, TutorialWorld.Moongate.Y, FrameSpot(map).Z + DaisHeight);

    /// <summary>Places what <paramref name="map"/> lacks and stands the gate on the frame. Returns the summary line.</summary>
    public static string Seed(Map map, bool dryRun)
    {
        var frameSpot = FrameSpot(map);
        var rows = new (Func<Item> Make, Type Type, Point3D Spot)[]
        {
            (() => new WakeningIsleShipwreckAddon(), typeof(WakeningIsleShipwreckAddon), WreckSpot),
            (() => new WakeningIsleLighthouseAddon(), typeof(WakeningIsleLighthouseAddon), LighthouseSpot),
            (() => new WakeningIsleMoongateFrameAddon(), typeof(WakeningIsleMoongateFrameAddon), frameSpot),
            (() => new WakeningIsleBrassFountain(), typeof(WakeningIsleBrassFountain), FountainSpot)
        };
        var missing = new List<(Func<Item> Make, Point3D Spot)>();
        foreach (var row in rows)
        {
            if (!Has(map, row.Type, row.Spot)) { missing.Add((row.Make, row.Spot)); }
        }

        var gate = FindGate(map);
        var lift = gate != null && gate.Z != GateOnFrame(map).Z;
        if (!dryRun)
        {
            foreach (var (make, spot) in missing) { make().MoveToWorld(spot, map); }
            if (lift) { gate!.Z = GateOnFrame(map).Z; }
        }

        return $"Isle addons: {(dryRun ? "would place" : "placed")} {missing.Count}{(lift ? ", the gate stood on its frame" : "")}.";
    }

    private static bool Has(Map map, Type type, Point3D spot)
    {
        foreach (var item in map.GetItemsInRange(spot, 0))
        {
            if (!item.Deleted && type.IsInstanceOfType(item)) { return true; }
        }
        return false;
    }

    public static TutorialTravelGate? FindGate(Map map)
    {
        foreach (var item in map.GetItemsInRange<TutorialTravelGate>(TutorialWorld.Ground(map, TutorialWorld.Moongate), 0))
        {
            if (!item.Deleted) { return item; }
        }
        return null;
    }

    /// <summary>Every tile an addon covers (tests: none may sit on a lesson's stop, sign, NPC or path).</summary>
    public static IEnumerable<Point2D> Footprint(Point3D centre, (int Id, int X, int Y, int Z)[] pieces)
    {
        foreach (var c in pieces) { yield return new Point2D(centre.X + c.X, centre.Y + c.Y); }
    }
}

/// <summary>Celestial's west-facing shipwreck (servuo.dev 1123), the bow toward the sea.</summary>
[SerializationGenerator(0, false)]
public partial class WakeningIsleShipwreckAddon : BaseAddon
{
    public static readonly (int Id, int X, int Y, int Z)[] Pieces =
    [
        (18694, 2, 2, 0), (18687, 3, 1, 0), (18702, 0, 3, 0), (18697, -1, 3, 0), (18696, -2, 3, 0), (18684, -1, 1, 0),
        (18691, -1, 2, 0), (18686, 2, 1, 0), (18693, 1, 2, 0), (18596, 1, 1, 0), (18692, 0, 2, 0), (18685, 0, 1, 0),
        (18682, 3, 0, 0), (18671, 0, -1, 0), (18681, 2, 0, 0), (18672, 1, -1, 0), (18681, 1, 0, 0), (18599, 0, -2, 0),
        (18680, 1, 0, 0), (18677, 0, 0, 0)
    ];

    [Constructible]
    public WakeningIsleShipwreckAddon()
    {
        foreach (var (id, x, y, z) in Pieces) { AddComponent(new AddonComponent(id) { Name = "shipwreck" }, x, y, z); }
    }
}

/// <summary>Milva's lighthouse (servuo.dev 157): three pieces, its door toward the camera.</summary>
[SerializationGenerator(0, false)]
public partial class WakeningIsleLighthouseAddon : BaseAddon
{
    public static readonly (int Id, int X, int Y, int Z)[] Pieces = [(18223, 0, 0, 0), (18212, 1, 0, 0), (18224, -1, 0, 0)];

    [Constructible]
    public WakeningIsleLighthouseAddon()
    {
        foreach (var (id, x, y, z) in Pieces) { AddComponent(new AddonComponent(id) { Name = "lighthouse" }, x, y, z); }
    }
}

/// <summary>
/// Carl Hamilton's moongate structure (servuo.dev 2275): stone steps all round (base height), a wooden floor at +5, the
/// dais at +10 with its blue tile, where the isle's gate stands. Three lanterns light it, as in the original.
/// </summary>
[SerializationGenerator(0, false)]
public partial class WakeningIsleMoongateFrameAddon : BaseAddon
{
    public static readonly (int Id, int X, int Y, int Z)[] Pieces =
    [
        (14186, 0, 0, 5), (464, 3, 2, 0), (465, 2, 3, 0), (466, 2, 2, 0), (1266, 0, 0, 10), (1997, 2, 2, 5),
        (1997, 2, 1, 5), (1997, 2, 0, 5), (1997, 2, -1, 5), (1997, 1, 2, 5), (1997, 0, 2, 5), (1935, 1, -1, 5),
        (1930, 1, 0, 5), (1934, 1, 1, 5), (1929, 0, 1, 5), (1931, 0, -1, 5), (1928, 0, 0, 5), (1928, 2, -1, 0),
        (1928, 2, 0, 0), (1928, 2, 1, 0), (1928, 2, 2, 0), (1928, 1, -1, 0), (1928, 1, 0, 0), (1928, 1, 1, 0),
        (1928, 1, 2, 0), (1928, 0, -1, 0), (1928, 0, 0, 0), (1928, 0, 1, 0), (1928, 0, 2, 0), (486, 2, 4, 0),
        (487, 4, 2, 0), (474, 2, 2, 18), (1930, 3, -1, 0), (1929, 2, 3, 0), (1929, 1, 3, 0), (1929, 0, 3, 0),
        (1930, 3, 1, 0), (1930, 3, 2, 0), (1930, 3, 0, 0), (487, 4, -3, 0), (464, 3, -3, 0), (465, 2, -3, 0),
        (1997, 2, -2, 5), (1997, 1, -2, 5), (1997, 0, -2, 5), (1928, 2, -2, 0), (1928, 1, -2, 0), (1928, 0, -2, 0),
        (474, 2, -3, 18), (1930, 3, -2, 0), (1931, 0, -3, 0), (1931, 1, -3, 0), (1931, 2, -3, 0), (486, -3, 4, 0),
        (465, -3, 3, 0), (464, -3, 2, 0), (1997, -1, 2, 5), (1997, -2, 2, 5), (1997, -2, 1, 5), (1997, -2, 0, 5),
        (1997, -2, -1, 5), (1933, -1, -1, 5), (1932, -1, 0, 5), (1936, -1, 1, 5), (1929, -2, 3, 0), (474, -3, 2, 18),
        (1932, -3, 0, 0), (1932, -3, -1, 0), (1932, -3, 1, 0), (1929, -1, 3, 0), (1932, -3, 2, 0), (1928, -2, 0, 0),
        (1928, -2, 1, 0), (1928, -1, 1, 0), (1928, -1, -1, 0), (1928, -1, 0, 0), (1928, -2, 2, 0), (1928, -2, -1, 0),
        (1928, -1, 2, 0), (463, -3, -3, 2), (1997, -1, -2, 5), (1997, -2, -2, 5), (474, -3, -3, 22), (1932, -3, -2, 0),
        (1931, -2, -3, 0), (1931, -1, -3, 0), (1928, -1, -2, 0), (1928, -2, -2, 0)
    ];

    // The original's AddComplexComponent pieces: a lantern (light type 2) and two hanging lanterns (light type 1).
    public static readonly (int Id, int X, int Y, int Z, LightType Light)[] Lights =
    [
        (2581, 2, -2, 21, (LightType)2), (2586, -2, 2, 18, (LightType)1), (2586, -2, -3, 18, (LightType)1)
    ];

    [Constructible]
    public WakeningIsleMoongateFrameAddon()
    {
        foreach (var (id, x, y, z) in Pieces) { AddComponent(new AddonComponent(id) { Name = "moongate frame" }, x, y, z); }
        foreach (var (id, x, y, z, light) in Lights) { AddComponent(new AddonComponent(id) { Light = light }, x, y, z); }
    }
}

/// <summary>Lagatha's brass fountain (servuo.dev 1516): stock art 0xA62B (0xA62A turned).</summary>
[Flippable(0xA62A, 0xA62B)]
[SerializationGenerator(0, false)]
public partial class WakeningIsleBrassFountain : Item
{
    [Constructible]
    public WakeningIsleBrassFountain() : base(0xA62B) { Movable = false; }

    public override string DefaultName => "brass fountain";
}
