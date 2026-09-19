// CC4 Despise: the "spawns generate" gate for the Despise revamp, plus the possession loop and the one
// upstream patch the port needed.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh
// and run as a gate by docker/uo/build.sh. See notes/s4-test-route.md and notes/s8-test-route.md.
//
// docs/tasks.md says CC4 is done when "each dungeon builds and its spawns generate". A green build says
// nothing about the second half, so this test drives the same code [SetupDespise runs and asserts on what
// comes out: 47 spawners, every one of them full after Respawn, every spawn a DespiseCreature from the
// right roster with a power level in the spawner's range, the lower level at 4-8, and the controller
// finding its 15 good and 15 evil army spawners by name inside the dynamic lower region.
//
// The test host has no map files (TileMatrix logs "map1.mul was not found" and serves an empty land block,
// TileData is skipped under xUnit), so CanSpawnMobile sees passable land at z 0 everywhere and every spawn
// lands inside its rectangle at z 0. Real-map placement is the headless run's job (notes/cc4-despise.md).
//
// THE PATCH THIS PINS: server/patches/BaseCreature-PlayerMobile-can-auto-stable.patch adds ServUO's
// BaseCreature.CanAutoStable and honours it in PlayerMobile.AutoStablePets. Without it a possessed Despise
// creature is taken into the stable when its master logs out, which ServUO never allows. Proved red on
// 2026-09-19 by removing DespiseCreature's override (notes/cc4-despise.md, verification section).

using System;
using System.Linq;
using Server;
using Server.Engines.Despise;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class DespiseRevampedVerification
{
    private readonly ITestOutputHelper _out;

    public DespiseRevampedVerification(ITestOutputHelper output) => _out = output;

    private static PlayerMobile NewPlayer(int karma, Point3D loc)
    {
        var pm = new PlayerMobile();
        pm.AddItem(new Backpack());
        pm.Karma = karma;
        pm.MoveToWorld(loc, Map.Trammel);
        return pm;
    }

    [Fact]
    public void SetupGeneratesTheSpawnersAndTheSpawnersGenerateDespiseCreatures()
    {
        DespiseRevampedSetup.DeleteDespise();
        Assert.False(DespiseSpawns.AnyPresent());

        var spawners = DespiseSpawns.Generate();

        Assert.Equal(47, spawners.Count);
        Assert.Equal(47, DespiseSpawns.Definitions.Length);

        // Respawn() spawns Count times, but each of the eight entries is capped at its own maxCount, so a
        // spawner can never hold more than 8 * EntryMaxCount. Two XML rows ("DespiseRevamped #13" at 5495,748
        // and "#14") ask for 9 with entries capped at 1 and so hold 8 on both emulators: 229 requested, 227
        // possible. Anything below the capacity is a Spawn(entry) failure (bad type, bad position).
        static int Capacity(DespiseSpawns.Definition d) => Math.Min(d.MaxCount, d.EntryMaxCount * d.Roster.Length);

        var requested = DespiseSpawns.Definitions.Sum(d => d.MaxCount);
        var expected = DespiseSpawns.Definitions.Sum(Capacity);
        var spawned = spawners.Sum(s => s.Spawned.Count);

        _out.WriteLine($"spawners={spawners.Count} requested={requested} capacity={expected} actual={spawned}");

        Assert.Equal(229, requested);
        Assert.Equal(227, expected);
        Assert.Equal(expected, spawned);

        var lowerCount = 0;

        foreach (var spawner in spawners)
        {
            var def = DespiseSpawns.Definitions.First(d => d.Name == spawner.Name && d.CentreX == spawner.X && d.CentreY == spawner.Y);

            Assert.True(spawner.Running, $"{spawner.Name} not running");
            Assert.Equal(Capacity(def), spawner.Spawned.Count);
            Assert.Equal(5, spawner.WalkingRange);
            Assert.Equal(def.PowerMin, spawner.PowerMin);
            Assert.Equal(def.PowerMax, spawner.PowerMax);

            foreach (var spawn in spawner.Spawned.Keys)
            {
                var creature = Assert.IsAssignableFrom<DespiseCreature>(spawn);

                Assert.Contains(creature.GetType().Name, def.Roster);
                Assert.Equal(def.Roster == DespiseSpawns.GoodRoster ? Alignment.Good : Alignment.Evil, creature.Alignment);
                Assert.InRange(creature.Power, def.PowerMin, def.PowerMax);
                Assert.Equal(Map.Trammel, creature.Map);
                Assert.True(spawner.SpawnBounds.Contains(creature.Location), $"{creature.GetType().Name} at {creature.Location} outside {spawner.Name}");
                Assert.Equal(5, creature.RangeHome);
                Assert.True(creature.NoKillAwards);
                Assert.False(creature.Commandable);

                if (def.IsLowerLevel)
                {
                    lowerCount++;
                    Assert.True(creature.Power >= 4, $"lower-level {creature.GetType().Name} rolled {creature.Power}");
                }
            }

            // Every roster entry is a real type with the powerLevel constructor the spawner relies on.
            foreach (var type in def.Roster)
            {
                Assert.NotNull(AssemblyHandler.FindTypeByName(type));
            }
        }

        Assert.Equal(30 * 5, lowerCount);

        // The live server has regions.json's static "Despise" DungeonRegion (Trammel, priority 50, 5377-5631 x
        // 516-1022) under the whole dungeon; the test host loads no regions.json. Register the same region here
        // FIRST, so that Region.Find has the tie to break that the 2026-09-19 headless run lost (it returned the
        // static dungeon everywhere and the controller found 0 army spawners). DespiseRegion.DespisePriority wins it.
        var staticDungeon = new DungeonRegion(
            "Despise", Map.Trammel, Region.DefaultPriority,
            Region.ConvertTo3D(new Rectangle2D(5377, 516, 255, 507))
        );
        staticDungeon.Register();
        Assert.Same(staticDungeon, Region.Find(new Point3D(5400, 800, 0), Map.Trammel));

        // The controller finds the army spawners by name inside the dynamic lower region, as ServUO's does.
        var controller = new DespiseController();
        controller.MoveToWorld(new Point3D(5571, 626, 30), Map.Trammel);

        Assert.Same(controller, DespiseController.Instance);
        Assert.True(controller.Enabled);
        Assert.Equal(15, controller.GoodSpawnerCount);
        Assert.Equal(15, controller.EvilSpawnerCount);

        // Region.Find returns the dynamic regions inside their rectangles, by name.
        Assert.Equal("Despise Lower", Region.Find(new Point3D(5400, 800, 0), Map.Trammel).Name);
        Assert.Equal("Despise Good", Region.Find(new Point3D(5400, 560, 0), Map.Trammel).Name);
        Assert.Equal("Despise Evil", Region.Find(new Point3D(5400, 700, 0), Map.Trammel).Name);
        Assert.Equal("Despise Start", Region.Find(new Point3D(5575, 630, 0), Map.Trammel).Name);
        Assert.IsType<DespiseRegion>(Region.Find(new Point3D(5400, 800, 0), Map.Trammel));

        // A spawned creature stands in a Despise region (its OnBeforeDeath power award depends on that).
        var sample = spawners[0].Spawned.Keys.OfType<DespiseCreature>().First();
        Assert.True(sample.Region.IsPartOf<DespiseRegion>());

        // Travel: recall out yes, recall/gate/mark in no; staff exempt.
        var lower = (BaseRegion)controller.LowerRegion;
        var player = NewPlayer(0, new Point3D(5400, 800, 0));
        Assert.True(lower.CheckTravel(player, player.Location, TravelCheckType.RecallFrom, out _));
        Assert.False(lower.CheckTravel(player, player.Location, TravelCheckType.RecallTo, out _));
        Assert.False(lower.CheckTravel(player, player.Location, TravelCheckType.GateTo, out _));
        Assert.False(lower.CheckTravel(player, player.Location, TravelCheckType.Mark, out _));
        player.AccessLevel = AccessLevel.GameMaster;
        Assert.True(lower.CheckTravel(player, player.Location, TravelCheckType.RecallTo, out _));
        player.Delete();

        // Disabling unregisters the four regions; deleting the controller does too.
        controller.Enabled = false;
        Assert.Same(staticDungeon, Region.Find(new Point3D(5400, 800, 0), Map.Trammel));
        controller.Enabled = true;
        Assert.IsType<DespiseRegion>(Region.Find(new Point3D(5400, 800, 0), Map.Trammel));

        controller.Delete();
        Assert.Null(DespiseController.Instance);
        Assert.IsNotType<DespiseRegion>(Region.Find(new Point3D(5400, 800, 0), Map.Trammel));
        Assert.Same(staticDungeon, Region.Find(new Point3D(5400, 800, 0), Map.Trammel));
        staticDungeon.Unregister();

        // [DeleteDespise takes the spawners and their spawns with it.
        var before = World.Mobiles.Values.OfType<DespiseCreature>().Count(c => !c.Deleted);
        Assert.True(before >= expected);
        DespiseRevampedSetup.DeleteDespise();
        Assert.False(DespiseSpawns.AnyPresent());
        Assert.All(spawners, s => Assert.True(s.Deleted));
    }

    [Fact]
    public void TheAnkhGrantsAnOrbToMatchingKarmaAndTheOrbPossessesAndReleases()
    {
        var ankh = new DespiseAnkh(Alignment.Good);
        ankh.MoveToWorld(new Point3D(5474, 525, 79), Map.Trammel);

        var good = NewPlayer(5000, new Point3D(5474, 526, 79));
        var evil = NewPlayer(-5000, new Point3D(5474, 527, 79));

        // Wrong karma: "Thy spirit be not compatible with our goals!"
        ankh.OnComponentUsed(ankh.Components[0], evil);
        Assert.Null(evil.Backpack.FindItemByType<WispOrb>());

        ankh.OnComponentUsed(ankh.Components[0], good);
        var orb = good.Backpack.FindItemByType<WispOrb>();

        Assert.NotNull(orb);
        Assert.Equal(Alignment.Good, orb.Alignment);
        Assert.Same(good, orb.Owner);
        Assert.Equal(LootType.Blessed, orb.LootType);
        Assert.Contains(orb, WispOrb.Orbs);
        Assert.Same(orb, DespiseController.GetWispOrb(good));

        // "Thou can guide but one of us."
        ankh.OnComponentUsed(ankh.Components[0], good);
        Assert.Single(good.Backpack.Items.OfType<WispOrb>());

        // Possession, the body of WispOrb.InternalTarget.OnTarget.
        var silenii = new Silenii(3);
        silenii.MoveToWorld(new Point3D(5475, 526, 79), Map.Trammel);

        Assert.Equal(3, silenii.Power);
        Assert.Equal(Alignment.Good, silenii.Alignment);
        Assert.Equal(500, silenii.Karma); // Karma = GetKarmaGood is read while Power is still 1 (1 * 500), as on ServUO

        orb.Anchor = good;
        orb.Pet = silenii;
        silenii.Link(orb);
        silenii.SetControlMaster(good);
        silenii.ControlTarget = good;
        silenii.ControlOrder = OrderType.Follow;

        Assert.Same(orb, silenii.Orb);
        Assert.Same(silenii, orb.Pet);
        Assert.True(silenii.Controlled);
        Assert.Same(good, silenii.ControlMaster);
        Assert.Equal(2, silenii.RangeHome);
        Assert.Equal(9, orb.GetArmyPower());
        Assert.Equal(ShortLeash(silenii), silenii.GetLeashLength());

        orb.LeashLength = LeashLength.Long;
        Assert.Equal(silenii.LongLeashLength, silenii.GetLeashLength());

        // Release.
        silenii.Unlink();

        Assert.Null(orb.Pet);
        Assert.Null(silenii.Orb);
        Assert.False(silenii.Controlled);
        Assert.Equal(10, silenii.RangeHome);
        Assert.Equal(Aggression.Aggressive, orb.Aggression);
        Assert.Equal(0, orb.GetArmyPower());

        // Deleting the orb takes it off the static list.
        orb.Delete();
        Assert.DoesNotContain(orb, WispOrb.Orbs);
        Assert.Null(DespiseController.GetWispOrb(good));

        silenii.Delete();
        good.Delete();
        evil.Delete();
        ankh.Delete();
    }

    private static int ShortLeash(DespiseCreature c) => c.ShortLeashLength;

    [Fact]
    public void TheOverlordTakesDamageOnlyFromDespiseCreatures()
    {
        var boss = new AndrosTheDreadLord();
        boss.MoveToWorld(new Point3D(5556, 823, 45), Map.Trammel);

        var player = NewPlayer(5000, new Point3D(5557, 823, 45));
        var hits = boss.Hits;

        boss.Damage(500, player);
        Assert.Equal(hits, boss.Hits);

        boss.Damage(500);
        Assert.Equal(hits, boss.Hits);

        var phantom = new Phantom(5);
        phantom.MoveToWorld(new Point3D(5558, 823, 45), Map.Trammel);

        boss.Damage(500, phantom);
        Assert.True(boss.Hits < hits, $"boss hits {hits} -> {boss.Hits}");

        Assert.True(boss.AlwaysMurderer);
        Assert.False(boss.InitialInnocent);
        Assert.Equal(100, boss.FollowersMax);
        Assert.Contains(typeof(HailstormGargoyle), DespiseBoss.Artifacts);
        Assert.Equal(7, DespiseBoss.Artifacts.Length);

        var adrian = new AdrianTheGloriousLord();
        Assert.True(adrian.InitialInnocent);
        Assert.IsType<EnsorcledWisp>(adrian.SummonWisp);
        Assert.IsType<CorruptedWisp>(boss.SummonWisp);

        adrian.Delete();
        phantom.Delete();
        player.Delete();
        boss.Delete();
    }

    [Fact]
    public void APossessedDespiseCreatureIsNotAutoStabledOnLogoutButAStockPetIs()
    {
        var pm = NewPlayer(5000, new Point3D(5480, 570, 60));

        var orb = new WispOrb(pm, Alignment.Good);
        pm.Backpack.DropItem(orb);

        var silenii = new Silenii(4);
        silenii.MoveToWorld(new Point3D(5481, 570, 60), Map.Trammel);
        orb.Anchor = pm;
        orb.Pet = silenii;
        silenii.SetControlMaster(pm);

        var wolf = new GreyWolf();
        wolf.MoveToWorld(new Point3D(5482, 570, 60), Map.Trammel);
        wolf.SetControlMaster(pm);

        Assert.Equal(2, pm.AllFollowers.Count);
        Assert.True(Core.SE);

        pm.AutoStablePets();

        Assert.True(wolf.IsStabled, "the stock pet should have been stabled");
        Assert.Same(pm, wolf.StabledBy);

        Assert.False(silenii.IsStabled, "the possessed Despise creature must not be stabled (ServUO CanAutoStable)");
        Assert.Same(pm, silenii.ControlMaster);
        Assert.Same(orb, silenii.Orb);
        Assert.Equal(Map.Trammel, silenii.Map);

        orb.Delete();
        silenii.Delete();
        wolf.Delete();
        pm.Delete();
    }
}
