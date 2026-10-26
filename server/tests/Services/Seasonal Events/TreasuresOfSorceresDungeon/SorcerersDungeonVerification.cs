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
//   9. cc-P624 (stage 1 of notes/cc-P564-design-pumpkin-king.md): the event owns what it put in the world, apart from
//      room progress, and off deletes exactly that (earlier rooms' survivors, Jack's pumpkins and their undead), with
//      nothing coming back; Jack's delayed throw lands only in the live encounter; kill credit is judged as the
//      creature was when it died (a tamed pet pays nothing, a death inside another's keeps both); the drop roll is
//      pinned through the real death hook; the spawner's version 1 save keeps the ownership and reads version 0.
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

            // A failed roll, pinned: the tally is the kill's (cc-P624; this read a live 0.09% roll before).
            using (SorcerersDungeonData.ForceDropRoll(0.999999))
            {
                KillBy(lich, pm);
            }

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

    // ---------------------------------------------------------------- 9 (cc-P624)

    // Up as the season brings it, with the spawner's own timer stopped so the fact drives every step itself.
    private static TOSDSpawner StartQuietly()
    {
        EnsureRegion();
        SorcerersDungeonGenerate.Up(false);
        var spawner = TOSDSpawner.Instance;
        Assert.NotNull(spawner);
        spawner.EndTimer();
        return spawner;
    }

    private static List<UnholyPumpkin> PumpkinsNear(Mobile m, int range = 2) =>
        World.Items.Values.OfType<UnholyPumpkin>()
            .Where(p => !p.Deleted && p.Map == m.Map && Utility.InRange(p.GetWorldLocation(), m.Location, range))
            .ToList();

    // A throw places 1 to 3 pumpkins and skips a spot that cannot take one (a mobile stands there), so a single throw
    // can place none. Throws until some land (or 30 throws), a second apart.
    private static List<UnholyPumpkin> ThrowUntilLanded(JackThePumpkinKing jack, Mobile at)
    {
        for (var i = 0; i < 30; i++)
        {
            jack.SchedulePumpkins(at);
            ShardTestClock.AdvanceSeconds(1.5);

            var landed = PumpkinsNear(at);

            if (landed.Count > 0)
            {
                return landed;
            }
        }

        return new List<UnholyPumpkin>();
    }

    // Five throws: where none may land, none of five does.
    private static void ThrowFive(JackThePumpkinKing jack, Mobile at)
    {
        for (var i = 0; i < 5; i++)
        {
            jack.SchedulePumpkins(at);
        }

        ShardTestClock.AdvanceSeconds(1.5);
    }

    private static int CreaturesInRooms() =>
        World.Mobiles.Values.Count(m =>
            !m.Deleted && m is BaseCreature { Controlled: false } && m.Map == Map.Ilshenar &&
            SorcerersDungeonGenerate.RoomsContain(m.Location));

    private static int TallyOf(Mobile m) =>
        SorcerersDungeonData.Instance.DungeonPoints.TryGetValue(m, out var tally) ? tally : 0;

    // A creature whose deletion kills another: that second death runs whole inside the first's, between its deletion
    // and its death event, as a death handler or an on-delete effect can make happen.
    public class DiesWithAnother : Lich
    {
        public Mobile Other { get; set; }

        public override void OnDelete()
        {
            base.OnDelete();

            var other = Other;
            Other = null;
            other?.Kill();
        }
    }

    [Fact]
    public void OffDeletesEverythingTheEventOwnsAndNothingElse()
    {
        ShardTestClock.Arm();
        ClearEvent();

        PlayerMobile pm = null;
        Zombie unrelated = null;
        Item lookalike = null;

        try
        {
            var spawner = StartQuietly();
            pm = NewPlayer(new Point3D(330, 30, 0), Map.Ilshenar);
            pm.Blessed = true;

            // Not the event's: a zombie staff put in the first room, and a pumpkin-art item.
            unrelated = new Zombie();
            unrelated.MoveToWorld(new Point3D(331, 31, 0), Map.Ilshenar);
            lookalike = new Item(0xC6A) { Name = "unholy pumpkin", Movable = false };
            lookalike.MoveToWorld(new Point3D(332, 32, 0), Map.Ilshenar);

            for (var i = 0; i < 3; i++)
            {
                spawner.DoSpawn(typeof(Zombie), false);
            }

            var survivors = spawner.Spawn.ToList();
            Assert.Equal(3, survivors.Count);

            // The room's boss, killed: the rotation moves on and the three left standing are no longer room progress.
            spawner.KillCount = 80;
            spawner.OnTick();
            var fairy = Assert.IsType<NightmareFairy>(spawner.Boss);
            KillBy(fairy, pm);
            spawner.OnTick();
            Assert.Equal(1, spawner.Index);
            Assert.Empty(spawner.Spawn);
            Assert.All(survivors, s => Assert.False(s.Deleted));

            // An earlier room's survivor dying does not count for the new room.
            KillBy(survivors[0], pm);
            Assert.Equal(0, spawner.KillCount);

            for (var i = 0; i < 2; i++)
            {
                spawner.DoSpawn(typeof(Skeleton), false);
            }

            var current = spawner.Spawn.ToList();
            var expected = survivors.Skip(1).Concat(current).Select(m => m.Serial).OrderBy(s => s).ToList();
            Assert.Equal(expected, spawner.Owned.Where(m => !m.Deleted).Select(m => m.Serial).OrderBy(s => s).ToList());

            var status = SorcerersDungeonGenerate.Status();
            _out.WriteLine(string.Join("\n", status));

            // A spawn pending on the wheel when the event goes down.
            spawner.BeginTimer();
            ShardTestClock.AdvanceSeconds(1.1);
            Assert.True(spawner.Spawning);

            var lines = SorcerersDungeonGenerate.Down(false);
            _out.WriteLine(string.Join("\n", lines));

            Assert.All(survivors.Skip(1), s => Assert.True(s.Deleted, $"an earlier room's {s.GetType().Name} outlived off"));
            Assert.All(current, s => Assert.True(s.Deleted, $"the current room's {s.GetType().Name} outlived off"));
            Assert.False(unrelated.Deleted, "off deleted a zombie the event did not own");
            Assert.False(lookalike.Deleted, "off deleted a pumpkin-art item the event did not own");

            // Every pending delay run out: nothing of the event's comes back.
            var after = CreaturesInRooms();
            ShardTestClock.Advance(TimeSpan.FromMinutes(5));
            Assert.Equal(after, CreaturesInRooms());
            Assert.Null(TOSDSpawner.Instance);
        }
        finally
        {
            pm?.Delete();
            unrelated?.Delete();
            lookalike?.Delete();
            ClearEvent();
        }
    }

    [Fact]
    public void JacksPumpkinsLandOnlyInTheLiveEncounterAndGoWithIt()
    {
        ShardTestClock.Arm();
        ClearEvent();

        PlayerMobile pm = null;
        JackThePumpkinKing handJack = null;

        try
        {
            var spawner = StartQuietly();
            pm = NewPlayer(new Point3D(300, 80, 0), Map.Ilshenar); // Jack's room
            pm.Blessed = true; // nothing harms the attacker while the clock runs

            SorcerersDungeonGenerate.RaiseBoss(9, false);
            var jack = Assert.IsType<JackThePumpkinKing>(spawner.Boss);
            jack.Location = new Point3D(301, 80, 0);

            // Inside the dungeon: pumpkins around the attacker a second later, all the event's.
            var thrown = ThrowUntilLanded(jack, pm);
            Assert.NotEmpty(thrown);
            Assert.Equal(
                thrown.Select(p => p.Serial).OrderBy(s => s).ToList(),
                spawner.Pumpkins.Where(p => !p.Deleted).Select(p => p.Serial).OrderBy(s => s).ToList()
            );

            // They hatch the event's undead, which are not room progress.
            var before = World.Mobiles.Values.ToHashSet();
            var kills = spawner.KillCount;
            ShardTestClock.AdvanceSeconds(11);
            Assert.All(thrown, p => Assert.True(p.Deleted, "a pumpkin did not hatch"));
            var hatched = World.Mobiles.Values.OfType<BaseCreature>().Where(m => !m.Deleted && !before.Contains(m)).ToList();
            Assert.Equal(thrown.Count, hatched.Count);
            Assert.All(hatched, h => Assert.Contains(h, spawner.Owned));
            Assert.All(hatched, h => Assert.DoesNotContain(h, spawner.Spawn));
            KillBy(hatched[0], pm);
            Assert.Equal(kills, spawner.KillCount);

            // Outside the dungeon on the same facet: none land.
            pm.MoveToWorld(new Point3D(1000, 1000, 0), Map.Ilshenar);
            ThrowFive(jack, pm);
            Assert.Empty(PumpkinsNear(pm));

            // Back inside: a throw lands, five more are pending when the event goes down.
            pm.MoveToWorld(new Point3D(300, 80, 0), Map.Ilshenar);
            var landed = ThrowUntilLanded(jack, pm);
            Assert.NotEmpty(landed);

            for (var i = 0; i < 5; i++)
            {
                jack.SchedulePumpkins(pm);
            }

            SorcerersDungeonGenerate.Down(false);
            Assert.True(jack.Deleted);
            Assert.All(landed, p => Assert.True(p.Deleted, "an event pumpkin outlived off"));
            Assert.All(hatched.Skip(1), h => Assert.True(h.Deleted, $"a hatched {h.GetType().Name} outlived off"));

            var afterOff = World.Mobiles.Values.Count(m => !m.Deleted);
            ShardTestClock.AdvanceSeconds(15);
            Assert.Empty(PumpkinsNear(pm));
            Assert.Equal(afterOff, World.Mobiles.Values.Count(m => !m.Deleted));

            // A Jack staff added by hand belongs to no event and throws as in ServUO, wherever he is.
            handJack = new JackThePumpkinKing();
            handJack.MoveToWorld(new Point3D(1001, 1000, 0), Map.Ilshenar);
            pm.MoveToWorld(new Point3D(1000, 1000, 0), Map.Ilshenar);
            Assert.NotEmpty(ThrowUntilLanded(handJack, pm));
        }
        finally
        {
            pm?.Delete();
            handJack?.Delete();
            ClearEvent();
        }
    }

    [Fact]
    public void KillCreditIsJudgedAsTheCreatureWasWhenItDied()
    {
        ShardTestClock.Arm();
        ClearEvent();
        EnsureRegion();

        var made = new List<Mobile>();

        T Place<T>(T m, int x, int y) where T : Mobile
        {
            made.Add(m);
            m.MoveToWorld(new Point3D(x, y, 0), Map.Ilshenar);
            return m;
        }

        try
        {
            SorcerersDungeonData.Instance.Enabled = true;
            using var roll = SorcerersDungeonData.ForceDropRoll(0.999999); // no artifacts: the tallies show the credit

            var pm = Place(NewPlayer(Point3D.Zero, Map.Ilshenar), 330, 30);
            var other = Place(NewPlayer(Point3D.Zero, Map.Ilshenar), 340, 40);
            var owner = Place(NewPlayer(Point3D.Zero, Map.Ilshenar), 332, 30);

            // A wild creature in the dungeon: credited.
            var wild = Place(new Lich(), 331, 30);
            var fame = wild.Fame / 4;
            KillBy(wild, pm);
            Assert.Equal(fame, TallyOf(pm));
            SorcerersDungeonData.Instance.DungeonPoints.Clear();

            // Someone's tamed pet, not bonded: nothing, though deleting it clears its master before the death hook.
            var pet = Place(new Lich(), 331, 31);
            Assert.True(pet.SetControlMaster(owner));
            Assert.True(pet.Controlled);
            KillBy(pet, pm);
            Assert.True(pet.Deleted);
            Assert.Equal(0, TallyOf(pm));

            // A summoned creature: nothing.
            var summon = Place(new Lich(), 331, 32);
            summon.Summoned = true;
            summon.SummonMaster = owner;
            KillBy(summon, pm);
            Assert.Equal(0, TallyOf(pm));

            // A bonded pet: it does not die away, and nothing.
            var bonded = Place(new Lich(), 331, 33);
            Assert.True(bonded.SetControlMaster(owner));
            bonded.IsBonded = true;
            KillBy(bonded, pm);
            Assert.False(bonded.Deleted);
            Assert.Equal(0, TallyOf(pm));

            // Outside the dungeon: nothing.
            var outside = Place(new Lich(), 1000, 1000);
            KillBy(outside, pm);
            Assert.Equal(0, TallyOf(pm));

            // A dead player: nothing.
            var late = Place(new Lich(), 331, 34);
            late.Damage(late.Hits - 1, other);
            other.Body = 0x190; // a body, so the corpse has one
            other.Kill();
            Assert.False(other.Alive);
            late.Kill();
            Assert.Equal(0, TallyOf(other));

            // Two players with looting rights to one room creature: each credited, the room counts it once.
            var spawner = new TOSDSpawner(activate: false);
            var shared = Place(new Lich(), 331, 35);
            spawner.AddSpawn(shared);
            var partner = Place(NewPlayer(Point3D.Zero, Map.Ilshenar), 333, 35);
            shared.Damage(shared.Hits / 2, pm);
            shared.Damage(shared.Hits - 1, partner);
            shared.Kill();
            Assert.Equal(1, spawner.KillCount);
            Assert.Equal(fame, TallyOf(pm));
            Assert.Equal(fame, TallyOf(partner));
            SorcerersDungeonData.Instance.DungeonPoints.Clear();
            spawner.Deactivate();

            // A death inside another's deletion: both are credited, neither takes the other's snapshot.
            var inner = Place(new Lich(), 331, 36);
            var outer = Place(new DiesWithAnother { Other = inner }, 331, 37);
            inner.Damage(inner.Hits - 1, pm);
            outer.Damage(outer.Hits - 1, pm);
            SorcerersDungeonData.ForgetPendingDeaths(); // other facts' 0-hit creatures may have left some
            outer.Kill();
            Assert.True(inner.Deleted);
            Assert.True(outer.Deleted);
            Assert.Equal(inner.Fame / 4 + outer.Fame / 4, TallyOf(pm));

            // Every death read its own snapshot, and a staff delete of a living creature takes none.
            Assert.Equal(0, SorcerersDungeonData.PendingDeaths);
            var deleted = Place(new Lich(), 331, 38);
            deleted.Delete();
            Assert.Equal(0, SorcerersDungeonData.PendingDeaths);
        }
        finally
        {
            foreach (var m in made)
            {
                m?.Delete();
            }

            ClearEvent();
        }
    }

    [Fact]
    public void TheDropRollIsPinnedThroughTheRealDeathHook()
    {
        ShardTestClock.Arm();
        ClearEvent();
        EnsureRegion();

        PlayerMobile pm = null;
        var liches = new List<Lich>();

        Lich NewLich()
        {
            var lich = new Lich();
            lich.MoveToWorld(new Point3D(331, 30 + liches.Count, 0), Map.Ilshenar);
            liches.Add(lich);
            return lich;
        }

        try
        {
            SorcerersDungeonData.Instance.Enabled = true;
            pm = NewPlayer(new Point3D(330, 30, 0), Map.Ilshenar);

            // A failed roll keeps the exact tally and gives nothing.
            var first = NewLich();
            var fame = first.Fame / 4;

            using (SorcerersDungeonData.ForceDropRoll(0.999999))
            {
                KillBy(first, pm);
            }

            Assert.Equal(fame, TallyOf(pm));
            Assert.DoesNotContain(pm.Backpack.Items, EnchantedOriginArtifacts.IsEnchantedOrigin);

            // A good roll gives an artifact into the pack and empties the tally.
            using (SorcerersDungeonData.ForceDropRoll(0.0))
            {
                KillBy(NewLich(), pm);

                var inPack = pm.Backpack.Items.Where(EnchantedOriginArtifacts.IsEnchantedOrigin).ToList();
                Assert.Single(inPack);
                Assert.False(SorcerersDungeonData.Instance.DungeonPoints.ContainsKey(pm));

                // No pack: the bank. Bank full too: at the player's feet.
                pm.Backpack.Delete();
                KillBy(NewLich(), pm);
                Assert.Single(pm.BankBox.Items, EnchantedOriginArtifacts.IsEnchantedOrigin);

                pm.BankBox.MaxItems = pm.BankBox.TotalItems;
                KillBy(NewLich(), pm);
                var onGround = World.Items.Values
                    .Where(i => !i.Deleted && i.Parent == null && i.Map == pm.Map && i.Location == pm.Location && EnchantedOriginArtifacts.IsEnchantedOrigin(i))
                    .ToList();
                Assert.Single(onGround);
                onGround[0].Delete();
            }

            // The scope restored the live roll.
            Assert.Equal((Func<double>)Utility.RandomDouble, SorcerersDungeonData.DropRoll);
        }
        finally
        {
            pm?.Delete();

            foreach (var lich in liches)
            {
                lich.Delete();
            }

            ClearEvent();
        }
    }

    [Fact]
    public void TheSpawnersSaveKeepsWhatTheEventOwnsAndReadsVersionZero()
    {
        ShardTestClock.Arm();
        ClearEvent();

        PlayerMobile pm = null;
        Spawner stock = null;
        UnholyPumpkin pumpkin = null;

        try
        {
            // Version 0, written as P408 wrote it: the boss, kills, room, the spawn list, the paused spawners.
            var v0Zombie = new Zombie();
            v0Zombie.MoveToWorld(new Point3D(330, 30, 0), Map.Ilshenar);
            var v0Boss = new NightmareFairy();
            v0Boss.MoveToWorld(new Point3D(331, 30, 0), Map.Ilshenar);

            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            writer.WriteEncodedInt(0);
            writer.Write(v0Boss);
            writer.Write(17);
            writer.Write(0);
            writer.Write(1);
            writer.Write(v0Zombie);
            writer.WriteEncodedInt(0);
            writer.Flush();

            var fromV0 = new TOSDSpawner(activate: false);
            fromV0.Deserialize(new BufferReader(buffer));
            Assert.Equal(17, fromV0.KillCount);
            Assert.Same(v0Boss, fromV0.Boss);
            Assert.Equal(new BaseCreature[] { v0Zombie }, fromV0.Spawn.ToArray());
            Assert.Equal(
                new[] { v0Zombie.Serial, v0Boss.Serial }.OrderBy(s => s).ToList(),
                fromV0.Owned.Select(m => m.Serial).OrderBy(s => s).ToList()
            );
            fromV0.Deactivate();
            Assert.True(v0Zombie.Deleted);
            Assert.True(v0Boss.Deleted);

            // Version 1, through the points system as the world saves it: an earlier room's survivors, the current
            // room, its boss, an outstanding pumpkin, a paused stock spawner, a tally and points.
            stock = new Spawner("Zombie");
            stock.MoveToWorld(new Point3D(330, 30, 0), Map.Ilshenar);
            stock.Start();

            var spawner = StartQuietly();
            Assert.Single(spawner.PausedSpawners);
            pm = NewPlayer(new Point3D(330, 30, 0), Map.Ilshenar);
            pm.Blessed = true;

            spawner.DoSpawn(typeof(Zombie), false);
            spawner.DoSpawn(typeof(Zombie), false);
            var survivors = spawner.Spawn.ToList();
            spawner.KillCount = 80;
            spawner.OnTick();
            KillBy(spawner.Boss, pm);
            spawner.OnTick();
            Assert.Equal(1, spawner.Index);

            spawner.DoSpawn(typeof(Skeleton), false);
            var current = spawner.Spawn.Single();
            spawner.KillCount = 5;
            SorcerersDungeonGenerate.RaiseBoss(0, false);
            var boss = Assert.IsType<JackInTheBoxMonster>(spawner.Boss);

            pumpkin = new UnholyPumpkin();
            pumpkin.MoveToWorld(new Point3D(300, 20, 0), Map.Ilshenar);
            spawner.Pumpkins.Add(pumpkin);

            SorcerersDungeonData.Instance.DungeonPoints[pm] = 999;
            SorcerersDungeonData.Instance.SetPoints(pm, 3);

            var owned = survivors.Append(current).Append(boss).Select(m => m.Serial).OrderBy(s => s).ToList();
            Assert.Equal(owned, spawner.Owned.Select(m => m.Serial).OrderBy(s => s).ToList());

            writer = new BufferWriter(buffer, true);
            PointsSystemPersistence.WriteAll(writer);
            writer.Flush();

            // Loaded twice, as a second load of the same save would: the same entities, once each.
            for (var load = 0; load < 2; load++)
            {
                TOSDSpawner.Instance = null;
                SorcerersDungeonData.Instance.DungeonPoints.Clear();
                SorcerersDungeonData.Instance.Clear();

                PointsSystemPersistence.ReadAll(new BufferReader(buffer));

                var loaded = TOSDSpawner.Instance;
                Assert.NotNull(loaded);
                Assert.NotSame(spawner, loaded);
                Assert.Equal(1, loaded.Index);
                Assert.Equal(5, loaded.KillCount);
                Assert.Same(boss, loaded.Boss);
                Assert.Equal(new[] { current }, loaded.Spawn.ToArray());
                Assert.Equal(owned, loaded.Owned.Select(m => m.Serial).OrderBy(s => s).ToList());
                Assert.Equal(new[] { pumpkin }, loaded.Pumpkins.ToArray());
                Assert.Equal(new BaseSpawner[] { stock }, loaded.PausedSpawners.ToArray());
                Assert.Null(loaded.Timer);
                Assert.Equal(999, SorcerersDungeonData.Instance.DungeonPoints[pm]);
                Assert.Equal(3.0, SorcerersDungeonData.Instance.GetPoints(pm));
            }

            // Remove after the load: nothing the event owned is left, the stock spawner runs again.
            var lines = SorcerersDungeonGenerate.Remove(false);
            _out.WriteLine(string.Join("\n", lines));
            Assert.All(survivors, s => Assert.True(s.Deleted, "an earlier room's survivor outlived remove after a load"));
            Assert.True(current.Deleted);
            Assert.True(boss.Deleted);
            Assert.True(pumpkin.Deleted, "the event's pumpkin outlived remove after a load");
            Assert.True(stock.Running);
            Assert.True(SorcerersDungeonResearcher.Instance == null || SorcerersDungeonResearcher.Instance.Deleted);
        }
        finally
        {
            pm?.Delete();
            stock?.Delete();
            pumpkin?.Delete();
            ClearEvent();
        }
    }
}
