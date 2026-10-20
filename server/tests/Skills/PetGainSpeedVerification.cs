// PetGainSpeedVerification.cs
//
// cc-P172 (Chase 2026-10-18, test shard, training a pet Giant Spider: "pet skills do not increase rate... that should be
// something that increases in speed after loops/skill/outriders rank"). Pinned SkillCheck.cs:140-143 only doubles a
// controlled creature's gain chance. Now (ClusterFPetGainSpeed, SkillCheck-pet-gain-speed.patch) a player's pet's chance is
// also x (1 + loops x perLoop + perSkillPoint x (Taming above 100 + Lore above 100, each to 200) + Outriders rung x perRank),
// at least 1, at most maxMultiplier; defaults 0.1, 0.004, 0.1, 3.0, settings clusterf.petGain.*.
//
// Facts:
//   1. The multiplier for each input with the defaults, and the cap.
//   2. The four settings are read (Configure) and change it.
//   3. A real owner's inputs: Taming and Lore, the Rangers' League rung (a member only; the Apprentice task's Scout bump),
//      the loops through EvoPet.OwnerLoopsSource (F-12's stub).
//   4. An uncontrolled creature is unchanged: multiplier 1, and pinned's roll at a fixed random still gains nothing.
//   5. A pet's gain chance rises with the multiplier (fixed random): the same roll a fresh tamer's pet misses, a 200 / 200
//      Legendary Outrider's pet gains; a roll above the raised chance still gains nothing.
//   6. The table (fresh, 120/120, 200/200, each Outriders rung, each loop count), printed for the notes.
//   7. The Animal Lore gump's skill pages show the pet's gain speed.

using System;
using System.Globalization;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Misc;
using Server.Mobiles;
using Server.Random;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PetGainSpeedVerification
{
    private const string PerLoopKey = "clusterf.petGain.perLoop";
    private const string PerSkillPointKey = "clusterf.petGain.perSkillPoint";
    private const string PerRankKey = "clusterf.petGain.perRank";
    private const string MaxKey = "clusterf.petGain.maxMultiplier";

    private static readonly Point3D Spot = new(1500, 1520, 0);

    private readonly ITestOutputHelper _out;

    public PetGainSpeedVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
    }

    private sealed class ConstantRandom : System.Random
    {
        public double Value;

        protected override double Sample() => Value;
        public override double NextDouble() => Value;
        public override int Next() => (int)(Value * int.MaxValue);
        public override int Next(int maxValue) => (int)(Value * maxValue);
        public override int Next(int minValue, int maxValue) => minValue + (int)(Value * (maxValue - minValue));
        public override long NextInt64(long maxValue) => (long)(Value * maxValue);
    }

    private static string S(double v) => v.ToString(CultureInfo.InvariantCulture);

    private static void UseSettings(double perLoop, double perSkillPoint, double perRank, double max)
    {
        ServerConfiguration.SetSetting(PerLoopKey, S(perLoop));
        ServerConfiguration.SetSetting(PerSkillPointKey, S(perSkillPoint));
        ServerConfiguration.SetSetting(PerRankKey, S(perRank));
        ServerConfiguration.SetSetting(MaxKey, S(max));
        ClusterFPetGainSpeed.Configure();
    }

    private static void UseDefaults() =>
        UseSettings(
            ClusterFPetGainSpeed.DefaultPerLoop,
            ClusterFPetGainSpeed.DefaultPerSkillPoint,
            ClusterFPetGainSpeed.DefaultPerRank,
            ClusterFPetGainSpeed.DefaultMaxMultiplier
        );

    private sealed class Owner : IDisposable
    {
        public readonly Account Account = new($"p172{Guid.NewGuid():N}"[..16], "p172-test-only");
        public readonly PlayerMobile Pm;
        private readonly Func<Mobile, int> _savedLoops = EvoPet.OwnerLoopsSource;

        public Owner(double taming, double lore, int loops = 0)
        {
            Pm = new PlayerMobile { Player = true };
            Account[0] = Pm;
            Pm.Skills.AnimalTaming.Cap = 200.0;
            Pm.Skills.AnimalTaming.Base = taming;
            Pm.Skills.AnimalLore.Cap = 200.0;
            Pm.Skills.AnimalLore.Base = lore;
            Pm.MoveToWorld(Spot, Map.Trammel);
            var pm = Pm;
            EvoPet.OwnerLoopsSource = m => m == pm ? loops : 0;
        }

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial);

        public Owner Outrider(int standing)
        {
            Guild.JoinedGuilds.Add(ClusterFPetGainSpeed.RangersKey);
            Guild.AddReputation(ClusterFPetGainSpeed.RangersKey, standing);
            return this;
        }

        public void Dispose()
        {
            EvoPet.OwnerLoopsSource = _savedLoops;
            Pm.Delete();
        }
    }

    private static GreyWolf Pet(Mobile owner)
    {
        var wolf = new GreyWolf();
        wolf.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);

        if (owner != null)
        {
            wolf.Owners.Add(owner);
            wolf.SetControlMaster(owner);
        }

        return wolf;
    }

    // ---------------------------------------------------------------- 1

    [Theory]
    [InlineData(0, 100.0, 100.0, 0, 1.0)]   // a fresh tamer
    [InlineData(0, 50.0, 30.0, 0, 1.0)]     // below 100 counts nothing
    [InlineData(0, 120.0, 120.0, 0, 1.16)]
    [InlineData(0, 200.0, 100.0, 0, 1.4)]
    [InlineData(0, 100.0, 200.0, 0, 1.4)]
    [InlineData(0, 200.0, 200.0, 0, 1.8)]
    [InlineData(0, 230.0, 250.0, 0, 1.8)]   // each skill counts to 200
    [InlineData(0, 100.0, 100.0, 1, 1.1)]
    [InlineData(0, 100.0, 100.0, 6, 1.6)]
    [InlineData(1, 100.0, 100.0, 0, 1.1)]
    [InlineData(5, 100.0, 100.0, 0, 1.5)]
    [InlineData(6, 200.0, 200.0, 6, 3.0)]   // a looped Legendary Outrider at 200 / 200: the cap exactly
    [InlineData(20, 200.0, 200.0, 6, 3.0)]  // capped
    [InlineData(-3, 100.0, 100.0, -1, 1.0)] // never below 1
    public void TheMultiplierForEachInputAndTheCap(int loops, double taming, double lore, int rank, double expected)
    {
        UseDefaults();
        Assert.Equal(expected, ClusterFPetGainSpeed.Multiplier(loops, taming, lore, rank), 6);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheFourSettingsAreRead()
    {
        try
        {
            UseSettings(0.5, 0.01, 0.2, 10.0);
            Assert.Equal(0.5, ClusterFPetGainSpeed.PerLoop, 6);
            Assert.Equal(0.01, ClusterFPetGainSpeed.PerSkillPoint, 6);
            Assert.Equal(0.2, ClusterFPetGainSpeed.PerRank, 6);
            Assert.Equal(10.0, ClusterFPetGainSpeed.MaxMultiplier, 6);
            Assert.Equal(1.0 + 1.0 + 0.5 + 0.6, ClusterFPetGainSpeed.Multiplier(2, 150.0, 100.0, 3), 6);

            UseSettings(0.5, 0.01, 0.2, 2.0);
            Assert.Equal(2.0, ClusterFPetGainSpeed.Multiplier(2, 150.0, 100.0, 3), 6);

            UseSettings(0.5, 0.01, 0.2, 0.5); // a cap below 1 is 1: never slower than pinned
            Assert.Equal(1.0, ClusterFPetGainSpeed.Multiplier(2, 150.0, 100.0, 3), 6);
        }
        finally
        {
            UseDefaults();
        }
    }

    // ---------------------------------------------------------------- 3

    [Theory]
    [InlineData(0, 0)]
    [InlineData(999, 0)]
    [InlineData(1_000, 1)]
    [InlineData(5_000, 2)]
    [InlineData(15_000, 3)]
    [InlineData(40_000, 4)]
    [InlineData(80_000, 5)]
    [InlineData(150_000, 6)]
    public void TheOutridersRungFollowsTheLadder(int standing, int rung)
    {
        using var owner = new Owner(100.0, 100.0).Outrider(standing);
        Assert.Equal(rung, ClusterFPetGainSpeed.OutridersRank(owner.Pm));
    }

    [Fact]
    public void ARealOwnersInputsReachThePet()
    {
        UseDefaults();
        using var owner = new Owner(160.0, 140.0, loops: 2).Outrider(15_000);
        var pet = Pet(owner.Pm);
        try
        {
            Assert.True(pet.Controlled);
            Assert.Equal(2, ClusterFPetGainSpeed.OwnerLoops(pet, owner.Pm));
            Assert.Equal(3, ClusterFPetGainSpeed.OutridersRank(owner.Pm));
            // 1 + 2 x 0.1 + 0.004 x (60 + 40) + 3 x 0.1
            Assert.Equal(1.9, ClusterFPetGainSpeed.Multiplier(pet), 6);

            // Standing without membership counts nothing; the Apprentice task's bump counts as Scout.
            owner.Guild.JoinedGuilds.Remove(ClusterFPetGainSpeed.RangersKey);
            Assert.Equal(0, ClusterFPetGainSpeed.OutridersRank(owner.Pm));
            Assert.Equal(1.6, ClusterFPetGainSpeed.Multiplier(pet), 6);

            owner.Guild.JoinedGuilds.Add(ClusterFPetGainSpeed.RangersKey);
            owner.Guild.AddReputation(ClusterFPetGainSpeed.RangersKey, -15_000);
            Assert.Equal(0, ClusterFPetGainSpeed.OutridersRank(owner.Pm));
            owner.Guild.ApprenticeGuilds.Add(ClusterFPetGainSpeed.RangersKey);
            Assert.Equal(1, ClusterFPetGainSpeed.OutridersRank(owner.Pm));
        }
        finally
        {
            pet.Delete();
        }
    }

    // ---------------------------------------------------------------- the roll

    // Pinned's gain chance for a controlled creature before the x2 (SkillCheck.cs:126-138), for a check that failed.
    private static double PinnedChance(Mobile from, Skill skill, double chance)
    {
        var gc = (double)(from.Skills.Cap - from.Skills.Total) / from.Skills.Cap;
        gc += (skill.Cap - skill.Base) / skill.Cap;
        gc /= 2;
        gc += (1.0 - chance) * (Core.AOS ? 0.0 : 0.2);
        gc /= 2;
        gc *= skill.Info.GainFactor;
        return Math.Max(gc, 0.01);
    }

    private static Skill Prepare(BaseCreature creature)
    {
        var skill = creature.Skills.Wrestling;
        skill.Cap = 100.0;
        skill.Base = 90.0;
        return skill;
    }

    /// <summary>One CheckSkill on Wrestling at 90 / 100 with every random roll at <paramref name="roll"/>; true when it gained.</summary>
    private static bool RollOnce(BaseCreature creature, double roll)
    {
        var skill = Prepare(creature);
        BuiltInRng.Generator = new ConstantRandom { Value = roll };
        try
        {
            SkillCheck.CheckSkill(creature, skill, creature, 0.0);
        }
        finally
        {
            BuiltInRng.Reset();
        }

        return skill.Base > 90.0;
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void AnUncontrolledCreatureIsUnchanged()
    {
        UseDefaults();
        using var owner = new Owner(200.0, 200.0, loops: 20).Outrider(150_000);
        var wild = Pet(null);
        try
        {
            Assert.False(wild.Controlled);
            Assert.Equal(1.0, ClusterFPetGainSpeed.Multiplier(wild));
            Assert.Equal(1.0, ClusterFPetGainSpeed.Multiplier(owner.Pm)); // a player is not a pet either

            // Pinned's uncontrolled chance (no x2): a roll just above it misses, just below it gains.
            var gc = PinnedChance(wild, Prepare(wild), 0.0);
            Assert.False(RollOnce(wild, gc + 0.01));
            Assert.True(RollOnce(wild, gc - 0.01));
        }
        finally
        {
            wild.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void APetsGainChanceRisesWithTheMultiplier()
    {
        UseDefaults();
        using var fresh = new Owner(100.0, 100.0);
        using var master = new Owner(200.0, 200.0, loops: 6).Outrider(150_000);
        var freshPet = Pet(fresh.Pm);
        var masterPet = Pet(master.Pm);
        try
        {
            Assert.Equal(1.0, ClusterFPetGainSpeed.Multiplier(freshPet), 6);
            Assert.Equal(3.0, ClusterFPetGainSpeed.Multiplier(masterPet), 6);

            var gc = PinnedChance(freshPet, Prepare(freshPet), 0.0) * 2; // pinned's x2 for a controlled creature
            _out.WriteLine($"pinned controlled chance {gc:0.000}; x3 is {gc * 3:0.000}");
            Assert.True(gc * 1.5 < 1.0, "the setup leaves no room between x1 and x3");

            var roll = gc * 1.5; // between the fresh tamer's chance and the master's
            Assert.False(RollOnce(freshPet, roll));
            Assert.True(RollOnce(masterPet, roll));

            // Likelier, never free: with the cap at 1.2 the same roll is above the raised chance and gains nothing.
            UseSettings(
                ClusterFPetGainSpeed.DefaultPerLoop,
                ClusterFPetGainSpeed.DefaultPerSkillPoint,
                ClusterFPetGainSpeed.DefaultPerRank,
                1.2
            );
            Assert.False(RollOnce(masterPet, roll));
            Assert.True(RollOnce(masterPet, gc * 1.15));
        }
        finally
        {
            UseDefaults();
            freshPet.Delete();
            masterPet.Delete();
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheTable()
    {
        UseDefaults();
        _out.WriteLine("| Owner | Multiplier |");
        _out.WriteLine("|---|---|");
        _out.WriteLine($"| fresh (100 / 100, no loops, not an Outrider) | x{ClusterFPetGainSpeed.Multiplier(0, 100, 100, 0):0.00} |");
        _out.WriteLine($"| 120 / 120 | x{ClusterFPetGainSpeed.Multiplier(0, 120, 120, 0):0.00} |");
        _out.WriteLine($"| 150 / 150 | x{ClusterFPetGainSpeed.Multiplier(0, 150, 150, 0):0.00} |");
        _out.WriteLine($"| 200 / 200 | x{ClusterFPetGainSpeed.Multiplier(0, 200, 200, 0):0.00} |");

        var thresholds = ClusterFGuildSystem.RankThresholds(ClusterFPetGainSpeed.RangersKey);
        for (var r = 0; r < thresholds.Length; r++)
        {
            var name = ClusterFGuildSystem.GetRankName(ClusterFPetGainSpeed.RangersKey, thresholds[r]);
            _out.WriteLine($"| Outriders rung {r} {name} (100 / 100) | x{ClusterFPetGainSpeed.Multiplier(0, 100, 100, r):0.00} |");
        }

        for (var l = 0; l <= 10; l++)
        {
            _out.WriteLine($"| {l} loops (100 / 100) | x{ClusterFPetGainSpeed.Multiplier(l, 100, 100, 0):0.00} |");
        }

        for (var l = 0; l <= 6; l++)
        {
            _out.WriteLine($"| 200 / 200, Legendary Outrider, {l} loops | x{ClusterFPetGainSpeed.Multiplier(l, 200, 200, 6):0.00} |");
        }

        Assert.Equal(7, thresholds.Length);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheLoreGumpShowsTheGainSpeed()
    {
        UseDefaults();
        using var owner = new Owner(200.0, 200.0, loops: 6).Outrider(150_000);
        var pet = Pet(owner.Pm);
        var ns = PacketTestUtilities.CreateTestNetState();
        owner.Pm.NetState = ns;
        ns.Mobile = owner.Pm;
        try
        {
            var gump = new NewAnimalLoreGump(owner.Pm, pet);
            owner.Pm.SendGump(gump);
            var wire = PetTrainingVerification.OnTheWire(ns, gump);
            _out.WriteLine(string.Join(" / ", wire.Strings));
            Assert.Equal(3, wire.Strings.FindAll(s => s.Contains("Learns x3.0")).Count); // the three skill pages
        }
        finally
        {
            owner.Pm.NetState = null;
            pet.Delete();
        }
    }
}
