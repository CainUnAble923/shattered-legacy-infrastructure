// cc-P195 the Exodus Encounter: the generate and delete commands, the creatures and items (construct, round trip,
// spawn), the ritual from the deed to Ver Lor Reg, the boss's death and the exit, the hidden chests, the Nexus
// puzzle, the Keypunch Reader, the Craftsman path's items, the loot box and stealing hooks, and the decaying items.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh. See notes/s4-test-route.md and notes/s8-test-route.md.
//
// The test host has no map files (land at z 0 everywhere), loads no regions.json (Ver Lor Reg is registered by
// ExodusRegions.Register where a fact needs it, the shrine region here), installs no skill callbacks, and runs every
// fact in one world in no fixed order, so each fact clears every Exodus object first and last. Timers are made
// after Arm (D16); hit points are read on the tick the damage lands (D15). Notes:
// shard-migration/notes/cc-P195-port-exodus-encounter.md.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Engines.CreatureStealing;
using Server.Engines.Exodus;
using Server.Engines.Harvest;
using Server.Engines.PartySystem;
using Server.Engines.Spawners;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Regions;
using Server.Targeting;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ExodusEncounterVerification
{
    private readonly ITestOutputHelper _out;

    public ExodusEncounterVerification(ITestOutputHelper output) => _out = output;

    private static readonly Type[] CreatureTypes =
    {
        typeof(ClockworkExodus), typeof(DupresChampion), typeof(DupresKnight), typeof(DupresSquire),
        typeof(ExodusDrone), typeof(ExodusJuggernaut), typeof(ExodusMinionLord), typeof(ExodusSentinel),
        typeof(ExodusZealot)
    };

    private static readonly Type[] ItemTypes =
    {
        typeof(ExodusAlterAddon), typeof(ExodusChest), typeof(ExodusNexus), typeof(ExodusSacrificalDagger),
        typeof(ExodusSacrificalGargishDagger), typeof(ExodusSummoningAlter), typeof(ExodusSummoningRite),
        typeof(ExoticToolkit), typeof(KeypunchReader), typeof(MechanicalComponent),
        typeof(PunchCard), typeof(RobeofRite), typeof(Asclepius), typeof(BracersofAlchemicalDevastation),
        typeof(ClockworkLeggings), typeof(DupresSword), typeof(GargishAsclepius),
        typeof(GargishBracersofAlchemicalDevastation), typeof(GargishClockworkLeggings), typeof(GargishDupresSword),
        typeof(HygieiasAmulet), typeof(ScrollofValiantCommendation), typeof(GoldDust), typeof(NexusCore),
        typeof(SmallPieceofBlackrock), typeof(AbyssalCloth), typeof(NexusAddon), typeof(NexusAddonDeed),
        typeof(DeathVortexTrap), typeof(VerLorRegController)
    };

    private static PlayerMobile NewPlayer(Point3D loc, Map map)
    {
        var pm = new PlayerMobile();
        pm.AddItem(new Backpack());
        pm.Player = true; // before MoveToWorld, so the sector lists it and Region.GetPlayers finds it
        pm.MoveToWorld(loc, map);
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

    private static Party MakeParty(params PlayerMobile[] members)
    {
        var party = new Party(members[0]);
        members[0].Party = party;

        for (var i = 1; i < members.Length; i++)
        {
            party.Members.Add(new PartyMemberInfo(members[i]));
            members[i].Party = party;
        }

        return party;
    }

    private static void ClearExodus()
    {
        ExodusGenerator.DeleteExodusEncounter();

        foreach (var item in World.Items.Values.ToList())
        {
            if (item is VerLorRegController or ExodusChest or ExodusNexus or KeypunchReader or ExodusTomeAltar or
                ExodusAlterAddon or DeathVortexTrap or NexusAddon ||
                item is Spawner spawner && ExodusSpawns.IsExodusSpawner(spawner))
            {
                item.Delete();
            }
        }

        foreach (var mobile in World.Mobiles.Values.ToList())
        {
            if (mobile is ClockworkExodus or ExodusArchZealot || CreatureTypes.Contains(mobile.GetType()))
            {
                mobile.Delete();
            }
        }

        foreach (var region in Region.Regions.OfType<ExodusChestRegion>().ToList())
        {
            region.Unregister();
        }

        ClockworkExodus.Instances.Clear();
        ExodusTomeAltar.Instances.Clear();
        ExodusTomeAltar.Altar = null;
    }

    private static VerLorRegController PlaceController()
    {
        var controller = new VerLorRegController();
        controller.MoveToWorld(ExodusEncounter.ControllerLocation, Map.Ilshenar);
        controller.StartHere();
        return controller;
    }

    [Fact]
    public void GenerateBuildsTheEncounterAndDeleteRemovesIt()
    {
        ShardTestClock.Arm();
        EnsureCorpses();
        ClearExodus();
        ExodusRegions.Register(Map.Ilshenar);

        try
        {
            Assert.False(ExodusGenerator.AnyPresent());

            var report = ExodusGenerator.Generate();
            _out.WriteLine(report);

            var controller = VerLorRegController.IlshenarInstance;
            Assert.NotNull(controller);
            Assert.Equal(ExodusEncounter.ControllerLocation, controller.Location);
            Assert.Equal(Map.Ilshenar, controller.Map);
            Assert.True(VerLorRegController.Active);

            var boss = VerLorRegController.CurrentExodus;
            Assert.NotNull(boss);
            Assert.False(boss.Deleted);
            Assert.Equal(ExodusEncounter.ExodusLocation, boss.Location);
            Assert.Equal(4, boss.RangeHome);
            Assert.True(boss.Region.IsPartOf(ExodusRegions.Name));

            Assert.Single(World.Items.Values.OfType<PublicMoongate>(), g => g.Location == ExodusEncounter.MoongateLocation && g.Map == Map.Ilshenar);
            Assert.Single(World.Items.Values.OfType<AnkhOfSacrificeAddon>(), a => a.Location == ExodusEncounter.AnkhLocation && a.Map == Map.Ilshenar);

            var nexus = World.Items.Values.OfType<ExodusNexus>().Where(n => !n.Deleted).ToList();
            Assert.Equal(39, nexus.Count);
            Assert.Equal(39, ExodusDecoration.NexusPoints.Length);
            Assert.All(nexus, n => Assert.Equal(Map.Ilshenar, n.Map));
            Assert.All(nexus, n => Assert.Contains(n.Location, ExodusDecoration.NexusPoints));
            Assert.All(nexus, n => Assert.False(n.Active));
            Assert.All(nexus, n => Assert.True(n.Path.Length >= 2 * n.SideLength - 1));

            var reader = Assert.Single(World.Items.Values.OfType<KeypunchReader>(), r => !r.Deleted);
            Assert.Equal(ExodusDecoration.KeypunchReaderLocation, reader.Location);

            var zealot = Assert.Single(World.Mobiles.Values.OfType<ExodusArchZealot>(), z => !z.Deleted);
            Assert.Equal(ExodusEncounter.ArchZealotLocation, zealot.Location);
            Assert.True(zealot.Blessed && zealot.CantWalk);

            // The spawners: 17 rows in the data, 16 spawners (the Arch Zealot row is the mobile above).
            var spawners = World.Items.Values.OfType<Spawner>().Where(s => !s.Deleted && ExodusSpawns.IsExodusSpawner(s)).ToList();

            Assert.Equal(17, ExodusSpawns.Definitions.Length);
            Assert.Equal(16, spawners.Count);
            Assert.Equal(12, spawners.Count(s => s.Name == ExodusSpawns.ChestSpawnerName));

            var capacity = ExodusSpawns.Definitions.Where(d => !d.IsArchZealot).Sum(d => d.Capacity);
            var spawned = spawners.Sum(s => s.Spawned.Count);
            _out.WriteLine($"spawners={spawners.Count} capacity={capacity} spawned={spawned}");

            Assert.Equal(90, capacity);
            Assert.Equal(capacity, spawned);

            foreach (var spawner in spawners)
            {
                var def = ExodusSpawns.Definitions.First(
                    d => d.Name == spawner.Name && d.CentreX == spawner.X && d.CentreY == spawner.Y && d.Entries.Length == spawner.Entries.Count
                );

                Assert.True(spawner.Running);
                Assert.Equal(Map.Ilshenar, spawner.Map);
                Assert.Equal(def.Range, spawner.WalkingRange);
                Assert.True(spawner.SpawnLocationIsHome);
                Assert.Equal(TimeSpan.FromMinutes(5), spawner.MinDelay);
                Assert.Equal(TimeSpan.FromMinutes(10), spawner.MaxDelay);

                var allowed = def.Entries.Select(e => e.Type).ToArray();

                foreach (var spawn in spawner.Spawned.Keys)
                {
                    Assert.Contains(spawn.GetType().Name, allowed);
                }
            }

            var chests = World.Items.Values.OfType<ExodusChest>().Where(c => !c.Deleted).ToList();
            Assert.Equal(12, chests.Count);
            Assert.All(chests, c => Assert.False(c.Visible));
            Assert.All(chests, c => Assert.True(c.Locked));
            Assert.All(chests, c => Assert.NotNull(c.ChestRegion));

            Assert.True(ExodusGenerator.AnyPresent());

            // A second run replaces, never doubles.
            var deleted = ExodusGenerator.DeleteExodusEncounter();
            _out.WriteLine($"deleted={deleted}");

            Assert.True(deleted >= 60, $"deleted {deleted}");
            Assert.True(controller.Deleted);
            Assert.True(boss.Deleted);
            Assert.All(nexus, n => Assert.True(n.Deleted));
            Assert.True(reader.Deleted);
            Assert.True(zealot.Deleted);
            Assert.All(spawners, s => Assert.True(s.Deleted));
            Assert.All(chests, c => Assert.True(c.Deleted));
            Assert.Null(VerLorRegController.IlshenarInstance);
            Assert.False(VerLorRegController.Active);
            Assert.False(ExodusGenerator.AnyPresent());
        }
        finally
        {
            ClearExodus();
            ExodusRegions.Unregister(Map.Ilshenar);
        }
    }

    [Fact]
    public void EveryCreatureConstructsRoundTripsAndKeepsItsShape()
    {
        ShardTestClock.Arm();
        ClearExodus();

        try
        {
            foreach (var type in CreatureTypes)
            {
                var bc = (BaseCreature)Activator.CreateInstance(type)!;
                bc.MoveToWorld(new Point3D(1969, 114, -28), Map.Ilshenar);

                var copy = RoundTrip(bc);

                Assert.Equal(bc.Body, copy.Body);
                Assert.Equal(bc.Hue, copy.Hue);
                Assert.Equal(bc.HitsMax, copy.HitsMax);
                Assert.Equal(bc.Fame, copy.Fame);
                Assert.True(bc.HitsMax > 0);

                _out.WriteLine($"{type.Name}: body {bc.Body} hue {bc.Hue} hits {bc.HitsMax} name '{bc.Name}'");

                copy.Delete();
                bc.Delete();
            }

            // The four clockworks carry pinned's barrier and energy bolt counter; the minion lord keeps ServUO's case.
            foreach (var type in new[] { typeof(ExodusDrone), typeof(ExodusJuggernaut), typeof(ExodusMinionLord), typeof(ExodusSentinel) })
            {
                var bc = (BaseCreature)Activator.CreateInstance(type)!;
                var abilities = bc.GetMonsterAbilities();
                Assert.Contains(abilities, a => a is MagicalBarrier);
                Assert.Contains(abilities, a => a is EnergyBoltCounter);
                bc.Delete();
            }

            var lord = new ExodusMinionLord();
            Assert.Equal("exodus minion lord", lord.Name);
            lord.Delete();

            var zealot = new ExodusZealot();
            Assert.Equal("The Exodus Zealot", zealot.Title);
            Assert.NotNull(zealot.FindItemOnLayer(Layer.OuterTorso));
            Assert.False(zealot.FindItemOnLayer(Layer.OuterTorso).Movable);
            zealot.Delete();

            var exodus = new ClockworkExodus();
            Assert.Equal(1248, exodus.Body.BodyID);
            Assert.Equal(50000, exodus.HitsMax);
            Assert.Equal(50000, exodus.MinHits);
            Assert.Equal(WeaponAbility.BleedAttack, exodus.GetWeaponAbility());
            Assert.True(exodus.HasAura);
            Assert.Contains(exodus, ClockworkExodus.Instances);
            var exodusCopy = RoundTrip(exodus);
            Assert.Equal(exodus.MinHits, exodusCopy.MinHits);
            exodusCopy.Delete();
            exodus.Delete();
            Assert.DoesNotContain(exodus, ClockworkExodus.Instances);

            // The spawn data's type names all resolve to the types above or the chest.
            foreach (var def in ExodusSpawns.Definitions)
            {
                foreach (var entry in def.Entries)
                {
                    Assert.NotNull(AssemblyHandler.FindTypeByName(entry.Type));
                }
            }
        }
        finally
        {
            ClearExodus();
        }
    }

    [Fact]
    public void EveryItemConstructsAndRoundTrips()
    {
        ShardTestClock.Arm();
        ClearExodus();

        try
        {
            foreach (var type in ItemTypes)
            {
                // Loot.Construct fills an optional amount (GoldDust, blackrock, abyssal cloth) as [add does.
                var item = Loot.Construct(type);
                Assert.NotNull(item);
                var copy = RoundTrip(item);

                Assert.Equal(item.ItemID, copy.ItemID);
                Assert.Equal(item.Hue, copy.Hue);
                Assert.Equal(item.LabelNumber, copy.LabelNumber);

                _out.WriteLine($"{type.Name}: id 0x{item.ItemID:X} hue {item.Hue} label {item.LabelNumber}");

                copy.Delete();
                item.Delete();
            }

            // The saved state the generator carries: the dagger's and robe's clocks, the tome's steps and owner, the nexus path.
            var dagger = new ExodusSacrificalDagger { TimeLeft = 1234 };
            var daggerCopy = RoundTrip(dagger);
            Assert.Equal(1234, daggerCopy.TimeLeft);

            var robe = new RobeofRite { TimeLeft = 4321 };
            var robeCopy = RoundTrip(robe);
            Assert.Equal(4321, robeCopy.TimeLeft);

            var card = new PunchCard { TimeLeft = 77 };
            var cardCopy = RoundTrip(card);
            Assert.Equal(77, cardCopy.TimeLeft);

            var owner = NewPlayer(new Point3D(1858, 876, 12), Map.Trammel);
            var tome = new ExodusTomeAltar(owner);
            tome.MoveToWorld(new Point3D(1858, 875, 12), Map.Trammel);
            Assert.NotNull(tome.Steps);
            Assert.Equal(new Point3D(1857, 874, -6), tome.Steps.Location);
            var tomeCopy = RoundTrip(tome);
            Assert.Same(tome.Steps, tomeCopy.Steps);
            Assert.Same(owner, tomeCopy.Owner);

            var nexus = new ExodusNexus(4);
            var nexusCopy = RoundTrip(nexus);
            Assert.Equal(4, nexusCopy.SideLength);
            Assert.Equal(nexus.Path, nexusCopy.Path);

            var chest = new ExodusChest();
            Assert.True(chest.Items.OfType<Gold>().Any());
            Assert.True(chest.Items.Count >= 1 + Loot.GemTypes.Length);
            var chestCopy = RoundTrip(chest);
            Assert.True(chestCopy.Locked);
            Assert.False(chestCopy.Visible);

            var scroll = new ScrollofValiantCommendation { Owner = "Tester" };
            Assert.Equal("Tester", RoundTrip(scroll).Owner);

            foreach (var item in new Item[] { dagger, daggerCopy, robe, robeCopy, card, cardCopy, tome, tomeCopy, nexus, nexusCopy, chest, chestCopy, scroll })
            {
                item.Delete();
            }

            owner.Delete();
        }
        finally
        {
            ClearExodus();
        }
    }

    [Fact]
    public void TheRitualBuildsTheTomeAndCarriesThePartyToVerLorReg()
    {
        ShardTestClock.Arm();
        EnsureCorpses();
        ClearExodus();
        ExodusRegions.Register(Map.Ilshenar);

        var shrine = new BaseRegion("Shrine of Compassion", Map.Trammel, Region.DefaultPriority, Region.ConvertTo3D(new Rectangle2D(1848, 865, 20, 20)));
        shrine.Register();

        var a = NewPlayer(new Point3D(1858, 876, 12), Map.Trammel);
        var b = NewPlayer(new Point3D(1859, 876, 12), Map.Trammel);

        try
        {
            PlaceController();
            Assert.True(ExodusSummoningAlter.CheckExodus());

            Assert.True(a.Region.IsPartOf("Shrine of Compassion"));
            var deed = new ExodusSummoningAlter();
            a.AddToBackpack(deed);

            // The test host has no tile data (D27): a StaticTarget masks its item id to 0 here, so the facts hand the
            // deed the tile and its id the way the target does.
            var tile = new Point3D(1858, 875, 12);
            const int shrineFloor = 0x14A0;

            // No party: the deed refuses through its double-click; Build itself is the target's path.
            MakeParty(a, b);
            Assert.Same(Party.Get(a), Party.Get(b));

            Assert.Null(deed.Build(a, tile, 0x0001)); // not a shrine tile
            Assert.False(deed.Deleted);

            var tome = deed.Build(a, tile, shrineFloor);
            Assert.NotNull(tome);
            Assert.True(deed.Deleted);
            Assert.Equal(new Point3D(1858, 875, 12), tome.Location);
            Assert.Equal(Map.Trammel, tome.Map);
            Assert.Same(a, tome.Owner);
            Assert.Contains(tome, ExodusTomeAltar.Instances);
            Assert.Equal(420, tome.TimeLeft);

            // A second deed on the same shrine: an altar has already been built here.
            var second = new ExodusSummoningAlter();
            a.AddToBackpack(second);
            Assert.Null(second.Build(a, tile, shrineFloor));
            Assert.False(second.Deleted);
            second.Delete();

            // A stranger is not of the party.
            var stranger = NewPlayer(new Point3D(1860, 876, 12), Map.Trammel);
            var strangerRite = new ExodusSummoningRite();
            strangerRite.Read(stranger, tome);
            Assert.False(strangerRite.Deleted);
            Assert.Null(tome.FindRitual(stranger));
            strangerRite.Delete();
            stranger.Delete();

            foreach (var pm in new[] { a, b })
            {
                // AddItem, not EquipItem: the host refuses to equip (cc-P193's finding; the set tests do the same).
                var robe = new RobeofRite();
                pm.AddItem(robe);
                Assert.Same(robe, pm.FindItemOnLayer(Layer.OuterTorso));
                var dagger = new ExodusSacrificalDagger();
                pm.AddItem(dagger);

                Assert.False(ExodusTomeAltar.HasUsedRite(pm));

                // The dagger before the rite: 1153603, nothing happens.
                dagger.Sacrifice(pm, tome);
                Assert.False(dagger.Deleted);

                var rite = new ExodusSummoningRite();
                pm.AddToBackpack(rite);
                rite.Read(pm, tome);

                Assert.True(rite.Deleted);
                Assert.True(tome.FindRitual(pm) is { Ritual1: true, Ritual2: false });
                Assert.True(ExodusTomeAltar.HasUsedRite(pm));

                // The rite twice: already used.
                var again = new ExodusSummoningRite();
                again.Read(pm, tome);
                Assert.False(again.Deleted);
                again.Delete();

                var karmaBefore = pm.Karma;
                dagger.Sacrifice(pm, tome);

                Assert.True(dagger.Deleted);
                Assert.True(tome.FindRitual(pm) is { Ritual1: true, Ritual2: true });
                Assert.False(ExodusTomeAltar.HasUsedRite(pm));
                Assert.True(pm.Karma > karmaBefore, $"karma {karmaBefore} -> {pm.Karma}");
            }

            // Not the owner: refused; the owner: the party goes to Ver Lor Reg, the robes burn, the tome follows.
            Assert.Equal(Map.Trammel, a.Map);

            // A member out of range holds the ritual.
            b.MoveToWorld(new Point3D(1870, 876, 12), Map.Trammel);
            Assert.False(tome.SendConfirmationsExodus(a));
            Assert.Equal(Map.Trammel, a.Map);
            b.MoveToWorld(new Point3D(1859, 876, 12), Map.Trammel);

            Assert.True(tome.SendConfirmationsExodus(a));

            Assert.Equal(Map.Ilshenar, a.Map);
            Assert.Equal(Map.Ilshenar, b.Map);
            Assert.Equal(ExodusEncounter.BattlegroundArrival, a.Location);
            Assert.Equal(ExodusEncounter.BattlegroundArrival, b.Location);
            Assert.Null(a.FindItemOnLayer(Layer.OuterTorso));
            Assert.Null(b.FindItemOnLayer(Layer.OuterTorso));

            ShardTestClock.Advance(TimeSpan.FromSeconds(3));
            Assert.True(tome.Deleted);
            Assert.True(tome.Steps == null || tome.Steps.Deleted);
            Assert.DoesNotContain(tome, ExodusTomeAltar.Instances);
        }
        finally
        {
            a.Delete();
            b.Delete();
            shrine.Unregister();
            ClearExodus();
            ExodusRegions.Unregister(Map.Ilshenar);
        }
    }

    [Fact]
    public void TheBossDeathStopsTheControllerAndTheExitRaisesANewExodus()
    {
        ShardTestClock.Arm();
        EnsureCorpses();
        ClearExodus();
        var region = ExodusRegions.Register(Map.Ilshenar);

        var pm = NewPlayer(new Point3D(840, 600, 0), Map.Ilshenar);

        try
        {
            Assert.True(pm.Region.IsPartOf(ExodusRegions.Name));

            var controller = PlaceController();
            var boss = VerLorRegController.CurrentExodus;
            Assert.NotNull(boss);

            // Below 60 percent and the low-water mark follows; back above 75 and it resets: the altar's gate.
            boss.Hits = (int)(boss.HitsMax * 0.5);
            boss.OnDamage(1, pm, false);
            Assert.Equal(boss.Hits, boss.MinHits);
            Assert.False(ExodusSummoningAlter.CheckExodus());
            boss.Hits = (int)(boss.HitsMax * 0.7);
            boss.OnDamage(1, pm, false);
            Assert.False(ExodusSummoningAlter.CheckExodus(), "at 70 with a mark under 60 a new ritual waits");
            boss.Hits = boss.HitsMax;
            boss.OnDamage(1, pm, false);
            Assert.Equal(boss.HitsMax, boss.MinHits);
            Assert.True(ExodusSummoningAlter.CheckExodus());

            // A melee hit from a player calls the vortices once per spot.
            var before = World.Items.Values.Count(i => i is DeathVortexTrap && !i.Deleted);
            boss.OnGotMeleeAttack(pm, 5);
            var after = World.Items.Values.Count(i => i is DeathVortexTrap && !i.Deleted);
            Assert.Equal(3, after - before);
            boss.OnGotMeleeAttack(pm, 5);
            Assert.Equal(after, World.Items.Values.Count(i => i is DeathVortexTrap && !i.Deleted));

            ShardTestClock.Advance(TimeSpan.FromSeconds(16));
            Assert.Equal(before, World.Items.Values.Count(i => i is DeathVortexTrap && !i.Deleted));

            // The kill.
            boss.Kill();

            Assert.False(boss.Alive);
            Assert.False(VerLorRegController.Active);
            Assert.Null(VerLorRegController.CurrentExodus);
            Assert.True(region.ExitPending);
            Assert.DoesNotContain(boss, ClockworkExodus.Instances);

            // Eight minutes: the warning; ten: the move and a new Exodus.
            ShardTestClock.Advance(TimeSpan.FromMinutes(8) + TimeSpan.FromSeconds(1));
            Assert.Equal(Map.Ilshenar, pm.Map);
            Assert.False(VerLorRegController.Active);

            ShardTestClock.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));

            Assert.Contains(pm.Location, VerLorRegCity.RandomLocations);
            Assert.Equal(Map.Ilshenar, pm.Map);
            Assert.True(VerLorRegController.Active);
            var next = VerLorRegController.CurrentExodus;
            Assert.NotNull(next);
            Assert.NotSame(boss, next);
            Assert.True(next.Alive);
            Assert.Equal(ExodusEncounter.ExodusLocation, next.Location);

            // The controller saves its boss and comes back running.
            var copy = RoundTrip(controller);
            Assert.Same(next, copy.Exodus);
            Assert.True(copy.IsRunning);
            copy.Delete();
        }
        finally
        {
            pm.Delete();
            ClearExodus();
            ExodusRegions.Unregister(Map.Ilshenar);
        }
    }

    [Fact]
    public void TheHiddenChestIsSensedRevealedAndGone()
    {
        ShardTestClock.Arm();
        ClearExodus();

        var rogue = NewPlayer(new Point3D(1990, 103, -28), Map.Ilshenar);

        try
        {
            var chest = new ExodusChest();
            chest.MoveToWorld(new Point3D(1990, 100, -28), Map.Ilshenar);

            Assert.False(chest.Visible);
            Assert.NotNull(chest.ChestRegion);
            Assert.True(chest.ChestRegion.Registered);
            Assert.True(chest.ChestRegion.Contains(new Point3D(1992, 102, -28)));
            Assert.False(chest.ChestRegion.Contains(new Point3D(1993, 102, -28)));

            rogue.Skills[SkillName.DetectHidden].Base = 97.0;
            Assert.False(chest.CheckReveal(rogue));

            rogue.Skills[SkillName.DetectHidden].Base = 98.0;
            Assert.True(chest.CheckReveal(rogue));

            rogue.MoveToWorld(new Point3D(1990, 104, -28), Map.Ilshenar);
            Assert.False(chest.CheckReveal(rogue), "four tiles is out of reach");

            // The passing rogue at 90: one step in 300 scaled by skill senses it; at 79 never.
            rogue.Skills[SkillName.DetectHidden].Base = 79.0;
            Assert.All(Enumerable.Range(0, 200), _ => Assert.False(chest.CheckPassiveDetect(rogue)));

            rogue.Skills[SkillName.DetectHidden].Base = 90.0;
            var sensed = Enumerable.Range(0, 500).Any(_ => chest.CheckPassiveDetect(rogue));
            Assert.True(sensed);

            rogue.MoveToWorld(new Point3D(1990, 106, -28), Map.Ilshenar);
            Assert.False(chest.CheckPassiveDetect(rogue), "five tiles is out of the passive reach");

            // Revealed: visible, maybe a key, and gone five minutes later.
            var items = chest.Items.Count;
            chest.OnRevealed(rogue);
            Assert.True(chest.Visible);
            Assert.True(chest.Items.Count == items || chest.Items.Count == items + 1);

            if (chest.Items.Count == items + 1)
            {
                Assert.Contains(chest.Items, i => ExodusChest.RituelItem.Contains(i.GetType()));
            }

            ShardTestClock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
            Assert.True(chest.Deleted);
            Assert.DoesNotContain(Region.Regions, r => r is ExodusChestRegion c && c.Chest == chest && r.Registered);

            // GiveRituelItem: one of the four keys in the pack.
            ExodusChest.GiveRituelItem(rogue);
            Assert.Contains(rogue.Backpack.Items, i => ExodusChest.RituelItem.Contains(i.GetType()));
        }
        finally
        {
            rogue.Delete();
            ClearExodus();
        }
    }

    [Fact]
    public void TheNexusPuzzleRepairsTheMachineAndAWrongStepShocks()
    {
        ShardTestClock.Arm();
        ClearExodus();

        var tinker = NewPlayer(new Point3D(2012, 67, -27), Map.Ilshenar);

        try
        {
            var nexus = new ExodusNexus(3);
            nexus.MoveToWorld(new Point3D(2011, 67, -27), Map.Ilshenar);

            Assert.Equal(ExodusNexus.BrokenHue, nexus.Hue);
            Assert.False(nexus.Active);

            var path = nexus.Path;
            Assert.Equal(new Point2D(0, 0), path[0]);
            Assert.Equal(new Point2D(2, 2), path[^1]);

            // The toolkit opens the puzzle only on a broken machine.
            var kit = new ExoticToolkit();
            tinker.AddToBackpack(kit);
            kit.Use(tinker, nexus.Components[0]);
            Assert.Same(tinker, nexus.User);

            // The right arrows, one by one, to the far corner.
            var step = 0;

            while (true)
            {
                var button = ExodusNexus.ButtonFor(path[step], path[step + 1]);
                Assert.NotEqual(0, button);

                var next = nexus.Respond(tinker, step, button, out var hint);

                if (step + 1 == path.Length - 1)
                {
                    Assert.Equal(-1, next);
                    break;
                }

                Assert.Equal(step + 1, next);
                Assert.False(hint);
                step = next;
            }

            Assert.True(nexus.Active);
            Assert.Equal(0, nexus.Hue);
            Assert.Null(nexus.User);
            Assert.Single(tinker.Backpack.Items.OfType<PunchCard>());

            // Running, the kit does nothing; ten minutes later it is broken again.
            kit.Use(tinker, nexus.Components[0]);
            Assert.Null(nexus.User);

            ShardTestClock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
            Assert.Equal(ExodusNexus.BrokenHue, nexus.Hue);
            Assert.False(nexus.Active);

            // A wrong arrow: 60 energy on the tick, frozen, free again after the three shocks.
            // A fresh test character has about 55 hits and the shock is 60: give it the strength to survive one.
            tinker.RawStr = 125;
            tinker.Hits = tinker.HitsMax;
            kit.Use(tinker, nexus.Components[0]);
            Assert.Same(tinker, nexus.User);
            var hitsBefore = tinker.Hits;
            Assert.True(hitsBefore > 60, $"hits {hitsBefore}");
            var wrong = ExodusNexus.ButtonFor(path[0], path[1]) == 3 ? 4 : 3;
            var result = nexus.Respond(tinker, 0, wrong, out _);

            Assert.Equal(-1, result);
            Assert.True(tinker.Hits < hitsBefore, $"hits {hitsBefore} -> {tinker.Hits}");
            Assert.True(tinker.Frozen);
            Assert.Null(nexus.User);

            ShardTestClock.Advance(TimeSpan.FromSeconds(16));
            Assert.False(tinker.Frozen);

            // Too far: refused, and the session ends.
            tinker.MoveToWorld(new Point3D(2020, 67, -27), Map.Ilshenar);
            nexus.OpenGump(tinker);
            Assert.Null(nexus.User);

            nexus.Delete();
        }
        finally
        {
            tinker.Delete();
            ClearExodus();
        }
    }

    [Fact]
    public void TheKeypunchReaderTheComponentAndTheScrollDoTheirJobs()
    {
        ShardTestClock.Arm();
        ClearExodus();

        var tinker = NewPlayer(new Point3D(1988, 88, -28), Map.Ilshenar);

        try
        {
            tinker.Skills[SkillName.Tinkering].Base = 79.9;
            var component = new MechanicalComponent();
            tinker.AddToBackpack(component);
            component.OnDoubleClick(tinker);
            Assert.False(component.Deleted);
            Assert.Empty(tinker.Backpack.Items.OfType<ExoticToolkit>());

            tinker.Skills[SkillName.Tinkering].Base = 80.0;
            component.OnDoubleClick(tinker);
            Assert.True(component.Deleted);
            var kit = Assert.Single(tinker.Backpack.Items.OfType<ExoticToolkit>());

            var reader = new KeypunchReader();
            reader.MoveToWorld(ExodusDecoration.KeypunchReaderLocation, Map.Ilshenar);

            for (var i = 0; i < 49; i++)
            {
                reader.DropItem(new PunchCard());
            }

            reader.DropItem(kit);
            Assert.False(reader.CheckItems(tinker), "49 cards and a kit");

            reader.DropItem(new PunchCard());
            Assert.True(reader.CheckItems(tinker));

            Assert.Empty(reader.Items.OfType<PunchCard>());
            Assert.Single(reader.Items.OfType<NexusAddonDeed>());
            Assert.Contains(kit, reader.Items);
            reader.Delete();

            // The scroll: five to the cap once; the second scroll is refused.
            var cap = tinker.StatCap;
            var scroll = new ScrollofValiantCommendation();
            tinker.AddToBackpack(scroll);
            scroll.OnDoubleClick(tinker);

            Assert.Equal(cap + 5, tinker.StatCap);
            Assert.Equal(tinker.Name, scroll.Owner);
            Assert.True(ExodusEncounter.HasValiantStatReward(tinker));

            var another = new ScrollofValiantCommendation();
            tinker.AddToBackpack(another);
            another.OnDoubleClick(tinker);

            Assert.Equal(cap + 5, tinker.StatCap);
            Assert.True(string.IsNullOrEmpty(another.Owner));

            ExodusEncounter.ClearValiantStatReward(tinker);
        }
        finally
        {
            tinker.Delete();
            ClearExodus();
        }
    }

    [Fact]
    public void TheBossIsALootBoxBossTheZealotYieldsAKeyAndTheCraftsmanPathIsRegistered()
    {
        ShardTestClock.Arm();
        ClearExodus();

        try
        {
            var boss = new ClockworkExodus();
            boss.MoveToWorld(ExodusEncounter.ExodusLocation, Map.Ilshenar);

            Assert.True(ClusterFLootBoxes.IsBoss(boss));
            Assert.Equal(5, LootTable.Current.TierForBoss(typeof(ClockworkExodus)));
            boss.Delete();

            var zealot = new ExodusZealot();
            var stolen = StealingHandler.MakeSpecialFor(zealot);
            Assert.NotNull(stolen);
            Assert.Contains(stolen.GetType(), ExodusChest.RituelItem);
            stolen.Delete();
            zealot.Delete();

            if (DefBlacksmithy.CraftSystem == null) DefBlacksmithy.Initialize();
            if (DefCarpentry.CraftSystem == null) DefCarpentry.Initialize();
            if (DefInscription.CraftSystem == null) DefInscription.Initialize();
            if (DefTailoring.CraftSystem == null) DefTailoring.Initialize();
            if (DefAlchemy.CraftSystem == null) DefAlchemy.Initialize();

            ExodusCraftRegistrations.Register();

            Assert.NotNull(DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(ExodusSacrificalDagger)));
            Assert.NotNull(DefCarpentry.CraftSystem.CraftItems.SearchFor(typeof(ExodusSummoningAlter)));
            Assert.NotNull(DefInscription.CraftSystem.CraftItems.SearchFor(typeof(ExodusSummoningRite)));
            Assert.NotNull(DefTailoring.CraftSystem.CraftItems.SearchFor(typeof(RobeofRite)));
            Assert.NotNull(DefAlchemy.CraftSystem.CraftItems.SearchFor(typeof(NexusCore)));
            Assert.NotNull(DefAlchemy.CraftSystem.CraftItems.SearchFor(typeof(GoldDust)));

            var dagger = DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(ExodusSacrificalDagger));
            Assert.Equal(10, dagger.Resources[^1].Amount);
            Assert.Equal(typeof(SmallPieceofBlackrock), dagger.Resources[^1].ItemType);
            Assert.True(dagger.ForceNonExceptional);

            // Blackrock on the ore table, once.
            var def = Mining.System.OreAndStone;
            var rows = def.BonusResources.Length;
            var added = ExodusMiningBonus.Apply(def);
            Assert.True(added || def.BonusResources.Any(r => r.Type == typeof(SmallPieceofBlackrock)));
            Assert.False(ExodusMiningBonus.Apply(def));
            Assert.Contains(def.BonusResources, r => r.Type == typeof(SmallPieceofBlackrock) && r.Chance == 0.1 && r.ReqSkill == 100.0);
            _out.WriteLine($"bonus rows {rows} -> {def.BonusResources.Length}");
        }
        finally
        {
            ClearExodus();
        }
    }

    [Fact]
    public void DecayingItemsCountDownAndExpire()
    {
        ShardTestClock.Arm();
        ClearExodus();

        var pm = NewPlayer(new Point3D(1500, 1500, 0), Map.Trammel);

        try
        {
            var card = new PunchCard();
            pm.AddToBackpack(card);
            Assert.Equal(21600, card.TimeLeft);

            ShardTestClock.Advance(TimeSpan.FromSeconds(10.1));
            Assert.Equal(21590, card.TimeLeft);

            card.TimeLeft = 5;
            ShardTestClock.Advance(TimeSpan.FromSeconds(10.1));
            Assert.True(card.Deleted);

            var dagger = new ExodusSacrificalDagger();
            pm.AddToBackpack(dagger);
            Assert.Equal(604800, dagger.TimeLeft);
            dagger.TimeLeft = 5;
            ShardTestClock.Advance(TimeSpan.FromSeconds(10.1));
            Assert.True(dagger.Deleted);

            var robe = new RobeofRite();
            pm.AddToBackpack(robe);
            robe.TimeLeft = 5;
            ShardTestClock.Advance(TimeSpan.FromSeconds(10.1));
            Assert.True(robe.Deleted);

            // A tome stands seven minutes.
            var tome = new ExodusTomeAltar(pm);
            tome.MoveToWorld(new Point3D(1500, 1501, 0), Map.Trammel);
            var steps = tome.Steps;
            Assert.Equal(420, tome.TimeLeft);
            ShardTestClock.Advance(TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(1));
            Assert.True(tome.Deleted);
            Assert.True(steps.Deleted);
        }
        finally
        {
            pm.Delete();
            ClearExodus();
        }
    }
}
