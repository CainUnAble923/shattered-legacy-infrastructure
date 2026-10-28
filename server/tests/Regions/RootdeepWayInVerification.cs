// RootdeepWayInVerification.cs
//
// cc-P188: Rootdeep goes in (the way in from Old Haven, the spawners, the real map). Notes: shard-migration
// notes/cc-P188-cont-rootdeep-goes-in.md. The map itself ships as a patch of our changes
// (player-package/vendor/shattered-legacy-map/trammel.mapops; docs repo tools/tests/test_mapops.py proves it); these
// facts are the shard's side of the walk.
//
// Facts:
//   W1. cc-P678 (was P188's two ends, then P413's twelve-tile mouth): on the Siege of Sosaria map the way down is a
//       cellar hatch in the Second Bank of Haven's main hall (3618,2620, the region's Entrance): the seeder places the
//       hatch item (0x3E65, one below the floor so its top is within a step) and one teleporter down on its tile,
//       landing on the Landing's arrival (5193,1138), and one at the foot of the ladder (5193,1132) that lands three
//       tiles from the hatch in the same hall (3621,2620); a player on the hatch goes down, the ladder brings them up
//       beside it; a creature is not moved; the hatch and the landing are in New Haven, the ladder's foot in the Burrows;
//       a re-run places nothing again.
//   W2. The region rectangles match the built floors: the Vault's region is exactly the Vault's floor extent of the
//       approved layout (x 5142-5178, y 1132-1159), and the Burrows' region holds every zone's floor with the wall
//       ring and the ladder's wall tile inside it (the layout's extents, notes/art/rootdeep-mockup/layout.json, as
//       build_burrows.py prints them: land x 5140-5245, y 1131-1261), and the hatch is outside both and inside the
//       Second Bank of Haven, in New Haven.
// (W3-W5, the Old Haven crier and skeleton spawners, are HavenTownSpawnsVerification's since cc-P678; W6, a world
// P188's seeder seeded, went with the fresh test world of swap day.)
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RootdeepWayInVerification
{

    private readonly ITestOutputHelper _out;

    public RootdeepWayInVerification(ITestOutputHelper output)
    {
        _out = output;
        ImportSpawnersCommand.Configure(); // Register is idempotent (Commands.cs DoRegister).
        RootdeepVaultRegion.Initialize();
        ApprenticeTrainingRegion.Initialize();
        HavenTownRegions.Initialize();
    }

    private static PlayerMobile NewPlayer(Point3D at, AccessLevel level = AccessLevel.Player)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Walker", AccessLevel = level };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(at, Map.Trammel);
        return pm;
    }

    /// <summary>The items of type T standing exactly on a tile (the map's enumerator is not a LINQ sequence).</summary>
    private static List<T> ItemsAt<T>(Point3D p) where T : Item
    {
        var list = new List<T>();
        foreach (var item in Map.Trammel.GetItemsInRange<T>(p, 0))
        {
            if (!item.Deleted)
            {
                list.Add(item);
            }
        }

        return list;
    }

    private static void DeleteAll(IEnumerable<Item> items)
    {
        foreach (var i in items.ToList())
        {
            if (i is BaseSpawner s)
            {
                s.RemoveSpawns();
            }

            i.Delete();
        }
    }

    // ---------------------------------------------------------------- W1

    /// <summary>Everything a Seed may have placed, collected before anything is asserted (so a red fact leaves no litter).</summary>
    private static List<Item> Seeded()
    {
        var placed = new List<Item>();
        foreach (var r in ClusterFRootdeepSeeder.Teleporters)
        {
            placed.AddRange(ItemsAt<Teleporter>(r.At));
        }

        foreach (var row in ClusterFRootdeepSeeder.Spawners)
        {
            if (ClusterFRootdeepSeeder.FindSpawner(Map.Trammel, row) is { } s)
            {
                placed.Add(s);
            }
        }

        if (ClusterFRootdeepSeeder.FindCrawlway(Map.Trammel) is { } c)
        {
            placed.Add(c);
        }

        foreach (var item in Map.Trammel.GetItemsInRange(new Point3D(ClusterFRootdeepSeeder.AnkhAt.X, ClusterFRootdeepSeeder.AnkhAt.Y, 0), 2))
        {
            if (ClusterFAnkhs.IsWorkingAnkh(item))
            {
                placed.Add(item);
            }
        }

        if (ClusterFRootdeepSeeder.FindHatch(Map.Trammel) is { } hatch) // cc-P678
        {
            placed.Add(hatch);
        }

        foreach (var row in ClusterFRootdeepSeeder.FloorTraps) // cc-P275: the seeder lays these too
        {
            if (ClusterFRootdeepSeeder.FindFloorTrap(Map.Trammel, row) is { } t)
            {
                placed.Add(t);
            }
        }

        return placed;
    }

    // The mouth by hand: cc-P678's hatch, one tile (notes/cc-P678-tools/crosscheck.py found it clear of every layout).
    private static readonly (int X, int Y)[] Mouth = [(3618, 2620)];

    private static readonly Point3D BesideHatch = new(3621, 2620, 0);

    [Fact]
    public void W1_TheHatchTakesAPlayerDownAndTheLadderLandsBesideIt()
    {
        var mapBuilt = ClusterFRootdeepSeeder.MapBuilt;
        var placed = new List<Item>();
        var mobiles = new List<Mobile>();
        try
        {
            ClusterFRootdeepSeeder.MapBuilt = () => true;
            var arrival = new Point3D(ClusterFRootdeepSeeder.Arrival.X, ClusterFRootdeepSeeder.Arrival.Y, 0);
            var rows = ClusterFRootdeepSeeder.Teleporters;
            var down = rows.Where(r => r.To == arrival).ToArray();
            var ladder = ClusterFRootdeepSeeder.LadderUp;

            Assert.Equal(Mouth.Length + 1, rows.Length);
            Assert.Equal(Mouth, down.Select(r => (r.X, r.Y)).ToArray());
            Assert.All(Mouth, t => Assert.True(ClusterFRootdeepSeeder.IsMouth(t.X, t.Y)));
            Assert.Contains((OldHavenBurrowsRegion.HoleEntrance.X, OldHavenBurrowsRegion.HoleEntrance.Y), Mouth);
            Assert.Equal(rows[^1], ladder);
            Assert.Equal(new Point3D(5193, 1132, 0), ladder.At);
            Assert.Equal(BesideHatch, ladder.To);
            Assert.False(ClusterFRootdeepSeeder.IsMouth(ladder.To.X, ladder.To.Y));
            Assert.True(Mouth.Min(t => Math.Max(Math.Abs(t.X - ladder.To.X), Math.Abs(t.Y - ladder.To.Y))) >= 2);

            Assert.All(rows, r => Assert.Empty(ItemsAt<Teleporter>(r.At)));
            var first = ClusterFRootdeepSeeder.Seed(false);
            placed.AddRange(Seeded());

            Assert.Equal(rows.Length, first.Teleporters);
            Assert.Equal(0, first.TeleportersRetargeted);
            Assert.Equal((1, 0), (first.Hatches, first.HatchesPresent));
            var hatch = ClusterFRootdeepSeeder.FindHatch(Map.Trammel)!;
            Assert.Equal(new Point3D(3618, 2620, -1), hatch.Location);
            Assert.Equal(0x3E65, hatch.ItemID);
            Assert.False(hatch.Movable);
            Assert.Equal(ClusterFRootdeepSeeder.HatchName, hatch.Name);
            foreach (var r in rows)
            {
                var t = ClusterFRootdeepSeeder.FindTeleporter(Map.Trammel, r);
                Assert.NotNull(t);
                Assert.True(t.Active);
                Assert.False(t.Creatures);
                Assert.False(t.Visible);
            }

            // The hatch is in the Second Bank of Haven, in New Haven; the ladder's foot in the Burrows; its landing in the bank.
            Assert.All(down, r => Assert.Equal("the Second Bank of Haven", Region.Find(r.At, Map.Trammel).Name));
            Assert.All(down, r => Assert.True(Region.Find(r.At, Map.Trammel).IsPartOf(HavenTownRegions.TownName)));
            Assert.IsType<OldHavenBurrowsRegion>(Region.Find(ladder.At, Map.Trammel));
            Assert.Equal("the Second Bank of Haven", Region.Find(ladder.To, Map.Trammel).Name);

            var walker = NewPlayer(BesideHatch);
            mobiles.Add(walker);
            var up = ClusterFRootdeepSeeder.FindTeleporter(Map.Trammel, ladder)!;
            foreach (var r in down)
            {
                // Down the hatch: the step is cancelled, the teleport took it, and the player is on the Landing.
                var t = ClusterFRootdeepSeeder.FindTeleporter(Map.Trammel, r)!;
                Assert.False(t.OnMoveOver(walker), $"{r.X},{r.Y}");
                Assert.Equal(arrival, walker.Location);
                Assert.IsType<OldHavenBurrowsRegion>(Region.Find(walker.Location, Map.Trammel));

                // Up the ladder: beside the hatch, not on it.
                walker.MoveToWorld(new Point3D(5193, 1133, 0), Map.Trammel);
                Assert.False(up.OnMoveOver(walker));
                Assert.Equal(BesideHatch, walker.Location);
                Assert.Empty(ItemsAt<Teleporter>(walker.Location));
            }

            // A creature does not use them.
            var skeleton = new Skeleton();
            mobiles.Add(skeleton);
            skeleton.MoveToWorld(BesideHatch, Map.Trammel);
            var hole = ClusterFRootdeepSeeder.FindTeleporter(Map.Trammel, down[^1])!;
            Assert.True(hole.OnMoveOver(skeleton));
            Assert.Equal(BesideHatch, skeleton.Location);

            var again = ClusterFRootdeepSeeder.Seed(false);
            Assert.Equal((0, 0), (again.Teleporters, again.TeleportersRetargeted));
            Assert.Equal((0, 1), (again.Hatches, again.HatchesPresent));
            Assert.Equal(rows.Length, rows.Sum(r => ItemsAt<Teleporter>(r.At).Count));
        }
        finally
        {
            ClusterFRootdeepSeeder.MapBuilt = mapBuilt;
            DeleteAll(placed.Concat(Seeded()).Distinct());
            foreach (var m in mobiles)
            {
                m.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- W2

    // The approved layout's floors by zone (notes/art/rootdeep-mockup/layout.json; P141 section 3), inclusive.
    private static readonly (string Zone, int X1, int Y1, int X2, int Y2)[] Floors =
    [
        ("Landing", 5180, 1132, 5206, 1144), ("Landing tunnel", 5192, 1145, 5195, 1161),
        ("Vault", 5142, 1132, 5178, 1159),
        ("Warren", 5142, 1162, 5242, 1192), ("Brood Pit", 5212, 1168, 5240, 1188),
        ("Tangle", 5142, 1198, 5188, 1228), ("Dawnseam", 5194, 1198, 5242, 1228),
        ("Gloom", 5150, 1234, 5236, 1260), ("Gloom lair", 5184, 1238, 5226, 1256),
    ];

    [Fact]
    public void W2_TheRegionRectanglesMatchTheBuiltFloors()
    {
        var vault = RootdeepVaultRegion.Area;
        Assert.Equal((5142, 1132, 5178, 1159), (vault.Start.X, vault.Start.Y, vault.End.X - 1, vault.End.Y - 1));
        var (_, vx1, vy1, vx2, vy2) = Floors.Single(f => f.Zone == "Vault");
        Assert.Equal((vx1, vy1, vx2, vy2), (vault.Start.X, vault.Start.Y, vault.End.X - 1, vault.End.Y - 1));

        var burrows = OldHavenBurrowsRegion.Area;
        foreach (var (zone, x1, y1, x2, y2) in Floors)
        {
            // every floor tile plus its wall ring (one tile each side) is inside the Burrows
            Assert.True(burrows.Contains(new Point3D(x1 - 1, y1 - 1, 0)), zone);
            Assert.True(burrows.Contains(new Point3D(x2 + 1, y2 + 1, 0)), zone);
            Assert.Equal(zone == "Vault", RootdeepVault.IsInside(Map.Trammel, new Point3D(x1, y1, 0)));
            Assert.Equal(zone == "Vault", RootdeepVault.IsInside(Map.Trammel, new Point3D(x2, y2, 0)));
        }

        // the built land's extent (build_burrows.py: x 5140-5245, y 1131-1261) and the ladder's wall tile
        Assert.True(burrows.Contains(new Point3D(5140, 1131, 0)) && burrows.Contains(new Point3D(5245, 1261, 0)));
        Assert.True(burrows.Contains(new Point3D(5193, 1131, 0)));
        // the region itself is x 5136-5247, y 1128-1263 (P36's rectangle, four tiles beyond the built land on the west and north)
        Assert.True(burrows.Contains(new Point3D(5136, 1128, 0)) && burrows.Contains(new Point3D(5247, 1263, 0)));
        Assert.False(burrows.Contains(new Point3D(5135, 1128, 0)) || burrows.Contains(new Point3D(5136, 1127, 0)));
        Assert.False(burrows.Contains(new Point3D(5248, 1263, 0)) || burrows.Contains(new Point3D(5247, 1264, 0)));

        // the hatch: outside the Burrows and the Vault, inside the Second Bank of Haven in New Haven (cc-P678)
        var hole = OldHavenBurrowsRegion.HoleEntrance;
        Assert.Equal(new Point3D(ClusterFRootdeepSeeder.Hole.X, ClusterFRootdeepSeeder.Hole.Y, 0), hole);
        Assert.False(OldHavenBurrowsRegion.IsInside(Map.Trammel, hole));
        Assert.False(RootdeepVault.IsInside(Map.Trammel, hole));
        Assert.Equal("the Second Bank of Haven", Region.Find(hole, Map.Trammel).Name);
        Assert.True(Region.Find(hole, Map.Trammel).IsPartOf(HavenTownRegions.TownName));
        Assert.Equal(hole, OldHavenBurrowsRegion.Instance!.Entrance);
    }
}
