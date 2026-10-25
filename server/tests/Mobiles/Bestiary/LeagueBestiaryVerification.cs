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
//   6. cc-P455 (cc-P404 #2, #3): every creature's numbers are P364's table (notes section 3): stats, hits, damage and
//      its type, the resists the table names, fame, speed, weapon ability, carve yields and fixed drops; with the
//      poisons registered (the fixture calls ShardTestHost.EnsurePoisons), the three poisonous hitters poison a player
//      with their own poison, and the four lethal-immune creatures shrug off Lethal.
//   7. cc-P455 (cc-P404 #1): a kill finished by the player's pet, summon, or a creature the player provoked onto it
//      records the bestiary entry for the player, as pinned credits fame (GetDamageMaster).
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
        ShardTestHost.EnsurePoisons(); // cc-P455 (cc-P404 #3): levels and hit poisons read the registered poisons
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

    // ---------------------------------------------------------------- 6

    // P364's creature table (notes/cc-P364-league-bestiary-batch-2.md section 3), as ranges. Dmg% is the one damage
    // type besides physical, if any; resists are only the ones the table names.
    private sealed record Numbers(
        Type Type, (int, int) Str, (int, int) Dex, (int, int) Int, (int, int) Hits, (int, int) Dmg,
        (ResistanceType Kind, int Pct)? DmgType, (ResistanceType Kind, int Lo, int Hi)[] Resists, int Fame)
    {
        public double? Speed { get; init; }
        public Type Ability { get; init; }
        public int Meat { get; init; }
        public int Hides { get; init; }
        public (Type Item, int Amount)[] Packs { get; init; } = [];
    }

    private static readonly Numbers[] Table =
    [
        new(typeof(HedgeSnatcher), (35, 45), (40, 50), (15, 25), (24, 30), (3, 6), null,
            [(ResistanceType.Physical, 10, 15)], 400),
        new(typeof(SnatcherOutrider), (60, 75), (70, 85), (20, 30), (50, 65), (5, 9), null,
            [(ResistanceType.Physical, 20, 25)], 1200) { Speed = 0.15 },
        new(typeof(HornbackRaider), (70, 85), (50, 65), (20, 30), (40, 52), (4, 8), null,
            [(ResistanceType.Physical, 15, 25)], 800) { Meat = 1 },
        new(typeof(DitchCrawler), (25, 35), (25, 35), (5, 10), (16, 22), (2, 5), (ResistanceType.Poison, 30),
            [(ResistanceType.Poison, 20, 30)], 250),
        new(typeof(MireCroaker), (60, 75), (55, 70), (20, 30), (40, 50), (4, 8), null,
            [(ResistanceType.Physical, 15, 20), (ResistanceType.Poison, 20, 30)], 900) { Meat = 1 },
        new(typeof(BrineLurker), (85, 100), (70, 85), (40, 55), (55, 70), (6, 10), (ResistanceType.Cold, 30),
            [(ResistanceType.Physical, 25, 30), (ResistanceType.Cold, 30, 40)], 1500)
            { Meat = 1, Packs = [(typeof(RawFishSteak), 2)] }, // meat: the code's, which the table leaves out
        new(typeof(Moonmaw), (100, 120), (90, 110), (30, 45), (70, 85), (8, 13), null,
            [(ResistanceType.Physical, 25, 35)], 2500) { Ability = typeof(BleedAttack), Meat = 2, Hides = 10 },
        new(typeof(Thornwisp), (60, 80), (60, 75), (80, 100), (50, 65), (6, 10), (ResistanceType.Poison, 50),
            [(ResistanceType.Poison, 50, 60)], 2500) { Packs = [(typeof(Log), 5), (typeof(BaseReagent), 3)] },
        new(typeof(PaleDrifter), (60, 80), (80, 100), (40, 60), (50, 65), (7, 11), (ResistanceType.Cold, 60),
            [(ResistanceType.Cold, 50, 60)], 3000),
        new(typeof(Slagheart), (150, 180), (40, 55), (30, 45), (95, 115), (10, 15), (ResistanceType.Fire, 60),
            [(ResistanceType.Physical, 40, 50), (ResistanceType.Fire, 70, 80), (ResistanceType.Cold, 0, 5)], 4000)
            { Packs = [(typeof(SulfurousAsh), 5)] },
        new(typeof(Glasswarden), (140, 170), (50, 65), (50, 70), (95, 115), (10, 15), (ResistanceType.Energy, 30),
            [(ResistanceType.Physical, 45, 55), (ResistanceType.Cold, 60, 70), (ResistanceType.Poison, 60, 70)], 4500),
        new(typeof(Tunnelmaw), (220, 260), (55, 70), (25, 40), (140, 170), (12, 18), null,
            [(ResistanceType.Physical, 45, 55)], 5000) { Ability = typeof(BleedAttack), Meat = 3 },
        new(typeof(TheSiegebreaker), (350, 400), (70, 90), (40, 60), (320, 380), (16, 24), null,
            [(ResistanceType.Physical, 50, 60)], 9000) { Ability = typeof(CrushingBlow), Meat = 4 },
        new(typeof(TheHollowOak), (300, 350), (30, 45), (60, 80), (300, 350), (14, 22), (ResistanceType.Poison, 20),
            [(ResistanceType.Physical, 50, 60), (ResistanceType.Fire, 5, 15), (ResistanceType.Poison, 60, 70)], 9000)
            { Speed = 0.35, Ability = typeof(ParalyzingBlow), Packs = [(typeof(Log), 20)] }
    ];

    private static int DamageOf(BaseCreature bc, ResistanceType kind) => kind switch
    {
        ResistanceType.Fire => bc.FireDamage,
        ResistanceType.Cold => bc.ColdDamage,
        ResistanceType.Poison => bc.PoisonDamage,
        ResistanceType.Energy => bc.EnergyDamage,
        _ => bc.PhysicalDamage
    };

    [Fact]
    public void EveryCreaturesNumbersAreP364sTableAndItsPoisonsWorkWithThePoisonsRegistered()
    {
        Assert.Equal(Lineup.Select(l => l.Type), Table.Select(n => n.Type));
        Assert.NotEmpty(Poison.Poisons);

        foreach (var n in Table)
        {
            var bc = Make(n.Type);
            var name = n.Type.Name;
            try
            {
                Assert.InRange(bc.RawStr, n.Str.Item1, n.Str.Item2);
                Assert.InRange(bc.RawDex, n.Dex.Item1, n.Dex.Item2);
                Assert.InRange(bc.RawInt, n.Int.Item1, n.Int.Item2);
                Assert.InRange(bc.HitsMax, n.Hits.Item1, n.Hits.Item2);
                Assert.Equal((n.Dmg.Item1, n.Dmg.Item2), (bc.DamageMin, bc.DamageMax));
                var other = n.DmgType?.Pct ?? 0;
                Assert.True(100 - other == bc.PhysicalDamage, $"{name}: physical damage {bc.PhysicalDamage}");
                if (n.DmgType is { } dt)
                {
                    Assert.True(dt.Pct == DamageOf(bc, dt.Kind), $"{name}: {dt.Kind} damage {DamageOf(bc, dt.Kind)}");
                }

                foreach (var (kind, lo, hi) in n.Resists)
                {
                    var r = bc.GetResistance(kind);
                    Assert.True(r >= lo && r <= hi, $"{name}: {kind} resist {r}, table {lo}-{hi}");
                }

                Assert.Equal(n.Fame, bc.Fame);
                if (n.Speed is { } speed)
                {
                    Assert.Equal(speed, bc.ActiveSpeed, 3);
                }

                Assert.Equal(n.Ability, bc.GetWeaponAbility()?.GetType());
                Assert.True(n.Meat == bc.Meat, $"{name}: meat {bc.Meat}");
                Assert.True(n.Hides == bc.Hides, $"{name}: hides {bc.Hides}");

                foreach (var (item, amount) in n.Packs)
                {
                    var packed = bc.Backpack?.Items.Where(i => item.IsInstanceOfType(i)).Sum(i => i.Amount) ?? 0;
                    Assert.True(packed >= amount, $"{name}: {packed} {item.Name}, table {amount}");
                }
            }
            finally
            {
                bc.Delete();
            }
        }

        // The poisonous hitters, each with its own poison, registered: Lesser for the crawler, Regular for the others.
        foreach (var (type, poison) in new[]
                 {
                     (typeof(DitchCrawler), Poison.Lesser), (typeof(Thornwisp), Poison.Regular), (typeof(TheHollowOak), Poison.Regular)
                 })
        {
            using var target = new Player();
            var bc = Make(type);
            bc.MoveToWorld(Here, Map.TerMur);
            Assert.NotNull(poison);
            Assert.Same(poison, bc.HitPoison);
            Assert.True(bc.HitPoisonChance > 0, type.Name);
            Assert.Equal(ApplyPoisonResult.Poisoned, target.Pm.ApplyPoison(bc, bc.HitPoison));
            Assert.Same(poison, target.Pm.Poison);
            target.Pm.CurePoison(target.Pm);
            bc.Delete();
        }

        // The four lethal-immune creatures shrug off the strongest poison; a creature that is not immune takes it.
        foreach (var type in new[] { typeof(PaleDrifter), typeof(Slagheart), typeof(Glasswarden), typeof(TheHollowOak) })
        {
            var bc = Make(type);
            bc.MoveToWorld(Here, Map.TerMur);
            Assert.Same(Poison.Lethal, bc.PoisonImmune);
            Assert.Equal(ApplyPoisonResult.Immune, bc.ApplyPoison(null, Poison.Lethal));
            bc.Delete();
        }

        var moonmaw = Make(typeof(Moonmaw));
        moonmaw.MoveToWorld(Here, Map.TerMur);
        Assert.Equal(ApplyPoisonResult.Poisoned, moonmaw.ApplyPoison(null, Poison.Lethal));
        moonmaw.Delete();
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void AKillByThePlayersPetSummonOrProvokedCreatureEntersThePlayersBestiary()
    {
        void KilledBy(Func<Player, BaseCreature, BaseCreature> makeKiller, string how)
        {
            using var hunter = new Player();
            var victim = Make(typeof(HedgeSnatcher));
            victim.MoveToWorld(Here, Map.TerMur);
            var killer = makeKiller(hunter, victim);
            killer.MoveToWorld(Here, Map.TerMur);
            try
            {
                victim.Damage(victim.Hits + 50, killer);
                Assert.False(victim.Alive, how);
                Assert.Same(killer, victim.LastKiller);
                Assert.True(hunter.Data.HasEncountered(nameof(HedgeSnatcher)), $"a kill by the player's {how} was not recorded");
            }
            finally
            {
                victim.Corpse?.Delete();
                killer.Delete();
            }
        }

        KilledBy((p, _) =>
        {
            var dog = new Dog();
            Assert.True(dog.SetControlMaster(p.Pm));
            return dog;
        }, "pet");

        KilledBy((p, _) => new Dog { Summoned = true, SummonMaster = p.Pm }, "summon");

        KilledBy((p, victim) => new Orc { BardProvoked = true, BardMaster = p.Pm, BardTarget = victim }, "provoked creature");

        // A wild creature's kill is nobody's.
        using var nobody = new Player();
        var wild = Make(typeof(HedgeSnatcher));
        wild.MoveToWorld(Here, Map.TerMur);
        var orc = new Orc();
        orc.MoveToWorld(Here, Map.TerMur);
        wild.Damage(wild.Hits + 50, orc);
        Assert.False(nobody.Data.HasEncountered(nameof(HedgeSnatcher)));
        wild.Corpse?.Delete();
        orc.Delete();
    }
}
