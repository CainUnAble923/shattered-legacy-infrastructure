// cc-P724 (Chase 10-28 12:02): Tutorial Island dressed with addons from the new downloads, all stock art (no client
// change). Ported to ModernUO from the ServUO scripts kept for reference in D:\UO\third-party\code (credits and terms
// in Tutorial/IsleAddons-SOURCE.txt):
//   - the wreck on the wake-up beach: Celestial's "Shipwrecks! East, West and North facing", the West one (its bow
//     points out to sea on the isle's west tip; Chase 10-28 12:18). cc-P741 (Chase 10-30: "shipwreck may need
//     orientation and both ship halves"): Celestial's West wreck is one half of a ship (20 of the art's 30 "East"
//     pieces). It is now the whole ship as OSI itself lays this art, the only time it does (Malas 1693-1708,
//     1635-1640; shard-migration notes/cc-P741-tools/survey_wrecks.py): both halves, 30 pieces each, broken open
//     toward each other across a swirl of water, with driftwood and seaweed round it. It lies across the west tip's
//     waterline the way a ship runs aground: one half in the shallows, the break in the surf, the other half up
//     the beach;
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
    /// <summary>
    /// The wreck's break, in the surf on the west tip's waterline (the shore runs down x 6626 here): the West half lies
    /// in the shallows (x 6617-6623), the East half up the beach (6626-6632), 6 tiles west of the wake-up spot
    /// (6638,3993). cc-P741; P724 had one half at 6627,3995,-3.
    /// </summary>
    public static readonly Point3D WreckSpot = new(6624, 3995, -4);

    /// <summary>P724's one-half wreck: an existing world's is swapped for the whole ship (cc-P741).</summary>
    public static readonly Point3D OldWreckSpot = new(6627, 3995, -3);

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
        // cc-P741: upgrade in place: a wreck that is not the whole ship at its spot (P724's half) goes, and the whole
        // ship is placed below like any missing addon
        var stale = new List<Item>();
        foreach (var item in map.GetItemsInRange(WreckSpot, 12))
        {
            if (item is WakeningIsleShipwreckAddon w && !w.Deleted &&
                (w.Location != WreckSpot || w.Components.Count != WakeningIsleShipwreckAddon.Pieces.Length))
            {
                stale.Add(w);
            }
        }

        var missing = new List<(Func<Item> Make, Point3D Spot)>();
        foreach (var row in rows)
        {
            if (!Has(map, row.Type, row.Spot, stale)) { missing.Add((row.Make, row.Spot)); }
        }

        var gate = FindGate(map);
        var lift = gate != null && gate.Z != GateOnFrame(map).Z;
        if (!dryRun)
        {
            foreach (var old in stale) { old.Delete(); }
            foreach (var (make, spot) in missing) { make().MoveToWorld(spot, map); }
            if (lift) { gate!.Z = GateOnFrame(map).Z; }
        }

        return $"Isle addons: {(dryRun ? "would place" : "placed")} {missing.Count}{(stale.Count > 0 ? $", replaced {stale.Count} old wreck" : "")}{(lift ? ", the gate stood on its frame" : "")}.";
    }

    private static bool Has(Map map, Type type, Point3D spot, List<Item> stale)
    {
        foreach (var item in map.GetItemsInRange(spot, 0))
        {
            if (!item.Deleted && type.IsInstanceOfType(item) && !stale.Contains(item)) { return true; }
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

/// <summary>
/// The whole wreck, both halves (cc-P741): OSI's own composition of the ShipwreckBow art on Malas, the "West" half's 30
/// pieces (0x47B8-0x47D5) and the "East" half's 30 (0x48A4-0x490F), its driftwood (0x0C2D-0x0C30), the waves and
/// swirl in the break (0x1FBC, 0x1FC5, 0x3492) and the seaweed (0x0D34) that falls on land. Offsets from the break.
/// Generated by shard-migration notes/cc-P741-tools/gen_wreck.py. Celestial's West wreck (P724) was the East half.
/// </summary>
[SerializationGenerator(0, false)]
public partial class WakeningIsleShipwreckAddon : BaseAddon
{
    public static readonly (int Id, int X, int Y, int Z)[] Pieces =
    [
        (18389, -7, -1, 0), (18388, -7, 0, 0), (18387, -7, 1, 0), (18375, -6, -2, 0), (18386, -6, -1, 0), (18385, -6, 0, 0),
        (18384, -6, 1, 0), (18383, -6, 2, 0), (18382, -5, -1, 0), (18381, -5, 0, 0), (18380, -5, 1, 0), (18379, -5, 2, 0),
        (18377, -4, -2, 0), (18376, -4, -1, 0), (18360, -4, 0, 0), (18374, -4, 1, 0), (18373, -4, 2, 0), (18378, -4, 3, 0),
        (18371, -3, -2, 0), (18370, -3, -1, 0), (18369, -3, 0, 0), (18368, -3, 1, 0), (18367, -3, 2, 0), (18366, -3, 3, 0),
        (18372, -2, -2, 0), (18363, -2, 1, 0), (18364, -2, 2, 0), (18365, -2, 3, 0), (18361, -1, 0, 0), (18362, -1, 1, 0),
        (18674, 2, 0, 0), (18683, 2, 1, 0), (18597, 3, -2, 0), (18669, 3, -1, 0), (18675, 3, 0, 0), (18696, 3, 3, 0),
        (18598, 4, -2, 0), (18670, 4, -1, 0), (18676, 4, 0, 0), (18684, 4, 1, 0), (18691, 4, 2, 0), (18697, 4, 3, 0),
        (18599, 5, -2, 0), (18671, 5, -1, 0), (18677, 5, 0, 0), (18685, 5, 1, 0), (18692, 5, 2, 0), (18702, 5, 3, 0),
        (18672, 6, -1, 0), (18680, 6, 0, 0), (18596, 6, 1, 0), (18693, 6, 2, 0), (18673, 7, -1, 0), (18681, 7, 0, 0),
        (18686, 7, 1, 0), (18694, 7, 2, 0), (18703, 7, 3, 0), (18682, 8, 0, 0), (18687, 8, 1, 0), (18695, 8, 2, 0),
        (3117, -5, 3, 0), (3117, -4, 3, 0), (3117, -4, 4, 0), (3117, -3, -3, 0), (3117, -3, 3, 0), (8124, -1, -1, 0),
        (13458, -1, -1, 0), (8124, -1, 0, 0), (13458, -1, 0, 0), (13458, -1, 1, 0), (8124, -1, 2, 0), (13458, -1, 2, 0),
        (13458, 0, -1, 0), (13458, 0, 0, 0), (13458, 0, 1, 0), (13458, 0, 2, 0), (8133, 1, 0, 0), (8133, 1, 2, 0),
        (3120, 3, 6, 0), (3118, 5, -8, 0), (3118, 5, -5, 0), (3380, 5, -4, 0), (3117, 5, 3, 0), (3118, 5, 7, 0),
        (3118, 6, -6, 0), (3380, 6, -4, 0), (3117, 6, -2, 0), (3117, 6, 2, 0), (3119, 6, 6, 0), (3117, 7, -1, 0),
        (3380, 7, 3, 0), (3119, 8, -9, 0), (3117, 8, 1, 0), (3118, 8, 9, 0)
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
