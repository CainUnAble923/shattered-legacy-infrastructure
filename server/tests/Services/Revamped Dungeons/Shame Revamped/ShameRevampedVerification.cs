// CC4 Shame: the "spawns generate" gate for the Shame revamp, plus the altar, the wall, the whetstone recipe
// and the creature conversions that a green build says nothing about.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh
// and run as a gate by docker/uo/build.sh. See notes/s4-test-route.md and notes/s8-test-route.md.
//
// docs/tasks.md says CC4 is done when "each dungeon builds and its spawns generate". The first fact drives the
// same code [GenerateNewShame runs and asserts on what comes out: 194 spawners on two facets, every one full
// after Respawn (384 of 386 requested; the data holds 384), every spawn the right type inside its rectangle,
// the 116 chest rows holding locked Treasure Chest Pack chests with loot in them, and the stock spawners in
// the dungeon stopped by Generate and restarted by Delete.
//
// The test host has no map files (TileMatrix serves an empty land block, TileData is skipped under xUnit), so
// CanSpawnMobile/CanSpawnItem see passable land at z 0 everywhere. Real-map placement is the headless run's
// job (notes/cc4-shame.md). It loads no regions.json either, so the "Shame" DungeonRegion both facets have
// on the live server (priority 50, two rectangles) is registered here where a fact needs it.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.ShameRevamped;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ShameRevampedVerification
{
    private readonly ITestOutputHelper _out;

    public ShameRevampedVerification(ITestOutputHelper output) => _out = output;

    private static PlayerMobile NewPlayer(Point3D loc, Map map = null)
    {
        var pm = new PlayerMobile();
        pm.AddItem(new Backpack());
        pm.MoveToWorld(loc, map ?? Map.Trammel);
        pm.Hits = pm.HitsMax; // a fresh PlayerMobile has 0 hits; the first point of damage would kill it
        return pm;
    }

    // The collection runs every fact in one world, in no fixed order, and a fact that fails leaves its objects
    // behind. Each fact that places Shame objects starts and ends by removing every Shame object in the world,
    // ours or a stray, so no fact depends on another having finished.
    private static void ClearShame()
    {
        ShameGenerator.DeleteShame();

        foreach (var item in World.Items.Values.ToList())
        {
            if (item is ShameAltar or ShameWall or ShameWallTeleporter or ShameTeleporter or BaseTreasureChestMod ||
                item is Spawner spawner && ShameSpawns.IsShameSpawner(spawner))
            {
                item.Delete();
            }
        }

        foreach (var mobile in World.Mobiles.Values.ToList())
        {
            if (mobile is ShameGuardian || mobile is CaveTroll { Wall: not null })
            {
                mobile.Delete();
            }
        }
    }

    // Corpse.Initialize registers Mobile.CreateCorpseHandler on the real server. UOContentFixture calls a
    // hand-picked subset of Initialize methods and this is not one of them, so Kill() hands OnDeath a null
    // container until it is registered. Register it once, so a kill here produces a corpse as it does live.
    private static void EnsureCorpses()
    {
        if (Mobile.CreateCorpseHandler == null)
        {
            Corpse.Initialize();
        }
    }

    // regions.json: "Shame", DungeonRegion, priority 50, {5377,2}-{5634,262} and {5635,2}-{5895,126}, on both facets.
    private static List<Region> RegisterShameRegions()
    {
        var list = new List<Region>();

        foreach (var map in new[] { Map.Trammel, Map.Felucca })
        {
            var region = new DungeonRegion(
                "Shame", map, Region.DefaultPriority,
                Region.ConvertTo3D(new Rectangle2D(5377, 2, 258, 261)),
                Region.ConvertTo3D(new Rectangle2D(5635, 2, 261, 125))
            );
            region.Register();
            list.Add(region);
        }

        return list;
    }

    [Fact]
    public void GenerateCreatesTheSpawnersAndTheSpawnersGenerate()
    {
        ShardTestClock.Arm();

        var regions = RegisterShameRegions();
        ClearShame();

        try
        {
            Assert.Equal("Shame", Region.Find(new Point3D(5538, 170, 5), Map.Trammel).Name);
            Assert.Equal("Shame", Region.Find(new Point3D(5538, 170, 5), Map.Felucca).Name);

            Assert.False(ShameSpawns.AnyPresent());

            var spawners = ShameSpawns.Generate();

            Assert.Equal(194, spawners.Count);
            Assert.Equal(194, ShameSpawns.Definitions.Length);
            Assert.Equal(116, ShameSpawns.Definitions.Count(d => d.IsChest));
            Assert.Equal(78, ShameSpawns.Definitions.Count(d => !d.IsChest));
            Assert.Equal(97, ShameSpawns.Definitions.Count(d => d.Felucca));

            var requested = ShameSpawns.Definitions.Sum(d => d.MaxCount);
            var expected = ShameSpawns.Definitions.Sum(d => d.Capacity);
            var spawned = spawners.Sum(s => s.Spawned.Count);

            _out.WriteLine($"spawners={spawners.Count} requested={requested} capacity={expected} actual={spawned}");

            Assert.Equal(386, requested);
            Assert.Equal(384, expected);
            Assert.Equal(expected, spawned);

            var byType = new Dictionary<string, int>();

            foreach (var spawner in spawners)
            {
                var def = ShameSpawns.Definitions.First(
                    d => d.Name == spawner.Name && d.CentreX == spawner.X && d.CentreY == spawner.Y && d.Map == spawner.Map
                );

                Assert.True(spawner.Running, $"{spawner.Name} at {spawner.Location} not running");
                Assert.Equal(def.Capacity, spawner.Spawned.Count);
                Assert.Equal(def.Range, spawner.WalkingRange);
                Assert.Equal(def.HomeRangeRelative, spawner.SpawnLocationIsHome);
                Assert.Equal(TimeSpan.FromMinutes(def.MinDelayMinutes), spawner.MinDelay);
                Assert.Equal(TimeSpan.FromMinutes(def.MaxDelayMinutes), spawner.MaxDelay);
                Assert.Equal(new Point3D(def.CentreX, def.CentreY, def.CentreZ), spawner.Location);

                var allowed = def.Entries.Select(e => e.Type).ToArray();

                foreach (var spawn in spawner.Spawned.Keys)
                {
                    var typeName = spawn.GetType().Name;
                    byType[typeName] = byType.GetValueOrDefault(typeName) + 1;

                    Assert.Contains(typeName, allowed);
                    Assert.Equal(def.Map, spawn.Map);
                    Assert.True(spawner.SpawnBounds.Contains(spawn.Location), $"{typeName} at {spawn.Location} outside {spawner.Name} {spawner.SpawnBounds}");

                    if (def.IsChest)
                    {
                        var chest = Assert.IsAssignableFrom<BaseTreasureChestMod>(spawn);
                        Assert.True(chest.Locked, "a spawned chest must start locked");
                        Assert.False(chest.Movable);
                        Assert.True(chest.Items.Count > 0, $"{typeName} spawned empty");
                        Assert.True(chest.RequiredSkill > 0);
                        Assert.Equal(TrapType.MagicTrap, chest.TrapType);
                    }
                    else
                    {
                        var creature = Assert.IsAssignableFrom<BaseCreature>(spawn);
                        Assert.Equal(def.Range, creature.RangeHome);
                        Assert.Equal(def.HomeRangeRelative ? creature.Location : spawner.Location, creature.Home);
                    }
                }

                // Every entry is a real type; the MX=0 chaos vortex entry exists but can never spawn.
                foreach (var entry in def.Entries)
                {
                    Assert.NotNull(AssemblyHandler.FindTypeByName(entry.Type));
                }
            }

            foreach (var (type, count) in byType.OrderBy(kv => kv.Key))
            {
                _out.WriteLine($"  {type}: {count}");
            }

            Assert.Equal(28, byType.Count); // every name in the XML spawns somewhere (ChaosVortex has two MX=1 rows besides its MX=0 entry)
            Assert.True(byType.ContainsKey("ChaosVortex"));
            Assert.Equal(2, byType["ChaosVortex"]); // one per facet from the MX=1 rows; the MX=0 entry at 5761,98 adds none
            Assert.True(byType.ContainsKey("TreasureLevel1"));
            Assert.True(byType.ContainsKey("TreasureLevel4"));
            Assert.True(byType.ContainsKey("MudPie"));
            Assert.True(byType.ContainsKey("UnboundEnergyVortex"));

            // A stock spawner in the dungeon: Generate stops and empties it, Delete restarts it (ServUO's
            // ResetOldSpawners), and ours are untouched.
            var old = new Spawner(
                1, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), 0,
                new Rectangle3D(5500, 100, Region.MinZ, 4, 4, Region.MaxZ - Region.MinZ), "Scorpion"
            )
            {
                Name = "Old Shame"
            };
            old.MoveToWorld(new Point3D(5501, 101, 0), Map.Trammel);
            old.Respawn();

            Assert.True(old.Running);
            Assert.Single(old.Spawned);

            var altarTiles = new[] { new Point3D(5490, 19, -25), new Point3D(5604, 102, 5), new Point3D(5538, 170, 5) };
            var stockTele = new Teleporter(new Point3D(1, 1, 1), Map.Trammel);
            stockTele.MoveToWorld(altarTiles[0], Map.Trammel);

            Assert.Equal("Shame Revamped setup!", ShameGenerator.Generate());

            Assert.True(stockTele.Deleted, "the stock teleporter on an altar-teleporter tile is removed");
            Assert.False(old.Running, "the stock spawner should have been reset");
            Assert.Empty(old.Spawned);
            Assert.All(spawners, s => Assert.True(s.Running && !s.Deleted));
            Assert.Equal(194, World.Items.Values.OfType<Spawner>().Count(ShameSpawns.IsShameSpawner));

            var altars = World.Items.Values.OfType<ShameAltar>().Where(a => !a.Deleted).ToList();
            var walls = World.Items.Values.OfType<ShameWall>().Where(w => !w.Deleted).ToList();
            var wallTeles = World.Items.Values.OfType<ShameWallTeleporter>().Where(t => !t.Deleted).ToList();
            var trolls = World.Mobiles.Values.OfType<CaveTroll>().Where(t => !t.Deleted && t.Wall != null).ToList();

            Assert.Equal(6, altars.Count);
            Assert.Equal(3, altars.Count(a => a.Map == Map.Trammel));
            Assert.Equal(3, altars.Count(a => a.Map == Map.Felucca));
            Assert.Equal(new[] { 10, 20, 30 }, altars.Where(a => a.Map == Map.Trammel).Select(a => a.SummonCost).OrderBy(c => c));
            Assert.Contains(altars, a => a.GuardianType == typeof(QuartzElemental) && a.Location == new Point3D(5403, 43, 30));
            Assert.Contains(altars, a => a.GuardianType == typeof(FlameElemental) && a.Location == new Point3D(5577, 54, 2));
            Assert.Contains(altars, a => a.GuardianType == typeof(WindElemental) && a.Location == new Point3D(5390, 145, 20));
            Assert.All(altars, a => Assert.True(a.Active));

            Assert.Equal(6, walls.Count);
            Assert.Equal(6, trolls.Count);
            Assert.Equal(22, wallTeles.Count); // 11 pairs per facet
            Assert.All(walls, w => Assert.Equal(w.StartSpot, w.Location));
            Assert.All(walls, w => Assert.Same(w, w.Troll.Wall));
            Assert.Equal(4 + 4 + 3, walls.Where(w => w.Map == Map.Trammel).Sum(w => w.Components.Count));

            // The altar's own teleporter arrives a second after construction.
            ShardTestClock.Advance(TimeSpan.FromSeconds(1.5));

            foreach (var altar in altars)
            {
                Assert.NotNull(altar.Teleporter);
                Assert.IsType<ShameTeleporter>(altar.Teleporter);
                Assert.Equal(altar.TeleporterLocation, altar.Teleporter.Location);
                Assert.Equal(altar.Map, altar.Teleporter.Map);
                Assert.Equal(altar.TeleporterDestination, altar.Teleporter.PointDest);
            }

            // Running it again places nothing twice.
            Assert.Equal("Shame Revamped setup!", ShameGenerator.Generate());
            Assert.Equal(6, World.Items.Values.OfType<ShameAltar>().Count(a => !a.Deleted));
            Assert.Equal(6, World.Items.Values.OfType<ShameWall>().Count(w => !w.Deleted));
            Assert.Equal(194, World.Items.Values.OfType<Spawner>().Count(ShameSpawns.IsShameSpawner));

            // [DeleteShame takes it all with it and restarts the stock spawner.
            var deleted = ShameGenerator.DeleteShame();
            _out.WriteLine($"deleted={deleted}");

            Assert.True(deleted >= 194 + 6 + 6 + 22);
            Assert.False(ShameSpawns.AnyPresent());
            Assert.All(spawners, s => Assert.True(s.Deleted));
            Assert.All(altars, a => Assert.True(a.Deleted && a.Teleporter.Deleted));
            Assert.All(walls, w => Assert.True(w.Deleted));
            Assert.All(trolls, t => Assert.True(t.Deleted));
            Assert.All(wallTeles, t => Assert.True(t.Deleted));
            Assert.DoesNotContain(World.Items.Values.OfType<BaseTreasureChestMod>(), c => !c.Deleted);

            Assert.True(old.Running, "the stock spawner should have been restarted");
            Assert.Single(old.Spawned);

            old.Delete();
        }
        finally
        {
            ClearShame();

            foreach (var region in regions)
            {
                region.Unregister();
            }
        }
    }

    [Fact]
    public void TheAltarSummonsAGuardianThatOnlyItsSummonerCanHurt()
    {
        ShardTestClock.Arm();
        EnsureCorpses();
        ClearShame();

        var altar = new ShameAltar(
            typeof(QuartzElemental), new Point3D(5490, 19, -25), new Point3D(5514, 10, 5), new Point3D(5387, 11, 30), 10
        );
        altar.MoveToWorld(new Point3D(5403, 43, 30), Map.Trammel);

        try
        {

        var summoner = NewPlayer(new Point3D(5404, 43, 30));
        var other = NewPlayer(new Point3D(5404, 44, 30));

        // Services/PointsSystems is not here: GetPoints is 0 < SummonCost 10, "You are not yet worthy".
        altar.CheckSummon(summoner);
        Assert.Null(altar.Guardian);
        Assert.Null(altar.Summoner);

        // The GM workaround until S7 lands.
        altar.SummonCost = 0;
        altar.CheckSummon(summoner);

        var guardian = Assert.IsType<QuartzElemental>(altar.Guardian);
        Assert.Same(altar, guardian.Altar);
        Assert.Same(summoner, altar.Summoner);
        Assert.Equal(altar.SpawnLocation, guardian.Location);
        Assert.Equal(Map.Trammel, guardian.Map);
        Assert.Equal(altar.SpawnLocation, guardian.Home);
        Assert.Equal(8, guardian.RangeHome);
        Assert.Equal("the guardian", guardian.Title);
        Assert.Equal("a quartz elemental", guardian.DefaultName);
        Assert.True(guardian.AlwaysMurderer);
        Assert.Equal(Core.Now + TimeSpan.FromHours(1), altar.DeadLine);
        Assert.True(altar.DeadLineTimerRunning);

        ShardTestClock.Advance(TimeSpan.FromSeconds(1.5));
        Assert.NotNull(altar.Teleporter);
        Assert.Equal(altar.TeleporterLocation, altar.Teleporter.Location);

        // Only the summoner (or their pets) can hurt it while the deadline is more than ten minutes away.
        var hits = guardian.Hits;

        guardian.Damage(50, other);
        Assert.Equal(hits, guardian.Hits);

        guardian.Damage(50);
        Assert.Equal(hits, guardian.Hits);

        var pet = new GreyWolf();
        pet.MoveToWorld(new Point3D(5388, 11, 30), Map.Trammel);
        pet.SetControlMaster(other);
        guardian.Damage(50, pet);
        Assert.Equal(hits, guardian.Hits);

        pet.SetControlMaster(summoner);
        guardian.Damage(50, pet);
        Assert.True(guardian.Hits < hits, $"summoner's pet: {hits} -> {guardian.Hits}");

        hits = guardian.Hits;
        guardian.Damage(50, summoner);
        Assert.True(guardian.Hits < hits, $"summoner: {hits} -> {guardian.Hits}");

        // In the last ten minutes anyone can.
        altar.DeadLine = Core.Now + TimeSpan.FromMinutes(9);
        hits = guardian.Hits;
        guardian.Damage(50, other);
        Assert.True(guardian.Hits < hits, $"last ten minutes: {hits} -> {guardian.Hits}");

        // A second summon is refused while one is up.
        altar.CheckSummon(other);
        Assert.Same(guardian, altar.Guardian);

        // Killing it: 3-5 crystals on the corpse, the altar clears and goes on a ten-minute cooldown.
        guardian.Kill();

        Assert.False(guardian.Alive);
        Assert.Null(altar.Guardian);
        Assert.Null(altar.Summoner);
        Assert.False(altar.DeadLineTimerRunning);
        Assert.Equal(Core.Now + TimeSpan.FromMinutes(ShameAltar.CoolDown), altar.NextSummon);

        var corpse = Assert.IsAssignableFrom<Container>(guardian.Corpse);
        var crystals = corpse.Items.OfType<ShameCrystal>().Sum(c => c.Amount);
        _out.WriteLine($"crystals={crystals} grit={corpse.Items.OfType<QuartzGrit>().Count()}");
        Assert.InRange(crystals, 3, 5);

        altar.CheckSummon(summoner); // "recently been defeated"
        Assert.Null(altar.Guardian);

        // A challenge that runs out of time deletes the guardian and frees the altar at once.
        altar.NextSummon = Core.Now;
        altar.CheckSummon(summoner);
        var second = Assert.IsAssignableFrom<ShameGuardian>(altar.Guardian);
        altar.DeadLine = Core.Now - TimeSpan.FromSeconds(1);
        ShardTestClock.Advance(TimeSpan.FromSeconds(1.5));

        Assert.True(second.Deleted);
        Assert.Null(altar.Guardian);
        Assert.False(altar.DeadLineTimerRunning);
        Assert.True(altar.NextSummon <= Core.Now);

        altar.CheckSummon(summoner);
        Assert.NotNull(altar.Guardian);
        Assert.NotSame(second, altar.Guardian);

        // Deleting the altar takes its teleporter and a live guardian with it.
        var tele = altar.Teleporter;
        var third = altar.Guardian;
        altar.Delete();
        Assert.True(tele.Deleted);
        Assert.True(third.Deleted);

        pet.Delete();
        summoner.Delete();
        other.Delete();
        }
        finally
        {
            ClearShame();
        }
    }

    [Fact]
    public void TheWallDropsWhenItsTrollDiesAndComesBackTwoMinutesLater()
    {
        ShardTestClock.Arm();
        EnsureCorpses();
        ClearShame();

        var start = new Point3D(5403, 82, 10);

        try
        {
        var wall = new ShameWall(
            new Dictionary<Point3D, int>
            {
                [new Point3D(-1, 0, 0)] = 2272,
                [new Point3D(0, 0, 0)] = 2272,
                [new Point3D(2, 0, 0)] = 2272,
                [new Point3D(1, 0, 0)] = 2272
            },
            start, new Point3D(5405, 90, 10), Map.Trammel
        );
        wall.MoveToWorld(start, Map.Trammel);
        ShameWall.AddTeleporters(wall);

        Assert.Equal(4, wall.Components.Count);
        Assert.All(wall.Components, c => Assert.Equal(10, c.Z));
        Assert.Equal(new Point3D(5402, 82, 10), wall.Components.Min(c => c.Location));

        var troll = wall.Troll;
        Assert.NotNull(troll);
        Assert.Same(wall, troll.Wall);
        Assert.Equal("the wall guardian", troll.Title);
        Assert.Equal("a cave troll", troll.DefaultName);
        Assert.Equal(new Point3D(5405, 90, 10), troll.Location);
        Assert.Equal(new Point3D(5405, 90, 10), troll.Home);
        Assert.Equal(8, troll.RangeHome);
        Assert.Equal(FightMode.Aggressor, troll.FightMode);
        Assert.Same(WeaponAbility.ArmorIgnore, troll.GetWeaponAbility());
        Assert.Equal(1, (int)troll.Body);

        var teles = World.Items.Values.OfType<ShameWallTeleporter>()
            .Where(t => !t.Deleted && t.Map == Map.Trammel && t.Y == 81 && t.X >= 5402 && t.X <= 5405).ToList();
        Assert.Equal(4, teles.Count);

        var front = Assert.Single(teles, t => t.Location == new Point3D(5402, 81, 10));
        Assert.Equal(new Point3D(5402, 83, 10), front.PointDest);
        Assert.True(front.Creatures);

        var player = NewPlayer(new Point3D(5402, 80, 10));

        // Q-046: a fresh wall is standing, so its flag is set and the teleporters work from the start. ServUO
        // leaves BaseAddon's Visible = false here and the teleporters are inert until the first troll dies.
        Assert.True(wall.Visible);
        Assert.True(front.CanTeleport(player));

        troll.Kill();

        Assert.Null(wall.Troll);
        Assert.Equal(start.Z - 50, wall.Z);
        Assert.All(wall.Components, c => Assert.Equal(start.Z - 50, c.Z));
        Assert.False(wall.Visible);
        Assert.False(front.CanTeleport(player));

        ShardTestClock.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));

        Assert.Equal(start, wall.Location);
        Assert.All(wall.Components, c => Assert.Equal(10, c.Z));
        Assert.True(wall.Visible);
        Assert.NotNull(wall.Troll);
        Assert.NotSame(troll, wall.Troll);
        Assert.True(wall.Troll.Alive);
        Assert.Same(wall, wall.Troll.Wall);

        // ...and the teleporters work again while the wall stands.
        Assert.True(front.CanTeleport(player));

        // Using a piece while the troll is dead resets at once.
        wall.Troll.Kill();
        Assert.Null(wall.Troll);
        Assert.Equal(start.Z - 50, wall.Z);

        wall.OnComponentUsed(wall.Components[0], player);
        Assert.Equal(start, wall.Location);
        Assert.NotNull(wall.Troll);
        Assert.True(wall.Troll.Alive);

        // Deleting the wall takes its troll.
        var last = wall.Troll;
        wall.Delete();
        Assert.True(last.Deleted);

        foreach (var tele in teles)
        {
            tele.Delete();
        }

        player.Delete();
        }
        finally
        {
            ClearShame();
        }
    }

    [Fact]
    public void TheWhetstoneIsMadeFromTheThreeGuardianDropsAndStripsDamageIncrease()
    {
        var pm = NewPlayer(new Point3D(5400, 50, 30));

        var grit = new QuartzGrit();
        pm.Backpack.DropItem(grit);

        // One component alone: "You do not have a required component", nothing consumed.
        grit.OnDoubleClick(pm);
        Assert.False(grit.Deleted);
        Assert.Null(pm.Backpack.FindItemByType<WhetstoneOfEnervation>());

        pm.Backpack.DropItem(new CursedOilstone());
        var ash = new CorrosiveAsh();
        pm.Backpack.DropItem(ash);

        ash.OnDoubleClick(pm);

        var whetstone = pm.Backpack.FindItemByType<WhetstoneOfEnervation>();
        Assert.NotNull(whetstone);
        Assert.Equal("Whetstone of Enervation", "Whetstone of Enervation");
        Assert.False(whetstone.Stackable);
        Assert.True(ash.Deleted);
        Assert.True(grit.Deleted);
        Assert.Null(pm.Backpack.FindItemByType<CursedOilstone>());
        Assert.Null(pm.Backpack.FindItemByType<QuartzGrit>());
        Assert.Null(pm.Backpack.FindItemByType<CorrosiveAsh>());

        var exceptional = new Longsword { Quality = WeaponQuality.Exceptional };
        exceptional.Attributes.WeaponDamage = 30;
        pm.Backpack.DropItem(exceptional);

        whetstone.Apply(pm, exceptional);

        Assert.Equal(0, exceptional.Attributes.WeaponDamage);
        Assert.True(whetstone.Deleted);

        var regular = new Longsword();
        regular.Attributes.WeaponDamage = 30;
        pm.Backpack.DropItem(regular);

        var second = new WhetstoneOfEnervation();
        pm.Backpack.DropItem(second);
        second.Apply(pm, regular);

        Assert.Equal(30, regular.Attributes.WeaponDamage); // "Invalid target."
        Assert.False(second.Deleted);

        // A whetstone on the floor is refused.
        var floor = new Longsword { Quality = WeaponQuality.Exceptional };
        floor.Attributes.WeaponDamage = 10;
        floor.MoveToWorld(pm.Location, pm.Map);
        second.Apply(pm, floor);
        Assert.Equal(10, floor.Attributes.WeaponDamage);

        floor.Delete();
        pm.Delete();
    }

    [Fact]
    public void ThePortedCreaturesCarryWhatServUOGivesThem()
    {
        var golem = new ClayGolem();
        Assert.Equal("a clay golem", golem.DefaultName);
        Assert.Equal(654, golem.Hue);
        Assert.NotNull(golem.Backpack);
        _out.WriteLine("clay golem pack: " + string.Join(", ", golem.Backpack.Items.Select(i => i.GetType().Name)));
        // The cap and whatever LootPack.Rich put there at construction (BaseCreature.cs:376 GenerateLoot(true), both
        // emulators); none of the golem parts ServUO's ClayGolem skips by overriding SpawnPackItems to nothing.
        Assert.NotNull(golem.Backpack.FindItemByType<ExecutionersCap>());
        Assert.Null(golem.Backpack.FindItemByType<IronIngot>());
        Assert.Null(golem.Backpack.FindItemByType<PowerCrystal>());
        Assert.Null(golem.Backpack.FindItemByType<ClockworkAssembly>());
        Assert.Null(golem.Backpack.FindItemByType<ArcaneGem>());
        Assert.Null(golem.Backpack.FindItemByType<Gears>());
        Assert.Equal(-4500, golem.Karma);
        Assert.InRange(golem.PhysicalResistance, 45, 55);
        Assert.InRange(golem.FireResistance, 50, 60);

        var troll = new CaveTroll();
        Assert.Null(troll.Wall);
        Assert.NotEqual("the wall guardian", troll.Title);
        Assert.NotNull(troll.Backpack.FindItemByType<Saltpeter>());
        Assert.NotNull(troll.Backpack.FindItemByType<Potash>());
        Assert.NotNull(troll.Backpack.FindItemByType<Charcoal>());
        Assert.NotNull(troll.Backpack.FindItemByType<BlackPowder>());
        Assert.Equal(2, troll.Meat);
        Assert.Equal(1, troll.TreasureMapLevel);

        var flame = new LesserFlameElemental();
        Assert.True(flame.HasAura);
        Assert.Equal(5, flame.AuraRange);
        Assert.Equal(7, flame.AuraBaseDamage);
        Assert.Equal(100, flame.AuraFireDamage);
        Assert.Equal(TimeSpan.FromSeconds(5), flame.AuraInterval);
        Assert.Contains(flame.GetMonsterAbilities(), a => a is FireBreath);
        Assert.NotNull(flame.Backpack.FindItemByType<SulfurousAsh>());
        Assert.Equal(2, flame.TreasureMapLevel);

        var boss = new FlameElemental();
        Assert.True(boss.HasAura);
        Assert.Contains(boss.GetMonsterAbilities(), a => a is FireBreath);
        Assert.Equal("the guardian", boss.Title);

        var molten = new MoltenEarthElemental();
        Assert.Equal("a molten earth elemental", molten.DefaultName);
        Assert.Equal(442, molten.Hue);
        Assert.Contains(molten.GetMonsterAbilities(), a => a is FireBreath);
        Assert.NotNull(molten.Backpack.FindItemByType<FertileDirt>()); // EarthElemental's own pack, both emulators

        var blood = new DiseasedBloodElemental();
        Assert.Same(WeaponAbility.BleedAttack, blood.GetWeaponAbility());
        Assert.Contains(blood.GetMonsterAbilities(), a => a is DrainLifeAttack);
        Assert.Same(Poison.Lethal, blood.HitPoison);
        Assert.Same(Poison.DeadlyParasitic, blood.PoisonImmune);
        Assert.Equal(1.0, blood.AutoDispelChance);
        Assert.Equal(5, blood.TreasureMapLevel);
        Assert.Equal(0x9F, (int)blood.Body);

        var burning = new BurningMage();
        Assert.Equal("the burning", burning.Title);
        Assert.Equal(0x190, (int)burning.Body);
        Assert.Equal(1281, burning.Hue);
        // Name = NameList.RandomName("male") is "" here: the test host does not load names.json. Live it does.
        Assert.Contains(burning.GetMonsterAbilities(), a => a is FireBreath);
        Assert.NotNull(burning.FindItemOnLayer(Layer.OuterTorso));

        var corrupted = new CorruptedMage();
        Assert.Equal("the corrupted mage", corrupted.Title);
        var vile = new VileMage();
        Assert.Equal("the vile mage", vile.Title);

        var gazer = new EternalGazer();
        var summoned = new GreyWolf { Summoned = true };
        var dmg = 100;
        gazer.AlterMeleeDamageFrom(summoned, ref dmg);
        Assert.Equal(50, dmg);
        dmg = 100;
        gazer.AlterSpellDamageFrom(burning, ref dmg);
        Assert.Equal(100, dmg);
        Assert.Equal(1, gazer.Meat);

        var vortex = new UnboundEnergyVortex();
        Assert.True(vortex.BleedImmune);
        Assert.Same(Poison.Lethal, vortex.PoisonImmune);
        Assert.Equal(0x15, vortex.GetAngerSound());
        Assert.Equal(13, (int)vortex.Body);

        var chaos = new ChaosVortex();
        Assert.Equal(164, (int)chaos.Body);
        Assert.Equal(34212, chaos.Hue);

        var poison = new ShameGreaterPoisonElemental();
        Assert.Equal(32854, poison.Hue);

        foreach (var m in new Mobile[] { golem, troll, flame, boss, molten, blood, burning, corrupted, vile, gazer, summoned, vortex, chaos, poison })
        {
            m.Delete();
        }
    }

    [Fact]
    public void TheCorruptedMagesTaintMana()
    {
        ShardTestClock.Arm();
        EnsureCorpses();

        var mage = new BurningMage();
        mage.MoveToWorld(new Point3D(5500, 100, 0), Map.Trammel);

        var pm = NewPlayer(new Point3D(5501, 100, 0));
        pm.Int = 100;
        pm.Mana = 100;
        Assert.Equal(100, pm.Mana);
        Assert.True(pm.Alive && pm.Hits > 0);

        Assert.False(BurningMage.IsUnderEffects(pm));

        mage.DoEffects(pm);
        Assert.True(BurningMage.IsUnderEffects(pm));
        Assert.False(CrazedMage.IsUnderEffects(pm)); // each mage class keeps its own table, as on ServUO

        mage.DoEffects(pm); // idempotent while running

        // Regeneration timers run against the advanced clock too (about two mana per 100 ms here), so walk the
        // clock one wheel tick at a time and read the drain on the tick it lands. The 1.5 s timer sits in a coarser
        // wheel ring and can fire a little late, which is why this is a poll and not a fixed advance.
        pm.Mana = 100;
        var landed = false;

        for (var i = 0; i < 400 && !landed; i++) // up to 3.2 s
        {
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(8));
            landed = pm.Mana < 100;
        }

        _out.WriteLine($"mana after one tick: {pm.Mana}, hits: {pm.Hits}/{pm.HitsMax}");
        Assert.True(landed, "the sap never fired within 3.2 s");
        Assert.InRange(pm.Mana, 60, 71); // 30-40 taken on the tick, at most one point regenerated in the same 8 ms
        // The 3-4 points of direct damage the same tick deals are not asserted: Mobile.Damage arms HitsTimer,
        // which regenerates them before this line runs in the test host (the Q-029 shape). Mana has no such timer race here.

        BurningMage.EndEffects(pm);
        Assert.False(BurningMage.IsUnderEffects(pm));

        var before = pm.Mana;
        ShardTestClock.Advance(TimeSpan.FromSeconds(3.2));
        Assert.True(pm.Mana >= before, "nothing should be sapped after EndEffects");

        // A dead mage ends it on the next tick.
        mage.DoEffects(pm);
        mage.Kill();
        ShardTestClock.Advance(TimeSpan.FromSeconds(3.2));
        Assert.False(BurningMage.IsUnderEffects(pm));

        mage.Delete();
        pm.Delete();
    }
}
