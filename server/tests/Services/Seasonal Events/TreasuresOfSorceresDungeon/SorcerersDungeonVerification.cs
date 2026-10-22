// cc-P408 Treasures of Sorcerer's Dungeon (Jack the Pumpkin King), ported from ServUO and run on the Halloween season
// switch. Notes: shard-migration/notes/cc-P408-sorcerers-dungeon-halloween.md.
//
// What the facts pin, each with ServUO's values written as literals (never the port's own constants, so a break in
// the port cannot move its expectation with it):
//   1. The nine bosses and the pumpkin: name, body, hits, fame, damage, AI, loot-relevant flags, their ServUO
//      abilities on the cc-P121 profile, and Jack's damage reflex.
//   2. The spawner: nine rooms in ServUO's order, inside the stock "Sorcerer's Dungeon" region, 80 kills and 15 at a
//      time; the boss comes at 80 kills, the room advances when the boss is gone; a creature deleted without dying
//      leaves the list without counting as a kill (deviation 6); the stock spawners in the rooms are paused while it
//      runs and only those are restarted.
//   3. On, off, remove: on places the researcher, the seven entrance pieces and the spawner once; off takes the
//      spawner, its creatures and its boss away and leaves the researcher; remove takes away exactly what on placed.
//      The staff verb boss <room> raises that room's boss once (ours).
//   4. The season: the event's switch reads the Halloween settings (clusterf.seasonal.halloween.*) with its own saved
//      state; the automatic check turns it on inside 10-24 to 11-02 and off after.
//   5. Kills: a kill in the dungeon while enabled adds fame / 4 to the tally, the chance formula is ServUO's, a hit
//      gives an artifact of Enchanted Origin (hue 1171); the death event reaches the spawner's kill count.
//   6. The researcher takes artifacts of Enchanted Origin for a point each; the reward list and prices are ServUO's
//      (without the three title deeds, deviation 2).
//   7. The rewards behave: the bag of tricks, the toy, the heart, the ring's skill bonus.
//   8. Every saved type round-trips (each [SerializationGenerator] type, the points system with the spawner inside
//      it, the artifact registry).
//
// The test host has no map files (every spawn point is good, z 0), loads no regions.json and runs no Initialize, so
// the facts register the region and the reward list themselves, and each clears every event object first and last.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Points;
using Server.Engines.SorcerersDungeon;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SorcerersDungeonVerification
{
    private readonly ITestOutputHelper _out;

    public SorcerersDungeonVerification(ITestOutputHelper output) => _out = output;

    // pinned Distribution/Data/regions.json, "Sorcerer's Dungeon" (Ilshenar), x1 y1 x2 y2.
    private static readonly (int X1, int Y1, int X2, int Y2)[] RegionArea =
    {
        (368, 0, 488, 120),
        (200, 0, 368, 112),
        (256, 120, 288, 152),
        (48, 0, 184, 136),
        (224, 112, 248, 144)
    };

    private static DungeonRegion _region;

    private static DungeonRegion EnsureRegion()
    {
        if (_region?.Registered == true)
        {
            return _region;
        }

        var rects = RegionArea
            .Select(r => new Rectangle3D(new Point3D(r.X1, r.Y1, Region.MinZ), new Point3D(r.X2, r.Y2, Region.MaxZ)))
            .ToArray();

        _region = new DungeonRegion("Sorcerer's Dungeon", Map.Ilshenar, 2, rects);
        _region.Register();
        return _region;
    }

    private static void EnsureRewards()
    {
        if (SorcerersDungeonRewards.Rewards == null)
        {
            SorcerersDungeonRewards.Initialize();
        }
    }

    private static PlayerMobile NewPlayer(Point3D loc, Map map)
    {
        var pm = new PlayerMobile();
        pm.AddItem(new Backpack());
        pm.Player = true;
        pm.MoveToWorld(loc, map);
        pm.Hits = pm.HitsMax;
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

    private static void ClearEvent()
    {
        TOSDSpawner.Instance?.Deactivate();
        TOSDSpawner.Instance = null;

        foreach (var m in World.Mobiles.Values.ToList())
        {
            if (m.Map == Map.Ilshenar && m is BaseCreature { Controlled: false } || m is SorcerersDungeonResearcher)
            {
                m.Delete();
            }
        }

        foreach (var item in World.Items.Values.ToList())
        {
            if (item.Map == Map.Ilshenar && item is Static or BaseSpawner or UnholyPumpkin)
            {
                item.Delete();
            }
        }

        SorcerersDungeonResearcher.Instance = null;
        ClusterFSeasonalState.Placed(SorcerersDungeonGenerate.SeasonName).Clear();
        ClusterFSeasonalState.SetLast(SorcerersDungeonGenerate.SeasonName, null);
        SorcerersDungeonData.Instance.Enabled = false;
        SorcerersDungeonData.Instance.DungeonPoints.Clear();
        SorcerersDungeonData.Instance.Clear();
        EnchantedOriginArtifacts.Clear();
    }

    // ServUO's Map.GetRandomSpawnPoint draws x in X..X+Width and y in Y..Y+Height, both ends in (Rectangle2D.Contains
    // leaves the far edge out).
    private static bool InRoom(Rectangle2D r, Point3D p) =>
        p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;

    private static void KillBy(BaseCreature bc, PlayerMobile pm)
    {
        bc.Damage(Math.Max(1, bc.Hits - 1), pm);
        bc.Kill();
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheBossesHaveServUOsStatsAndAbilities()
    {
        ShardTestClock.Arm();

        // (type, name, body, hits, fame, min damage, max damage, AI)
        var expected = new (Type Type, string Name, int Body, int Hits, int MinDamage, int MaxDamage, AIType Ai)[]
        {
            (typeof(NightmareFairy), "a nightmare fairy", 176, 8000, 21, 27, AIType.AI_Mage),
            (typeof(JackInTheBoxMonster), "jack in the box", 1428, 8000, 21, 27, AIType.AI_Mage),
            // AI_Mage in its constructor, then SetMagicalAbility(WrestlingMastery) makes it AI_Melee: ServUO
            // AbilityProfile.AddMagicalAbility (Services/Pet Training/AbilityProfile.cs:408-414), which cc-P121 reproduces.
            (typeof(HeadlessElf), "a headless elf", 31, 8000, 21, 27, AIType.AI_Melee),
            (typeof(AbominableSnowman), "the abominable snowman", 241, 8000, 21, 27, AIType.AI_Melee),
            (typeof(TwistedHolidayTree), "a twisted holidy tree", 47, 8000, 21, 27, AIType.AI_Mage),
            (typeof(RabidReindeer), "a rabid reindeer", 0xEA, 8000, 22, 29, AIType.AI_Melee),
            (typeof(GarishGingerman), "a garish gingerman", 14, 8000, 21, 27, AIType.AI_Mage),
            (typeof(StockingSerpent), "a stocking serpent", 0x509, 8000, 22, 29, AIType.AI_Melee),
            (typeof(JackThePumpkinKing), "jack", 0x190, 26000, 22, 26, AIType.AI_Mage)
        };

        var made = new List<BaseCreature>();

        try
        {
            foreach (var e in expected)
            {
                var bc = (BaseCreature)Activator.CreateInstance(e.Type)!;
                made.Add(bc);

                Assert.Equal(e.Name, bc.Name);
                Assert.Equal(e.Body, (int)bc.Body);
                Assert.Equal(e.Hits, bc.HitsMax);
                Assert.Equal(12000, bc.Fame);
                Assert.Equal(-12000, bc.Karma);
                Assert.Equal(e.MinDamage, bc.DamageMin);
                Assert.Equal(e.MaxDamage, bc.DamageMax);
                Assert.True(e.Ai == bc.AI, $"{e.Type.Name}: AI {bc.AI}, expected {e.Ai}");
                Assert.True(bc.AlwaysMurderer, e.Type.Name);

                // The poison table is empty in the test host (Poison.Deadly reads null there), so the fact checks the
                // creature declares its own immunity, as each ServUO file does.
                Assert.Equal(e.Type, e.Type.GetProperty(nameof(BaseCreature.PoisonImmune))!.DeclaringType);
            }

            var jack = (JackThePumpkinKing)made[8];
            Assert.Equal("the pumpkin king", jack.Title);
            Assert.Equal(1200, jack.RawInt);
            Assert.Equal(120.0, jack.Skills.Necromancy.Base);
            Assert.Contains(jack.Items, i => i is ClothNinjaHood { Hue: 1281 });
            Assert.Contains(jack.Items, i => i is BoneChest { Hue: 1175 });
            Assert.Contains(jack.Items, i => i is Scepter);

            var elf = (HeadlessElf)made[2];
            Assert.Same(Race.Elf, elf.Race);
            Assert.Equal(0x39D, elf.BaseSoundID);

            // The abilities, on the cc-P121 profile as ServUO's SetSpecialAbility / SetMagicalAbility / SetAreaEffect.
            Assert.True(made[0].HasAbility(SpecialAbility.LifeLeech));
            Assert.True(made[0].HasAbility(SpecialAbility.LifeDrain));
            Assert.True(made[2].HasAbility(MagicalAbility.WrestlingMastery));
            Assert.True(made[3].HasAbility(SpecialAbility.TrueFear));
            Assert.True(made[4].HasAbility(AreaEffect.ExplosiveGoo));
            Assert.True(made[5].HasAbility(SpecialAbility.VenomousBite));
            Assert.True(made[6].HasAbility(MagicalAbility.MageryMastery));
            Assert.True(made[7].HasAbility(SpecialAbility.ViciousBite));

            // SetWeaponAbility pairs: GetWeaponAbility draws one of the two.
            var seen = new HashSet<WeaponAbility>();
            for (var i = 0; i < 200; i++)
            {
                seen.Add(made[1].GetWeaponAbility());
                seen.Add(made[3].GetWeaponAbility());
            }

            Assert.Equal(
                new HashSet<WeaponAbility> { WeaponAbility.ArmorIgnore, WeaponAbility.BleedAttack, WeaponAbility.ConcussionBlow, WeaponAbility.CrushingBlow },
                seen
            );

            Assert.Equal(Poison.Lethal, ((RabidReindeer)made[5]).HitPoison);
            Assert.Equal(15, ((RabidReindeer)made[5]).Hides);
            Assert.Equal(Poison.Lethal, ((StockingSerpent)made[7]).HitPoison);
        }
        finally
        {
            foreach (var bc in made)
            {
                bc.Delete();
            }
        }
    }

    [Fact]
    public void JackHitsBackForEightToTwentyOnEveryBlow()
    {
        ShardTestClock.Arm();
        ClearEvent();

        PlayerMobile pm = null;
        JackThePumpkinKing jack = null;

        try
        {
            pm = NewPlayer(new Point3D(300, 80, 0), Map.Ilshenar);
            pm.RawStr = 100;
            pm.Hits = pm.HitsMax;
            jack = new JackThePumpkinKing();
            jack.MoveToWorld(new Point3D(301, 80, 0), Map.Ilshenar);

            var before = pm.Hits;
            jack.Damage(10, pm);

            var lost = before - pm.Hits;
            _out.WriteLine($"player {before} -> {pm.Hits}");
            Assert.InRange(lost, 1, 20); // 8 to 20 physical, less the player's physical resistance

            var pumpkin = new UnholyPumpkin();
            Assert.Equal(0xF7E, pumpkin.ItemID);
            Assert.False(pumpkin.Movable);
            Assert.Equal("unholy pumpkin", pumpkin.Name ?? pumpkin.DefaultName);
            pumpkin.Delete();
        }
        finally
        {
            pm?.Delete();
            jack?.Delete();
            ClearEvent();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheSpawnerRotatesNineRoomsInsideTheDungeon()
    {
        ShardTestClock.Arm();
        ClearEvent();

        try
        {
            var spawner = new TOSDSpawner(activate: false);

            var bosses = new[]
            {
                typeof(NightmareFairy), typeof(JackInTheBoxMonster), typeof(HeadlessElf), typeof(AbominableSnowman),
                typeof(TwistedHolidayTree), typeof(RabidReindeer), typeof(GarishGingerman), typeof(StockingSerpent),
                typeof(JackThePumpkinKing)
            };

            var rooms = new[]
            {
                new Rectangle2D(327, 26, 29, 32), new Rectangle2D(296, 10, 17, 26), new Rectangle2D(271, 4, 20, 33),
                new Rectangle2D(227, 39, 21, 19), new Rectangle2D(251, 68, 25, 32), new Rectangle2D(144, 5, 23, 19),
                new Rectangle2D(60, 53, 13, 34), new Rectangle2D(152, 48, 16, 23), new Rectangle2D(291, 73, 37, 36)
            };

            Assert.Equal(9, spawner.Entries.Count);

            for (var i = 0; i < 9; i++)
            {
                var e = spawner.Entries[i];
                Assert.Equal(bosses[i], e.Boss);
                Assert.Equal(rooms[i], e.SpawnArea);
                Assert.Equal(80, e.ToKill);
                Assert.Equal(15, e.MaxSpawn);
                Assert.Equal(new[] { typeof(Zombie), typeof(Skeleton), typeof(Gargoyle), typeof(Lich), typeof(LichLord) }, e.Spawn);

                // Every corner of every room lies in the stock region, so every kill there can roll an artifact.
                foreach (var (x, y) in new[] { (e.SpawnArea.X, e.SpawnArea.Y), (e.SpawnArea.X + e.SpawnArea.Width, e.SpawnArea.Y + e.SpawnArea.Height) })
                {
                    Assert.True(RegionArea.Any(r => x >= r.X1 && x < r.X2 && y >= r.Y1 && y < r.Y2), $"{e.Boss.Name} room corner {x},{y} is outside Sorcerer's Dungeon");
                }
            }

            // Fill the room by hand (the timed spawn is a lambda on the wheel), then kill 80.
            for (var i = 0; i < 15; i++)
            {
                spawner.DoSpawn(typeof(Zombie), false);
            }

            Assert.Equal(15, spawner.Spawn.Count);
            Assert.All(spawner.Spawn, z => Assert.True(InRoom(rooms[0], z.Location), z.Location.ToString()));
            Assert.All(spawner.Spawn, z => Assert.Equal(Map.Ilshenar, z.Map));

            // A creature deleted without dying leaves the list on the next tick and does not count (deviation 6).
            spawner.Spawn[0].Delete();
            spawner.OnTick();
            Assert.Equal(14, spawner.Spawn.Count);
            Assert.Equal(0, spawner.KillCount);
            spawner.Spawning = false;

            // A death counts once.
            var victim = spawner.Spawn[0];
            spawner.OnCreatureDeath(victim);
            spawner.OnCreatureDeath(victim);
            Assert.Equal(1, spawner.KillCount);

            spawner.KillCount = 80;
            spawner.OnTick();
            Assert.IsType<NightmareFairy>(spawner.Boss);
            Assert.True(InRoom(rooms[0], spawner.Boss.Location), spawner.Boss.Location.ToString());

            // The boss gone, the next room; after the last room, the first.
            spawner.Boss.Delete();
            spawner.OnTick();
            Assert.Equal(1, spawner.Index);
            Assert.Null(spawner.Boss);
            Assert.Equal(0, spawner.KillCount);

            spawner.Index = 8;
            spawner.KillCount = 80;
            spawner.OnTick();
            Assert.IsType<JackThePumpkinKing>(spawner.Boss);
            spawner.Boss.Delete();
            spawner.OnTick();
            Assert.Equal(0, spawner.Index);

            spawner.Deactivate();
            Assert.Null(TOSDSpawner.Instance);
        }
        finally
        {
            ClearEvent();
        }
    }

    [Fact]
    public void TheStockSpawnersInTheRoomsPauseWhileItRunsAndOnlyThoseRestart()
    {
        ShardTestClock.Arm();
        ClearEvent();

        Spawner running = null, stopped = null, outside = null;

        try
        {
            running = new Spawner("Zombie");
            running.MoveToWorld(new Point3D(330, 30, 0), Map.Ilshenar); // the Nightmare Fairy's room
            running.Start();
            stopped = new Spawner("Zombie");
            stopped.MoveToWorld(new Point3D(300, 20, 0), Map.Ilshenar); // the Jack in the Box room
            stopped.Stop();
            outside = new Spawner("Zombie");
            outside.MoveToWorld(new Point3D(600, 600, 0), Map.Ilshenar);
            outside.Start();

            Assert.True(running.Running);
            Assert.False(stopped.Running);

            var spawner = new TOSDSpawner();
            Assert.Equal(new[] { running }, spawner.PausedSpawners.Cast<Spawner>().ToArray());
            Assert.False(running.Running);
            Assert.True(outside.Running);

            spawner.Deactivate();
            Assert.True(running.Running);
            Assert.False(stopped.Running); // staff had it off: it stays off
            Assert.True(outside.Running);
        }
        finally
        {
            running?.Delete();
            stopped?.Delete();
            outside?.Delete();
            ClearEvent();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void OnPlacesOnceOffLeavesTheResearcherRemoveTakesExactlyWhatItPlaced()
    {
        ShardTestClock.Arm();
        ClearEvent();

        Static lookalike = null;

        try
        {
            var dry = SorcerersDungeonGenerate.Up(true);
            _out.WriteLine(string.Join("\n", dry));
            Assert.Null(SorcerersDungeonResearcher.Instance);
            Assert.Null(TOSDSpawner.Instance);
            Assert.False(SorcerersDungeonData.Instance.Enabled);

            var lines = SorcerersDungeonGenerate.Up(false);
            _out.WriteLine(string.Join("\n", lines));

            var researcher = SorcerersDungeonResearcher.Instance;
            Assert.NotNull(researcher);
            Assert.Equal(new Point3D(536, 456, -53), researcher.Location);
            Assert.Equal(Map.Ilshenar, researcher.Map);
            Assert.True(researcher.IsInvulnerable);

            var pieces = new (int Id, Point3D At)[]
            {
                (0x9D2B, new Point3D(546, 460, 6)), (0x9D2C, new Point3D(548, 460, 6)), (0x9D2D, new Point3D(548, 458, 6)),
                (0x9F34, new Point3D(545, 462, -53)), (0x9F34, new Point3D(550, 462, -53)),
                (0x9F28, new Point3D(545, 463, -55)), (0x9F24, new Point3D(550, 463, -55))
            };

            var statics = World.Items.Values.OfType<Static>().Where(s => !s.Deleted && s.Map == Map.Ilshenar).ToList();
            Assert.Equal(7, statics.Count);
            Assert.All(pieces, p => Assert.Single(statics, s => s.ItemID == p.Id && s.Location == p.At));
            Assert.All(statics, s => Assert.False(s.Movable));

            Assert.NotNull(TOSDSpawner.Instance);
            Assert.True(SorcerersDungeonData.Instance.Enabled);

            // On twice places once.
            var spawner = TOSDSpawner.Instance;
            SorcerersDungeonGenerate.Up(false);
            Assert.Same(researcher, SorcerersDungeonResearcher.Instance);
            Assert.Same(spawner, TOSDSpawner.Instance);
            Assert.Equal(7, World.Items.Values.OfType<Static>().Count(s => !s.Deleted && s.Map == Map.Ilshenar));
            Assert.Single(World.Mobiles.Values.OfType<SorcerersDungeonResearcher>(), r => !r.Deleted);

            // Spawn and a boss, then off: they go, the researcher and the pieces stay.
            for (var i = 0; i < 5; i++)
            {
                spawner.DoSpawn(typeof(Skeleton), false);
            }

            spawner.Index = 8;
            spawner.DoSpawn(typeof(JackThePumpkinKing), true);
            var jack = spawner.Boss;
            var spawned = spawner.Spawn.ToList();
            Assert.IsType<JackThePumpkinKing>(jack);

            // status reads the world: the boss and the five skeletons stand in the rooms.
            var before = SorcerersDungeonGenerate.Status();
            _out.WriteLine(string.Join("\n", before));
            Assert.Contains(before, l => l.Contains("In the world: 1 event boss(es) (jack at ") && l.EndsWith("6 creature(s) inside the nine rooms."));

            var offDry = SorcerersDungeonGenerate.Down(true);
            Assert.NotNull(TOSDSpawner.Instance);
            Assert.False(jack.Deleted);
            _out.WriteLine(string.Join("\n", offDry));

            var off = SorcerersDungeonGenerate.Down(false);
            _out.WriteLine(string.Join("\n", off));
            Assert.Null(TOSDSpawner.Instance);
            Assert.False(SorcerersDungeonData.Instance.Enabled);
            Assert.True(jack.Deleted);
            Assert.All(spawned, s => Assert.True(s.Deleted));
            Assert.False(researcher.Deleted);
            Assert.Equal(7, World.Items.Values.OfType<Static>().Count(s => !s.Deleted && s.Map == Map.Ilshenar));
            Assert.Contains(SorcerersDungeonGenerate.Status(), l => l.Contains("In the world: 0 event boss(es); 0 creature(s) inside the nine rooms."));

            // On again: nothing placed twice, a new spawner.
            SorcerersDungeonGenerate.Up(false);
            Assert.Same(researcher, SorcerersDungeonResearcher.Instance);
            Assert.NotNull(TOSDSpawner.Instance);
            Assert.Equal(7, World.Items.Values.OfType<Static>().Count(s => !s.Deleted && s.Map == Map.Ilshenar));

            // Remove takes exactly what was placed: a same-art piece on the same tile, not placed by it, stays.
            lookalike = new Static(0x9F34);
            lookalike.MoveToWorld(new Point3D(545, 462, -53), Map.Ilshenar);

            var removeDry = SorcerersDungeonGenerate.Remove(true);
            Assert.False(researcher.Deleted);
            _out.WriteLine(string.Join("\n", removeDry));

            var removed = SorcerersDungeonGenerate.Remove(false);
            _out.WriteLine(string.Join("\n", removed));
            Assert.True(researcher.Deleted);
            Assert.Null(SorcerersDungeonResearcher.Instance);
            Assert.Null(TOSDSpawner.Instance);
            Assert.False(lookalike.Deleted);
            Assert.Equal(new[] { lookalike }, World.Items.Values.OfType<Static>().Where(s => !s.Deleted && s.Map == Map.Ilshenar).ToArray());
            Assert.Empty(ClusterFSeasonalState.Placed("sorcerersdungeon"));

            // The command verbs reach the same code; status reports without changing anything.
            var status = SorcerersDungeonGenerate.Run(new[] { "status" });
            _out.WriteLine(string.Join("\n", status));
            Assert.Contains(status, l => l.Contains("Enabled: False"));
            Assert.Null(SorcerersDungeonResearcher.Instance);
        }
        finally
        {
            lookalike?.Delete();
            ClearEvent();
        }
    }

    [Fact]
    public void TheBossVerbRaisesTheRoomsBossNow()
    {
        ShardTestClock.Arm();
        ClearEvent();

        try
        {
            Assert.Contains("not running", SorcerersDungeonGenerate.Run(new[] { "boss", "9" })[0]);

            SorcerersDungeonGenerate.Up(false);
            var spawner = TOSDSpawner.Instance;
            Assert.Equal(0, spawner.Index);

            Assert.Contains("1 to 9", SorcerersDungeonGenerate.Run(new[] { "boss", "10" })[0]);

            var dry = SorcerersDungeonGenerate.Run(new[] { "boss", "9", "dryrun" });
            _out.WriteLine(string.Join("\n", dry));
            Assert.Null(spawner.Boss);
            Assert.Equal(0, spawner.Index);

            var lines = SorcerersDungeonGenerate.Run(new[] { "boss", "9" });
            _out.WriteLine(string.Join("\n", lines));
            var jack = Assert.IsType<JackThePumpkinKing>(spawner.Boss);
            Assert.Equal(8, spawner.Index);
            Assert.True(InRoom(new Rectangle2D(291, 73, 37, 36), jack.Location), jack.Location.ToString());
            Assert.Equal(Map.Ilshenar, jack.Map);

            // A second call does not raise a second boss.
            Assert.Contains("already up", SorcerersDungeonGenerate.Run(new[] { "boss" })[0]);
            Assert.Single(World.Mobiles.Values.OfType<JackThePumpkinKing>(), j => !j.Deleted);

            // The rotation carries on from Jack's room: back to the first.
            jack.Delete();
            spawner.OnTick();
            Assert.Equal(0, spawner.Index);

            EnsureRewards();
            var status = SorcerersDungeonGenerate.Status();
            _out.WriteLine(string.Join("\n", status));
            Assert.Contains(status, l => l.Contains("Reward list (7): JacksBagOfTricks 75, SerpentsJawbone 50"));
        }
        finally
        {
            ClearEvent();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ItRunsOnTheHalloweenDatesWithItsOwnState()
    {
        ShardTestClock.Arm();
        ClearEvent();

        var season = SorcerersDungeonGenerate.Season;
        var oldClock = season.Clock;

        try
        {
            Assert.Equal("clusterf.seasonal.halloween.start", season.StartKey);
            Assert.Equal("clusterf.seasonal.halloween.end", season.EndKey);
            Assert.Equal("clusterf.seasonal.halloween.automatic", season.AutomaticKey);
            Assert.Equal("sorcerersdungeon", season.Name);
            Assert.Equal("10-24 to 11-02", season.Window.ToString());

            // The decor's switch is untouched by this one's name and state.
            Assert.Equal("clusterf.seasonal.halloween.start", ClusterFHalloweenDecor.Season.StartKey);
            Assert.Equal("halloween", ClusterFHalloweenDecor.Season.Name);

            // A world first seen out of season is left as it is.
            Assert.Empty(season.Sync(new DateTime(2026, 10, 20, 12, 0, 0)));
            Assert.Null(TOSDSpawner.Instance);

            // The window opens: it comes up.
            var up = season.Sync(new DateTime(2026, 10, 24, 0, 30, 0));
            _out.WriteLine(string.Join("\n", up));
            Assert.NotNull(TOSDSpawner.Instance);
            Assert.NotNull(SorcerersDungeonResearcher.Instance);
            Assert.True(SorcerersDungeonData.Instance.Enabled);
            Assert.True(ClusterFSeasonalState.GetLast("sorcerersdungeon"));

            // Inside the window, a staff off stands.
            SorcerersDungeonGenerate.Down(false);
            Assert.Empty(season.Sync(new DateTime(2026, 10, 31, 12, 0, 0)));
            Assert.Null(TOSDSpawner.Instance);

            SorcerersDungeonGenerate.Up(false);

            // After November 2: down. The researcher stays.
            var down = season.Sync(new DateTime(2026, 11, 3, 0, 30, 0));
            _out.WriteLine(string.Join("\n", down));
            Assert.Null(TOSDSpawner.Instance);
            Assert.False(SorcerersDungeonData.Instance.Enabled);
            Assert.NotNull(SorcerersDungeonResearcher.Instance);
            Assert.False(ClusterFSeasonalState.GetLast("sorcerersdungeon"));

            season.Clock = () => new DateTime(2026, 10, 25, 12, 0, 0);
            Assert.True(SorcerersDungeonData.Instance.InSeason);
            season.Clock = () => new DateTime(2026, 11, 3, 12, 0, 0);
            Assert.False(SorcerersDungeonData.Instance.InSeason);
        }
        finally
        {
            season.Clock = oldClock;
            ClearEvent();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void AKillInTheDungeonTalliesAndRollsAnArtifactOfEnchantedOrigin()
    {
        ShardTestClock.Arm();
        ClearEvent();
        var region = EnsureRegion();

        PlayerMobile pm = null;
        Lich lich = null, lich2 = null, outside = null;

        try
        {
            pm = NewPlayer(new Point3D(330, 30, 0), Map.Ilshenar);
            Assert.Same(region, pm.Region);

            // Not enabled: nothing.
            lich = new Lich();
            lich.MoveToWorld(new Point3D(331, 30, 0), Map.Ilshenar);
            Assert.Null(SorcerersDungeonData.Instance.ProcessKill(lich, pm, true));
            Assert.False(SorcerersDungeonData.Instance.DungeonPoints.ContainsKey(pm));

            SorcerersDungeonData.Instance.Enabled = true;
            var expected = lich.Fame / 4;
            Assert.True(expected > 0);

            // The death event: a spawned creature's death reaches the spawner's kill count, and the kill is tallied.
            var spawner = new TOSDSpawner(activate: false);
            spawner.AddSpawn(lich);

            KillBy(lich, pm);

            Assert.Equal(1, spawner.KillCount);
            Assert.True(SorcerersDungeonData.Instance.DungeonPoints.TryGetValue(pm, out var tally), "the kill was tallied");
            Assert.Equal(expected, tally);

            Assert.InRange(SorcerersDungeonData.ChanceAt(0), 0.00086, 0.00087);
            Assert.InRange(SorcerersDungeonData.ChanceAt(250_000), 0.0099, 0.0101);

            lich2 = new Lich();
            lich2.MoveToWorld(new Point3D(331, 31, 0), Map.Ilshenar);
            var artifact = SorcerersDungeonData.Instance.ProcessKill(lich2, pm, true);
            Assert.NotNull(artifact);
            Assert.Equal(1171, artifact.Hue);
            Assert.True(EnchantedOriginArtifacts.IsEnchantedOrigin(artifact));
            Assert.True(artifact.IsChildOf(pm.Backpack));
            Assert.True(artifact is BaseWeapon or BaseArmor or BaseJewel or BaseHat, artifact.GetType().Name);
            Assert.False(SorcerersDungeonData.Instance.DungeonPoints.ContainsKey(pm));

            // Outside the dungeon: nothing.
            outside = new Lich();
            outside.MoveToWorld(new Point3D(1000, 1000, 0), Map.Ilshenar);
            Assert.Null(SorcerersDungeonData.Instance.ProcessKill(outside, pm, true));

            spawner.Deactivate();
        }
        finally
        {
            pm?.Delete();
            lich?.Delete();
            lich2?.Delete();
            outside?.Delete();
            ClearEvent();
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheResearcherTakesArtifactsForAPointAndSellsServUOsRewards()
    {
        ShardTestClock.Arm();
        EnsureRewards();
        ClearEvent();

        PlayerMobile pm = null;
        SorcerersDungeonResearcher researcher = null;

        try
        {
            researcher = new SorcerersDungeonResearcher();
            researcher.MoveToWorld(new Point3D(536, 456, -53), Map.Ilshenar);
            pm = NewPlayer(new Point3D(537, 456, -53), Map.Ilshenar);

            Assert.Equal("the Researcher", researcher.Title);
            Assert.Equal(1157657, researcher.TitleLocalization);
            Assert.Equal(1158763, researcher.TurnInLocalization);
            Assert.NotNull(researcher.Backpack.FindItemByType<SterlingSilverRing>());
            Assert.NotNull(researcher.Backpack.FindItemByType<TalonsOfEscaping>());
            Assert.NotNull(researcher.Backpack.FindItemByType<BootsOfEscaping>());
            Assert.Contains(researcher.Items, i => i is JinBaori { Hue: 2722 });

            var artifact = EnchantedOriginArtifacts.Create(pm, 700, 0, false, false);
            Assert.NotNull(artifact);
            pm.Backpack.DropItem(artifact);
            var katana = new Katana();
            pm.Backpack.DropItem(katana);

            Assert.True(researcher.IsRedeemableItem(artifact));
            Assert.False(researcher.IsRedeemableItem(katana));
            Assert.Single(researcher.FindRedeemableItems(pm));

            researcher.AwardPoints(pm, artifact, 1);
            Assert.Equal(1.0, SorcerersDungeonData.Instance.GetPoints(pm));
            Assert.False(EnchantedOriginArtifacts.IsEnchantedOrigin(artifact));

            var rewards = SorcerersDungeonRewards.Rewards.Select(r => (r.Type, r.ItemID, r.Tooltip, r.Points)).ToList();
            Assert.Equal(
                new List<(Type, int, int, double)>
                {
                    (typeof(JacksBagOfTricks), 0x9F85, 1157656, 75),
                    (typeof(SerpentsJawbone), 0x9F74, 1157654, 50),
                    (typeof(Server.Items.JackInTheBox), 0x9F64, 1157655, 20),
                    (typeof(ShackledHeartOfThePumpkinKing), 0x4A9C, 1157653, 20),
                    (typeof(SterlingSilverRing), 0x1F09, 1155606, 50),
                    (typeof(TalonsOfEscaping), 0x41D8, 1155682, 50),
                    (typeof(BootsOfEscaping), 0x1711, 1155607, 50)
                },
                rewards
            );

            // Every reward constructs as the reward gump makes it.
            foreach (var r in SorcerersDungeonRewards.Rewards)
            {
                var item = (Item)Activator.CreateInstance(r.Type)!;
                Assert.Equal(r.ItemID, item.ItemID);
                item.Delete();
            }

            katana.Delete();
            artifact.Delete();
        }
        finally
        {
            pm?.Delete();
            researcher?.Delete();
            ClearEvent();
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheRewardsBehaveAsInServUO()
    {
        ShardTestClock.Arm();
        ClearEvent();

        PlayerMobile pm = null;

        try
        {
            pm = NewPlayer(new Point3D(536, 460, 0), Map.Ilshenar);

            // The bag: a player outside October is refused; staff always; once a day.
            var bag = new JacksBagOfTricks();
            pm.Backpack.DropItem(bag);
            var month = Core.Now.ToLocalTime().Month;
            var before = pm.Backpack.Items.Count;

            if (month != 10)
            {
                bag.OnDoubleClick(pm);
                Assert.Equal(before, pm.Backpack.Items.Count);
                Assert.True(bag.NextUse <= Core.Now);
            }

            pm.AccessLevel = AccessLevel.GameMaster;
            bag.OnDoubleClick(pm);
            Assert.Equal(before + 1, pm.Backpack.Items.Count);
            Assert.True(bag.NextUse > Core.Now + TimeSpan.FromHours(23));
            bag.OnDoubleClick(pm);
            Assert.Equal(before + 1, pm.Backpack.Items.Count); // used today
            pm.AccessLevel = AccessLevel.Player;

            // The oddities.
            Assert.Equal(2707, new RancidReindeerMeat().Hue);
            Assert.Equal(0x9DC3, new GlassyCandyCane().ItemID);
            Assert.Equal(1976, new SuspiciousGiftBox().Hue);
            Assert.Equal(1933, new InsultingDoll().Hue);
            Assert.Equal(0x142A, new SpikedEggnog().ItemID);
            var ears = new NamedSeveredElfEars();
            Assert.Contains(ears.ElfName, new[] { "Alabaster Snowball", "Pepper Minstix", "Wunorse Openslae", "Sugarplum Mary" });

            // The ring: unset, double-click offers the six skills; a set bonus is +20 at index 0.
            var ring = new SterlingSilverRing();
            Assert.Equal(3, ring.Attributes.RegenHits);
            Assert.Equal(5, ring.Attributes.RegenMana);
            Assert.Equal(75, ring.Attributes.WeaponDamage);
            Assert.Equal(255, ring.MaxHitPoints);
            Assert.False(ring.HasSkillBonus);
            Assert.Equal(new[] { SkillName.Archery, SkillName.Fencing, SkillName.Macing, SkillName.Swords, SkillName.Throwing, SkillName.Wrestling }, SterlingSilverRing.Skills);
            ring.SkillBonuses.SetValues(0, SkillName.Swords, 20);
            Assert.True(ring.HasSkillBonus);
            Assert.Equal(20.0, ring.SkillBonuses.Skill_1_Value);

            var boots = new BootsOfEscaping();
            Assert.Equal(4, boots.Attributes.BonusDex);
            Assert.Equal(1, boots.Attributes.RegenStam);
            var talons = new TalonsOfEscaping();
            Assert.Equal(4, talons.Attributes.BonusDex);
            Assert.Equal(0x41D8, talons.ItemID);

            // The toy and the heart.
            var toy = new Server.Items.JackInTheBox();
            pm.Backpack.DropItem(toy);
            toy.OnDoubleClick(pm);
            Assert.Equal(0x9F64, toy.ItemID);

            var heart = new ShackledHeartOfThePumpkinKing();
            pm.Backpack.DropItem(heart);
            heart.OnDoubleClick(pm);
            Assert.Equal(0x4A9C, heart.ItemID);

            foreach (var i in new Item[] { ring, boots, talons, ears })
            {
                i.Delete();
            }
        }
        finally
        {
            pm?.Delete();
            ClearEvent();
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void EverySavedTypeRoundTrips()
    {
        ShardTestClock.Arm();
        ClearEvent();

        var made = new List<IEntity>();

        try
        {
            foreach (var t in new[]
            {
                typeof(NightmareFairy), typeof(JackInTheBoxMonster), typeof(HeadlessElf), typeof(AbominableSnowman),
                typeof(TwistedHolidayTree), typeof(RabidReindeer), typeof(GarishGingerman), typeof(StockingSerpent),
                typeof(JackThePumpkinKing), typeof(SorcerersDungeonResearcher)
            })
            {
                var m = (Mobile)Activator.CreateInstance(t)!;
                made.Add(m);
                var copy = RoundTrip(m);
                Assert.Equal(m.Name, copy.Name);
                Assert.Equal(m.Body, copy.Body);
                Assert.Equal(m.HitsMax, copy.HitsMax);
            }

            var bag = new JacksBagOfTricks { NextUse = new DateTime(2026, 10, 30, 1, 2, 3, DateTimeKind.Utc) };
            made.Add(bag);
            Assert.Equal(bag.NextUse, RoundTrip(bag).NextUse);

            var ears = new NamedSeveredElfEars();
            made.Add(ears);
            Assert.Equal(ears.ElfName, RoundTrip(ears).ElfName);

            var ring = new SterlingSilverRing();
            ring.SkillBonuses.SetValues(0, SkillName.Fencing, 20);
            made.Add(ring);
            var ringCopy = RoundTrip(ring);
            Assert.True(ringCopy.HasSkillBonus);
            Assert.Equal(75, ringCopy.Attributes.WeaponDamage);

            foreach (var item in new Item[]
            {
                new Server.Items.JackInTheBox(), new ShackledHeartOfThePumpkinKing(), new RancidReindeerMeat(),
                new GlassyCandyCane(), new SuspiciousGiftBox(), new InsultingDoll(), new SpikedEggnog(),
                new BootsOfEscaping(), new TalonsOfEscaping()
            })
            {
                made.Add(item);
                var copy = RoundTrip(item);
                Assert.Equal(item.ItemID, copy.ItemID);
                Assert.Equal(item.Hue, copy.Hue);
            }

            // The pumpkin restarts its hatch timer on load; the copy is deleted (after the original, whose serial it
            // shares) so that timer finds it deleted and hatches nothing.
            var pumpkin = new UnholyPumpkin { ItemID = 0xC6B };
            var pumpkinCopy = RoundTrip(pumpkin);
            Assert.Equal(0xC6B, pumpkinCopy.ItemID);
            Assert.False(pumpkinCopy.Movable);
            pumpkin.Delete();
            pumpkinCopy.Delete();

            // The points system, with the spawner inside it, its paused spawners, the tallies and the points.
            var pm = NewPlayer(new Point3D(536, 460, 0), Map.Ilshenar);
            made.Add(pm);
            var stock = new Spawner("Zombie");
            stock.MoveToWorld(new Point3D(330, 30, 0), Map.Ilshenar);
            stock.Start();
            made.Add(stock);

            var spawner = new TOSDSpawner();
            Assert.Single(spawner.PausedSpawners);
            spawner.Index = 3;
            spawner.KillCount = 42;
            spawner.DoSpawn(typeof(Zombie), false);
            spawner.DoSpawn(typeof(AbominableSnowman), true);
            var zombie = spawner.Spawn.Single();
            var boss = spawner.Boss;

            SorcerersDungeonData.Instance.Enabled = true;
            SorcerersDungeonData.Instance.DungeonPoints[pm] = 4321;
            SorcerersDungeonData.Instance.SetPoints(pm, 7);

            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            PointsSystemPersistence.WriteAll(writer);
            writer.Flush();

            TOSDSpawner.Instance = null;
            SorcerersDungeonData.Instance.Enabled = false;
            SorcerersDungeonData.Instance.DungeonPoints.Clear();
            SorcerersDungeonData.Instance.Clear();

            PointsSystemPersistence.ReadAll(new BufferReader(buffer));

            var loaded = TOSDSpawner.Instance;
            Assert.NotNull(loaded);
            Assert.NotSame(spawner, loaded);
            Assert.True(SorcerersDungeonData.Instance.Enabled);
            Assert.Equal(3, loaded.Index);
            Assert.Equal(42, loaded.KillCount);
            Assert.Same(boss, loaded.Boss);
            Assert.Equal(new[] { zombie }, loaded.Spawn.ToArray());
            Assert.Equal(new BaseSpawner[] { stock }, loaded.PausedSpawners.ToArray());
            Assert.Equal(4321, SorcerersDungeonData.Instance.DungeonPoints[pm]);
            Assert.Equal(7.0, SorcerersDungeonData.Instance.GetPoints(pm));

            // A world saved with no spawner reads back with none.
            loaded.Deactivate();
            Assert.True(stock.Running);
            writer = new BufferWriter(buffer, true);
            PointsSystemPersistence.WriteAll(writer);
            writer.Flush();
            PointsSystemPersistence.ReadAll(new BufferReader(buffer));
            Assert.Null(TOSDSpawner.Instance);

            // The artifact registry.
            var artifact = EnchantedOriginArtifacts.Create(pm, 700, 0, false, false);
            made.Add(artifact);
            var regBuffer = new byte[4096];
            var regWriter = new BufferWriter(regBuffer, true);
            EnchantedOriginArtifacts.Serialize(regWriter);
            regWriter.Flush();
            EnchantedOriginArtifacts.Clear();
            Assert.False(EnchantedOriginArtifacts.IsEnchantedOrigin(artifact));
            EnchantedOriginArtifacts.Deserialize(new BufferReader(regBuffer));
            Assert.True(EnchantedOriginArtifacts.IsEnchantedOrigin(artifact));
        }
        finally
        {
            foreach (var e in made)
            {
                e?.Delete();
            }

            ClearEvent();
        }
    }
}
