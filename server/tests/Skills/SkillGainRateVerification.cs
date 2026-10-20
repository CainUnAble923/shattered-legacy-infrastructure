// SkillGainRateVerification.cs
//
// cc-P191: the shard's base skill gain rate, clusterf.skillGain.chanceAttempts and clusterf.skillGain.amountMultiplier
// (customizations ClusterFSkillGain, cc-P66 B). The simulation (S1) drives pinned's own SkillCheck handlers with the
// shard's hook installed over them, as the server does, and counts uses to each milestone under each candidate rate;
// the play model (which window each use rolls, seconds per use) is the test's own and is stated beside each skill.
// Notes: shard-migration notes/cc-P191-cont-earned-skill-gain.md.
//
// Facts:
//   R1. The configured rate is what the gain code uses: Configure reads the two keys, and one use with every roll held
//       at 0 raises Base by attempts x amount x 0.1 (every attempt succeeds and gains, every gain is repeated amount
//       times: ClusterFSkillGain.MoreAttempts and ApplyAmountBonus over pinned SkillCheck.Gain, SkillCheck.cs:236-282).
//   R2. The simulation is reproducible: one seed gives the same use counts twice; another seed gives different ones.
//   R3. GainFactor still multiplies the gain chance under the recommended rate (SkillCheck.cs:133): the Test Crystal
//       (cc-P190) applies its factor there, so it multiplies on top of the rate.
//   R4. The accelerated skill (pinned Gain, SkillCheck.cs:273-276: the New Haven training quests and cc-P189's Old Haven
//       apprentice boost) still multiplies each gain's amount under the recommended rate.
//   S1. The table: uses and hours to 50, 80, 100, 120, 150 and 200 for six skills under stock, today and four candidates.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server;
using Server.Misc;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SkillGainRateVerification
{
    private const double Cap = 200.0;

    // The recommended live rate (cc-P191, notes): R3 and R4 run under it.
    public const int RecommendedAttempts = 2;
    public const int RecommendedAmount = 1;

    private readonly ITestOutputHelper _out;

    public SkillGainRateVerification(ITestOutputHelper output)
    {
        _out = output;
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

    // The shard's hook over pinned's handlers at the given rate, with the given generator (every roll held, or seeded).
    private sealed class Hook : IDisposable
    {
        private readonly SkillCheckLocationHandler _location = Mobile.SkillCheckLocationHandler;
        private readonly SkillCheckDirectLocationHandler _directLocation = Mobile.SkillCheckDirectLocationHandler;
        private readonly SkillCheckTargetHandler _target = Mobile.SkillCheckTargetHandler;
        private readonly SkillCheckDirectTargetHandler _directTarget = Mobile.SkillCheckDirectTargetHandler;
        private readonly bool _enabled = ClusterFSkillGain.Enabled;
        private readonly int _attempts = ClusterFSkillGain.ChanceAttempts;
        private readonly int _amount = ClusterFSkillGain.AmountMultiplier;

        public Hook(bool enabled, int attempts, int amount, System.Random generator)
        {
            Mobile.SkillCheckLocationHandler = SkillCheck.Mobile_SkillCheckLocation;
            Mobile.SkillCheckDirectLocationHandler = SkillCheck.Mobile_SkillCheckDirectLocation;
            Mobile.SkillCheckTargetHandler = SkillCheck.Mobile_SkillCheckTarget;
            Mobile.SkillCheckDirectTargetHandler = SkillCheck.Mobile_SkillCheckDirectTarget;
            ClusterFSkillGain.Initialize();
            ClusterFSkillGain.Enabled = enabled;
            ClusterFSkillGain.ChanceAttempts = attempts;
            ClusterFSkillGain.AmountMultiplier = amount;
            BuiltInRng.Generator = generator;
        }

        public void Dispose()
        {
            BuiltInRng.Reset();
            Mobile.SkillCheckLocationHandler = _location;
            Mobile.SkillCheckDirectLocationHandler = _directLocation;
            Mobile.SkillCheckTargetHandler = _target;
            Mobile.SkillCheckDirectTargetHandler = _directTarget;
            ClusterFSkillGain.Enabled = _enabled;
            ClusterFSkillGain.ChanceAttempts = _attempts;
            ClusterFSkillGain.AmountMultiplier = _amount;
        }
    }

    // A player with a 200 cap on every skill, the shard's "unlimited" total (58 x 200, ClusterFSkillCaps), six other
    // skills at 100 (the total-cap term A of SkillCheck.cs:126 reads about 0.94), stats locked so no stat roll runs.
    private static PlayerMobile Player(Point3D at)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Server.Items.Backpack());
        pm.RawStr = 100;
        pm.RawDex = 100;
        pm.RawInt = 100;
        pm.StrLock = StatLockType.Locked;
        pm.DexLock = StatLockType.Locked;
        pm.IntLock = StatLockType.Locked;
        pm.Skills.Cap = 58 * 2000;

        for (var i = 0; i < pm.Skills.Length; i++)
        {
            pm.Skills[i].Cap = Cap;
        }

        foreach (var other in new[]
                 {
                     SkillName.Anatomy, SkillName.Healing, SkillName.Parry, SkillName.MagicResist, SkillName.Meditation,
                     SkillName.EvalInt
                 })
        {
            pm.Skills[other].Base = 100.0;
        }

        pm.MoveToWorld(at, Map.Trammel);
        return pm;
    }

    // ---------------------------------------------------------------- the settings

    // Name, rate on (off is pinned alone: stock), attempts, amount.
    public sealed record Setting(string Name, bool Enabled, int Attempts, int Amount);

    public static readonly Setting[] Settings =
    [
        new("stock 1 and 1", false, 1, 1),
        new("1 and 2", true, 2, 1),
        new("2 and 1", true, 1, 2),
        new("2 and 2", true, 2, 2),
        new("3 and 1", true, 1, 3),
        new("today 5 and 3", true, 3, 5)
    ];

    // ---------------------------------------------------------------- the play model

    // One use of a skill at value v: the window (or direct chance) a real use would roll, through Mobile.CheckSkill.
    private delegate bool Use(PlayerMobile pm, double v);

    private sealed record SkillModel(string Name, SkillName Skill, double SecondsPerUse, double Ceiling, string Model, Use Use);

    // (required, window min, window max): pinned Mining.cs:115-187 and ours ClusterFMiningExtension.Ore (tier +/- 40).
    private static readonly (double Req, double Min, double Max)[] Ores =
    [
        (0.0, 0.0, 100.0), (65.0, 25.0, 105.0), (70.0, 30.0, 110.0), (75.0, 35.0, 115.0), (80.0, 40.0, 120.0),
        (85.0, 45.0, 125.0), (90.0, 50.0, 130.0), (95.0, 55.0, 135.0), (99.0, 59.0, 139.0),
        (112.5, 72.5, 152.5), (125.0, 85.0, 165.0), (137.5, 97.5, 177.5), (150.0, 110.0, 190.0),
        (162.5, 122.5, 202.5), (175.0, 135.0, 215.0), (187.5, 147.5, 227.5), (200.0, 160.0, 240.0)
    ];

    // Circle minimums without a scroll: MagerySpell.cs:15-17 (_requiredSkill[circle + 2], Core.ML), window min + 40.
    private static readonly double[] CircleMin = [-18.0, -4.0, 10.0, 24.0, 38.0, 52.0, 66.0, 80.0];

    private static readonly SkillModel[] Skills =
    [
        new(
            "Swordsmanship", SkillName.Swords, 3.0, Cap,
            "each swing rolls the hit chance (BaseWeapon.cs:1296-1340) against a creature of equal skill up to 120: 50 percent, then rising",
            (pm, v) =>
            {
                var defender = Math.Min(v, 120.0);
                var chance = Math.Max(0.02, (v + 20.0) / ((defender + 20.0) * 2.0));
                return pm.CheckSkill(SkillName.Swords, chance);
            }
        ),
        new(
            "Tactics", SkillName.Tactics, 3.0, Cap,
            "each hit rolls (0, cap) (BaseWeapon.cs:2524)",
            (pm, v) => pm.CheckSkill(SkillName.Tactics, 0.0, pm.Skills.Tactics.Cap)
        ),
        new(
            "Blacksmithy", SkillName.Blacksmith, 5.0, Cap,
            "each craft is an item or order at 50 percent (window v - 20 to v + 20; CraftItem.cs:891-894, shard metals to 200 per ClusterFCraftGain)",
            (pm, v) => pm.CheckSkill(SkillName.Blacksmith, v - 20.0, v + 20.0)
        ),
        new(
            "Mining", SkillName.Mining, 5.0, Cap,
            "each dig is the richest vein the miner can work (HarvestSystem.cs:147, the ore's own window)",
            (pm, v) =>
            {
                var ore = Ores[0];

                foreach (var o in Ores)
                {
                    if (v >= o.Req && v < o.Max)
                    {
                        ore = o;
                    }
                }

                return pm.CheckSkill(SkillName.Mining, ore.Min, ore.Max);
            }
        ),
        new(
            "Magery", SkillName.Magery, 5.0, 120.0,
            "each cast is the highest circle at 37.5 percent or better (Spell.cs:627, MagerySpell.cs:30-44); the 8th circle window ends at 120",
            (pm, v) =>
            {
                var min = CircleMin[0];

                foreach (var m in CircleMin)
                {
                    if (v >= m + 15.0 && v < m + 40.0)
                    {
                        min = m;
                    }
                }

                return pm.CheckSkill(SkillName.Magery, min, min + 40.0);
            }
        ),
        new(
            "Imbuing", SkillName.Imbuing, 10.0, 140.0,
            "each imbue targets a property at 50 percent (ArtificersImbueGump.cs:1854, difficulty 40 to 120, window +/- 20)",
            (pm, v) =>
            {
                var diff = Math.Clamp(v, 40.0, 120.0);
                return pm.CheckSkill(SkillName.Imbuing, diff - 20.0, diff + 20.0);
            }
        )
    ];

    public static readonly double[] Milestones = [50.0, 80.0, 100.0, 120.0, 150.0, 200.0];

    private const int UseLimit = 400_000;

    // Uses to each milestone from 0 (a milestone past the model's ceiling, or never reached, is -1).
    private static int[] Climb(PlayerMobile pm, SkillModel model, Setting setting, int seed, double cap = Cap)
    {
        var skill = pm.Skills[model.Skill];
        skill.Cap = cap;
        skill.Base = 0.0;
        var result = Enumerable.Repeat(-1, Milestones.Length).ToArray();
        var top = Math.Min(model.Ceiling, cap);

        using var hook = new Hook(setting.Enabled, setting.Attempts, setting.Amount, new System.Random(seed));

        for (var uses = 1; uses <= UseLimit && skill.Base < top; uses++)
        {
            model.Use(pm, skill.Value);

            for (var i = 0; i < Milestones.Length; i++)
            {
                if (result[i] < 0 && skill.Base >= Milestones[i])
                {
                    result[i] = uses;
                }
            }
        }

        return result;
    }

    // ---------------------------------------------------------------- R1

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(1, 3)]
    [InlineData(3, 5)]
    public void R1_TheConfiguredRateIsWhatTheGainCodeUses(int attempts, int amount)
    {
        var storedAttempts = ServerConfiguration.GetSetting("clusterf.skillGain.chanceAttempts", 3);
        var storedAmount = ServerConfiguration.GetSetting("clusterf.skillGain.amountMultiplier", 5);
        var pm = Player(new Point3D(2310, 2310, 0));

        try
        {
            ServerConfiguration.SetSetting("clusterf.skillGain.chanceAttempts", attempts);
            ServerConfiguration.SetSetting("clusterf.skillGain.amountMultiplier", amount);
            ClusterFSkillGain.Configure();
            Assert.Equal(attempts, ClusterFSkillGain.ChanceAttempts);
            Assert.Equal(amount, ClusterFSkillGain.AmountMultiplier);
            Assert.True(ClusterFSkillGain.Enabled);

            using var hook = new Hook(
                ClusterFSkillGain.Enabled, ClusterFSkillGain.ChanceAttempts, ClusterFSkillGain.AmountMultiplier,
                new ConstantRandom { Value = 0.0 }
            );
            var tactics = pm.Skills[SkillName.Tactics];
            tactics.Base = 50.0;
            pm.CheckSkill(SkillName.Tactics, 0.0, tactics.Cap);
            _out.WriteLine($"{attempts} attempt(s) x {amount} amount: 50.0 -> {tactics.Base}");
            Assert.Equal(50.0 + attempts * amount * 0.1, tactics.Base, 9);
        }
        finally
        {
            ServerConfiguration.SetSetting("clusterf.skillGain.chanceAttempts", storedAttempts);
            ServerConfiguration.SetSetting("clusterf.skillGain.amountMultiplier", storedAmount);
            ClusterFSkillGain.Configure();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- R2

    [Fact]
    public void R2_TheSimulationIsSeeded()
    {
        var pm = Player(new Point3D(2320, 2320, 0));
        var setting = Settings[2];
        var model = Skills[0];

        try
        {
            var first = Climb(pm, model, setting, 7);
            var again = Climb(pm, model, setting, 7);
            var other = Climb(pm, model, setting, 8);
            _out.WriteLine($"seed 7: {string.Join(", ", first)}; seed 7 again: {string.Join(", ", again)}; seed 8: {string.Join(", ", other)}");
            Assert.Equal(first, again);
            Assert.NotEqual(first, other);
            Assert.All(first, uses => Assert.True(uses > 0));
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- R3

    [Fact]
    public void R3_GainFactorStillMultipliesTheChanceUnderTheRecommendedRate()
    {
        var pm = Player(new Point3D(2330, 2330, 0));
        var tactics = pm.Skills[SkillName.Tactics];
        var factor = tactics.Info.GainFactor;

        // At 100 on (0, 200) the roll is 0.5 and the gain chance about 0.49 (A 0.94, B 0.5, +0.25 on success, halved):
        // a roll held at 0.5 passes the use and fails the gain at factor 1, and passes it at factor 3.
        using var hook = new Hook(true, RecommendedAttempts, RecommendedAmount, new ConstantRandom { Value = 0.5 });

        try
        {
            tactics.Base = 100.0;
            tactics.Info.GainFactor = 1.0;
            Assert.True(pm.CheckSkill(SkillName.Tactics, 0.0, tactics.Cap));
            _out.WriteLine($"factor 1: 100.0 -> {tactics.Base}");
            Assert.Equal(100.0, tactics.Base);

            tactics.Info.GainFactor = 3.0;
            Assert.True(pm.CheckSkill(SkillName.Tactics, 0.0, tactics.Cap));
            _out.WriteLine($"factor 3: 100.0 -> {tactics.Base}");
            Assert.True(tactics.Base > 100.0);
        }
        finally
        {
            tactics.Info.GainFactor = factor;
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- R4

    [Fact]
    public void R4_TheAcceleratedSkillStillMultipliesEachGainUnderTheRecommendedRate()
    {
        var pm = Player(new Point3D(2340, 2340, 0));
        var tactics = pm.Skills[SkillName.Tactics];

        // Every roll held at 0: each attempt gains, and Gain's RandomMinMax(2, 5) reads 2, so an accelerated gain is 0.2.
        using var hook = new Hook(true, RecommendedAttempts, RecommendedAmount, new ConstantRandom { Value = 0.0 });

        try
        {
            tactics.Base = 100.0;
            pm.CheckSkill(SkillName.Tactics, 0.0, tactics.Cap);
            var plain = tactics.Base - 100.0;

            pm.AcceleratedSkill = SkillName.Tactics;
            pm.AcceleratedStart = Core.Now + TimeSpan.FromHours(1.0);
            tactics.Base = 100.0;
            pm.CheckSkill(SkillName.Tactics, 0.0, tactics.Cap);
            var accelerated = tactics.Base - 100.0;

            _out.WriteLine($"one use at the recommended rate: plain +{plain:F1}, accelerated +{accelerated:F1}");
            Assert.Equal(RecommendedAttempts * RecommendedAmount * 0.1, plain, 9);
            Assert.Equal(RecommendedAttempts * RecommendedAmount * 0.2, accelerated, 9);
        }
        finally
        {
            pm.AcceleratedStart = DateTime.MinValue;
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- S1

    private static readonly int[] Seeds = [1, 2, 3, 4, 5];

    [Fact]
    public void S1_UsesAndHoursToEachMilestone()
    {
        var pm = Player(new Point3D(2350, 2350, 0));
        var sb = new StringBuilder();

        try
        {
            sb.AppendLine("Seeds " + string.Join(", ", Seeds) + ", mean uses; a dash is past the skill's ceiling today.");

            foreach (var model in Skills)
            {
                sb.AppendLine();
                sb.AppendLine($"### {model.Name}: {model.Model}; {model.SecondsPerUse:0} s per use ({3600.0 / model.SecondsPerUse:0} an hour)");
                sb.AppendLine();
                sb.AppendLine("| setting | " + string.Join(" | ", Milestones.Select(m => $"uses to {m:0}")) + " | " +
                              string.Join(" | ", Milestones.Select(m => $"h to {m:0}")) + " |");
                sb.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", Milestones.Length * 2)));

                foreach (var setting in Settings)
                {
                    var runs = Seeds.Select(seed => Climb(pm, model, setting, seed)).ToArray();
                    var mean = new double[Milestones.Length];

                    for (var i = 0; i < Milestones.Length; i++)
                    {
                        mean[i] = runs.Any(r => r[i] < 0) ? -1 : runs.Average(r => r[i]);
                    }

                    sb.AppendLine(
                        $"| {setting.Name} | " +
                        string.Join(" | ", mean.Select(u => u < 0 ? "-" : $"{u:0}")) + " | " +
                        string.Join(" | ", mean.Select(u => u < 0 ? "-" : $"{u * model.SecondsPerUse / 3600.0:0.00}")) + " |"
                    );
                    Assert.True(runs.All(r => r[0] > 0), $"{model.Name} under {setting.Name} never reached 50");
                }
            }

            // Loop 0: the cap is 50, so the skill's own headroom term B (SkillCheck.cs:127) reads against 50.
            sb.AppendLine();
            sb.AppendLine("### Swordsmanship with the loop-0 cap of 50: uses to 50");
            sb.AppendLine();
            sb.AppendLine("| setting | uses to 50 | h to 50 |");
            sb.AppendLine("|---|---|---|");

            foreach (var setting in Settings)
            {
                var mean = Seeds.Select(seed => Climb(pm, Skills[0], setting, seed, 50.0)[0]).Average();
                sb.AppendLine($"| {setting.Name} | {mean:0} | {mean * Skills[0].SecondsPerUse / 3600.0:0.00} |");
            }

            _out.WriteLine(sb.ToString());
        }
        finally
        {
            pm.Delete();
        }
    }
}
