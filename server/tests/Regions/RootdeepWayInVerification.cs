// RootdeepWayInVerification.cs
//
// cc-P188: Rootdeep goes in (the way in from Old Haven, the spawners, the real map). Notes: shard-migration
// notes/cc-P188-cont-rootdeep-goes-in.md. The map itself ships as a patch of our changes
// (player-package/vendor/shattered-legacy-map/trammel.mapops; docs repo tools/tests/test_mapops.py proves it); these
// facts are the shard's side of the walk.
//
// Facts:
//   W1. cc-P413 (was P188's two ends): the seeder places a teleporter on every tile of the burrow's mouth (x 3617-3619,
//       y 2617-2620, the dark core of the map patch's sinkhole; P188's hole 3619,2620, the region's Entrance, is one of
//       them), each landing on the Landing's arrival (5193,1138), and one at the foot of the ladder (5193,1132) that
//       lands beside the mound (3619,2623, outside the bank's collapsed wall, not a mouth tile and two tiles from the
//       nearest); a player stepping on each mouth tile goes down, and the ladder brings them up beside the mound; a
//       creature is not moved; the mouth is inside Old Haven Training (cc-P189), the ladder's foot in the Burrows; a
//       re-run places nothing again.
//   W2. The region rectangles match the built floors: the Vault's region is exactly the Vault's floor extent of the
//       approved layout (x 5142-5178, y 1132-1159), and the Burrows' region holds every zone's floor with the wall
//       ring and the ladder's wall tile inside it (the layout's extents, notes/art/rootdeep-mockup/layout.json, as
//       build_burrows.py prints them: land x 5140-5245, y 1131-1261), and the hole is outside both and inside the
//       Old Haven Training region.
//   W3. The crier's source is gone: the shipped shared/trammel/TownsPeople.json still holds pinned's Old Haven town
//       crier spawner (guid 8b75bcf8..., 3652,2619, one TownCrier); the import hook refuses exactly it (Admit); a real
//       import of the file places every other spawner of the file on Trammel and not that one.
//   W4. Spawner (113) still spawns on a fresh world (kept on purpose): the import hook admits it, a real import of
//       post-uoml/trammel/Outdoors.json places it at 3651,2627 with its 10 Skeleton entry, and it spawns skeletons.
//   W5. [ClusterFOldHavenRetire removes the crier spawner and its crier from a world that has them (dry run first,
//       changing nothing), and leaves the skeleton spawner alone.
//   W6. cc-P413: a world P188's seeder already seeded (the hole's teleporter and the ladder's, landing at 3619,2621)
//       gets the other eleven mouth teleporters, and its ladder teleporter is pointed at the new landing, not doubled:
//       the dry run says so and changes nothing; a re-run then finds everything in place.
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
    private const string TownsPeopleFile = "shared/trammel/TownsPeople.json";
    private const string OutdoorsFile = "post-uoml/trammel/Outdoors.json";
    private static readonly Point3D SkeletonSpawnerAt = new(3651, 2627, 0);

    private readonly ITestOutputHelper _out;

    public RootdeepWayInVerification(ITestOutputHelper output)
    {
        _out = output;
        ImportSpawnersCommand.Configure(); // Register is idempotent (Commands.cs DoRegister).
        RootdeepVaultRegion.Initialize();
        ApprenticeTrainingRegion.Initialize();
    }

    private static PlayerMobile NewPlayer(Point3D at, AccessLevel level = AccessLevel.Player)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Walker", AccessLevel = level };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(at, Map.Trammel);
        return pm;
    }

    private static void Import(Mobile dev, string pattern) =>
        Assert.True(CommandSystem.Handle(dev, $"{CommandSystem.Prefix}GenerateSpawners Data/Spawns/{pattern}"));

    private static List<BaseSpawner> InWorld(IEnumerable<ClusterFStockSpawnData.StockSpawner> stock)
    {
        var guids = stock.Select(s => s.Guid).ToHashSet();
        return World.Items.Values.OfType<BaseSpawner>().Where(s => !s.Deleted && s.Map == Map.Trammel && guids.Contains(s.Guid)).ToList();
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

        foreach (var row in ClusterFRootdeepSeeder.FloorTraps) // cc-P275: the seeder lays these too
        {
            if (ClusterFRootdeepSeeder.FindFloorTrap(Map.Trammel, row) is { } t)
            {
                placed.Add(t);
            }
        }

        return placed;
    }

    // The mouth by hand, row by row (the map patch's dark core: notes/cc-P413-tools/statics-placed.csv).
    private static readonly (int X, int Y)[] Mouth =
    [
        (3617, 2617), (3618, 2617), (3619, 2617),
        (3617, 2618), (3618, 2618), (3619, 2618),
        (3617, 2619), (3618, 2619), (3619, 2619),
        (3617, 2620), (3618, 2620), (3619, 2620),
    ];

    private static readonly Point3D BesideMound = new(3619, 2623, 0);

    [Fact]
    public void W1_EveryMouthTileTakesAPlayerDownAndTheLadderLandsBesideTheMound()
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
            Assert.Equal(BesideMound, ladder.To);
            Assert.False(ClusterFRootdeepSeeder.IsMouth(ladder.To.X, ladder.To.Y));
            Assert.True(Mouth.Min(t => Math.Max(Math.Abs(t.X - ladder.To.X), Math.Abs(t.Y - ladder.To.Y))) >= 2);

            Assert.All(rows, r => Assert.Empty(ItemsAt<Teleporter>(r.At)));
            var first = ClusterFRootdeepSeeder.Seed(false);
            placed.AddRange(Seeded());

            Assert.Equal(rows.Length, first.Teleporters);
            Assert.Equal(0, first.TeleportersRetargeted);
            foreach (var r in rows)
            {
                var t = ClusterFRootdeepSeeder.FindTeleporter(Map.Trammel, r);
                Assert.NotNull(t);
                Assert.True(t.Active);
                Assert.False(t.Creatures);
                Assert.False(t.Visible);
            }

            // The mouth is in Old Haven Training, the ladder's foot in the Burrows, its landing back in Old Haven Training.
            Assert.All(down, r => Assert.Equal(ApprenticeTraining.OldHavenTraining, Region.Find(r.At, Map.Trammel).Name));
            Assert.IsType<OldHavenBurrowsRegion>(Region.Find(ladder.At, Map.Trammel));
            Assert.Equal(ApprenticeTraining.OldHavenTraining, Region.Find(ladder.To, Map.Trammel).Name);

            var walker = NewPlayer(BesideMound);
            mobiles.Add(walker);
            var up = ClusterFRootdeepSeeder.FindTeleporter(Map.Trammel, ladder)!;
            foreach (var r in down)
            {
                // Down from this mouth tile: the step is cancelled, the teleport took it, and the player is on the Landing.
                var t = ClusterFRootdeepSeeder.FindTeleporter(Map.Trammel, r)!;
                Assert.False(t.OnMoveOver(walker), $"{r.X},{r.Y}");
                Assert.Equal(arrival, walker.Location);
                Assert.IsType<OldHavenBurrowsRegion>(Region.Find(walker.Location, Map.Trammel));

                // Up the ladder: beside the mound, on no mouth tile.
                walker.MoveToWorld(new Point3D(5193, 1133, 0), Map.Trammel);
                Assert.False(up.OnMoveOver(walker));
                Assert.Equal(BesideMound, walker.Location);
                Assert.Empty(ItemsAt<Teleporter>(walker.Location));
            }

            // A creature does not use them.
            var skeleton = new Skeleton();
            mobiles.Add(skeleton);
            skeleton.MoveToWorld(BesideMound, Map.Trammel);
            var hole = ClusterFRootdeepSeeder.FindTeleporter(Map.Trammel, down[^1])!;
            Assert.True(hole.OnMoveOver(skeleton));
            Assert.Equal(BesideMound, skeleton.Location);

            var again = ClusterFRootdeepSeeder.Seed(false);
            Assert.Equal((0, 0), (again.Teleporters, again.TeleportersRetargeted));
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

        // the hole: outside the Burrows and the Vault, inside Old Haven Training
        var hole = OldHavenBurrowsRegion.HoleEntrance;
        Assert.False(OldHavenBurrowsRegion.IsInside(Map.Trammel, hole));
        Assert.False(RootdeepVault.IsInside(Map.Trammel, hole));
        Assert.Equal(ApprenticeTraining.OldHavenTraining, Region.Find(hole, Map.Trammel).Name);
        Assert.Equal(hole, OldHavenBurrowsRegion.Instance!.Entrance);
    }

    // ---------------------------------------------------------------- W3

    [Fact]
    public void W3_TheCriersSourceIsGone()
    {
        var stock = ClusterFStockSpawnData.LoadRelative(TownsPeopleFile, Map.Trammel);
        Assert.True(stock.Count > 10, $"{stock.Count} spawners in {TownsPeopleFile}"); // 48 on Trammel in pinned's file
        var crier = ClusterFOldHavenStockSpawns.CrierStock();
        Assert.NotNull(crier);
        Assert.Equal(ClusterFOldHavenStockSpawns.CrierAt, crier.Location);
        Assert.Equal(["TownCrier"], crier.Names);

        // the hook: that one refused, every other spawner of the file admitted
        var dev = NewPlayer(new Point3D(1300, 1300, 0), AccessLevel.Developer);
        var placed = new List<Item>();
        try
        {
            DeleteAll(InWorld(stock));
            Import(dev, TownsPeopleFile);
            var made = InWorld(stock);
            placed.AddRange(made);
            _out.WriteLine($"{made.Count} of {stock.Count} placed");
            Assert.Equal(stock.Count - 1, made.Count);
            Assert.DoesNotContain(made, s => s.Guid == ClusterFOldHavenStockSpawns.CrierSpawner);
            Assert.Empty(ItemsAt<Spawner>(ClusterFOldHavenStockSpawns.CrierAt));
            Assert.Null(ClusterFOldHavenStockSpawns.FindCrierSpawner());
            Assert.All(made, s => Assert.True(ClusterFOldHavenStockSpawns.Admit(s, Map.Trammel)));
        }
        finally
        {
            DeleteAll(placed);
            dev.Delete();
        }
    }

    // ---------------------------------------------------------------- W4

    [Fact]
    public void W4_SpawnerOneThirteenStillSpawnsOnAFreshWorld()
    {
        var stock = ClusterFStockSpawnData.LoadRelative(OutdoorsFile, Map.Trammel);
        var skeletons = Assert.Single(stock, s => s.Location == SkeletonSpawnerAt);
        Assert.Equal(["Skeleton"], skeletons.Names);

        var dev = NewPlayer(new Point3D(1300, 1300, 0), AccessLevel.Developer);
        var placed = new List<Item>();
        try
        {
            DeleteAll(InWorld(stock));
            Import(dev, OutdoorsFile);
            placed.AddRange(InWorld(stock));
            var spawner = Assert.Single(ItemsAt<Spawner>(SkeletonSpawnerAt));
            Assert.Equal(skeletons.Guid, spawner.Guid);
            Assert.True(ClusterFOldHavenStockSpawns.Admit(spawner, Map.Trammel));
            Assert.True(ClusterFDespiseStockSpawns.Admit(spawner, Map.Trammel));
            Assert.Equal(10, spawner.Count);
            Assert.Equal("Skeleton", Assert.Single(spawner.Entries).SpawnedName);

            spawner.Respawn();
            Assert.Equal(10, spawner.Spawned.Count);
            Assert.All(spawner.Spawned.Keys, m => Assert.IsType<Skeleton>(m));
            Assert.All(spawner.Spawned.Keys.OfType<Mobile>(), m => Assert.True(m.InRange(SkeletonSpawnerAt, spawner.HomeRange), m.Location.ToString()));
        }
        finally
        {
            DeleteAll(placed);
            dev.Delete();
        }
    }

    // ---------------------------------------------------------------- W5

    [Fact]
    public void W5_RetireRemovesTheCrierSpawnerAndItsCrierAndLeavesTheSkeletons()
    {
        var crierStock = ClusterFOldHavenStockSpawns.CrierStock()!;
        // as the seeder builds spawners: bounds after the move, so they centre on the spot (BaseSpawner.cs:189-219)
        var crier = new Spawner(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), 0, default, "TownCrier") { Guid = crierStock.Guid };
        crier.MoveToWorld(crierStock.Location, Map.Trammel);
        crier.HomeRange = 1;
        crier.Respawn();
        var skeletons = new Spawner(10, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), 0, default, "Skeleton");
        skeletons.MoveToWorld(SkeletonSpawnerAt, Map.Trammel);
        skeletons.HomeRange = 8;
        skeletons.Respawn();
        try
        {
            var spawnedCrier = Assert.IsType<TownCrier>(Assert.Single(crier.Spawned.Keys));
            Assert.Same(crier, ClusterFOldHavenStockSpawns.FindCrierSpawner());

            var dry = ClusterFOldHavenSeeder.Retire(true);
            dry.ForEach(l => _out.WriteLine($"dry: {l}"));
            Assert.False(crier.Deleted);
            Assert.False(spawnedCrier.Deleted);
            Assert.Contains(dry, l => l.Contains("(3652,2619,0)") && l.Contains("TownCrier"));

            var real = ClusterFOldHavenSeeder.Retire(false);
            real.ForEach(l => _out.WriteLine($"real: {l}"));
            Assert.True(crier.Deleted);
            Assert.True(spawnedCrier.Deleted);
            Assert.False(skeletons.Deleted);
            Assert.Equal(10, skeletons.Spawned.Count);
            Assert.Null(ClusterFOldHavenStockSpawns.FindCrierSpawner());
            Assert.Equal("ClusterF Old Haven retire complete: deleted 1 spawners with 1 creatures, and 0 creatures with no spawner.", real[^1]);
        }
        finally
        {
            DeleteAll([crier, skeletons]);
        }
    }

    // ---------------------------------------------------------------- W6

    [Fact]
    public void W6_AWorldSeededByP188GetsTheWholeMouthAndItsLadderPointedAtTheNewLandingNotDoubled()
    {
        var mapBuilt = ClusterFRootdeepSeeder.MapBuilt;
        var placed = new List<Item>();
        try
        {
            ClusterFRootdeepSeeder.MapBuilt = () => true;
            var arrival = new Point3D(ClusterFRootdeepSeeder.Arrival.X, ClusterFRootdeepSeeder.Arrival.Y, 0);

            // What P188's seeder placed (its labels and destinations, ClusterFRootdeepSeeder before cc-P413).
            var hole = ClusterFRootdeepSeeder.PlaceTeleporter(Map.Trammel,
                new ClusterFRootdeepSeeder.TeleporterRow("the hole down to the Landing", 3619, 2620, arrival.X, arrival.Y));
            var oldLadder = ClusterFRootdeepSeeder.PlaceTeleporter(Map.Trammel,
                new ClusterFRootdeepSeeder.TeleporterRow("the rope ladder up to Old Haven", 5193, 1132, 3619, 2621));
            placed.Add(hole);
            placed.Add(oldLadder);

            var dry = ClusterFRootdeepSeeder.Seed(true);
            Assert.Equal((Mouth.Length - 1, 1), (dry.Teleporters, dry.TeleportersRetargeted));
            Assert.Equal(new Point3D(3619, 2621, 0), oldLadder.PointDest); // the dry run changes nothing

            var real = ClusterFRootdeepSeeder.Seed(false);
            placed.AddRange(Seeded());
            Assert.Equal((Mouth.Length - 1, 1), (real.Teleporters, real.TeleportersRetargeted));

            var ladderTile = ItemsAt<Teleporter>(ClusterFRootdeepSeeder.LadderUp.At);
            Assert.Same(oldLadder, Assert.Single(ladderTile));
            Assert.Equal(BesideMound, oldLadder.PointDest);
            Assert.Same(hole, Assert.Single(ItemsAt<Teleporter>(new Point3D(3619, 2620, 0))));
            Assert.All(Mouth, t => Assert.Single(ItemsAt<Teleporter>(new Point3D(t.X, t.Y, 0))));

            var again = ClusterFRootdeepSeeder.Seed(false);
            Assert.Equal((0, 0), (again.Teleporters, again.TeleportersRetargeted));
        }
        finally
        {
            ClusterFRootdeepSeeder.MapBuilt = mapBuilt;
            DeleteAll(placed.Concat(Seeded()).Distinct());
        }
    }
}
