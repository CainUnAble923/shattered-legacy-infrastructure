// cc-P196 Blackthorn Dungeon (revamped): [GenBlackthorn and what it places, the two regions (the gas cloud, the
// recall ban, the castle's spell, mount and pet rules), the Minax artifact tally and roll, the 136 crest items and
// the reward list, the Agent of the Crown's turn-in and rewards, and the invaders, which a green build says nothing
// about.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh. See notes/s4-test-route.md and notes/s8-test-route.md.
//
// The test host has no map files (land at z 0 everywhere, so every spawn point is good), loads no regions.json and
// runs no Initialize, so the facts register the regions and the reward list themselves; it runs every fact in one
// world in no fixed order, so each fact clears every Blackthorn object first and last. Hit points are read on the
// tick the damage lands (D15); timers are made after Arm, and a test NetState before anything that reads the clock
// (D16); Player is set before MoveToWorld. Expected values are literals, never the port's own constants, so a break in
// the port cannot move the expectation with it (the first red proof found two that did). Notes:
// shard-migration/notes/cc-P196-port-blackthorn-dungeon.md.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Blackthorn;
using Server.Engines.Points;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
using Server.Spells.Chivalry;
using Server.Spells.First;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class BlackthornDungeonVerification
{
    private readonly ITestOutputHelper _out;

    public BlackthornDungeonVerification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D DungeonEntrance = new(6430, 2678, 0);
    private static readonly Point3D CastleCourt = new(1529, 1477, 0);

    private static PlayerMobile NewPlayer(Point3D loc, Map map = null)
    {
        var pm = new PlayerMobile();
        pm.AddItem(new Backpack());
        pm.Player = true;
        pm.MoveToWorld(loc, map ?? Map.Trammel);
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
        if (BlackthornRewards.Rewards == null)
        {
            BlackthornRewards.Initialize();
        }
    }

    // PropertyListReader reads items; a mobile's list is built the same way.
    private static List<(int Number, string Argument)> ReadProperties(Mobile m)
    {
        var opl = new ObjectPropertyList(m);
        m.GetProperties(opl);
        opl.Terminate();

        var buffer = opl.Buffer;
        var entries = new List<(int, string)>();
        var pos = 15;

        while (pos + 6 <= buffer.Length)
        {
            var number = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(pos));

            if (number == 0)
            {
                break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(pos + 4));
            entries.Add((number, Encoding.Unicode.GetString(buffer, pos + 6, length)));
            pos += 6 + length;
        }

        return entries;
    }

    private static void ClearBlackthorn()
    {
        GenerateBlackthornDungeon.Delete();

        foreach (var item in World.Items.Values.ToList())
        {
            if (item is DungeonHitchingPost or Blocker ||
                item is Spawner spawner && BlackthornSpawns.IsBlackthornSpawner(spawner) ||
                item is MetalDoor && BlackthornRegions.DungeonBounds.Contains(item.Location) ||
                item is Teleporter && item.Map == Map.Felucca && (BlackthornRegions.DungeonBounds.Contains(item.Location) || item.X == 1517 && item.Y is >= 1417 and <= 1420))
            {
                item.Delete();
            }
        }

        foreach (var mobile in World.Mobiles.Values.ToList())
        {
            if (mobile is AgentOfTheCrown or Invader ||
                mobile is BaseCreature { Spawner: null } bc && !bc.Controlled && BlackthornRegions.DungeonBounds.Contains(bc.Location) && bc.Map != Map.Internal)
            {
                mobile.Delete();
            }
        }

        BlackthornRegions.Unregister(Map.Trammel);
        BlackthornRegions.Unregister(Map.Felucca);
        BlackthornData.Instance.DungeonPoints.Clear();
        BlackthornData.Instance.Clear();
        MinaxArtifacts.Clear();
    }

    // A creature killed by a player with looting rights (most of its hits taken by the player), through Kill.
    private static void KillBy(BaseCreature bc, PlayerMobile pm)
    {
        bc.Damage(Math.Max(1, bc.Hits - 1), pm);
        bc.Kill();
    }

    [Fact]
    public void GenerateBuildsTheDungeonAndDeleteRemovesIt()
    {
        ShardTestClock.Arm();
        ClearBlackthorn();

        try
        {
            var report = GenerateBlackthornDungeon.Generate();
            _out.WriteLine(report);

            // The hitching posts, doors and blockers, per facet.
            foreach (var map in new[] { Map.Trammel, Map.Felucca })
            {
                var post = Assert.Single(World.Items.Values.OfType<DungeonHitchingPost>(), p => !p.Deleted && p.Map == map);
                Assert.Equal(GenerateBlackthornDungeon.HitchingPostLocation, post.Location);
                Assert.False(post.Movable);

                var doors = World.Items.Values.OfType<MetalDoor>().Where(d => !d.Deleted && d.Map == map && BlackthornRegions.DungeonBounds.Contains(d.Location)).ToList();
                Assert.Equal(8, doors.Count);
                Assert.All(GenerateBlackthornDungeon.Doors, d => Assert.Contains(doors, door => door.Location == d.At));

                var blockers = World.Items.Values.OfType<Blocker>().Where(b => !b.Deleted && b.Map == map && BlackthornRegions.DungeonBounds.Contains(b.Location)).ToList();
                Assert.Equal(16, blockers.Count);
                Assert.All(GenerateBlackthornDungeon.Blockers, p => Assert.Contains(blockers, b => b.Location == p));
            }

            // The Felucca teleporters, both ways; none placed on Trammel (stock teleporters.json has those).
            var tele = World.Items.Values.OfType<Teleporter>().Where(t => !t.Deleted && t.Map == Map.Felucca &&
                (BlackthornRegions.DungeonBounds.Contains(t.Location) || t.X == 1517 && t.Y is >= 1417 and <= 1420)).ToList();
            Assert.Equal(9, tele.Count);

            foreach (var (dest, at) in GenerateBlackthornDungeon.FeluccaTeleporters)
            {
                var t = Assert.Single(tele, x => x.Location == at);
                Assert.Equal(dest, t.PointDest);
                Assert.Equal(Map.Felucca, t.MapDest);
            }

            Assert.Equal(20, Assert.Single(tele, t => t.Location == new Point3D(1517, 1420, 9)).PointDest.Z); // ServUO's z 12, corrected
            Assert.DoesNotContain(World.Items.Values.OfType<Teleporter>(), t => !t.Deleted && t.Map == Map.Trammel && BlackthornRegions.DungeonBounds.Contains(t.Location));

            // The 54 spawners: 22 dungeon rows a facet, 9 Felucca Agents and the Trammel one, each full.
            var spawners = World.Items.Values.OfType<Spawner>().Where(s => !s.Deleted && BlackthornSpawns.IsBlackthornSpawner(s)).ToList();
            Assert.Equal(54, spawners.Count);
            Assert.Equal(BlackthornSpawns.Definitions.Length, spawners.Count);
            Assert.Equal(22, spawners.Count(s => s.Name == BlackthornSpawns.DungeonSpawnerName && s.Map == Map.Trammel));
            Assert.Equal(22, spawners.Count(s => s.Name == BlackthornSpawns.DungeonSpawnerName && s.Map == Map.Felucca));
            Assert.Equal(9, spawners.Count(s => s.Name == BlackthornSpawns.AgentSpawnerName && s.Map == Map.Felucca));
            Assert.Equal(1, spawners.Count(s => s.Name == BlackthornSpawns.AgentSpawnerName && s.Map == Map.Trammel));

            var capacity = BlackthornSpawns.Definitions.Where(d => !d.IsAgent && !d.Felucca).Sum(d => d.Capacity);
            Assert.Equal(104, capacity);
            Assert.Equal(capacity, BlackthornSpawns.Definitions.Where(d => !d.IsAgent && d.Felucca).Sum(d => d.Capacity));

            var typesSeen = new HashSet<Type>();

            foreach (var def in BlackthornSpawns.Definitions)
            {
                var spawner = Assert.Single(spawners, s => s.Name == def.Name && s.Map == def.Map && s.Location == new Point3D(def.CentreX, def.CentreY, def.CentreZ));
                Assert.Equal(def.Capacity, spawner.Spawned.Count);
                Assert.Equal(def.MaxCount, spawner.Count);
                Assert.Equal(TimeSpan.FromSeconds(def.MinDelaySeconds), spawner.MinDelay);
                Assert.Equal(TimeSpan.FromSeconds(def.MaxDelaySeconds), spawner.MaxDelay);
                Assert.Equal(def.Range, spawner.WalkingRange);
                Assert.Equal(def.HomeRangeRelative, spawner.SpawnLocationIsHome);
                Assert.Equal(def.Entries.Length, spawner.Entries.Count);
                Assert.True(spawner.Running, $"{def.Name} at {spawner.Location} is not running");

                var rect = new Rectangle2D(def.X, def.Y, Math.Max(1, def.Width), Math.Max(1, def.Height));
                var allowed = def.Entries.Select(e => e.Type).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var spawn in spawner.Spawned.Keys)
                {
                    var m = Assert.IsAssignableFrom<BaseCreature>(spawn);
                    Assert.Equal(def.Map, m.Map);
                    Assert.True(rect.Contains(m.Location), $"{m.GetType().Name} at {m.Location} outside {rect} of {def.Name}");
                    Assert.Contains(m.GetType().Name, allowed);
                    Assert.Equal(def.Range, m.RangeHome);
                    typesSeen.Add(m.GetType());
                }

                foreach (var entry in def.Entries)
                {
                    Assert.Equal(entry.MaxCount, spawner.Spawned.Keys.Count(s => string.Equals(s.GetType().Name, entry.Type, StringComparison.OrdinalIgnoreCase)));
                }
            }

            Assert.Equal(22, typesSeen.Count); // 21 stock creatures and the Agent

            // The ten Agents, each with the 136 display items in his pack and his property line.
            var agents = World.Mobiles.Values.OfType<AgentOfTheCrown>().Where(a => !a.Deleted).ToList();
            Assert.Equal(10, agents.Count);
            Assert.Equal(9, agents.Count(a => a.Map == Map.Felucca));

            foreach (var agent in agents)
            {
                Assert.Equal("the Agent Of The Crown", agent.Title);
                Assert.Equal(0x190, agent.Body.BodyID);
                Assert.True(agent.IsInvulnerable);
                Assert.NotNull(agent.Backpack);
                Assert.Equal(136, agent.Backpack.Items.Count(i => AgentOfTheCrown.DisplayTypes.Contains(i.GetType())));
                Assert.All(agent.Backpack.Items.Where(i => AgentOfTheCrown.DisplayTypes.Contains(i.GetType())), i => Assert.False(i.Movable));
                Assert.Contains(ReadProperties(agent), e => e.Number == 1154517);
            }

            // A second run replaces rather than doubles.
            var deleted = GenerateBlackthornDungeon.Delete();
            Assert.True(deleted >= 54 + 2 + 16 + 32 + 9, $"deleted {deleted}");
            GenerateBlackthornDungeon.Generate();
            Assert.Equal(54, World.Items.Values.OfType<Spawner>().Count(s => !s.Deleted && BlackthornSpawns.IsBlackthornSpawner(s)));
            Assert.Equal(2, World.Items.Values.OfType<DungeonHitchingPost>().Count(p => !p.Deleted));
            Assert.Equal(10, World.Mobiles.Values.OfType<AgentOfTheCrown>().Count(a => !a.Deleted));

            // Delete takes it all down, spawn included.
            GenerateBlackthornDungeon.Delete();
            Assert.DoesNotContain(World.Items.Values.OfType<Spawner>(), s => !s.Deleted && BlackthornSpawns.IsBlackthornSpawner(s));
            Assert.DoesNotContain(World.Items.Values.OfType<DungeonHitchingPost>(), p => !p.Deleted);
            Assert.DoesNotContain(World.Items.Values.OfType<Blocker>(), b => !b.Deleted && BlackthornRegions.DungeonBounds.Contains(b.Location));
            Assert.DoesNotContain(World.Mobiles.Values.OfType<AgentOfTheCrown>(), a => !a.Deleted);
            Assert.DoesNotContain(World.Mobiles.Values.OfType<BaseCreature>(), c => !c.Deleted && c.Map != Map.Internal && BlackthornRegions.DungeonBounds.Contains(c.Location));
        }
        finally
        {
            ClearBlackthorn();
        }
    }

    [Fact]
    public void TheRegionsRevealTheHiddenThrowTheIdleBanRecallAndTallyKills()
    {
        // D16: the NetState first, then Arm, then anything with a timer.
        var ns = PacketTestUtilities.CreateTestNetState();
        ShardTestClock.Arm();
        EnsureCorpses();
        ClearBlackthorn();

        PlayerMobile pm = null, idleNorth = null, rider = null;
        Horse horse = null, pet = null;
        Lich lich = null, lich2 = null;

        try
        {
            var dungeonTram = BlackthornRegions.Register(Map.Trammel);
            var dungeonFel = BlackthornRegions.Register(Map.Felucca);

            Assert.NotNull(dungeonTram);
            Assert.True(dungeonTram.Registered);
            Assert.Same(dungeonTram, BlackthornRegions.Register(Map.Trammel)); // idempotent
            Assert.Same(dungeonTram, Region.Find(DungeonEntrance, Map.Trammel));
            Assert.Same(dungeonFel, Region.Find(DungeonEntrance, Map.Felucca));
            Assert.Equal(BlackthornRegions.DungeonName, dungeonTram.Name);
            Assert.True(dungeonTram.IsPartOf("BlackthornDungeon"));
            Assert.Equal(new Point3D(6430, 2678, 0), dungeonTram.GoLocation);

            var castle = BlackthornRegions.Castle;
            Assert.NotNull(castle);
            Assert.Same(castle, Region.Find(CastleCourt, Map.Trammel));
            Assert.IsNotType<BlackthornCastle>(Region.Find(CastleCourt, Map.Felucca));
            Assert.Equal(Region.DefaultPriority + 1, castle.Priority);
            Assert.False(castle.IsDisabled());

            // Recall, gate and mark are refused in both; teleport is allowed; staff pass.
            pm = NewPlayer(DungeonEntrance);
            Assert.False(dungeonTram.CheckTravel(pm, DungeonEntrance, TravelCheckType.RecallTo, out _));
            Assert.False(dungeonTram.CheckTravel(pm, DungeonEntrance, TravelCheckType.GateTo, out _));
            Assert.False(dungeonTram.CheckTravel(pm, DungeonEntrance, TravelCheckType.Mark, out _));
            Assert.False(dungeonTram.CheckTravel(pm, DungeonEntrance, TravelCheckType.RecallFrom, out _));
            Assert.True(dungeonTram.CheckTravel(pm, DungeonEntrance, TravelCheckType.TeleportTo, out _));
            Assert.True(dungeonTram.CheckTravel(pm, DungeonEntrance, TravelCheckType.TeleportFrom, out _));
            Assert.False(castle.CheckTravel(pm, CastleCourt, TravelCheckType.RecallTo, out _));
            Assert.True(castle.CheckTravel(pm, CastleCourt, TravelCheckType.TeleportTo, out _));
            pm.AccessLevel = AccessLevel.GameMaster;
            Assert.True(dungeonTram.CheckTravel(pm, DungeonEntrance, TravelCheckType.RecallTo, out _));
            pm.AccessLevel = AccessLevel.Player;

            // The gas cloud: a hidden player is revealed; one idle two minutes south of the invasion rooms is thrown
            // to one of the eight spots, poisoned; a player north of them is left alone.
            idleNorth = NewPlayer(new Point3D(6300, 2500, 0));
            Assert.Same(dungeonTram, idleNorth.Region);
            idleNorth.LastMoveTime = Core.TickCount - 130000;

            pm.Hidden = true;
            pm.LastMoveTime = Core.TickCount - 130000;
            var northBefore = idleNorth.Location;

            ShardTestClock.Advance(TimeSpan.FromSeconds(5.016));

            Assert.False(pm.Hidden);
            Assert.Contains(pm.Location, BlackthornDungeon.RandomLocations);
            Assert.Same(Poison.Lethal, pm.Poison);
            Assert.True(pm.LastMoveTime > Core.TickCount - 6000, "the throw stamps the move time");
            Assert.Equal(northBefore, idleNorth.Location);
            Assert.Null(idleNorth.Poison);

            // The castle: a rider is dismounted, the mount stabled; a pet walking in is stabled; no spell casts.
            rider = NewPlayer(new Point3D(1529, 1520, 0));
            rider.NetState = ns;
            ns.Mobile = rider;

            horse = new Horse();
            horse.MoveToWorld(rider.Location, Map.Trammel);
            horse.SetControlMaster(rider);
            horse.Rider = rider;
            Assert.Same(horse, rider.Mount);

            pet = new Horse();
            pet.MoveToWorld(rider.Location, Map.Trammel);
            pet.SetControlMaster(rider);

            rider.Location = CastleCourt;
            Assert.Same(castle, rider.Region);
            Assert.Null(rider.Mount);
            Assert.Null(horse.Rider);
            Assert.True(horse.IsStabled);
            Assert.Same(rider, horse.StabledBy);
            Assert.Contains(horse, rider.Stabled);
            Assert.Equal(Map.Internal, horse.Map);

            pet.Location = CastleCourt;
            Assert.True(pet.IsStabled);
            Assert.Contains(pet, rider.Stabled);

            Assert.False(castle.OnBeginSpellCast(rider, new MagicArrowSpell(rider, null)));
            Assert.False(castle.OnBeginSpellCast(rider, new CloseWoundsSpell(rider, null)));
            rider.AccessLevel = AccessLevel.GameMaster;
            Assert.True(castle.OnBeginSpellCast(rider, new MagicArrowSpell(rider, null)));
            rider.AccessLevel = AccessLevel.Player;

            // A kill in the dungeon by a player with looting rights adds fame / 2 to the tally; the roll, when it
            // hits, hands the player a Minax artifact and empties the tally.
            pm.Hidden = false;
            pm.MoveToWorld(new Point3D(6405, 2595, 0), Map.Trammel);
            Assert.Same(dungeonTram, pm.Region);
            pm.LastMoveTime = Core.TickCount;

            lich = new Lich();
            lich.MoveToWorld(new Point3D(6406, 2595, 0), Map.Trammel);
            Assert.True(lich.Fame > 0);
            var expected = lich.Fame / 2;

            KillBy(lich, pm);

            Assert.True(BlackthornData.Instance.DungeonPoints.TryGetValue(pm, out var tally), "the kill was tallied");
            Assert.Equal(expected, tally);
            Assert.InRange(BlackthornData.ChanceAt(0), 0.00086, 0.00087);
            Assert.True(BlackthornData.ChanceAt(250_000) > 0.0099 && BlackthornData.ChanceAt(250_000) < 0.0101);

            lich2 = new Lich();
            lich2.MoveToWorld(new Point3D(6406, 2596, 0), Map.Trammel);

            var artifact = BlackthornData.Instance.ProcessKill(lich2, pm, true);
            Assert.NotNull(artifact);
            Assert.Equal(1157, artifact.Hue);
            Assert.True(MinaxArtifacts.IsMinax(artifact));
            Assert.True(artifact.IsChildOf(pm.Backpack));
            Assert.True(artifact is BaseWeapon or BaseArmor or BaseJewel or BaseHat, artifact.GetType().Name); // pinned's pool has hats, as ServUO's does
            Assert.False(BlackthornData.Instance.DungeonPoints.ContainsKey(pm));

            // A kill outside the dungeon, or by a creature, tallies nothing.
            var outside = new Lich();
            outside.MoveToWorld(new Point3D(1000, 1000, 0), Map.Trammel);
            Assert.Null(BlackthornData.Instance.ProcessKill(outside, pm));
            Assert.False(BlackthornData.Instance.DungeonPoints.ContainsKey(pm));
            outside.Delete();

            // The points system round-trips its tally and its reward points.
            BlackthornData.Instance.DungeonPoints[pm] = 4321;
            BlackthornData.Instance.SetPoints(pm, 7);

            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            PointsSystemPersistence.WriteAll(writer);
            writer.Flush();

            BlackthornData.Instance.DungeonPoints.Clear();
            BlackthornData.Instance.Clear();
            Assert.Equal(0.0, BlackthornData.Instance.GetPoints(pm));

            PointsSystemPersistence.ReadAll(new BufferReader(buffer));
            Assert.Equal(7.0, BlackthornData.Instance.GetPoints(pm));
            Assert.Equal(4321, BlackthornData.Instance.DungeonPoints[pm]);
        }
        finally
        {
            if (rider != null)
            {
                ns.Mobile = null;
                rider.NetState = null;
            }

            pm?.Delete();
            idleNorth?.Delete();
            rider?.Delete();
            horse?.Delete();
            pet?.Delete();
            lich?.Delete();
            lich2?.Delete();
            ClearBlackthorn();
        }
    }

    [Fact]
    public void TheCrestItemsAndRewardsConstructRoundTripAndBearTheCrest()
    {
        ShardTestClock.Arm();
        EnsureRewards();

        var made = new List<Item>();

        try
        {
            Assert.Equal(136, AgentOfTheCrown.DisplayTypes.Length);
            Assert.Equal(136, AgentOfTheCrown.DisplayTypes.Distinct().Count());

            foreach (var type in AgentOfTheCrown.DisplayTypes)
            {
                var item = Activator.CreateInstance(type) as Item;
                Assert.NotNull(item);
                made.Add(item);

                Assert.True(item.Hue > 0, $"{type.Name} has no hue");
                Assert.True(item is BaseClothing or BaseArmor, type.Name);

                var props = PropertyListReader.Read(item);
                Assert.True(props.Has(1154548, $"#{item.LabelNumber}"), $"{type.Name}: {props.Describe()}");
                Assert.False(props.Has(item.LabelNumber), $"{type.Name} shows its plain name as well");

                var copy = RoundTrip(item);
                made.Add(copy);
                Assert.Equal(item.Hue, copy.Hue);
                Assert.Equal(item.ItemID, copy.ItemID);
                Assert.Equal(item.Layer, copy.Layer);
            }

            // Samples of ServUO's values across the bases.
            var epaulette = new EpauletteBearingTheCrestOfBlackthorn6();
            made.Add(epaulette);
            Assert.Equal(0x9985, epaulette.ItemID);
            Assert.Equal(Layer.OuterTorso, epaulette.Layer);
            Assert.Equal(2019, epaulette.Hue);
            Assert.Equal(3, epaulette.Attributes.AttackChance);
            Assert.Equal(3, epaulette.Attributes.SpellDamage);
            Assert.Equal(1123325, epaulette.LabelNumber);

            var gargEpaulette = new GargishEpauletteBearingTheCrestOfBlackthorn6();
            made.Add(gargEpaulette);
            Assert.Equal(0x9986, gargEpaulette.ItemID);
            Assert.Same(Race.Gargoyle, gargEpaulette.RequiredRace);

            var bandana = new BandanaBearingTheCrestOfBlackthorn3();
            made.Add(bandana);
            Assert.Equal(45, bandana.StrRequirement);
            Assert.Equal(20, bandana.BasePhysicalResistance);
            Assert.Equal(255, bandana.MaxHitPoints);
            Assert.Equal(1, bandana.Attributes.NightSight);
            Assert.Equal(1150, bandana.Hue);

            var doublet = new DoubletBearingTheCrestOfBlackthorn();
            made.Add(doublet);
            Assert.Equal(150, doublet.MaxHitPoints);
            Assert.Equal(10, doublet.Attributes.LowerRegCost);
            Assert.Equal(0xe8, doublet.Hue);

            var stone = new GargishStoneChestBearingTheCrestOfBlackthorn();
            made.Add(stone);
            Assert.IsAssignableFrom<GargishStoneChestType1>(stone);
            Assert.Equal(0x286, stone.ItemID);
            Assert.Equal(100, stone.ArmorAttributes.LowerStatReq);
            Assert.Equal(1, stone.ArmorAttributes.MageArmor);
            Assert.Equal(14, stone.BaseColdResistance);

            var robe = new RobeBearingTheCrestOfBlackthorn3();
            made.Add(robe);
            Assert.Equal(SkillName.Stealth, robe.SkillBonuses.GetSkill(0));
            Assert.Equal(10.0, robe.SkillBonuses.GetBonus(0));
            Assert.Equal(2130, robe.Hue);

            var hooded = new HoodedRobeBearingTheCrestOfBlackthorn1();
            made.Add(hooded);
            Assert.Equal(0x2683, hooded.ItemID);
            Assert.Equal(1029863, hooded.LabelNumber);
            Assert.Equal(2075, hooded.Hue);

            var sash = new GargishSashBearingTheCrestOfBlackthorn();
            made.Add(sash);
            Assert.Equal(0x46B4, sash.ItemID);
            Assert.Same(Race.Gargoyle, sash.RequiredRace);
            Assert.Equal(150, sash.MaxHitPoints);

            var apron = new GargishHalfApronBearingTheCrestOfBlackthorn2();
            made.Add(apron);
            Assert.Equal(0x50D8, apron.ItemID);
            Assert.Equal(Layer.Waist, apron.Layer);

            var wing = new GargishClothWingArmorBearingTheCrestOfBlackthorn();
            made.Add(wing);
            Assert.Equal(0x45A4, wing.ItemID);
            Assert.Equal(Layer.Cloak, wing.Layer);
            Assert.Equal(5, wing.Attributes.DefendChance);

            var fancy = new GargishFancyBearingTheCrestOfBlackthorn7();
            made.Add(fancy);
            Assert.Equal(0x4002, fancy.ItemID);

            var earrings = new GargishEarringsBearingTheCrestOfBlackthorn1(); // Night Eyes: no strength requirement of its own
            made.Add(earrings);
            Assert.Equal(Layer.Earrings, earrings.Layer);
            Assert.Equal(10, earrings.Attributes.DefendChance);
            Assert.Equal(3, earrings.Attributes.CastRecovery);
            Assert.Equal(1233, earrings.Hue);

            var steelEarrings = new GargishEarringsBearingTheCrestOfBlackthorn3(); // Folded Steel: strength 45
            made.Add(steelEarrings);
            Assert.Equal(45, steelEarrings.StrRequirement);
            Assert.Equal(20, steelEarrings.BasePhysicalResistance);

            // The Agent's own epaulette, a leaf port.
            var plain = new Epaulette();
            made.Add(plain);
            Assert.Equal(0x9985, plain.ItemID);
            Assert.Equal(1123325, plain.LabelNumber);
            Assert.True(PropertyListReader.Read(plain).Has(1123325));

            // The reward list: 132 rows in ServUO's order and prices; every type constructs.
            var rewards = BlackthornRewards.Rewards;
            Assert.Equal(132, rewards.Count);
            Assert.Equal(132, rewards.Select(r => r.Type).Distinct().Count());
            Assert.Equal(typeof(DecorativeShardShieldDeed), rewards[0].Type);
            Assert.Equal(10, rewards[0].Points);
            Assert.Equal(typeof(BlackthornPainting1), rewards[1].Type);
            Assert.Equal(typeof(BlackthornPainting2), rewards[2].Type);
            Assert.Equal(25, rewards[3].Points);
            Assert.Equal(typeof(GargishStoneChestBearingTheCrestOfBlackthorn), rewards[3].Type);
            Assert.Equal(100, rewards[^1].Points);
            Assert.Equal(typeof(WoodlandBeltBearingTheCrestOfBlackthorn2), rewards[^1].Type);
            Assert.Equal(new[] { 10.0, 25.0, 50.0, 75.0, 100.0 }, rewards.Select(r => r.Points).Distinct().OrderBy(p => p).ToArray());
            Assert.Equal(129, rewards.Count(r => AgentOfTheCrown.DisplayTypes.Contains(r.Type)));
            Assert.All(AgentOfTheCrown.DisplayTypes.Where(t => !rewards.Any(r => r.Type == t)), t => Assert.StartsWith("RobeBearing", t.Name));
            Assert.True(BlackthornRewards.IsTokunoDyable(typeof(BlackthornPainting1)));
            Assert.False(BlackthornRewards.IsTokunoDyable(typeof(Katana)));

            foreach (var reward in rewards)
            {
                var item = Activator.CreateInstance(reward.Type) as Item;
                Assert.NotNull(item);
                made.Add(item);
            }

            // The paintings, the shard shield and its deed.
            var painting = new BlackthornPainting1();
            made.Add(painting);
            Assert.Equal(0x4C62, painting.ItemID);
            Assert.Equal(1023744, painting.LabelNumber);
            Assert.Equal(0x4C64, new BlackthornPainting2().ItemID);

            var deed = new DecorativeShardShieldDeed();
            made.Add(deed);
            Assert.Equal(1153729, deed.LabelNumber);
            Assert.Equal(LootType.Blessed, deed.LootType);
            Assert.Equal(25499, DecorativeShardShieldDeed.GetSouthItemID(DecorativeShardShieldDeed.Start));
            Assert.Equal(27, DecorativeShardShieldDeed.End - DecorativeShardShieldDeed.Start + 1);

            var east = new DecorativeShardShield(0x6380);
            made.Add(east);
            Assert.True(east.FacingEast);
            Assert.False(east.Movable);
            var south = new DecorativeShardShield(DecorativeShardShieldDeed.GetSouthItemID(0x6380));
            made.Add(south);
            Assert.False(south.FacingEast);
            var reDeed = east.Deed;
            made.Add(reDeed);
            Assert.IsType<DecorativeShardShieldDeed>(reDeed);
        }
        finally
        {
            foreach (var item in made)
            {
                item?.Delete();
            }
        }
    }

    [Fact]
    public void TheAgentTakesMinaxArtifactsAndSellsTheCrest()
    {
        var ns = PacketTestUtilities.CreateTestNetState();
        ShardTestClock.Arm();
        EnsureRewards();
        ClearBlackthorn();

        PlayerMobile pm = null;
        AgentOfTheCrown agent = null;

        try
        {
            agent = new AgentOfTheCrown();
            agent.MoveToWorld(new Point3D(1488, 1473, 1), Map.Trammel);

            pm = NewPlayer(new Point3D(1489, 1473, 1));
            pm.NetState = ns;
            ns.Mobile = pm;

            // His menu: the shrine healer's three, Turn In (disabled while the pack holds nothing of Minax) and Claim.
            var list = PooledRefList<ContextMenuEntry>.Create();
            agent.GetContextMenuEntries(pm, ref list);
            var numbers = new List<(int Number, bool Enabled)>();

            for (var i = 0; i < list.Count; i++)
            {
                numbers.Add((list[i].Number, list[i].Enabled));
            }

            list.Dispose();

            Assert.Contains((3006195, false), numbers); // Resurrect, disabled while alive
            Assert.Contains((3006196, true), numbers);  // Lock Karma
            Assert.Contains((3006198, true), numbers);  // Tithe Gold
            Assert.Contains((1154571, false), numbers); // Turn In Minax Artifacts
            Assert.Contains((1154572, true), numbers);  // Claim Blackthorn Artifacts

            // A Minax artifact is redeemable; a plain katana and a crest item are not.
            var minax = MinaxArtifacts.Create(pm, 750, 0);
            Assert.NotNull(minax);
            Assert.True(pm.Backpack.TryDropItem(pm, minax, false));
            var katana = new Katana();
            pm.Backpack.DropItem(katana);
            var crest = new CloakBearingTheCrestOfBlackthorn();
            pm.Backpack.DropItem(crest);

            Assert.True(agent.IsRedeemableItem(minax));
            Assert.False(agent.IsRedeemableItem(katana));
            Assert.False(agent.IsRedeemableItem(crest));

            var buttons = agent.FindRedeemableItems(pm);
            Assert.Single(buttons);
            Assert.Same(minax, ((ItemTileButtonInfo)buttons[0]).Item);

            list = PooledRefList<ContextMenuEntry>.Create();
            agent.GetContextMenuEntries(pm, ref list);
            var turnInEnabled = false;
            var turnInSeen = 0;

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Number == 1154571)
                {
                    turnInSeen++;
                    turnInEnabled = list[i].Enabled;
                }
            }

            list.Dispose();
            Assert.Equal(1, turnInSeen);
            Assert.True(turnInEnabled);

            // Turning it in: the item is gone, one point is banked, the registry forgets it.
            Assert.Equal(0.0, BlackthornData.Instance.GetPoints(pm));
            var gump = new TurnInGump(agent, buttons);
            gump.HandleButtonResponse(ns, 0, buttons[0]);

            Assert.True(minax.Deleted);
            Assert.Equal(1.0, BlackthornData.Instance.GetPoints(pm));
            Assert.False(MinaxArtifacts.IsMinax(minax));
            Assert.Empty(agent.FindRedeemableItems(pm));

            // Too far away, nothing happens.
            var far = MinaxArtifacts.Create(pm, 750, 0);
            Assert.True(pm.Backpack.TryDropItem(pm, far, false));
            pm.Location = new Point3D(1500, 1473, 1);
            new TurnInGump(agent, agent.FindRedeemableItems(pm)).HandleButtonResponse(ns, 0, agent.FindRedeemableItems(pm)[0]);
            Assert.False(far.Deleted);
            Assert.Equal(1.0, BlackthornData.Instance.GetPoints(pm));
            pm.Location = new Point3D(1489, 1473, 1);

            // The reward gump: ten points buy a painting and leave nothing.
            BlackthornData.Instance.SetPoints(pm, 10);
            var reward = new BlackthornRewardGump(agent, pm);
            Assert.Equal(10.0, reward.Points);
            Assert.Equal(10.0, reward.GetPoints(pm));
            Assert.Equal(1154516, reward.Title);

            reward.OnConfirmed(BlackthornRewards.Rewards[1], 1);

            Assert.Single(pm.Backpack.Items.OfType<BlackthornPainting1>());
            Assert.Equal(0.0, BlackthornData.Instance.GetPoints(pm));

            // 25 points buy a crest item that bears the crest.
            BlackthornData.Instance.SetPoints(pm, 25);
            var row = BlackthornRewards.Rewards.First(r => r.Type == typeof(ChainmailTunicBearingTheCrestOfBlackthorn));
            new BlackthornRewardGump(agent, pm).OnConfirmed(row, BlackthornRewards.Rewards.IndexOf(row));
            var tunic = Assert.Single(pm.Backpack.Items.OfType<ChainmailTunicBearingTheCrestOfBlackthorn>());
            Assert.Equal(1773, tunic.Hue);
            Assert.True(PropertyListReader.Read(tunic).Has(1154548, $"#{tunic.LabelNumber}"));
            Assert.Equal(0.0, BlackthornData.Instance.GetPoints(pm));

            // Not enough points: the base gump refuses before confirming (OnResponse path); DeductPoints refuses an
            // overdraft on its own too.
            Assert.False(BlackthornData.Instance.DeductPoints(pm, 1));

            // The Agent round-trips with his display pack.
            var copy = RoundTrip(agent);
            Assert.Equal(agent.Title, copy.Title);
            copy.Delete();
        }
        finally
        {
            if (pm != null)
            {
                ns.Mobile = null;
                pm.NetState = null;
            }

            pm?.Delete();
            agent?.Delete();
            ClearBlackthorn();
        }
    }

    [Fact]
    public void TheMinaxArtifactRollFollowsServUOAndTheRegistrySaves()
    {
        ShardTestClock.Arm();
        MinaxArtifacts.Clear();

        var made = new List<Item>();
        PlayerMobile pm = null;

        try
        {
            Assert.Equal(5, MinaxArtifacts.GetDivisor(100));
            Assert.Equal(4, MinaxArtifacts.GetDivisor(400));
            Assert.Equal(3, MinaxArtifacts.GetDivisor(550));
            Assert.Equal(2, MinaxArtifacts.GetDivisor(650));
            Assert.False(MinaxArtifacts.IsPowerful(549));
            Assert.True(MinaxArtifacts.IsPowerful(550));

            for (var i = 0; i < 300; i++)
            {
                Assert.InRange(MinaxArtifacts.GetProperties(5), 0, 5);
                Assert.InRange(MinaxArtifacts.GetProperties(10), 0, 10);
                Assert.InRange(MinaxArtifacts.GetProperties(1), 0, 1);
            }

            Assert.Equal(0, MinaxArtifacts.GetProperties(0));

            // The invasion budget (700-800): the spread runs from half the base to 500 above it, so most rolls are
            // powerful (4 to 10 properties at 50-70 to 100 percent) and the ones that land under 550 are not (1 to 5).
            var powerful = 0;

            for (var i = 0; i < 300; i++)
            {
                var (mods, low, high, budget) = MinaxArtifacts.Roll(null, 750, 0);
                Assert.InRange(budget, 375, 1250);

                if (MinaxArtifacts.IsPowerful(budget))
                {
                    powerful++;
                    Assert.InRange(mods, 4, 10);
                    Assert.Equal(100, high);
                    Assert.InRange(low, 50, 70);
                }
                else
                {
                    Assert.InRange(mods, 1, 5);
                    Assert.InRange(high, 50, 100);
                    Assert.InRange(low, 20, 80);
                }
            }

            // About two thirds of 750-base rolls reach 550 (mean 199 of 300, sd 8); six sigmas either way.
            Assert.InRange(powerful, 150, 248);

            // The smallest kill budget (150): 1 to 5 properties, intensity from 20-50 up.
            for (var i = 0; i < 300; i++)
            {
                var (mods, low, high, budget) = MinaxArtifacts.Roll(null, 150, 0);
                Assert.InRange(budget, 150, 650);
                Assert.InRange(mods, 1, 10);
                Assert.InRange(low, 10, 80);
                Assert.InRange(high, low, 100);
            }

            // A Felucca killer adds the Felucca bonus to the budget.
            pm = NewPlayer(new Point3D(1000, 1000, 0), Map.Felucca);
            var felBudgets = Enumerable.Range(0, 100).Select(_ => MinaxArtifacts.Roll(pm, 150, 0).Budget).ToList();
            Assert.Contains(felBudgets, b => b > 650 || b >= 250);

            // ServUO's difficulty: fame / 29, clamped to 150..700.
            var lich = new Lich();
            made.Add(new Gold()); // placeholder so the list is never empty
            Assert.Equal(Math.Clamp(lich.Fame / 29, 150, 700), MinaxArtifacts.GetDifficultyFor(lich));
            lich.Delete();
            Assert.Equal(0, MinaxArtifacts.GetDifficultyFor(null));

            // Fifty artifacts: each a weapon, armour, shield, jewel or hat, hued 1157, registered, with properties on it.
            var withAttributes = 0;

            for (var i = 0; i < 50; i++)
            {
                var item = MinaxArtifacts.Create(pm, 750, 0);
                Assert.NotNull(item);
                made.Add(item);

                Assert.Equal(1157, item.Hue);
                Assert.True(MinaxArtifacts.IsMinax(item));
                Assert.True(item is BaseWeapon or BaseArmor or BaseJewel or BaseHat, item.GetType().Name);
                Assert.True(item.Name == null || item.Name.EndsWith(" of Minax", StringComparison.Ordinal), item.Name);

                var magic = item switch
                {
                    BaseWeapon w => !w.Attributes.IsEmpty || !w.WeaponAttributes.IsEmpty || !w.SkillBonuses.IsEmpty || w.Slayer != SlayerName.None,
                    BaseArmor a  => !a.Attributes.IsEmpty || !a.ArmorAttributes.IsEmpty || !a.SkillBonuses.IsEmpty || a.PhysicalBonus + a.FireBonus + a.ColdBonus + a.PoisonBonus + a.EnergyBonus > 0,
                    BaseJewel j  => !j.Attributes.IsEmpty || !j.SkillBonuses.IsEmpty || !j.Resistances.IsEmpty,
                    BaseHat h    => !h.Attributes.IsEmpty || !h.SkillBonuses.IsEmpty || !h.ClothingAttributes.IsEmpty,
                    _            => false
                };

                if (magic)
                {
                    withAttributes++;
                }
            }

            Assert.True(withAttributes >= 45, $"{withAttributes} of 50 Minax artifacts carry a property");
            Assert.Equal(50, MinaxArtifacts.Count);

            // The registry saves the live items and forgets a deleted one.
            var dead = made.Last();
            dead.Delete();

            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            MinaxArtifacts.Serialize(writer);
            writer.Flush();

            MinaxArtifacts.Clear();
            Assert.Equal(0, MinaxArtifacts.Count);
            Assert.False(MinaxArtifacts.IsMinax(made[1]));

            MinaxArtifacts.Deserialize(new BufferReader(buffer));
            Assert.Equal(49, MinaxArtifacts.Count);
            Assert.True(MinaxArtifacts.IsMinax(made[1]));
            Assert.False(MinaxArtifacts.IsMinax(dead));

            // Mark and Remove by hand.
            var plain = new Katana();
            made.Add(plain);
            Assert.False(MinaxArtifacts.IsMinax(plain));
            MinaxArtifacts.Mark(plain);
            Assert.True(MinaxArtifacts.IsMinax(plain));
            Assert.Equal(1157, plain.Hue);
            Assert.True(MinaxArtifacts.Remove(plain));
            Assert.False(MinaxArtifacts.IsMinax(plain));
        }
        finally
        {
            foreach (var item in made)
            {
                item?.Delete();
            }

            pm?.Delete();
            MinaxArtifacts.Clear();
        }
    }

    [Fact]
    public void TheInvadersConstructRoundTripAndTheCaptainDropsTheCrestOfMinax()
    {
        ShardTestClock.Arm();
        EnsureCorpses();
        MinaxArtifacts.Clear();

        var made = new List<Mobile>();
        PlayerMobile pm = null;

        try
        {
            var specialties = new[]
            {
                SkillName.Swords, SkillName.Fencing, SkillName.Macing, SkillName.Archery, SkillName.Magery,
                SkillName.Mysticism, SkillName.Spellweaving, SkillName.Bushido, SkillName.Ninjitsu,
                SkillName.Chivalry, SkillName.Necromancy, SkillName.Poisoning
            };

            foreach (InvasionType type in Enum.GetValues<InvasionType>())
            {
                foreach (var specialty in specialties)
                {
                    var inv = new Invader(specialty, type);
                    made.Add(inv);

                    Assert.Equal(specialty, inv.Specialty);
                    Assert.Equal(type, inv.InvasionType);
                    Assert.True(inv.AlwaysMurderer);
                    Assert.True(inv.HasAura);
                    Assert.Equal(TimeSpan.FromSeconds(30), inv.AuraInterval);
                    Assert.Equal(5, inv.AuraRange);
                    Assert.Equal(Invader.IsFighter(specialty), inv.CanHeal);
                    Assert.Equal(Invader.IsFighter(specialty) ? 0.4 : 0.1, inv.WeaponAbilityChance);
                    Assert.Equal(Invader.GetAI(specialty), inv.AI);
                    Assert.True(inv.Body == 0x190 || inv.Body == 0x191 || inv.Body == 0x25D || inv.Body == 0x25E, $"body {inv.Body.BodyID}");
                    Assert.StartsWith("the ", inv.Title);
                    Assert.False(string.IsNullOrEmpty(inv.Name));
                    Assert.InRange(inv.HitsMax, 800, 1250);
                    Assert.InRange(inv.Skills[SkillName.Tactics].Base, 100.0, 120.0);
                    Assert.True(inv.Skills[specialty].Base >= 100.0, $"{specialty} {inv.Skills[specialty].Base}");
                    // D27: the host has no tile data, so weapons and armour sit on Layer.Invalid; read them by type.
                    Assert.Contains(inv.Items, i => i is BaseWeapon or Spellbook);
                    Assert.Contains(inv.Items, i => i is ThighBoots);
                    Assert.Equal(8000, inv.Fame);
                    Assert.Equal(-8000, inv.Karma);
                    Assert.Contains(ReadProperties(inv), e => e.Number == 1154196 + (int)type);

                    if (specialty is SkillName.Archery or SkillName.Spellweaving)
                    {
                        Assert.Same(Race.Elf, inv.Race);
                    }

                    var ability = inv.GetWeaponAbility();
                    var wep = inv.Weapon as BaseWeapon; // Fists in the host (D27), a real weapon in the world
                    Assert.True(wep == null || ability == wep.PrimaryAbility || ability == wep.SecondaryAbility);

                    var copy = RoundTrip(inv);
                    made.Add(copy);
                    Assert.Equal(specialty, copy.Specialty);
                    Assert.Equal(type, copy.InvasionType);
                    Assert.Equal(inv.Sampire, copy.Sampire);
                }
            }

            // The specialties' gear.
            var ninja = made.OfType<Invader>().First(i => i.Specialty == SkillName.Ninjitsu && !(i is InvaderCaptain));
            var belt = Assert.IsType<LeatherNinjaBelt>(ninja.FindItemOnLayer(Layer.Waist));
            Assert.Equal(20, belt.UsesRemaining);
            Assert.Equal(20, belt.PoisonCharges);
            Assert.Same(Poison.Greater, belt.Poison);
            Assert.Equal(2, ninja.Backpack.Items.OfType<Fukiya>().Count());
            Assert.Contains(ninja.Items, i => i is LeatherNinjaHood);
            Assert.Equal(100.0, ninja.Skills[SkillName.Stealth].Base);

            var paladin = made.OfType<Invader>().First(i => i.Specialty == SkillName.Chivalry);
            Assert.Contains(paladin.Items, i => i is MetalKiteShield);
            Assert.Contains(paladin.Items, i => i is PlateChest);
            Assert.Equal(120.0, paladin.Skills[SkillName.Parry].Base);
            Assert.Contains(paladin.Backpack.Items, i => i is Bandage);

            var assassin = made.OfType<Invader>().First(i => i.Specialty == SkillName.Poisoning);
            var blade = assassin.Items.OfType<BaseWeapon>().Single();
            Assert.Same(Poison.Lethal, blade.Poison);
            Assert.Equal(100, blade.PoisonCharges);

            var mage = made.OfType<Invader>().First(i => i.Specialty == SkillName.Magery);
            Assert.Equal("the wizard", mage.Title);
            Assert.IsType<WizardsHat>(mage.FindItemOnLayer(Layer.Helm));
            Assert.True(mage.SpellCaster);
            Assert.True(mage.CanDoSpecial);
            Assert.InRange(mage.Skills[SkillName.Magery].Base, 100.0, 120.0);
            Assert.DoesNotContain(mage.Backpack.Items, i => i is Bandage);

            var samurai = made.OfType<Invader>().First(i => i.Specialty == SkillName.Bushido);
            Assert.Contains(samurai.Items, i => i is LeatherDo);
            Assert.Equal(120.0, samurai.Skills[SkillName.Parry].Base);

            // A sampire turns up within a few dozen samurai; a woman swordsman is "the Swordswoman".
            Invader sampire = null;
            Invader swordswoman = null;

            for (var i = 0; i < 200 && (sampire == null || swordswoman == null); i++)
            {
                var s = new Invader(SkillName.Bushido, InvasionType.Undead);
                made.Add(s);
                sampire ??= s.Sampire ? s : null;

                var w = new Invader(SkillName.Swords, InvasionType.Orc);
                made.Add(w);
                swordswoman ??= w.Female ? w : null;
            }

            Assert.NotNull(sampire);
            Assert.Equal("the sampire", sampire.Title);
            Assert.InRange(sampire.Skills[SkillName.Necromancy].Base, 100.0, 120.0);
            Assert.InRange(sampire.Skills[SkillName.SpiritSpeak].Base, 100.0, 120.0);
            Assert.Equal(100, sampire.Items.OfType<BaseWeapon>().Single().WeaponAttributes.HitLeechHits);

            Assert.NotNull(swordswoman);
            Assert.EndsWith("woman", swordswoman.Title);

            // The captain: tougher, famous, and his death hands the crest of Minax to the two best damagers in range.
            var captain = new InvaderCaptain(InvasionType.Dragons);
            made.Add(captain);
            Assert.InRange(captain.HitsMax, 8000, 12000);
            Assert.Equal(48000, captain.Fame);
            Assert.Equal(250, captain.RawStr);
            Assert.True(captain.Skills[SkillName.Tactics].Base >= 105.0);
            Assert.True(captain.PhysicalResistance >= 20);
            Assert.Equal(InvasionType.Dragons, captain.InvasionType);
            Assert.Equal(captain.Specialty, RoundTrip(captain).Specialty);

            pm = NewPlayer(new Point3D(1000, 1000, 0));
            captain.MoveToWorld(new Point3D(1001, 1000, 0), Map.Trammel);
            KillBy(captain, pm);

            var drops = pm.Backpack.Items.Where(MinaxArtifacts.IsMinax).ToList();
            Assert.Equal(2, drops.Count);
            Assert.All(drops, d => Assert.Equal(1157, d.Hue));
            Assert.All(drops, d => Assert.True(d is BaseWeapon or BaseArmor or BaseJewel or BaseHat, d.GetType().Name));
        }
        finally
        {
            foreach (var m in made)
            {
                m?.Delete();
            }

            pm?.Delete();
            MinaxArtifacts.Clear();
        }
    }
}
