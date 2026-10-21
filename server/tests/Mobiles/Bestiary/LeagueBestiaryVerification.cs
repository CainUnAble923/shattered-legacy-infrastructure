// LeagueBestiaryVerification.cs
//
// cc-P364, F-24: the League hunt bestiary, batch 2 (fourteen creatures on package bodies 2072-2085, the hunt and culling
// tables, the bestiary lines, the spawn data). Notes in shard-migration notes/cc-P364-league-bestiary-batch-2.md.
//
// Facts:
//   1. Every creature constructs with its name and body, the server types the body (ShardBodyTypes: Monster or Animal,
//      never Empty), it spawns into a world, and killed it leaves a corpse carrying the body.
//   2. Every creature round-trips the generated serializer with its body and name; so does every part.
//   3. Every creature fights a player (aggressive, an enemy, harmable) and, killed by that player, drops its part (one;
//      the mini-bosses three, with a jewel), and the player's bestiary notes it once: the first kill records the type in
//      the account's EncounteredCreatures and reports it, the second does not.
//   4. Every creature is in the League hunt table or the culling table (and in exactly one), with its part type, its
//      home, both bestiary lines, and a level (ClusterFCreatureLevels) inside its band: the spread the prompt asked for.
//   5. The spawn data resolves (every type a creature of the table), every creature has a home row, and the seeder
//      places every row on Trammel once: a dry run places nothing, the first run places all, the second finds them all.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.Spawners;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeagueBestiaryVerification
{
    private static readonly Point3D Here = new(1060, 1060, 0);
    private static bool _startupHooksRun;

    private readonly ITestOutputHelper _out;

    public LeagueBestiaryVerification(ITestOutputHelper output)
    {
        _out = output;
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }

        ShardBodyTypes.Apply();
    }

    // (type, name, corpse name, body, server type, part, parts a kill)
    private static readonly (Type Type, string Name, string Corpse, int Body, BodyType Kind, Type Part, int Parts)[] Lineup =
    {
        (typeof(HedgeSnatcher), "a hedge snatcher", "a hedge snatcher corpse", 2072, BodyType.Animal, typeof(SnatcherEars), 1),
        (typeof(SnatcherOutrider), "a snatcher outrider", "a snatcher outrider corpse", 2073, BodyType.Animal, typeof(SnatcherEars), 2),
        (typeof(HornbackRaider), "a hornback raider", "a hornback raider corpse", 2074, BodyType.Monster, typeof(HornbackHorn), 1),
        (typeof(DitchCrawler), "a ditch crawler", "a ditch crawler corpse", 2075, BodyType.Monster, typeof(CrawlerMandible), 1),
        (typeof(MireCroaker), "a mire croaker", "a mire croaker corpse", 2076, BodyType.Monster, typeof(CroakerTongue), 1),
        (typeof(BrineLurker), "a brine lurker", "a brine lurker corpse", 2077, BodyType.Monster, typeof(LurkerGills), 1),
        (typeof(Moonmaw), "a moonmaw", "a moonmaw corpse", 2078, BodyType.Animal, typeof(MoonmawPelt), 1),
        (typeof(Thornwisp), "a thornwisp", "a thornwisp corpse", 2079, BodyType.Monster, typeof(ThornwispSpark), 1),
        (typeof(PaleDrifter), "a pale drifter", "a pale drifter corpse", 2080, BodyType.Monster, typeof(DrifterEctoplasm), 1),
        (typeof(Slagheart), "a slagheart", "a slagheart corpse", 2081, BodyType.Animal, typeof(SlagCore), 1),
        (typeof(Glasswarden), "a glasswarden", "a glasswarden corpse", 2082, BodyType.Monster, typeof(GlasswardenShard), 1),
        (typeof(Tunnelmaw), "a tunnelmaw", "a tunnelmaw corpse", 2083, BodyType.Monster, typeof(TunnelmawClaw), 1),
        (typeof(TheSiegebreaker), "The Siegebreaker", "the Siegebreaker's corpse", 2084, BodyType.Monster, typeof(SiegebreakerTusk), 3),
        (typeof(TheHollowOak), "The Hollow Oak", "the Hollow Oak's corpse", 2085, BodyType.Monster, typeof(HollowOakHeartwood), 3)
    };

    private static readonly int[] Bodies = { 2072, 2073, 2074, 2075, 2076, 2077, 2078, 2079, 2080, 2081, 2082, 2083, 2084, 2085 };

    private sealed class Player : IDisposable
    {
        public readonly Account Account;
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player()
        {
            Account = new Account($"p364{Guid.NewGuid():N}"[..16], "p364-test-only");
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Hunter" };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1060, 1062, 0), Map.TerMur);
            Pm.Hits = Pm.HitsMax;
            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public ClusterFAccountData Data => ClusterFAccountPersistence.GetOrCreate(Account);

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            for (var i = 0; i < Account.Length; i++)
            {
                Account[i]?.Delete();
            }

            Accounts.Remove(Account);
        }
    }

    private static BaseCreature Make(Type t) => (BaseCreature)Activator.CreateInstance(t)!;

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

    private static Corpse Kill(BaseCreature bc, Mobile killer = null)
    {
        bc.MoveToWorld(Here, Map.TerMur);
        if (killer != null)
        {
            bc.Damage(bc.Hits + 50, killer);
        }
        else
        {
            bc.Kill();
        }

        return Assert.IsAssignableFrom<Corpse>(bc.Corpse);
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EveryCreatureConstructsOnATypedBodySpawnsAndDiesToACorpseCarryingTheBody()
    {
        Assert.Equal(14, Lineup.Length);

        foreach (var (type, name, corpse, body, kind, _, _) in Lineup)
        {
            var bc = Make(type);
            _out.WriteLine($"{type.Name}: name='{bc.Name}' body={(int)bc.Body} type={bc.Body.Type} hits={bc.HitsMax} ai={bc.AI} fame={bc.Fame}");

            Assert.Equal(name, bc.Name);
            Assert.Equal(corpse, bc.CorpseName);
            Assert.Equal(body, (int)bc.Body);
            Assert.Equal(kind, bc.Body.Type);
            Assert.NotEqual(BodyType.Empty, bc.Body.Type);

            bc.MoveToWorld(Here, Map.TerMur);
            Assert.True(bc.Alive);
            Assert.Same(Map.TerMur, bc.Map);

            var c = Kill(bc);
            Assert.Equal(0x2006, c.ItemID);
            Assert.Equal((int)bc.Body, c.Amount);
            c.Delete();
        }

        foreach (var body in Bodies)
        {
            Assert.Contains(ShardBodyTypes.Entries, e => e.Body == body);
            Assert.NotEqual(BodyType.Empty, new Body(body).Type);
        }

        // The animal slots of anim7 (the low-layout .vd files) are typed Animal, the rest Monster.
        Assert.Equal(BodyType.Animal, new Body(2072).Type);
        Assert.Equal(BodyType.Animal, new Body(2078).Type);
        Assert.Equal(BodyType.Animal, new Body(2081).Type);
        Assert.Equal(BodyType.Monster, new Body(2085).Type);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void EveryCreatureAndPartRoundTripsTheGeneratedSerializer()
    {
        foreach (var (type, name, _, _, _, part, _) in Lineup)
        {
            var bc = Make(type);
            var copy = RoundTrip(bc);
            Assert.Equal((int)bc.Body, (int)copy.Body);
            Assert.Equal(name, copy.Name);
            copy.Delete();
            bc.Delete();

            var item = Loot.Construct(part); // the parts take an optional amount: Activator.CreateInstance cannot make them
            Assert.NotNull(item);
            item.Amount = 3;
            var itemCopy = RoundTrip(item);
            Assert.IsAssignableFrom<BestiaryPartBase>(itemCopy);
            Assert.Equal(3, itemCopy.Amount);
            Assert.Equal(item.Name, itemCopy.Name);
            Assert.Equal(item.Hue, itemCopy.Hue);
            Assert.True(itemCopy.Stackable);
            itemCopy.Delete();
            item.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void EveryCreatureFightsAPlayerDropsItsPartWhenKilledAndEntersTheBestiaryOnce()
    {
        using var hunter = new Player();

        foreach (var (type, _, _, _, _, part, parts) in Lineup)
        {
            var bc = Make(type);
            bc.MoveToWorld(Here, Map.TerMur);

            Assert.Equal(FightMode.Closest, bc.FightMode);
            Assert.True(bc.IsEnemy(hunter.Pm), type.Name);
            Assert.True(bc.CanBeHarmful(hunter.Pm, false, false), type.Name);
            Assert.True(hunter.Pm.CanBeHarmful(bc, false, false), type.Name);
            Assert.False(bc.Controlled);
            Assert.False(bc.IsInvulnerable);

            // Hurt, then killed, by the player.
            var hits = bc.Hits;
            bc.Damage(1, hunter.Pm);
            Assert.True(bc.Hits < hits, type.Name);
            Assert.False(hunter.Data.HasEncountered(type.Name));

            bc.Damage(bc.Hits + 50, hunter.Pm);
            var corpse = Assert.IsAssignableFrom<Corpse>(bc.Corpse);
            var dropped = corpse.Items.Where(i => i.GetType() == part).ToList();
            _out.WriteLine($"{type.Name}: {string.Join(", ", corpse.Items.Select(i => $"{i.GetType().Name} x{i.Amount}"))}");
            Assert.Single(dropped);
            Assert.Equal(parts, dropped[0].Amount);
            if (parts == 3)
            {
                Assert.Contains(corpse.Items, i => i is GoldRing or GoldBracelet or GoldEarrings or GoldNecklace);
            }

            // The kill reached the bestiary through the death event (LastKiller is the player), once.
            Assert.Same(hunter.Pm, bc.LastKiller);
            Assert.True(hunter.Data.HasEncountered(type.Name), $"{type.Name} was not recorded on the first kill");
            Assert.False(ClusterFLeagueBestiary.RecordKill(hunter.Pm, bc), "a second kill is not a new entry");
            corpse.Delete();
        }

        // A creature outside the table is not the bestiary's business.
        var rat = new Rat();
        Assert.Null(ClusterFLeagueBestiary.Find(rat));
        Assert.False(ClusterFLeagueBestiary.RecordKill(hunter.Pm, rat));
        rat.Delete();
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void EveryCreatureIsInTheHuntTableOrTheCullingTableWithItsPartHomeLinesAndLevelBand()
    {
        var hunts = ClusterFLeagueBestiary.HuntTargets.ToList();
        var culls = ClusterFLeagueBestiary.CullingPacks.ToList();
        Assert.Equal(ClusterFLeagueBestiary.Entries.Length, hunts.Count + culls.Count);
        Assert.Empty(hunts.Select(e => e.Type).Intersect(culls.Select(e => e.Type)));
        Assert.Equal(2, ClusterFLeagueBestiary.MiniBosses.Count());
        Assert.True(culls.Count >= 5, "mid-level culling packs");
        Assert.True(hunts.Count(e => e.Use == BestiaryUse.HuntTarget) >= 3, "hunt targets");

        foreach (var (type, name, _, _, _, part, _) in Lineup)
        {
            var entry = ClusterFLeagueBestiary.Find(type);
            Assert.NotNull(entry);
            Assert.Equal(name, entry.Name);
            Assert.Equal(part, entry.Part);
            Assert.True(typeof(BestiaryPartBase).IsAssignableFrom(entry.Part));
            Assert.False(string.IsNullOrWhiteSpace(entry.Home));
            Assert.False(string.IsNullOrWhiteSpace(entry.Flavor));
            Assert.False(string.IsNullOrWhiteSpace(entry.Plain));
            Assert.StartsWith("(", entry.PlainLine);
            Assert.EndsWith(")", entry.PlainLine);
            Assert.Contains(entry, entry.Use == BestiaryUse.CullingPack ? culls : hunts);

            // No source or brand name reaches the game.
            foreach (var banned in new[] { "goblin", "goatman", "bullywog", "kuo", "werewolf", "umber", "treant", "golem" })
            {
                Assert.DoesNotContain(banned, entry.Name, StringComparison.OrdinalIgnoreCase);
            }

            // The level sits in the band the table promises (the spread: low near New Haven, middle culls, bosses past 70).
            var bc = Make(type);
            var level = ClusterFCreatureLevels.Level(bc, out var why);
            _out.WriteLine($"{type.Name}: level {level} ({why}); band {entry.LevelFrom}-{entry.LevelTo}; {entry.Use}; {entry.Home}");
            Assert.InRange(level, entry.LevelFrom, entry.LevelTo);
            if (entry.Use == BestiaryUse.MiniBoss)
            {
                Assert.True(level >= 70, $"{type.Name} is a mini-boss at level {level}");
            }
            else if (entry.Home == ClusterFLeagueBestiary.HavenHills)
            {
                Assert.True(level <= 45, $"{type.Name} is a New Haven target at level {level}");
            }

            bc.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheSpawnDataResolvesGivesEveryCreatureAHomeAndTheSeederPlacesEveryRowOnce()
    {
        foreach (var typeName in ClusterFLeagueBestiary.AllTypeNames())
        {
            var t = AssemblyHandler.FindTypeByName(typeName);
            Assert.True(t != null && typeof(BaseCreature).IsAssignableFrom(t), typeName);
            Assert.NotNull(ClusterFLeagueBestiary.Find(t));
        }

        foreach (var entry in ClusterFLeagueBestiary.Entries)
        {
            Assert.Contains(ClusterFLeagueBestiary.Spawners, r => r.Types.Contains(entry.Type.Name) && r.Home == entry.Home);
        }

        foreach (var row in ClusterFLeagueBestiary.Spawners)
        {
            Assert.True(row.Count >= 1 && row.HomeRange >= 1 && row.MinMinutes <= row.MaxMinutes, row.Label);
            Assert.StartsWith("Bestiary: ", row.SpawnerName);
        }

        var placed = new List<Spawner>();
        try
        {
            var dry = ClusterFLeagueBestiary.Seed(true);
            Assert.Equal((ClusterFLeagueBestiary.Spawners.Length, 0), (dry.Placed, dry.Present));
            Assert.All(ClusterFLeagueBestiary.Spawners, r => Assert.Null(ClusterFLeagueBestiary.FindSpawner(Map.Trammel, r)));

            var first = ClusterFLeagueBestiary.Seed(false);
            _out.WriteLine(first.Message);
            Assert.Equal((ClusterFLeagueBestiary.Spawners.Length, 0), (first.Placed, first.Present));

            foreach (var row in ClusterFLeagueBestiary.Spawners)
            {
                var s = ClusterFLeagueBestiary.FindSpawner(Map.Trammel, row);
                Assert.NotNull(s);
                placed.Add(s);
                Assert.Equal(row.Count, s.Count);
                Assert.Equal(row.HomeRange, s.HomeRange);
                Assert.Equal(row.Types, s.Entries.Select(e => e.SpawnedName).ToArray());
                Assert.Equal(TimeSpan.FromMinutes(row.MinMinutes), s.MinDelay);
                Assert.Equal(TimeSpan.FromMinutes(row.MaxMinutes), s.MaxDelay);
                Assert.Same(Map.Trammel, s.Map);
            }

            var again = ClusterFLeagueBestiary.Seed(false);
            Assert.Equal((0, ClusterFLeagueBestiary.Spawners.Length), (again.Placed, again.Present));
        }
        finally
        {
            foreach (var s in placed)
            {
                s.RemoveSpawns();
                s.Delete();
            }
        }
    }
}
