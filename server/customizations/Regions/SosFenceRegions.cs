// cc-P687 (SoS swap S14, parts 2 and 4): the fences round the Siege of Sosaria land that is not ready for players.
//
// Chase's 1.0 scope (10-27): fence the author's unfinished land, and seal his dungeons until each has monsters. Six fences,
// each a region registered in code over its own rectangles on Trammel and on Felucca (Felucca stands on the same land,
// cc-P675), every start after the regions load (Initialize, as the Burrows and New Haven are):
//   - S14's five closure boxes (shard-migration notes/cc-P673-sos-swap-plan.md section 2; the same five as
//     ClusterFSosTreasureLand.Closures): the rooster island, the north-east snow isles, the bare land east of Haven, the cells
//     round Echoback and Darkfathom, and Glimmerwood's broken cliffs (part 3 fences those rather than dressing them);
//   - the Swamp of Desolation, the one dungeon of the author's that can be walked into (a walled valley on the surface;
//     notes/cc-P687-tools/dungeon_ways_in.py). His other 33 are reached only by the teleporters S6 (cc-P683) left out.
//     S18 removes a dungeon's fence when it is spawned: its row in Fences goes.
//
// What a fence does, to a player and to a player's pet or summon (staff are exempt, and the world's own creatures):
//   1. a step into it is refused, with the line "Thick fog rolls in; the way is lost.", so the walker stays on the shore
//      they came from;
//   2. boats sail by: a player on a boat's deck may cross it, and a step off the deck onto its land is refused the same way;
//   3. anyone who is inside anyway, off a boat (a login, a move by some other code), is put on the fence's TurnBack spot
//      with the same line: the nearest walkable mainland outside every fence (notes/cc-P687-tools/fences.py);
//   4. no recall, gate, mark or teleport ends inside it; leaving that way is allowed;
//   5. no house may be placed in it.
//
// Priority DefaultPriority + 10: above S3's no-housing and wild regions over the same land (pinned constructs every region
// as Dynamic, so the higher priority is the one Region.Find returns). No fence overlaps a town (fences.py checks).

using System;
using System.Collections.Generic;
using Server.Mobiles;
using Server.Multis;
using Server.Spells;

namespace Server.Regions;

public class SosFenceRegion : BaseRegion
{
    public const string FogLine = "Thick fog rolls in; the way is lost.";

    public const int FencePriority = DefaultPriority + 10;

    /// <summary>A fence: its name, its rectangles (x1, y1, x2, y2 inclusive), and where someone found inside is put.</summary>
    public record Fence(string Name, (int X1, int Y1, int X2, int Y2)[] Rects, Point3D TurnBack, bool Dungeon);

    public static readonly Fence[] Fences =
    [
        new("Unfinished land: the rooster island", [(2840, 1324, 3039, 1523)], new Point3D(2665, 1148, -1), false),
        new("Unfinished land: the north-east snow isles", [(4224, 256, 4479, 767)], new Point3D(4223, 511, 0), false),
        new("Unfinished land: the bare land east of Haven", [(3072, 2304, 3199, 2559)], new Point3D(2958, 2675, 0), false),
        new("Unfinished land: the cells round Echoback and Darkfathom", [(4096, 2560, 4223, 2815)], new Point3D(4401, 3078, -1), false),
        new("Unfinished land: Glimmerwood's broken cliffs", [(2688, 2688, 2943, 3071)], new Point3D(2944, 2879, 0), false),
        new(
            "Sealed: the Swamp of Desolation",
            [
                (1805, 984, 2116, 1214), (1987, 913, 2065, 1011), (2055, 943, 2098, 1000), (2075, 955, 2106, 994),
                (1970, 924, 1998, 989), (1938, 955, 1981, 997), (1930, 974, 1949, 994), (1960, 947, 1978, 962)
            ],
            new Point3D(1960, 912, 3),
            true
        )
    ];

    /// <summary>Every fence registered, on Trammel and Felucca.</summary>
    public static List<SosFenceRegion> Registered { get; } = [];

    /// <summary>Whether a boat's deck covers a point (BaseBoat.FindBoatAt); a test can stand in for it.</summary>
    public static Func<Map, Point3D, bool> OnBoatAt = (map, p) => map != null && BaseBoat.FindBoatAt(p, map) != null;

    public Fence Spec { get; }

    public SosFenceRegion(Fence spec, Map map) : base(spec.Name, map, FencePriority, ToArea(spec)) => Spec = spec;

    public static Rectangle3D[] ToArea(Fence spec)
    {
        var area = new Rectangle3D[spec.Rects.Length];
        for (var i = 0; i < area.Length; i++)
        {
            var (x1, y1, x2, y2) = spec.Rects[i];
            area[i] = new Rectangle3D(new Point3D(x1, y1, MinZ), new Point3D(x2 + 1, y2 + 1, MaxZ)); // the end is exclusive
        }

        return area;
    }

    public static void Initialize()
    {
        if (Registered.Count > 0)
        {
            return;
        }

        foreach (var map in new[] { Map.Trammel, Map.Felucca })
        {
            if (map == null)
            {
                continue;
            }

            foreach (var spec in Fences)
            {
                var r = new SosFenceRegion(spec, map);
                r.Register();
                Registered.Add(r);
            }
        }
    }

    /// <summary>True when <paramref name="x"/>, <paramref name="y"/> is inside any fence's rectangles.</summary>
    public static bool InAnyFence(int x, int y)
    {
        foreach (var f in Fences)
        {
            foreach (var (x1, y1, x2, y2) in f.Rects)
            {
                if (x >= x1 && x <= x2 && y >= y1 && y <= y2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>A player (not staff), or a creature a player owns or summoned: the fence holds them back.</summary>
    public static bool IsHeldBack(Mobile m) =>
        m switch
        {
            null                                         => false,
            PlayerMobile pm                              => pm.AccessLevel == AccessLevel.Player,
            BaseCreature { Controlled: true } bc         => bc.ControlMaster is PlayerMobile { AccessLevel: AccessLevel.Player },
            BaseCreature { Summoned: true } bc           => bc.SummonMaster is PlayerMobile { AccessLevel: AccessLevel.Player },
            _                                            => false
        };

    private static void Tell(Mobile m)
    {
        if (m is PlayerMobile)
        {
            m.SendMessage(FogLine);
        }
    }

    // 1 and 2: a step in from outside, unless onto a boat's deck
    public override bool OnMoveInto(Mobile m, Direction d, Point3D newLocation, Point3D oldLocation)
    {
        if (IsHeldBack(m) && !OnBoatAt(Map, newLocation))
        {
            Tell(m);
            return false;
        }

        return base.OnMoveInto(m, d, newLocation, oldLocation);
    }

    /// <summary>
    /// Where a held-back mobile now at <paramref name="now"/> inside the fence goes, or null when it may stay (on a boat).
    /// Off a boat it goes back where it came from when that was a boat's deck or outside every fence (a step off the deck,
    /// or a move by other code from the shore), otherwise to the TurnBack spot.
    /// </summary>
    public Point3D? Placement(Point3D now, Point3D from)
    {
        if (OnBoatAt(Map, now))
        {
            return null;
        }

        return OnBoatAt(Map, from) || !InAnyFence(from.X, from.Y) ? from : Spec.TurnBack;
    }

    // 2 and 3: a move inside, or an arrival by any means but a step
    public override void OnLocationChanged(Mobile m, Point3D oldLocation)
    {
        base.OnLocationChanged(m, oldLocation);

        if (!IsHeldBack(m) || m.Map != Map || !Contains(m.Location))
        {
            return;
        }

        var to = Placement(m.Location, oldLocation);
        if (to == null)
        {
            return;
        }

        // after the move in progress has finished (the mobile's own packets and region update come first)
        Timer.StartTimer(() =>
        {
            if (!m.Deleted && m.Map == Map && Contains(m.Location) && Placement(m.Location, to.Value) != null)
            {
                m.MoveToWorld(to.Value, Map);
                Tell(m);
            }
        });
    }

    // 4: no magic ends inside
    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;

        if (m == null || m.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        var endsInside = Contains(newLocation);
        switch (travelType)
        {
            case TravelCheckType.RecallTo or TravelCheckType.GateTo or TravelCheckType.Mark or TravelCheckType.TeleportTo
                when endsInside:
                message = FogLine;
                return false;
        }

        return true;
    }

    // 5: no houses
    public override bool AllowHousing(Mobile from, Point3D p) => false;
}
