// cc-P194 Covetous Void Spawn: [SetupNewCovetous and what it places, the level-3 population, a battle for the
// Void Pool from the first wave to the lost walls, the void creatures, the rewards and artifacts, and the
// scoreboards' save, which a green build says nothing about.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh. See notes/s4-test-route.md and notes/s8-test-route.md.
//
// The test host has no map files (land at z 0 everywhere, so every spawn point is good), loads no regions.json (the
// static "Covetous" region of both facets is registered here where a fact needs it; the Void Pool region must win
// over it, the Despise rule), installs no skill callbacks, and runs every fact in one world in no fixed order, so
// each fact clears every Covetous object first and last. Hit points are read on the tick the damage lands (D15);
// timers are made after Arm (D16). Notes: shard-migration/notes/cc-P194-port-wrong-and-covetous-void.md.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Points;
using Server.Engines.Spawners;
using Server.Engines.VoidPool;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;
using VoidPoolPoints = Server.Engines.Points.VoidPool;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CovetousVoidSpawnVerification
{
    private readonly ITestOutputHelper _out;

    public CovetousVoidSpawnVerification(ITestOutputHelper output) => _out = output;

    private static readonly Type[] VoidTypes = VoidPoolController.SpawnTable.SelectMany(row => row).ToArray();

    private static PlayerMobile NewPlayer(Point3D loc, Map map = null)
    {
        var pm = new PlayerMobile();
        pm.AddItem(new Backpack());
        pm.MoveToWorld(loc, map ?? Map.Trammel);
        pm.Player = true;
        pm.Hits = pm.HitsMax; // a fresh PlayerMobile has 0 hits; the first point of damage would kill it
        return pm;
    }

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    private static void EnsureCorpses()
    {
        if (Mobile.CreateCorpseHandler == null)
        {
            Corpse.Initialize();
        }
    }

    private static void EnsureRewards()
    {
        if (VoidPoolRewards.Rewards == null)
        {
            VoidPoolRewards.Initialize();
        }
    }

    // regions.json: "Covetous", DungeonRegion, priority 50, {5376,1793}-{5577,2048} and {5576,1791}-{5633,2048}.
    private static List<Region> RegisterCovetousRegions()
    {
        var list = new List<Region>();

        foreach (var map in new[] { Map.Trammel, Map.Felucca })
        {
            var region = new DungeonRegion(
                "Covetous", map, Region.DefaultPriority,
                Region.ConvertTo3D(new Rectangle2D(5376, 1793, 201, 255)),
                Region.ConvertTo3D(new Rectangle2D(5576, 1791, 57, 257))
            );
            region.Register();
            list.Add(region);
        }

        return list;
    }

    private static void ClearCovetous()
    {
        Generate.Delete();

        foreach (var item in World.Items.Values.ToList())
        {
            if (item is VoidPoolController or Level3Spawner or VoidPoolGate or CoraManaDrainItem or BestWaveBoard ||
                item is Static { Name: "Void Pool" } ||
                item is Spawner spawner && spawner.Entries.Any(e => e.SpawnedName is "CoraTheSorceress" or "VelaTheSorceress"))
            {
                item.Delete();
            }
        }

        foreach (var mobile in World.Mobiles.Values.ToList())
        {
            if (mobile is CovetousCreature or CoraTheSorceress or VelaTheSorceress or DazzledHarpy or VampireMongbat or HeadlessMiner or StrangeGazer)
            {
                mobile.Delete();
            }
        }

        foreach (var region in Region.Regions.OfType<VoidPoolRegion>().ToList())
        {
            region.Unregister();
        }

        VoidPoolController.InstanceTram = null;
        VoidPoolController.InstanceFel = null;
        VoidPoolStats.Clear();
    }

    // A creature killed by a player with looting rights (most of its hits taken by the player), through Kill.
    private static void KillBy(BaseCreature bc, PlayerMobile pm)
    {
        bc.Damage(Math.Max(1, bc.Hits - 1), pm);
        bc.Kill();
    }

    [Fact]
    public void SetupPlacesTheControllersThePoolTheSpawnersAndThePaths()
    {
        ShardTestClock.Arm();
        EnsureCorpses();

        var regions = RegisterCovetousRegions();
        ClearCovetous();

        try
        {
            // A stock level-2 spawner: ConvertSpawners retypes its gazers.
            var stock = new Spawner(
                1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), 0,
                new Rectangle3D(5450, 1870, Region.MinZ, 4, 4, Region.MaxZ - Region.MinZ), "Gazer"
            );
            stock.MoveToWorld(new Point3D(5451, 1871, 0), Map.Trammel);
            stock.Respawn();
            Assert.IsType<Gazer>(Assert.Single(stock.Spawned.Keys));

            var controllers = Generate.Setup();

            Assert.Equal(2, controllers.Length);
            Assert.Same(controllers[0], VoidPoolController.InstanceTram);
            Assert.Same(controllers[1], VoidPoolController.InstanceFel);
            Assert.All(controllers, c => Assert.Equal(Generate.ControllerLocation, c.Location));
            Assert.All(controllers, c => Assert.True(c.Active));
            Assert.All(controllers, c => Assert.True(c.Region?.Registered == true));
            Assert.Equal(60, controllers[0].RespawnMin);
            Assert.Equal(90, controllers[0].RespawnMax);
            Assert.False(controllers[0].OnGoing);

            // The Void Pool region wins over the static dungeon region inside its rectangles (priority 51).
            var atPool = Region.Find(new Point3D(5500, 1998, 5), Map.Trammel);
            Assert.IsType<VoidPoolRegion>(atPool);
            Assert.Same(controllers[0].Region, atPool);
            Assert.IsType<VoidPoolRegion>(Region.Find(new Point3D(5435, 1950, 0), Map.Felucca));
            Assert.Equal("Covetous", Region.Find(new Point3D(5500, 1900, 0), Map.Trammel).Name);

            // The pool: 49 tiles per facet, stairs round the edge, stars inside.
            var pool = World.Items.Values.OfType<Static>().Where(s => !s.Deleted && s.Name == "Void Pool").ToList();
            Assert.Equal(98, pool.Count);
            Assert.Equal(49, pool.Count(s => s.Map == Map.Trammel));
            var corner = Assert.Single(pool, s => s.Map == Map.Trammel && s.X == 5497 && s.Y == 1995);
            Assert.Equal(1886, corner.ItemID);
            Assert.Equal(1954, corner.Hue);
            var centre = Assert.Single(pool, s => s.Map == Map.Felucca && s.X == 5500 && s.Y == 1998);
            Assert.InRange(centre.ItemID, 8511, 8516);
            Assert.Equal(0, centre.Hue);
            Assert.All(pool, s => Assert.Equal(5, s.Z));

            // Cora and Vela, one each per facet, from their spawners.
            foreach (var map in new[] { Map.Trammel, Map.Felucca })
            {
                var cora = Assert.Single(World.Mobiles.Values.OfType<CoraTheSorceress>(), c => !c.Deleted && c.Map == map);
                Assert.True(cora.InRange(Generate.CoraSpawnerLocation, 5));
                Assert.IsType<Spawner>(cora.Spawner);
                Assert.Equal(TimeSpan.FromHours(1), ((Spawner)cora.Spawner).MinDelay);
                Assert.Equal(TimeSpan.FromHours(1.5), ((Spawner)cora.Spawner).MaxDelay);

                var vela = Assert.Single(World.Mobiles.Values.OfType<VelaTheSorceress>(), v => !v.Deleted && v.Map == map);
                Assert.Equal(Generate.VelaSpawnerLocation, vela.Location);
            }

            // The two paths, linked in order, per facet.
            foreach (var controller in controllers)
            {
                Assert.Equal(Generate.PathA, controller.WaypointsA.Select(w => w.Location));
                Assert.Equal(Generate.PathB, controller.WaypointsB.Select(w => w.Location));
                Assert.All(controller.WaypointsA, w => Assert.Equal(controller.Map, w.Map));

                for (var i = 0; i < controller.WaypointsA.Count - 1; i++)
                {
                    Assert.Same(controller.WaypointsA[i + 1], controller.WaypointsA[i].NextPoint);
                }

                Assert.Null(controller.WaypointsA[^1].NextPoint);
                Assert.Same(controller.WaypointsB[^1], controller.WaypointsB[^2].NextPoint);
                Assert.Equal(9, controller.WaypointACount);
                Assert.Equal(10, controller.WaypointBCount);
            }

            // The stock spawner now spawns strange gazers.
            Assert.Equal("StrangeGazer", Assert.Single(stock.Entries).SpawnedName);
            Assert.True(stock.Running);
            Assert.Contains(stock.Spawned.Keys, s => s is StrangeGazer);
            Assert.DoesNotContain(stock.Spawned.Keys, s => s is Gazer and not StrangeGazer);
            Assert.Equal("StrangeGazer", Generate.ConvertName("gazerlarva"));
            Assert.Equal("HeadlessMiner", Generate.ConvertName("HeadlessOne"));
            Assert.Equal("VampireMongbat", Generate.ConvertName("StoneHarpy"));
            Assert.Equal("DazzledHarpy", Generate.ConvertName("Harpy"));
            Assert.Equal("Orc", Generate.ConvertName("Orc"));

            // The level-3 population arrives a second after the controller.
            ShardTestClock.Advance(TimeSpan.FromSeconds(1.5));

            var level3 = controllers[0].Level3Spawner;
            Assert.NotNull(level3);
            Assert.True(level3.Active);
            Assert.Equal(9, level3.EntryCount);
            Assert.Equal(Map.Trammel, level3.Map);

            var population = World.Mobiles.Values.OfType<CovetousCreature>()
                .Where(c => !c.Deleted && c.Map == Map.Trammel && c.Level3Entry >= 0).ToList();

            Assert.Equal(8 * 7 + 15 * 2, population.Count);
            Assert.All(population, c => Assert.Same(level3, c.Spawner));
            Assert.All(population, c => Assert.InRange(c.Level, 5, 15));
            Assert.All(population, c => Assert.False(c.VoidSpawn));
            Assert.All(population, c => Assert.True(Level3Spawner.Level3Bounds.Contains(c.Location), $"{c} at {c.Location}"));
            Assert.All(population, c => Assert.True(c.InRange(c.Home, 3), $"{c} wandered from {c.Home} to {c.Location}")); // homed where it spawned; the AI has had a second to step
            Assert.All(population, c => Assert.Equal(20, c.RangeHome));

            foreach (var entry in level3.Spawns)
            {
                Assert.Equal(entry.MaxCount, entry.CurrentCount);
                var allowed = VoidPoolController.SpawnTable[(int)entry.SpawnType];
                Assert.All(entry.Spawn, c => Assert.Contains(c.GetType(), allowed));
                Assert.All(entry.Spawn, c => Assert.Equal(entry.Index, ((CovetousCreature)c).Level3Entry));
            }

            // A kill credits the looter's overall total and leaves the entry one short; an emptied entry is
            // re-rolled to another type two to five minutes later.
            var pm = NewPlayer(new Point3D(5570, 1890, 0));
            var entry0 = level3.Spawns[0];
            var typeBefore = entry0.SpawnType;
            var victims = entry0.Spawn.ToList();
            var first = victims[0];
            first.Fame = 5000;

            KillBy(first, pm);

            Assert.Equal(7, entry0.CurrentCount);
            Assert.Null(first.Spawner);
            Assert.Equal(5, VoidPoolStats.GetStats(controllers[0]).OverallTotal[pm]);

            foreach (var bc in victims.Skip(1))
            {
                KillBy(bc, pm);
            }

            Assert.Equal(0, entry0.CurrentCount);

            ShardTestClock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

            Assert.Equal(8, entry0.CurrentCount);
            Assert.NotEqual(typeBefore, entry0.SpawnType);
            Assert.All(entry0.Spawn, c => Assert.Contains(c.GetType(), VoidPoolController.SpawnTable[(int)entry0.SpawnType]));

            // The controller and its spawner survive a save; the level-3 creatures are listed again from theirs.
            var copy = RoundTrip(level3);
            Assert.True(copy.SpawnActive);
            Assert.Same(controllers[0], copy.Controller);
            copy.Delete();

            // [DeleteCovetous takes everything with it.
            var deleted = Generate.Delete();
            _out.WriteLine($"deleted={deleted}");

            Assert.True(deleted >= 2 + 98 + 4 + 38);
            Assert.All(controllers, c => Assert.True(c.Deleted));
            Assert.Null(VoidPoolController.InstanceTram);
            Assert.Null(VoidPoolController.InstanceFel);
            Assert.DoesNotContain(World.Items.Values.OfType<Static>(), s => !s.Deleted && s.Name == "Void Pool");
            Assert.DoesNotContain(World.Items.Values.OfType<WayPoint>(), w => !w.Deleted && w.Map == Map.Trammel && Generate.PathA.Contains(w.Location));
            Assert.DoesNotContain(World.Mobiles.Values.OfType<CoraTheSorceress>(), c => !c.Deleted);
            Assert.DoesNotContain(World.Mobiles.Values.OfType<CovetousCreature>(), c => !c.Deleted && c.Level3Entry >= 0);
            Assert.True(level3.Deleted);
            Assert.Equal("Covetous", Region.Find(new Point3D(5500, 1998, 5), Map.Trammel).Name);

            pm.Delete();
            stock.Delete();
        }
        finally
        {
            ClearCovetous();

            foreach (var region in regions)
            {
                region.Unregister();
            }
        }
    }

    [Fact]
    public void TheBattleSendsWavesScoresKillsAndEndsWhenTheWallsFall()
    {
        ShardTestClock.Arm();
        EnsureCorpses();

        var regions = RegisterCovetousRegions();
        ClearCovetous();

        try
        {
            // GetPlayerCount counts connected players (Region.cs:590): the player gets a test NetState (D16's
            // rules: both links set, cleared before disposal). Made first: CreateTestNetState resets Core.Now to
            // real time (D16), and the controller computes its first battle time from Core.Now as it is built.
            var ns = PacketTestUtilities.CreateTestNetState();

            var controller = new VoidPoolController(Map.Trammel);
            controller.MoveToWorld(Generate.ControllerLocation, Map.Trammel);
            Generate.AddWaypoints(controller, Map.Trammel);

            var pm = NewPlayer(new Point3D(5500, 2000, 0));
            pm.NetState = ns;
            ns.Mobile = pm;
            pm.MoveToWorld(new Point3D(5500, 2000, 0), Map.Trammel);
            Assert.Same(controller.Region, pm.Region);
            Assert.Equal(1, controller.Region.GetPlayerCount());

            // The menu entry a player gets inside the region (through the PlayerMobile patch).
            var menu = PooledRefList<ContextMenuEntry>.Create();
            VoidPoolContextMenu.AddEntries(pm, ref menu);
            Assert.Equal(1, menu.Count);
            Assert.IsType<VoidPoolInfo>(menu[0]);
            menu.Dispose();

            var outside = NewPlayer(new Point3D(5500, 1900, 0));
            var none = PooledRefList<ContextMenuEntry>.Create();
            VoidPoolContextMenu.AddEntries(outside, ref none);
            Assert.Equal(0, none.Count);
            none.Dispose();
            outside.Delete();

            _out.WriteLine($"before: ongoing={controller.OnGoing} next={controller.NextStart:O} now={Core.Now:O} players={controller.Region.GetPlayerCount()}");
            Assert.False(controller.OnGoing, "no battle before the first fifteen minutes");
            Assert.True(controller.NextStart > Core.Now, "the first battle is still ahead");
            Assert.True(controller.NextStart <= Core.Now + TimeSpan.FromMinutes(VoidPoolController.RestartSpan), "the first battle is at most fifteen minutes ahead");

            // Fifteen minutes with a player inside: the battle begins, wave one pours out over six seconds.
            ShardTestClock.Advance(TimeSpan.FromMinutes(VoidPoolController.RestartSpan) + TimeSpan.FromSeconds(2));

            _out.WriteLine($"after: ongoing={controller.OnGoing} next={controller.NextStart:O} now={Core.Now:O} players={controller.Region.GetPlayerCount()} wave={controller.Wave}");
            Assert.True(controller.OnGoing, "fifteen minutes with a player inside starts the battle");
            Assert.Equal(1, controller.Wave);
            Assert.Single(controller.Waves);
            Assert.Equal(VoidPoolController.PoolStartHits, controller.PoolHits);
            Assert.Equal(2, World.Items.Values.OfType<VoidPoolGate>().Count(g => !g.Deleted));

            ShardTestClock.Advance(TimeSpan.FromSeconds(7));

            var wave1 = controller.Waves[0];
            Assert.Equal(8, wave1.Creatures.Count); // ceil(max(5, 2 sqrt(1)) * 1.5)
            Assert.All(wave1.Creatures, c => Assert.True(c is CovetousCreature { VoidSpawn: true, Level: 1 }));
            Assert.All(wave1.Creatures, c => Assert.Contains(c.GetType(), VoidPoolController.SpawnTable[(int)controller.VoidType]));
            Assert.All(wave1.Creatures, c => Assert.Equal(VoidPoolController.EndPoint, c.Home));
            Assert.All(wave1.Creatures, c => Assert.Equal(1, c.RangeHome));
            Assert.All(wave1.Creatures, c => Assert.NotNull(c.CurrentWayPoint));
            Assert.All(wave1.Creatures, c => Assert.Equal(Map.Trammel, c.Map));
            Assert.All(wave1.Creatures, c => Assert.True(c.InRange(VoidPoolController.StartPoint1, 10) || c.InRange(VoidPoolController.StartPoint2, 10), $"a wave creature at {c.Location} is near a gate"));
            Assert.DoesNotContain(World.Items.Values.OfType<VoidPoolGate>(), g => !g.Deleted);

            // Wave two follows ten seconds after wave one.
            ShardTestClock.Advance(TimeSpan.FromSeconds(10));
            Assert.Equal(2, controller.Wave);
            Assert.Equal(2, controller.Waves.Count);

            // A kill: credit on the wave, score by fame, the corpse gives nothing and cannot be opened.
            var victim = wave1.Creatures[0];
            victim.Fame = 1996;
            var fameBefore = pm.Fame;
            KillBy(victim, pm);

            Assert.DoesNotContain(victim, wave1.Creatures);
            Assert.Contains(pm, wave1.Credit);
            Assert.Equal(2, controller.CurrentScore[pm]);
            Assert.Equal(fameBefore, pm.Fame);
            var corpse = Assert.IsType<Corpse>(victim.Corpse);
            Assert.Empty(corpse.Items);
            Assert.False(controller.Region.OnDoubleClick(pm, corpse));

            // Creatures on the walls wear them down; at zero the battle is lost: points, scores, a clear.
            controller.PoolHits = 1;
            var wrecker = wave1.Creatures[1];
            wrecker.MoveToWorld(new Point3D(5500, 1998, 5), Map.Trammel);

            for (var i = 0; i < 120 && controller.OnGoing; i++)
            {
                ShardTestClock.Advance(TimeSpan.FromSeconds(1));
            }

            Assert.False(controller.OnGoing, "a creature on the pool walls brings the last hit down within two minutes");
            Assert.True(controller.NextStart > Core.Now);
            Assert.Equal(1, VoidPoolPoints.Instance.GetPoints(pm)); // one credited wave at stage 1
            var stats = VoidPoolStats.GetStats(controller);
            Assert.NotNull(stats.BestWave);
            Assert.Equal(2, stats.BestWave.Waves);
            Assert.Equal(2, stats.BestSingle[pm]);
            Assert.Equal(2, stats.OverallTotal[pm]);
            ShardTestClock.Advance(TimeSpan.FromSeconds(8)); // a wave creature still waiting at its gate is dropped instead
            Assert.DoesNotContain(World.Mobiles.Values.OfType<CovetousCreature>(), c => !c.Deleted && c.VoidSpawn);

            // The gumps build.
            _ = new VoidPoolGump(controller, pm);
            _ = new ScoresGump(controller, pm, ScoreType.BestWave);
            _ = new ScoresGump(controller, pm, ScoreType.OverallTotal);

            // The scoreboards survive a save.
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            VoidPoolStats.SerializeAll(writer);
            writer.Flush();
            VoidPoolStats.Clear();
            VoidPoolStats.DeserializeAll(new BufferReader(buffer));
            var loaded = Assert.Single(VoidPoolStats.Stats);
            Assert.Same(controller, loaded.Controller);
            Assert.Equal(2, loaded.BestWave.Waves);
            Assert.Equal(2, loaded.OverallTotal[pm]);
            Assert.Equal(2, loaded.BestSingle[pm]);

            // The controller survives a save with its spawner, paths and settings.
            var copy = RoundTrip(controller);
            Assert.True(copy.Active);
            Assert.Equal(60, copy.RespawnMin);
            Assert.Equal(9, copy.WaypointsA.Count);
            Assert.Same(controller.Level3Spawner, copy.Level3Spawner);
            copy.Region?.Unregister();
            copy.Delete();

            ns.Mobile = null;
            pm.NetState = null;
            pm.Delete();
            controller.Delete();
            Assert.Null(VoidPoolController.InstanceTram);
        }
        finally
        {
            ClearCovetous();

            foreach (var region in regions)
            {
                region.Unregister();
            }
        }
    }

    [Fact]
    public void TheVoidCreaturesConstructGrowWithTheirLevelAndRoundTrip()
    {
        ShardTestClock.Arm();
        EnsureCorpses();
        ClearCovetous();

        try
        {
            Assert.Equal(25, VoidTypes.Length);
            Assert.Equal(25, VoidTypes.Distinct().Count());

            // Every void type builds plain (level 60) and by (level, voidSpawn), and round-trips its level fields.
            foreach (var type in VoidTypes)
            {
                var plain = Assert.IsAssignableFrom<CovetousCreature>(Activator.CreateInstance(type));
                Assert.Equal(60, plain.Level);
                Assert.False(plain.VoidSpawn);
                Assert.NotNull(plain.CorpseName);
                Assert.False(string.IsNullOrEmpty(plain.Name ?? plain.DefaultName));

                var leveled = Assert.IsAssignableFrom<CovetousCreature>(Activator.CreateInstance(type, 7, true));
                Assert.Equal(7, leveled.Level);
                Assert.True(leveled.VoidSpawn);
                leveled.Level3Entry = 3;

                var copy = (CovetousCreature)RoundTrip<Mobile>(leveled);
                Assert.Equal(7, copy.Level);
                Assert.True(copy.VoidSpawn);
                Assert.Equal(3, copy.Level3Entry);
                Assert.Equal(leveled.Body, copy.Body);

                plain.Delete();
                leveled.Delete();
                copy.Delete();
            }

            // Half a second in, a level-60 creature (stage 12) has been raised toward the stage's numbers.
            var strong = new OgreBoneCrusher();
            strong.MoveToWorld(new Point3D(5570, 1890, 0), Map.Trammel);
            Assert.Equal(12, strong.Stage);
            Assert.InRange(strong.Skills.Wrestling.Base, 35, 50);

            ShardTestClock.Advance(TimeSpan.FromSeconds(0.6));

            _out.WriteLine($"stage 12: wrestling={strong.Skills.Wrestling.Base} phys={strong.PhysicalResistance} hits={strong.HitsMax} str={strong.RawStr}");
            Assert.InRange(strong.Skills.Wrestling.Base, 123, 133);
            Assert.InRange(strong.PhysicalResistance, 67, 77); // 95 / 15 * 12, integer arithmetic as on ServUO
            Assert.InRange(strong.HitsMax, 2395, 2400);
            Assert.InRange(strong.RawStr, 319, 329);
            Assert.Equal(Math.Min(8500, 60 * 142), strong.Fame);
            strong.Delete();

            // A void spawn hands out no fame and no loot; a level-3 creature does both.
            var pm = NewPlayer(new Point3D(5570, 1890, 0));
            var fameBefore = pm.Fame;

            var voidSpawn = new OrcFootSoldier(10, true);
            voidSpawn.MoveToWorld(new Point3D(5571, 1890, 0), Map.Trammel);
            KillBy(voidSpawn, pm);
            Assert.Equal(fameBefore, pm.Fame);
            Assert.Empty(Assert.IsType<Corpse>(voidSpawn.Corpse).Items);

            var wild = new OrcFootSoldier(10, false);
            wild.MoveToWorld(new Point3D(5571, 1890, 0), Map.Trammel);
            KillBy(wild, pm);
            Assert.Equal(fameBefore, pm.Fame);
            Assert.NotEmpty(Assert.IsType<Corpse>(wild.Corpse).Items);

            // The four retyped level 1-2 creatures and the two sorceresses.
            var harpy = new DazzledHarpy();
            Assert.Equal(FightMode.Aggressor, harpy.FightMode);
            Assert.Equal("a dazzled harpy", harpy.Name);
            var gazer = new StrangeGazer();
            Assert.InRange(gazer.Skills.Meditation.Base, 50, 100);
            var miner = new HeadlessMiner();
            Assert.Equal("headless miner", miner.Name);
            var bat = new VampireMongbat();
            Assert.Equal(1461, bat.Hue);

            var vela = new VelaTheSorceress();
            vela.MoveToWorld(new Point3D(2254, 1207, 0), Map.Trammel);
            Assert.True(vela.Blessed);
            Assert.True(vela.IsInvulnerable);
            Assert.Equal("Vela", vela.DefaultName);
            Assert.Equal(2, vela.RangePerception);
            Assert.Contains(vela.Backpack.Items, i => i is BraceletOfProtection);
            Assert.Contains(vela.Backpack.Items, i => i is Hephaestus);
            Assert.Contains(vela.Backpack.Items, i => i is GargishBlightOfTheTundra);

            var cora = new CoraTheSorceress();
            cora.MoveToWorld(new Point3D(5457, 1808, 0), Map.Trammel);
            Assert.Equal("Cora", cora.DefaultName);
            Assert.Equal(35000, cora.HitsMax);
            Assert.True(cora.HasAura);
            Assert.Equal(3, cora.AuraRange);
            Assert.Contains(cora.GetMonsterAbilities(), a => a is FireBreath);
            Assert.Contains(cora.Items, i => i is WildStaff);

            pm.MoveToWorld(new Point3D(5459, 1808, 0), Map.Trammel);
            pm.Mana = 50;
            cora.AuraEffect(pm);
            Assert.True(pm.Mana < 50, "the aura drains mana");

            pm.MoveToWorld(new Point3D(5465, 1812, 0), Map.Trammel);
            cora.TryTeleport();
            Assert.True(pm.InRange(cora.Location, 1), $"teleported to Cora: {pm.Location} vs {cora.Location}");
            Assert.Same(pm, cora.Combatant);

            cora.DoManaDrain();
            ShardTestClock.Advance(TimeSpan.FromSeconds(3));
            var cracks = World.Items.Values.OfType<CoraManaDrainItem>().Count(c => !c.Deleted);
            _out.WriteLine($"mana drain cracks after 3 s: {cracks}");
            Assert.True(cracks > 0, "the mana drain lays cracks along a path");

            ShardTestClock.Advance(TimeSpan.FromSeconds(8));
            Assert.DoesNotContain(World.Items.Values.OfType<CoraManaDrainItem>(), c => !c.Deleted);

            foreach (var m in new BaseCreature[] { harpy, gazer, miner, bat, vela, cora })
            {
                m.Delete();
            }

            pm.Delete();
        }
        finally
        {
            ClearCovetous();
        }
    }

    [Fact]
    public void TheRewardsAndArtifactsAreSoldDroppedAndSaved()
    {
        ShardTestClock.Arm();
        ClearCovetous();
        EnsureRewards();

        try
        {
            Assert.Equal(13, VoidPoolRewards.Rewards.Count);
            Assert.Equal(8, VoidPoolRewards.Rewards.Count(r => r.Type == typeof(CauldronOfTransmutationDeed)));
            Assert.Equal(100, VoidPoolRewards.Rewards[0].Points);
            Assert.Equal((int)CraftResource.Bronze, VoidPoolRewards.Rewards[0].Hue);
            Assert.Equal(2515, VoidPoolRewards.Rewards[^1].Points);

            var seen = new HashSet<Type>();

            for (var i = 0; i < 200; i++)
            {
                var artifact = VoidPoolRewards.DropRandomArtifact();
                Assert.NotNull(artifact);
                seen.Add(artifact.GetType());
                artifact.Delete();
            }

            Assert.Equal(5, seen.Count);
            Assert.Contains(typeof(PrismaticLenses), seen);
            Assert.Contains(typeof(Brightblade), seen);

            // The artifacts.
            var shield = new Hephaestus();
            Assert.Equal(1910, shield.Hue);
            Assert.Equal(5, shield.ArmorAttributes.SelfRepair);
            Assert.Equal(15, shield.PhysicalBonus);
            Assert.Equal(SkillName.Parry, shield.SkillBonuses.GetSkill(0));
            Assert.Equal(10.0, shield.SkillBonuses.GetBonus(0));
            Assert.Equal(250, shield.MaxHitPoints);
            Assert.Equal(255, new Hephaestus(false).MaxHitPoints);
            var shieldCopy = RoundTrip(shield);
            Assert.Equal(5, shieldCopy.ArmorAttributes.SelfRepair);
            Assert.Equal(1152909, shieldCopy.LabelNumber);

            var bracelet = new BraceletOfProtection();
            var eaters = new[]
            {
                bracelet.AbsorptionAttributes.EaterKinetic, bracelet.AbsorptionAttributes.EaterFire,
                bracelet.AbsorptionAttributes.EaterCold, bracelet.AbsorptionAttributes.EaterPoison,
                bracelet.AbsorptionAttributes.EaterEnergy, bracelet.AbsorptionAttributes.EaterDamage
            };
            Assert.Equal(15, eaters.Sum());
            Assert.Equal(1, eaters.Count(e => e == 15));
            Assert.Equal(5, bracelet.Attributes.BonusHits);
            Assert.Equal(10, bracelet.Attributes.RegenHits);
            var braceletCopy = RoundTrip(bracelet);
            var copiedEaters = braceletCopy.AbsorptionAttributes.EaterKinetic + braceletCopy.AbsorptionAttributes.EaterFire +
                braceletCopy.AbsorptionAttributes.EaterCold + braceletCopy.AbsorptionAttributes.EaterPoison +
                braceletCopy.AbsorptionAttributes.EaterEnergy + braceletCopy.AbsorptionAttributes.EaterDamage;
            Assert.Equal(15, copiedEaters);
            Assert.Equal(5, braceletCopy.Attributes.BonusHits);

            var bow = new BlightOfTheTundra(false);
            Assert.NotEqual(SlayerName.None, bow.Slayer);
            Assert.Equal(100, bow.AosElementDamages.Fire);
            Assert.Equal(15, bow.WeaponAttributes.ResistColdBonus);
            Assert.Equal(255, bow.MaxHitPoints);
            Assert.Equal(1165, RoundTrip(bow).Hue);
            var glaive = new GargishBlightOfTheTundra();
            Assert.Equal(0x090A, glaive.ItemID);
            Assert.Equal(250, glaive.MaxHitPoints);

            var blade = new Brightblade();
            Assert.Equal(100, blade.WeaponAttributes.HitLeechStam);
            Assert.Equal(1756, blade.Hue);
            Assert.Equal(255, blade.InitMinHits);
            Assert.Equal(100, RoundTrip(blade).AosElementDamages.Fire);

            var lenses = new PrismaticLenses();
            Assert.Equal(0x2FB8, lenses.ItemID);
            Assert.Equal(18, lenses.BasePhysicalResistance);
            Assert.Equal(30, lenses.WeaponAttributes.HitLowerDefend);
            Assert.Equal(1152716, lenses.LabelNumber);
            Assert.Equal(30, RoundTrip(lenses).WeaponAttributes.HitLowerDefend);

            // Points buy a reward through the gump; a cauldron deed comes hued for its resource.
            var vela = new VelaTheSorceress();
            vela.MoveToWorld(new Point3D(2254, 1207, 0), Map.Trammel);
            var pm = NewPlayer(new Point3D(2255, 1207, 0));

            Assert.Equal(0, VoidPoolPoints.Instance.GetPoints(pm));
            VoidPoolPoints.Instance.AwardPoints(pm, 400, message: false);
            Assert.Equal(400, VoidPoolPoints.Instance.GetPoints(pm));

            var gump = new VoidPoolRewardGump(vela, pm);
            Assert.Equal(400, gump.Points);
            gump.OnConfirmed(VoidPoolRewards.Rewards[0], 0);

            var deed = Assert.Single(pm.Backpack.Items.OfType<CauldronOfTransmutationDeed>());
            _out.WriteLine($"deed resource={deed.Resource} hue={deed.Hue} row hue={VoidPoolRewards.Rewards[0].Hue}");
            Assert.Equal(CraftResource.Bronze, deed.Resource);
            Assert.Equal(CraftResources.GetHue(CraftResource.Bronze), deed.Hue);
            Assert.True(deed.Expires > Core.Now + TimeSpan.FromHours(23));
            Assert.Equal(300, VoidPoolPoints.Instance.GetPoints(pm));

            gump.OnConfirmed(VoidPoolRewards.Rewards[8], 8);
            var forgeDeed = Assert.Single(pm.Backpack.Items.OfType<TemporaryForgeDeed>());
            Assert.Equal(50, VoidPoolPoints.Instance.GetPoints(pm));

            var deedCopy = RoundTrip(deed);
            Assert.Equal(CraftResource.Bronze, deedCopy.Resource);
            Assert.Equal(deed.Expires, deedCopy.Expires);
            deedCopy.Delete();

            var cauldron = Assert.IsType<CauldronOfTransmutation>(deed.Addon);
            Assert.Equal(5000, cauldron.Charges);
            Assert.True(cauldron.RetainDeedHue);
            Assert.Single(cauldron.Components);
            cauldron.Delete();

            var forge = Assert.IsType<TemporaryForge>(forgeDeed.Addon);
            Assert.IsAssignableFrom<ForgeComponent>(Assert.Single(forge.Components));
            Assert.True(forge.Expires > Core.Now + TimeSpan.FromHours(3));
            forge.Delete();

            // An expired deed goes up in smoke on its next check.
            deed.Expires = Core.Now - TimeSpan.FromSeconds(1);
            ShardTestClock.Advance(TimeSpan.FromSeconds(11));
            Assert.True(deed.Deleted, "an expired cauldron deed is gone within ten seconds");

            // The best wave board opens nothing without a controller and is a plain fixed item.
            var board = new BestWaveBoard();
            Assert.False(board.Movable);
            Assert.Equal("Void Pool - Best Wave", board.Name);
            board.Delete();

            VoidPoolPoints.Instance.SetPoints(pm, 0);
            pm.Delete();
            vela.Delete();
        }
        finally
        {
            ClearCovetous();
        }
    }
}
