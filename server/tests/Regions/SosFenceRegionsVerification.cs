// SosFenceRegionsVerification.cs
//
// cc-P687 (SoS swap S14, parts 2 and 4): the fences round the land that is not ready (server/customizations/Regions/
// SosFenceRegions.cs). Notes: shard-migration notes/cc-P687-fences-and-seams.md. The map facts (each turn-back spot on
// walkable mainland, nothing a player needs inside, the seam dressing under every tile's walking Z) need the map and are
// in shard-migration tools/tests/test_sos_fences.py.
//
// One fact per fence (a Theory row each), on Trammel and on Felucca:
//   F1. Initialize registers it over its rectangles at FencePriority: Region.Find at its corners and centre is it, one
//       tile past a corner is not; no house may stand in it.
//   F2. A player's step in is refused; staff walk in; a step onto a boat's deck inside is allowed (boats sail by), and
//       a step off the deck onto its land is refused.
//   F3. A player inside, off a boat, is put back: where they came from when that was a deck or outside every fence,
//       otherwise on the fence's TurnBack spot, which lies outside every fence. On a deck they stay.
//   F4. Recall, gate, mark and teleport ending inside are refused with the fog line; recalling out is allowed; staff
//       are exempt.
// And for all of them:
//   F5. The five unfinished-land fences are ClusterFSosTreasureLand.Closures (S14's boxes, as cc-P684 copied them), and
//       the sealed dungeon is the Swamp of Desolation's own rectangles (S3's regions-trammel.json, end exclusive).

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Server;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SosFenceRegionsVerification : IDisposable
{
    private readonly Func<Map, Point3D, bool> _realBoats = SosFenceRegion.OnBoatAt;
    private readonly HashSet<(int, int)> _decks = [];
    private readonly List<Mobile> _made = [];

    public SosFenceRegionsVerification()
    {
        SosFenceRegion.Initialize();
        SosFenceRegion.OnBoatAt = (_, p) => _decks.Contains((p.X, p.Y));
    }

    public void Dispose()
    {
        SosFenceRegion.OnBoatAt = _realBoats;
        foreach (var m in _made)
        {
            m.Delete(); // no stray players for the saved-map check (FoldVerification) to report
        }
    }

    public static IEnumerable<object[]> Each() => SosFenceRegion.Fences.Select((_, i) => new object[] { i });

    private static IEnumerable<Map> Maps() => [Map.Trammel, Map.Felucca];

    private static SosFenceRegion On(int i, Map map) =>
        SosFenceRegion.Registered.Single(r => r.Spec == SosFenceRegion.Fences[i] && r.Map == map);

    private static Point3D Centre((int X1, int Y1, int X2, int Y2) q) => new((q.X1 + q.X2) / 2, (q.Y1 + q.Y2) / 2, 0);

    private PlayerMobile Player(AccessLevel level = AccessLevel.Player)
    {
        var pm = new PlayerMobile { Player = true, AccessLevel = level };
        _made.Add(pm);
        return pm;
    }

    [Theory]
    [MemberData(nameof(Each))]
    public void F1_RegisteredOverItsRectanglesAndNoHousing(int i)
    {
        var spec = SosFenceRegion.Fences[i];
        foreach (var map in Maps())
        {
            var r = On(i, map);
            Assert.Equal(spec.Name, r.Name);
            Assert.Equal(SosFenceRegion.FencePriority, r.Priority);
            foreach (var q in spec.Rects)
            {
                foreach (var p in new[] { new Point3D(q.X1, q.Y1, 0), new Point3D(q.X2, q.Y2, 0), Centre(q) })
                {
                    Assert.Same(r, Region.Find(p, map));
                    Assert.False(r.AllowHousing(Player(), p));
                }
            }

            var (x1, y1, x2, y2) = spec.Rects[0];
            foreach (var p in new[] { new Point3D(x1 - 1, y1 - 1, 0), new Point3D(x2 + 1, y2 + 1, 0) })
            {
                if (!SosFenceRegion.InAnyFence(p.X, p.Y))
                {
                    Assert.False(r.Contains(p), $"{spec.Name}: {p} is one past a corner");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Each))]
    public void F2_AStepInIsRefusedUnlessOntoADeckAndStaffWalkIn(int i)
    {
        var spec = SosFenceRegion.Fences[i];
        var q = spec.Rects[0];
        var inside = new Point3D(q.X1, (q.Y1 + q.Y2) / 2, 0);
        var outside = new Point3D(q.X1 - 1, inside.Y, 0);
        var next = new Point3D(q.X1 + 1, inside.Y, 0);
        foreach (var map in Maps())
        {
            var r = On(i, map);
            Assert.False(r.OnMoveInto(Player(), Direction.East, inside, outside));
            Assert.True(r.OnMoveInto(Player(AccessLevel.GameMaster), Direction.East, inside, outside));

            _decks.Add((inside.X, inside.Y));
            _decks.Add((next.X, next.Y));
            Assert.True(r.OnMoveInto(Player(), Direction.East, inside, outside)); // onto a deck: boats sail by
            Assert.Null(r.Placement(next, inside));                               // along the deck: stays
            _decks.Remove((next.X, next.Y));
            Assert.Equal(inside, r.Placement(next, inside));                      // off the deck: back onto it
            _decks.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(Each))]
    public void F3_SomeoneInsideIsPutBackOrOnTheTurnBackSpot(int i)
    {
        var spec = SosFenceRegion.Fences[i];
        var q = spec.Rects[0];
        var inside = Centre(q);
        var shore = new Point3D(q.X1 - 1, inside.Y, 0);
        Assert.False(SosFenceRegion.InAnyFence(spec.TurnBack.X, spec.TurnBack.Y), $"{spec.Name}: TurnBack {spec.TurnBack}");
        foreach (var map in Maps())
        {
            var r = On(i, map);
            Assert.False(r.Contains(spec.TurnBack));
            Assert.Equal(spec.TurnBack, r.Placement(inside, inside));    // a login inside, or a move from inside
            if (!SosFenceRegion.InAnyFence(shore.X, shore.Y))
            {
                Assert.Equal(shore, r.Placement(inside, shore));         // a move by other code from the shore
            }

            _decks.Add((inside.X, inside.Y));
            Assert.Null(r.Placement(inside, inside));                    // on a deck: stays
            _decks.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(Each))]
    public void F4_NoMagicEndsInside(int i)
    {
        var spec = SosFenceRegion.Fences[i];
        var inside = Centre(spec.Rects[0]);
        var outside = spec.TurnBack;
        foreach (var map in Maps())
        {
            var r = On(i, map);
            foreach (var type in new[] { TravelCheckType.RecallTo, TravelCheckType.GateTo, TravelCheckType.Mark, TravelCheckType.TeleportTo })
            {
                Assert.False(r.CheckTravel(Player(), inside, type, out var message), $"{spec.Name}: {type}");
                Assert.Equal(SosFenceRegion.FogLine, message.String);
                Assert.True(r.CheckTravel(Player(AccessLevel.GameMaster), inside, type, out _));
            }

            Assert.True(r.CheckTravel(Player(), outside, TravelCheckType.RecallFrom, out _));
            Assert.True(r.CheckTravel(Player(), outside, TravelCheckType.RecallTo, out _));
        }
    }

    [Fact]
    public void F5_TheFencesAreS14sBoxesAndTheSwampsOwnRectangles()
    {
        var land = SosFenceRegion.Fences.Where(f => !f.Dungeon).Select(f => f.Rects.Single()).ToArray();
        Assert.Equal(ClusterFSosTreasureLand.Closures, land);

        var path = FindData("ShatteredLegacy", "regions-trammel.json");
        Assert.NotNull(path);
        var swamp = JsonNode.Parse(File.ReadAllText(path!))!.AsArray()
            .Single(n => (string?)n!["Name"] == "Swamp of Desolation")!["Area"]!.AsArray()
            .Select(a => ((int)a!["x1"]!, (int)a["y1"]!, (int)a["x2"]! - 1, (int)a["y2"]! - 1))
            .ToArray();
        Assert.Equal(swamp, SosFenceRegion.Fences.Single(f => f.Dungeon).Rects);
    }

    private static string? FindData(params string[] rel)
    {
        var root = AppContext.BaseDirectory;
        var beside = Path.Combine([root, "Data", .. rel]);
        if (File.Exists(beside))
        {
            return beside;
        }

        for (var d = new DirectoryInfo(root); d != null; d = d.Parent)
        {
            var candidate = Path.Combine([d.FullName, "Distribution", "Data", .. rel]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
