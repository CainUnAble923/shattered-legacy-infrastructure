// cc-P194 Wrong (revamped): the "spawns generate" gate for the Wrong revamp, plus the two regions, the hot-item
// chests, the creatures, the items and the prisoner's escort quest that a green build says nothing about.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh. See notes/s4-test-route.md and notes/s8-test-route.md.
//
// The test host has no map files (land at z 0 everywhere), loads no regions.json (the static "Wrong" region of
// both facets is registered here where a fact needs it, and WrongRegions.Register parents its two regions to it),
// installs no skill callbacks, and runs every fact in one world in no fixed order, so each fact clears every
// Wrong object first and last. Hit points are read on the tick the damage lands (D15). Notes:
// shard-migration/notes/cc-P194-port-wrong-and-covetous-void.md.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.MLQuests.Objectives;
using Server.Engines.MLQuests.Rewards;
using Server.Engines.Spawners;
using Server.Engines.WrongRevamp;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class WrongRevampedVerification
{
    private readonly ITestOutputHelper _out;

    public WrongRevampedVerification(ITestOutputHelper output) => _out = output;

    private static readonly string[] SpawnTypeNames =
    {
        "Archmage", "BrigandCannibal", "CaveTrollWrong", "DemonicJailor", "Executioner", "Fezzik", "HungryOgre",
        "LizardmanDefender", "LizardmanSquatter", "PrisonRat", "SavagePackWolf", "WrongPrisoner"
    };

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

    // regions.json: "Wrong", DungeonRegion, priority 50, {5633,511}-{5886,1021}, on both facets.
    private static List<Region> RegisterWrongRegions()
    {
        var list = new List<Region>();

        foreach (var map in new[] { Map.Trammel, Map.Felucca })
        {
            var region = new DungeonRegion("Wrong", map, Region.DefaultPriority, Region.ConvertTo3D(new Rectangle2D(5633, 511, 254, 511)));
            region.Register();
            list.Add(region);
        }

        return list;
    }

    private static void ClearWrong()
    {
        WrongGenerator.DeleteWrongRevamp();

        foreach (var item in World.Items.Values.ToList())
        {
            if (item is BedrollSpawner or WrongBedrollBase or MysteriousTunnel or HotItemChest or WrongMazeWall or
                WrongBarredMetalDoor or StinkingCauldron or GooeyMaggotSlime ||
                item is Spawner spawner && WrongSpawns.IsWrongSpawner(spawner))
            {
                item.Delete();
            }
        }

        foreach (var mobile in World.Mobiles.Values.ToList())
        {
            if (mobile is GooeyMaggots or Fezzik or DemonicJailor or WrongPrisoner)
            {
                mobile.Delete();
            }
        }
    }

    [Fact]
    public void GenerateBuildsTheDungeonAndDeleteRemovesIt()
    {
        ShardTestClock.Arm();

        var regions = RegisterWrongRegions();
        ClearWrong();

        try
        {
            // What a world has before the revamp, inside the dungeon rectangle: ServUO's DeleteOldWrong removes them.
            var old = new Spawner(
                1, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), 0,
                new Rectangle3D(5700, 600, Region.MinZ, 4, 4, Region.MaxZ - Region.MinZ), "Brigand"
            )
            {
                Name = "Old Wrong"
            };
            old.MoveToWorld(new Point3D(5701, 601, 0), Map.Trammel);
            old.Respawn();
            var oldSpawn = Assert.Single(old.Spawned.Keys);

            var stockTele = new Teleporter(new Point3D(1, 1, 1), Map.Trammel);
            stockTele.MoveToWorld(new Point3D(5700, 700, 0), Map.Trammel);

            var stockDoor = new BarredMetalDoor(DoorFacing.WestCW);
            stockDoor.MoveToWorld(new Point3D(5710, 610, 0), Map.Felucca);

            var sparkle = new Static(0x375A);
            sparkle.MoveToWorld(new Point3D(5720, 620, 0), Map.Trammel);

            var outside = new Static(0x375A);
            outside.MoveToWorld(new Point3D(1500, 1500, 0), Map.Trammel);

            var deleted = WrongGenerator.DeleteOldWrong();
            _out.WriteLine($"DeleteOldWrong: {deleted}");

            Assert.True(deleted >= 4);
            Assert.True(old.Deleted && stockTele.Deleted && stockDoor.Deleted && sparkle.Deleted);
            Assert.True(oldSpawn.Deleted, "deleting a spawner deletes what it spawned");
            Assert.False(outside.Deleted, "a sparkle outside the dungeon rectangle is not touched");
            Assert.False(WrongSpawns.AnyPresent());

            var report = WrongGenerator.Generate();
            _out.WriteLine(report);

            // The 38 spawners.
            var spawners = World.Items.Values.OfType<Spawner>().Where(s => !s.Deleted && WrongSpawns.IsWrongSpawner(s)).ToList();

            Assert.Equal(38, spawners.Count);
            Assert.Equal(38, WrongSpawns.Definitions.Length);
            Assert.Equal(19, WrongSpawns.Definitions.Count(d => d.Felucca));
            Assert.Equal(10, WrongSpawns.Definitions.Count(d => d.IsPrisoner));

            var requested = WrongSpawns.Definitions.Sum(d => d.MaxCount);
            var capacity = WrongSpawns.Definitions.Sum(d => d.Capacity);
            var spawned = spawners.Sum(s => s.Spawned.Count);

            _out.WriteLine($"spawners={spawners.Count} requested={requested} capacity={capacity} actual={spawned}");

            Assert.Equal(126, requested);
            Assert.Equal(68, capacity);
            Assert.Equal(capacity, spawned);

            var byType = new Dictionary<string, int>();

            foreach (var spawner in spawners)
            {
                var def = WrongSpawns.Definitions.First(
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

                    var creature = Assert.IsAssignableFrom<BaseCreature>(spawn);
                    Assert.Equal(def.Range, creature.RangeHome);
                    Assert.Equal(def.HomeRangeRelative ? creature.Location : spawner.Location, creature.Home);

                    if (def.IsPrisoner)
                    {
                        var prisoner = Assert.IsType<WrongPrisoner>(spawn);
                        Assert.True(prisoner.IsPrisoner);
                    }
                }

                foreach (var entry in def.Entries)
                {
                    Assert.NotNull(AssemblyHandler.FindTypeByName(entry.Type));
                }
            }

            foreach (var (type, count) in byType.OrderBy(kv => kv.Key))
            {
                _out.WriteLine($"  {type}: {count}");
            }

            Assert.Equal(SpawnTypeNames.Length, byType.Count);
            Assert.All(SpawnTypeNames, name => Assert.True(byType.ContainsKey(name), $"{name} never spawned"));
            Assert.Equal(10, byType["WrongPrisoner"]);
            Assert.Equal(2, byType["Fezzik"]);
            Assert.Equal(6, byType["DemonicJailor"]);

            // The maze: every piece of the cfg stands on both facets, once.
            Assert.Equal(290, WrongDecoration.Pieces.Length);

            foreach (var map in new[] { Map.Trammel, Map.Felucca })
            {
                Assert.All(WrongDecoration.Pieces, piece => Assert.True(WrongDecoration.Exists(map, piece), $"{piece} missing on {map}"));
                Assert.Equal(224, World.Items.Values.OfType<WrongMazeWall>().Count(w => !w.Deleted && w.Map == map));
                Assert.Equal(34, World.Items.Values.OfType<WrongBarredMetalDoor>().Count(d => !d.Deleted && d.Map == map));
                Assert.All(World.Items.Values.OfType<WrongBarredMetalDoor>().Where(d => d.Map == map), d => Assert.True(d.Locked && !d.Movable));
                Assert.All(World.Items.Values.OfType<WrongMazeWall>().Where(w => w.Map == map), w => Assert.False(w.Visible));
                Assert.Equal(1, World.Items.Values.Count(i => i is BarredMetalDoor && !i.Deleted && i.Map == map && WrongGenerator.DungeonBounds.Contains(i.Location)));
            }

            // The bedroll spawner lays its bedrolls and tunnels ten seconds in.
            var bedrollSpawners = World.Items.Values.OfType<BedrollSpawner>().Where(b => !b.Deleted).ToList();

            Assert.Equal(2, bedrollSpawners.Count);
            Assert.All(bedrollSpawners, b => Assert.Equal(WrongGenerator.BedrollSpawnerLocation, b.Location));
            Assert.Contains(bedrollSpawners, b => b.Map == Map.Trammel);
            Assert.Contains(bedrollSpawners, b => b.Map == Map.Felucca);
            Assert.All(bedrollSpawners, b => Assert.Empty(b.Bedrolls));

            ShardTestClock.Advance(TimeSpan.FromSeconds(11));

            foreach (var b in bedrollSpawners)
            {
                Assert.Equal(80, b.Bedrolls.Count);
                Assert.Equal(5, b.MysteriousTunnels.Count);
                Assert.All(b.Bedrolls, r => Assert.True(!r.Deleted && !r.Movable && r.Map == b.Map));
                Assert.All(b.MysteriousTunnels, t => Assert.True(!t.Deleted && t.Map == b.Map));
                Assert.Equal(BedrollSpawner.Entries.Select(e => e.Location).OrderBy(p => p.X).ThenBy(p => p.Y).ToArray(),
                    b.Bedrolls.Select(r => r.Location).OrderBy(p => p.X).ThenBy(p => p.Y).ToArray());
                Assert.Equal(BedrollSpawner.OutsideTunnels, b.MysteriousTunnels.Select(t => t.Location).ToArray());

                // The first three tunnels lead to a bedroll, which leads back; the other two lead to a room.
                // Each picks its bedroll with Utility.Random over all 80 (ServUO's), so two can pick the same one; the
                // later link then overwrites the way back (cc-P245: red in about one full-suite run in eight).
                for (var i = 0; i < 3; i++)
                {
                    var tunnel = b.MysteriousTunnels[i];
                    var bedroll = Assert.Single(b.Bedrolls, r => r.Location == tunnel.PointDest);
                    var lastLink = Enumerable.Range(0, 3).Last(j => b.MysteriousTunnels[j].PointDest == tunnel.PointDest);
                    Assert.Equal(BedrollSpawner.OutsideTunnels[lastLink], bedroll.PointDest);
                    Assert.Same(b, bedroll.BedrollSpawner);
                }

                for (var i = 3; i < 5; i++)
                {
                    Assert.Contains(b.MysteriousTunnels[i].PointDest, BedrollSpawner.RoomDestinations);
                }

                var linked = b.Bedrolls.Count(r => r.PointDest != Point3D.Zero);
                _out.WriteLine($"{b.Map}: {linked} bedrolls lead outside");
                Assert.InRange(linked, 3, 5);
            }

            // The hot-item chests.
            var chests = World.Items.Values.OfType<HotItemChest>().Where(c => !c.Deleted).ToList();

            Assert.Equal(14, chests.Count);

            foreach (var map in new[] { Map.Trammel, Map.Felucca })
            {
                var onMap = chests.Where(c => c.Map == map).OrderBy(c => c.Location.X).ThenBy(c => c.Location.Y).ToList();
                Assert.Equal(7, onMap.Count);
                Assert.Equal(EnchantedHotItem.ChestLocs.OrderBy(p => p.X).ThenBy(p => p.Y), onMap.Select(c => c.Location));
            }

            foreach (var chest in chests)
            {
                Assert.True(chest.Locked && !chest.Movable);
                Assert.Equal(92, chest.RequiredSkill);
                Assert.InRange(chest.LockLevel, 82, 91);
                Assert.Equal(92, chest.MaxLockLevel);
                Assert.InRange(chest.Items.Count, 2, 4);
                Assert.Contains(chest.Items, i => i is SpellScroll);
                Assert.Equal(chest.Items.Count - 1, chest.Items.Count(i => EnchantedHotItem.IsHot(i)));
                Assert.All(chest.Items.Where(EnchantedHotItem.IsHot), i => Assert.Equal(1258, i.Hue));
            }

            // The sixteen teleporters.
            foreach (var map in new[] { Map.Trammel, Map.Felucca })
            {
                foreach (var (dest, at) in WrongGenerator.Teleporters)
                {
                    var tele = Assert.Single(World.Items.Values.OfType<Teleporter>(), t => !t.Deleted && t.Map == map && t.Location == at);
                    Assert.Equal(dest, tele.PointDest);
                }
            }

            // The command runs again: deletes first, then places the same set once more; the maze is not doubled.
            WrongGenerator.DeleteOldWrong();
            WrongGenerator.Generate();

            Assert.Equal(38, World.Items.Values.OfType<Spawner>().Count(s => !s.Deleted && WrongSpawns.IsWrongSpawner(s)));
            Assert.Equal(2, World.Items.Values.OfType<BedrollSpawner>().Count(b => !b.Deleted));
            Assert.Equal(14, World.Items.Values.OfType<HotItemChest>().Count(c => !c.Deleted));
            Assert.Equal(16, World.Items.Values.OfType<Teleporter>().Count(t => !t.Deleted && WrongGenerator.DungeonBounds.Contains(t.Location)));
            Assert.Equal(448, World.Items.Values.OfType<WrongMazeWall>().Count(w => !w.Deleted));
            Assert.Equal(68, World.Items.Values.OfType<WrongBarredMetalDoor>().Count(d => !d.Deleted));
            Assert.All(bedrollSpawners, b => Assert.True(b.Deleted));

            // [DeleteWrongRevamp takes it all with it.
            deleted = WrongGenerator.DeleteWrongRevamp();
            _out.WriteLine($"DeleteWrongRevamp: {deleted}");

            Assert.True(deleted >= 38 + 290 * 2 + 2 + 14 + 16);
            Assert.False(WrongSpawns.AnyPresent());
            Assert.DoesNotContain(World.Items.Values.OfType<WrongMazeWall>(), w => !w.Deleted);
            Assert.DoesNotContain(World.Items.Values.OfType<HotItemChest>(), c => !c.Deleted);
            Assert.DoesNotContain(World.Items.Values.OfType<BedrollSpawner>(), b => !b.Deleted);
            Assert.DoesNotContain(World.Items.Values.OfType<WrongBedrollBase>(), b => !b.Deleted);
            Assert.DoesNotContain(World.Items.Values.OfType<MysteriousTunnel>(), t => !t.Deleted);
            Assert.DoesNotContain(World.Items.Values.OfType<Teleporter>(), t => !t.Deleted && WrongGenerator.DungeonBounds.Contains(t.Location));
            Assert.DoesNotContain(World.Mobiles.Values.OfType<WrongPrisoner>(), p => !p.Deleted);
        }
        finally
        {
            ClearWrong();

            foreach (var region in regions)
            {
                region.Unregister();
            }
        }
    }

    [Fact]
    public void Level3ReturnsTheDeadToTheirCellAndTheJailIsPeaceful()
    {
        ShardTestClock.Arm();

        var regions = RegisterWrongRegions();
        var level3 = WrongRegions.Register(Map.Trammel);

        try
        {
            Assert.NotNull(level3);
            Assert.True(level3.Registered);
            Assert.Same(level3, WrongRegions.Register(Map.Trammel)); // registering again is the same region

            var jail = Assert.IsType<WrongJail>(Region.Find(WrongRegions.CellLocation, Map.Trammel));
            Assert.Same(level3, jail.Parent);
            Assert.Same(regions[0], level3.Parent);
            Assert.True(jail.IsPartOf("Wrong"), "the jail is still part of Wrong, which the hot-item curse relies on");
            Assert.IsType<WrongLevel3>(Region.Find(new Point3D(5690, 630, 0), Map.Trammel));
            Assert.Equal("Wrong", Region.Find(new Point3D(5800, 600, 0), Map.Trammel).Name);

            var pm = NewPlayer(new Point3D(5690, 630, 0));
            var other = NewPlayer(new Point3D(5691, 630, 0));

            Assert.False(jail.AllowHarmful(pm, other));
            Assert.True(level3.AllowHarmful(pm, other));

            // Dead in level 3: back to the cell, alive again, and remembered.
            pm.Player = true;
            pm.Body = 0x192;
            Assert.False(pm.Alive);

            level3.OnDeath(pm);

            Assert.Equal(WrongRegions.CellLocation, pm.Location);
            Assert.True(pm.Alive, "the first death in level 3 is a resurrection in the cell");
            Assert.Contains(pm, level3.DeathList);

            // Dead again while remembered: a real death, and the memory is cleared.
            pm.MoveToWorld(new Point3D(5690, 630, 0), Map.Trammel);
            pm.Body = 0x192;
            level3.OnDeath(pm);

            Assert.Equal(WrongRegions.CellLocation, pm.Location);
            Assert.False(pm.Alive, "the second death in a row is not a resurrection");
            Assert.DoesNotContain(pm, level3.DeathList);

            // Leaving the region forgets a remembered player.
            pm.Body = 0x190;
            pm.MoveToWorld(new Point3D(5690, 630, 0), Map.Trammel);
            pm.Body = 0x192;
            level3.OnDeath(pm);
            Assert.Contains(pm, level3.DeathList);
            pm.MoveToWorld(new Point3D(5800, 600, 0), Map.Trammel);
            Assert.DoesNotContain(pm, level3.DeathList);

            pm.Delete();
            other.Delete();
        }
        finally
        {
            WrongRegions.Unregister(Map.Trammel);

            foreach (var region in regions)
            {
                region.Unregister();
            }
        }
    }

    [Fact]
    public void HotItemsBurnInWrongAndCoolOutsideAndTheChestRestocks()
    {
        ShardTestClock.Arm();

        var regions = RegisterWrongRegions();
        ClearWrong();

        try
        {
            var chest = new HotItemChest(3648);
            chest.MoveToWorld(EnchantedHotItem.ChestLocs[0], Map.Trammel);
            chest.SpawnLoot();

            Assert.InRange(chest.Items.Count, 2, 4);
            var hot = chest.Items.First(EnchantedHotItem.IsHot);
            Assert.Same(chest, EnchantedHotItem.GetChest(hot));
            Assert.Equal(1258, hot.Hue);

            var pm = NewPlayer(new Point3D(5703, 660, 0));
            Assert.True(pm.Region.IsPartOf("Wrong"));
            pm.AddToBackpack(hot);

            var before = pm.Hits;
            EnchantedHotItem.OnTick();
            var burned = before - pm.Hits;
            _out.WriteLine($"hot item tick in Wrong: {burned}");
            Assert.InRange(burned, 10, 13);
            Assert.True(EnchantedHotItem.IsHot(hot), "the curse stays while the holder is in Wrong");

            // Out of the dungeon: the next tick lifts the curse and the hue (a player, as the tick asks).
            pm.Player = true;
            pm.MoveToWorld(new Point3D(1500, 1500, 0), Map.Trammel);
            pm.Hits = pm.HitsMax;
            before = pm.Hits;
            EnchantedHotItem.OnTick();

            Assert.Equal(before, pm.Hits);
            Assert.False(EnchantedHotItem.IsHot(hot));
            Assert.Equal(0, hot.Hue);

            // The registry survives a save.
            EnchantedHotItem.Attach(hot, chest);
            var buffer = new byte[16384];
            var writer = new BufferWriter(buffer, true);
            EnchantedHotItem.Serialize(writer);
            writer.Flush();
            EnchantedHotItem.Remove(hot);
            Assert.False(EnchantedHotItem.IsHot(hot));
            EnchantedHotItem.Deserialize(new BufferReader(buffer));
            Assert.True(EnchantedHotItem.IsHot(hot));
            Assert.Same(chest, EnchantedHotItem.GetChest(hot));
            EnchantedHotItem.Remove(hot);

            // Picking the lock opens it; ten minutes later it is locked and restocked.
            var oldItems = chest.Items.ToList();
            chest.LockPick(pm);
            Assert.False(chest.Locked);

            ShardTestClock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

            Assert.True(chest.Locked, "the chest locks itself again ten minutes after the pick");
            Assert.All(oldItems, i => Assert.True(i.Deleted, "the old loot is replaced"));
            Assert.InRange(chest.Items.Count, 2, 4);
            Assert.Contains(chest.Items, i => i is SpellScroll);
            Assert.All(chest.Items.Where(i => i is not SpellScroll), i => Assert.True(EnchantedHotItem.IsHot(i)));

            // The chest round-trips as a locked container.
            var copy = RoundTrip(chest);
            Assert.True(copy.Locked);
            Assert.Equal(92, copy.RequiredSkill);
            copy.Delete();

            pm.Delete();
            chest.Delete();
        }
        finally
        {
            ClearWrong();

            foreach (var region in regions)
            {
                region.Unregister();
            }
        }
    }

    [Fact]
    public void TheCreaturesConstructRoundTripAndKeepTheirTricks()
    {
        ShardTestClock.Arm();
        EnsureCorpses();
        ClearWrong();

        try
        {
            var creatures = new BaseCreature[]
            {
                new Archmage(), new BrigandCannibal(), new BrigandCannibalMage(), new CaveTrollWrong(), new DemonicJailor(),
                new Fezzik(), new GooeyMaggots(), new HungryOgre(), new LizardmanDefender(), new LizardmanSquatter(),
                new PrisonRat(), new SavagePackWolf(), new WrongPrisoner()
            };

            foreach (var creature in creatures)
            {
                creature.MoveToWorld(new Point3D(5750, 600, 0), Map.Trammel);
                var copy = (BaseCreature)RoundTrip<Mobile>(creature);
                Assert.Equal(creature.Name, copy.Name);
                Assert.Equal(creature.Body, copy.Body);
                Assert.Equal(creature.Hue, copy.Hue);
                copy.Delete();
            }

            Assert.Equal("Fezzik", creatures[5].DefaultName);
            Assert.Equal("The Ogre Cook", creatures[5].Title);
            Assert.Equal("a Cave Troll", creatures[3].DefaultName);
            Assert.Equal(443, creatures[10].Hue);
            Assert.Equal(15, creatures[4].RangePerception);
            Assert.Equal(FightMode.Weakest, creatures[11].FightMode);
            Assert.Contains(creatures[2].GetMonsterAbilities(), a => a is DrainLifeAttack);
            Assert.True(creatures[1].Title == "the brigand cannibal");
            Assert.Equal(94.0, creatures[2].Skills.Necromancy.Base, 1);

            // Gooey maggots leave slime behind and live three minutes.
            var maggots = (GooeyMaggots)creatures[6];
            var start = new Point3D(5760, 610, 0);
            maggots.MoveToWorld(start, Map.Trammel);

            foreach (var old in World.Items.Values.OfType<GooeyMaggotSlime>().ToList())
            {
                old.Delete(); // the move from the round-trip spot above left one; the test wants the next
            }

            maggots.Location = new Point3D(5761, 610, 0);
            var slime = Assert.Single(World.Items.Values.OfType<GooeyMaggotSlime>(), s => !s.Deleted);
            Assert.Equal(start, slime.Location);
            Assert.Equal(363, slime.Hue);

            ShardTestClock.Advance(TimeSpan.FromSeconds(10.5));
            Assert.True(slime.Deleted, "slime fades after ten seconds");

            ShardTestClock.Advance(TimeSpan.FromMinutes(3));
            Assert.True(maggots.Deleted, "gooey maggots live three minutes");

            // Fezzik drops a stinking cauldron when hit, one every two minutes, and it bursts into maggots.
            var fezzik = (Fezzik)creatures[5];
            fezzik.MoveToWorld(new Point3D(5770, 620, 0), Map.Trammel);
            fezzik.SpawnGreenGoo();
            var cauldron = Assert.Single(World.Items.Values.OfType<StinkingCauldron>(), c => !c.Deleted);
            Assert.Equal(fezzik.Location, cauldron.Location);
            fezzik.SpawnGreenGoo();
            Assert.Single(World.Items.Values.OfType<StinkingCauldron>(), c => !c.Deleted);

            ShardTestClock.Advance(TimeSpan.FromSeconds(10.5));
            Assert.True(cauldron.Deleted, "the cauldron bursts ten seconds in");
            var born = World.Mobiles.Values.OfType<GooeyMaggots>().Where(m => !m.Deleted).ToList();
            Assert.InRange(born.Count, 1, 2);
            Assert.All(born, m => Assert.True(m.InRange(fezzik.Location, 2)));

            // The jailor sends anyone it hits back to the cell and slows a caster who hurts it.
            var jailor = (DemonicJailor)creatures[4];
            jailor.MoveToWorld(new Point3D(5690, 630, 0), Map.Trammel);
            var pm = NewPlayer(new Point3D(5691, 630, 0));
            jailor.OnGaveMeleeAttack(pm, 5);
            Assert.Equal(DemonicJailor.CellLocation, pm.Location);

            Assert.False(DemonicJailor.UnderParalyzeAttack(pm));
            jailor.OnDamagedBySpell(pm, 10);
            Assert.True(DemonicJailor.UnderParalyzeAttack(pm));
            ShardTestClock.Advance(TimeSpan.FromSeconds(15.5));
            Assert.False(DemonicJailor.UnderParalyzeAttack(pm));

            pm.Delete();

            foreach (var creature in creatures)
            {
                if (!creature.Deleted)
                {
                    creature.Delete();
                }
            }

            foreach (var m in born)
            {
                m.Delete();
            }
        }
        finally
        {
            ClearWrong();
        }
    }

    [Fact]
    public void TheItemsConstructRoundTripAndWork()
    {
        ShardTestClock.Arm();
        ClearWrong();

        try
        {
            var pm = NewPlayer(new Point3D(5750, 600, 0));

            // A bedroll with a destination opens a tunnel; the tunnel moves a player.
            var spawner = new BedrollSpawner();
            spawner.MoveToWorld(new Point3D(5823, 601, 0), Map.Trammel);
            var bedroll = new WrongBedrollEast { PointDest = new Point3D(5670, 550, 22), BedrollSpawner = spawner };
            bedroll.MoveToWorld(new Point3D(5751, 600, 0), Map.Trammel);

            var copy = RoundTrip(bedroll);
            Assert.Equal(bedroll.PointDest, copy.PointDest);
            Assert.Same(spawner, copy.BedrollSpawner);
            copy.Delete();

            bedroll.OnDoubleClick(pm);
            Assert.True(bedroll.Deleted);
            var tunnel = Assert.Single(spawner.MysteriousTunnels);
            Assert.Equal(new Point3D(5751, 600, 0), tunnel.Location);
            Assert.Equal(new Point3D(5670, 550, 22), tunnel.PointDest);

            Assert.False(tunnel.OnMoveOver(pm));
            Assert.Equal(tunnel.PointDest, pm.Location);
            Assert.Equal(tunnel.PointDest, RoundTrip(tunnel).PointDest);

            // A bedroll without one is torn apart.
            pm.MoveToWorld(new Point3D(5750, 600, 0), Map.Trammel);
            var plain = new WrongBedrollSouth();
            plain.MoveToWorld(new Point3D(5751, 600, 0), Map.Trammel);
            Assert.Equal(1022645, plain.LabelNumber);
            plain.OnDoubleClick(pm);
            Assert.True(plain.Deleted);

            var far = new WrongBedrollSouth();
            far.MoveToWorld(new Point3D(5760, 600, 0), Map.Trammel);
            far.OnDoubleClick(pm);
            Assert.False(far.Deleted, "too far away to search");
            far.Delete();
            spawner.Delete();

            // Soap stacks; the cauldron and tunnel are plain items.
            var soap = new Soap(3);
            Assert.True(soap.Stackable);
            Assert.Equal(3, soap.Amount);
            Assert.Equal(1285, soap.Hue);
            Assert.Equal(3, RoundTrip(soap).Amount);
            soap.Delete();

            // The maze door: picked open for ten seconds; telekinesis at 100 Magery unlocks it; loads locked.
            var door = new WrongBarredMetalDoor(DoorFacing.WestCCW);
            door.MoveToWorld(new Point3D(5697, 639, 0), Map.Trammel);
            Assert.True(door.Locked);
            Assert.Equal(100, door.RequiredSkill);
            Assert.Equal(80, door.LockLevel);
            Assert.Equal(110, door.MaxLockLevel);

            door.LockPick(pm);
            Assert.False(door.Locked);
            Assert.Same(pm, door.Picker);
            ShardTestClock.Advance(TimeSpan.FromSeconds(10.5));
            Assert.True(door.Locked, "the door locks again ten seconds after the pick");

            pm.Skills.Magery.Base = 100.0;
            pm.MoveToWorld(new Point3D(5698, 639, 0), Map.Trammel);
            door.OnTelekinesis(pm);
            Assert.False(door.Locked, "telekinesis at 100 Magery unlocks it");
            ShardTestClock.Advance(TimeSpan.FromSeconds(10.5));
            Assert.True(door.Locked);

            door.Locked = false;
            var doorCopy = RoundTrip(door);
            Assert.True(doorCopy.Locked, "a door loaded from a save is locked");
            Assert.Equal(100, doorCopy.RequiredSkill);
            doorCopy.Delete();
            door.Delete();

            // The maze wall shows itself to whoever stands beside it.
            var wall = new WrongMazeWall();
            wall.MoveToWorld(new Point3D(5700, 650, 0), Map.Trammel);
            Assert.False(wall.Visible);
            Assert.Equal(578, wall.ItemID);
            pm.MoveToWorld(new Point3D(5701, 650, 0), Map.Trammel);
            wall.OnMovement(pm, new Point3D(5710, 650, 0));
            Assert.True(wall.Visible);
            pm.MoveToWorld(new Point3D(5710, 650, 0), Map.Trammel);
            wall.OnMovement(pm, new Point3D(5701, 650, 0));
            Assert.False(wall.Visible);
            wall.Delete();

            // The decoration table builds every kind of piece.
            foreach (var piece in WrongDecoration.Pieces.GroupBy(p => (p.Type, p.ItemID)).Select(g => g.First()))
            {
                var item = WrongDecoration.Construct(piece);
                Assert.NotNull(item);
                Assert.Equal(piece.ItemID, item.ItemID);
                item.Delete();
            }

            // The blanket of darkness unrolls and rolls like a bedroll.
            var blanket = new BlanketOfDarkness();
            blanket.MoveToWorld(new Point3D(5751, 600, 0), Map.Trammel);
            pm.MoveToWorld(new Point3D(5750, 600, 0), Map.Trammel);
            Assert.Equal(0xA57, blanket.ItemID);
            Assert.Equal(2076, blanket.Hue);
            blanket.OnDoubleClick(pm);
            Assert.Equal(0xA56, blanket.ItemID);
            blanket.OnDoubleClick(pm);
            Assert.Equal(0xA57, blanket.ItemID);
            Assert.Equal(2076, RoundTrip(blanket).Hue);
            blanket.Delete();

            // The torture rack: three pieces each way, deeds that build them.
            var east = new TortureRackEast();
            Assert.Equal(3, east.Components.Count);
            Assert.All(east.Components, c => Assert.Equal(1152307, c.LabelNumber));
            Assert.IsType<TortureRackEastDeed>(east.Deed);
            Assert.IsType<TortureRackSouth>(new TortureRackSouthDeed().Addon);
            Assert.Equal(new[] { 0x4AAB, 0x4AA3, 0x4AA2 }, east.Components.Select(c => c.ItemID));
            east.Delete();

            pm.Delete();
        }
        finally
        {
            ClearWrong();
        }
    }

    [Fact]
    public void ThePrisonerOffersTheEscortToTheWrongEntrance()
    {
        ShardTestClock.Arm();
        ClearWrong();

        try
        {
            if (MLQuestSystem.FindQuest(typeof(EscortToWrongEntrance)) == null)
            {
                WrongEscortQuests.Configure();
            }

            var quest = Assert.IsType<EscortToWrongEntrance>(MLQuestSystem.FindQuest(typeof(EscortToWrongEntrance)));
            Assert.True(quest.Activated);
            Assert.True(quest.IsEscort);
            Assert.Equal(1152096, quest.Title.Number);
            Assert.Equal(1152097, quest.Description.Number);
            Assert.Equal(1152099, quest.RefusalMessage.Number);
            Assert.Equal(1152100, quest.InProgressMessage.Number);
            Assert.Equal(1152101, quest.CompletionMessage.Number);

            var objective = Assert.IsType<EscortObjective>(Assert.Single(quest.Objectives));
            Assert.Equal("Wrong Entrance", objective.Destination.RegionName);

            var reward = Assert.IsType<ItemReward>(Assert.Single(quest.Rewards));
            Assert.Equal(1062577, reward.Name.Number);

            var prisoner = new WrongPrisoner();
            prisoner.MoveToWorld(new Point3D(5651, 555, 20), Map.Trammel);

            Assert.True(prisoner.IsPrisoner);
            Assert.True(prisoner.IsInvulnerable);
            Assert.Equal("the prisoner", prisoner.Title);
            Assert.Equal(0x190, prisoner.Body.BodyID);
            Assert.Equal(33802, prisoner.Hue);
            Assert.Contains(prisoner.Items, i => i is PlateChest);
            Assert.Contains(prisoner.Items, i => i is PlateLegs);
            Assert.Same(quest, Assert.Single(prisoner.MLQuests));
            Assert.Contains(quest, MLQuestSystem.FindQuestList(typeof(WrongPrisoner)));

            var copy = (WrongPrisoner)RoundTrip<Mobile>(prisoner);
            Assert.True(copy.IsPrisoner);
            copy.Delete();
            prisoner.Delete();
        }
        finally
        {
            ClearWrong();
        }
    }
}
