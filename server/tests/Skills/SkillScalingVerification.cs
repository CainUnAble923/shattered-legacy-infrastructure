// SkillScalingVerification.cs
//
// cc-P71 (cc-P66 decision 4, Chase 2026-10-06: "update these to appropriate levels at 200"). Five pinned effects kept
// growing with skill past OSI's top. Patched to read the skill through customizations ClusterFSkillClamps, which returns
// pinned's number at or below OSI's top and past it one diminishing-returns curve to a value at 200 from modernuo.json
// (clusterf.skillScaling.*). Each fact drives pinned's own code where a test can reach it, at 200 (today's number on the
// red tree, the curve's on green) and at and below OSI's top (pinned's on both). Notes: shard-migration
// notes/cc-P71-high-skill-effect-scaling.md.
//
// Facts:
//   S1. Nerve Strike's paralysis chance (NerveStrike.cs:66): 0.90 at Bushido 120 and certain from 137.5; now 0.95 at 200,
//       never certain. A source and value fact: a player's weapon ability validates against its NetState, which the test
//       host has none of, so OnHit is not reachable for a player attacker.
//   S2. Stable slots (AnimalTrainer.GetMaxStabled): 14 at 120/120/120 as pinned; 38 at 200/200/200, now 23.
//   S3. Fire Horn damage (FireHorn.Use): four bard skills 200 dealt 2.33 times the damage at 120; now 1.10 times.
//   S4. Stealth steps (Stealth.OnUse): 24 at 120 as pinned; 40 at 200, now 34.
//   S5. The heal per bandage (BandageContext.OnTick, the roll held at the middle): 57 at 120/120 as pinned; 93 at 200/200,
//       now 72.
//   S6. The curve itself: every effect rises past the top, never falls, reaches its value exactly at 200 and stays there
//       past 200 (worn bonuses); creatures read pinned; the switch off reads pinned.

using System;
using System.IO;
using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Server.SkillHandlers;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SkillScalingVerification
{
    private static readonly double[] Ladder = [100.0, 120.0, 140.0, 160.0, 180.0, 200.0];

    private readonly ITestOutputHelper _out;

    public SkillScalingVerification(ITestOutputHelper output)
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

    private static PlayerMobile Player(Point3D at)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.RawStr = 100;
        pm.RawDex = 100;
        pm.RawInt = 100;
        pm.Skills.Cap = 58 * 2000;
        pm.MoveToWorld(at, Map.Trammel);
        return pm;
    }

    private static void SetSkill(Mobile m, SkillName name, double value)
    {
        m.Skills[name].Cap = 200.0;
        m.Skills[name].Base = value;
    }

    private static string BuildTreeFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Projects", "UOContent")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, $"no Projects/UOContent above {AppContext.BaseDirectory}");
        return File.ReadAllText(Path.Combine(dir!.FullName, "Projects", "UOContent", relative));
    }

    private static double PinnedNerveStrike(double bushido) => (150.0 / 7.0 + 4.0 * bushido / 7.0) / 100.0;

    // ---------------------------------------------------------------- S1

    [Fact]
    public void S1_NerveStrikeIsNeverCertain()
    {
        var source = BuildTreeFile("Items/Weapons/Abilities/NerveStrike.cs");
        var reads = source.Contains("ClusterFSkillClamps.NerveStrikeChance(attacker) > Utility.RandomDouble()");
        _out.WriteLine($"build tree's paralysis line reads the scaled chance: {reads}");

        var pm = Player(new Point3D(1500, 1500, 0));
        var bat = new Mongbat();

        try
        {
            foreach (var b in Ladder)
            {
                SetSkill(pm, SkillName.Bushido, b);
                var today = Math.Min(PinnedNerveStrike(b), 1.0);
                _out.WriteLine(
                    $"Bushido {b}: today {today:0.0000}, scaled {ClusterFSkillClamps.NerveStrikeChance(pm):0.0000}");
            }

            Assert.True(reads, "NerveStrike.cs's paralysis roll does not read ClusterFSkillClamps.NerveStrikeChance");

            // OSI's range, as pinned.
            SetSkill(pm, SkillName.Bushido, 100.0);
            Assert.Equal(PinnedNerveStrike(100.0), ClusterFSkillClamps.NerveStrikeChance(pm), 12);
            SetSkill(pm, SkillName.Bushido, 120.0);
            Assert.Equal(0.9, ClusterFSkillClamps.NerveStrikeChance(pm), 12);
            // Past it: 0.95 at 200 (pinned: 1.357, certain), never certain.
            SetSkill(pm, SkillName.Bushido, 137.5);
            Assert.True(ClusterFSkillClamps.NerveStrikeChance(pm) < 1.0);
            SetSkill(pm, SkillName.Bushido, 200.0);
            Assert.Equal(0.95, ClusterFSkillClamps.NerveStrikeChance(pm), 12);

            // Creatures as pinned.
            SetSkill(bat, SkillName.Bushido, 200.0);
            Assert.Equal(PinnedNerveStrike(200.0), ClusterFSkillClamps.NerveStrikeChance(bat), 12);
        }
        finally
        {
            bat.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- S2

    private int Stables(double taming, double lore, double vet)
    {
        var pm = Player(new Point3D(1520, 1520, 0));

        try
        {
            SetSkill(pm, SkillName.AnimalTaming, taming);
            SetSkill(pm, SkillName.AnimalLore, lore);
            SetSkill(pm, SkillName.Veterinary, vet);
            var max = AnimalTrainer.GetMaxStabled(pm);
            _out.WriteLine($"Taming/Lore/Vet {taming}/{lore}/{vet}: {max} stable slots");
            return max;
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void S2_StableSlotsRiseToTwentyThreeAtTwoHundred()
    {
        var all = new int[Ladder.Length];
        for (var i = 0; i < Ladder.Length; i++)
        {
            all[i] = Stables(Ladder[i], Ladder[i], Ladder[i]);
        }

        var one = Stables(200.0, 120.0, 120.0);

        // OSI's range, as pinned: 5 + 1 + 1 + 1 and 5 + 3 + 3 + 3.
        Assert.Equal(8, all[0]);
        Assert.Equal(14, all[1]);
        // Past it (pinned: 38 and 22).
        Assert.Equal(23, all[^1]);
        Assert.Equal(17, one);
        for (var i = 1; i < all.Length; i++)
        {
            Assert.True(all[i] >= all[i - 1], "stable slots fell as skill rose");
        }
    }

    // ---------------------------------------------------------------- S3

    // One fire horn blast from a player with all four bard skills at `skill` into a single mongbat with no fire resist, the
    // damage roll held at the middle: the hit points it lost.
    private int FireHornBlast(double skill)
    {
        var at = new Point3D(1540, 1540, 0);
        var pm = Player(at);
        var bat = new Mongbat();
        var horn = new FireHorn();

        foreach (var name in new[] { SkillName.Musicianship, SkillName.Provocation, SkillName.Discordance, SkillName.Peacemaking })
        {
            SetSkill(pm, name, skill);
        }

        pm.Backpack.DropItem(new SulfurousAsh(10));
        pm.Backpack.DropItem(horn);
        bat.SetResistance(ResistanceType.Fire, 0);
        bat.SetHits(5000);
        var target = new Point3D(at.X + 2, at.Y, at.Z);
        bat.MoveToWorld(target, Map.Trammel);

        BuiltInRng.Generator = new ConstantRandom { Value = 0.5 };

        try
        {
            var before = bat.Hits;
            horn.Use(pm, target);
            var lost = before - bat.Hits;
            _out.WriteLine($"bard skills {skill}: the blast took {lost} hit points");
            return lost;
        }
        finally
        {
            BuiltInRng.Reset();
            horn.Delete();
            bat.Delete();
            pm.Delete();
        }
    }

    // Pinned's AOS PvM arithmetic (FireHorn.cs:125-142) for one target, every skill scaled to `scaled`, under the same held
    // roll; fire damage against no fire resist lands whole.
    private static int ExpectedBlast(int scaled)
    {
        var weightAvg = scaled * 8 / 80;
        var avgDamage = weightAvg / 2;
        BuiltInRng.Generator = new ConstantRandom { Value = 0.5 };

        try
        {
            return Utility.RandomMinMax(avgDamage * 9 / 10, avgDamage * 10 / 9);
        }
        finally
        {
            BuiltInRng.Reset();
        }
    }

    [Fact]
    public void S3_FireHornGainsATenthAtTwoHundred()
    {
        var damage = new int[Ladder.Length];
        for (var i = 0; i < Ladder.Length; i++)
        {
            damage[i] = FireHornBlast(Ladder[i]);
        }

        var at120 = damage[1];
        var at200 = damage[^1];
        _out.WriteLine($"200 over 120: x{(double)at200 / at120:0.000}");

        Assert.True(damage[0] > 0, "the blast did no damage; the fact measures nothing");
        // OSI's range, as pinned: each skill's Fixed v scaled v + 2 x max(0, v - 900).
        Assert.Equal(ExpectedBlast(1000 + 200), damage[0]);
        Assert.Equal(ExpectedBlast(1200 + 600), at120);
        // Past it: 1.10 times at 200 (pinned: 2000 + 2200 = 4200 each, 2.33 times).
        Assert.Equal(ExpectedBlast((int)(1800 * 1.10)), at200);
        Assert.InRange((double)at200 / at120, 1.07, 1.13);
        for (var i = 1; i < damage.Length; i++)
        {
            Assert.True(damage[i] >= damage[i - 1], "Fire Horn damage fell as skill rose");
        }
    }

    // ---------------------------------------------------------------- S4

    private int StealthSteps(double stealth)
    {
        var pm = Player(new Point3D(1560, 1560, 0));
        SetSkill(pm, SkillName.Hiding, 100.0);
        SetSkill(pm, SkillName.Stealth, stealth);
        var handler = Mobile.SkillCheckLocationHandler;
        Mobile.SkillCheckLocationHandler = (_, _, _, _) => true;

        try
        {
            pm.Hidden = true;
            Stealth.OnUse(pm);
            _out.WriteLine($"Stealth {stealth}: {pm.AllowedStealthSteps} steps");
            return pm.AllowedStealthSteps;
        }
        finally
        {
            Mobile.SkillCheckLocationHandler = handler;
            pm.Delete();
        }
    }

    [Fact]
    public void S4_StealthStepsRiseToThirtyFourAtTwoHundred()
    {
        var steps = new int[Ladder.Length];
        for (var i = 0; i < Ladder.Length; i++)
        {
            steps[i] = StealthSteps(Ladder[i]);
        }

        // OSI's range, as pinned: Stealth / 5.
        Assert.Equal(20, steps[0]);
        Assert.Equal(24, steps[1]);
        // Past it (pinned: 40).
        Assert.Equal(34, steps[^1]);
        for (var i = 1; i < steps.Length; i++)
        {
            Assert.True(steps[i] >= steps[i - 1], "Stealth steps fell as skill rose");
        }
    }

    // ---------------------------------------------------------------- S5

    // One bandage from a player healer onto a badly hurt player, the roll held at the middle: the hit points it healed.
    private int BandageHeal(double healing, double anatomy)
    {
        var at = new Point3D(1580, 1580, 0);
        var healer = Player(at);
        var patient = Player(new Point3D(at.X + 1, at.Y, at.Z));
        SetSkill(healer, SkillName.Healing, healing);
        SetSkill(healer, SkillName.Anatomy, anatomy);
        patient.Hits = 1;

        BuiltInRng.Generator = new ConstantRandom { Value = 0.5 };

        try
        {
            // The largest heal here is pinned's 93 at 200/200; 1 + 93 must fit under the patient's maximum.
            Assert.True(patient.HitsMax > 94, $"the patient has {patient.HitsMax} hit points at most, no room for the heal");
            var context = new BandageContext(healer, patient, TimeSpan.Zero);
            typeof(BandageContext).GetMethod("OnTick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(context, null);
            var healed = patient.Hits - 1;
            _out.WriteLine($"Healing/Anatomy {healing}/{anatomy}: healed {healed}");
            return healed;
        }
        finally
        {
            BuiltInRng.Reset();
            patient.Delete();
            healer.Delete();
        }
    }

    [Fact]
    public void S5_ABandageHealsSeventyTwoAtTwoHundred()
    {
        var heals = new int[Ladder.Length];
        for (var i = 0; i < Ladder.Length; i++)
        {
            heals[i] = BandageHeal(Ladder[i], Ladder[i]);
        }

        var healingOnly = BandageHeal(200.0, 120.0);

        // OSI's range, as pinned: (min + max) / 2 = 48.33 at 100/100 and 57.5 at 120/120.
        Assert.Equal(48, heals[0]);
        Assert.Equal(57, heals[1]);
        // Past it (pinned: 93); 72 to rounding (the curve's 72.0 can land a hair under).
        Assert.InRange(heals[^1], 71, 72);
        Assert.InRange(healingOnly, heals[1] + 1, heals[^1] - 1);
        for (var i = 1; i < heals.Length; i++)
        {
            Assert.True(heals[i] >= heals[i - 1], "the heal fell as skill rose");
        }
    }

    // ---------------------------------------------------------------- S6

    [Fact]
    public void S6_TheCurveRisesStopsAtTwoHundredAndLeavesCreaturesAlone()
    {
        // The curve: 0 at the top, exactly 1 at 200 and above, rising, each step smaller than the last.
        Assert.Equal(0.0, ClusterFSkillClamps.Curve(0.0));
        Assert.Equal(1.0, ClusterFSkillClamps.Curve(1.0));
        Assert.Equal(1.0, ClusterFSkillClamps.Curve(ClusterFSkillClamps.Progress(215.0, 120.0)));
        var last = 0.0;
        var lastStep = double.MaxValue;
        for (var v = 121.0; v <= 200.0; v += 1.0)
        {
            var c = ClusterFSkillClamps.Curve(ClusterFSkillClamps.Progress(v, 120.0));
            Assert.True(c > last, $"the curve did not rise at {v}");
            Assert.True(c - last < lastStep + 1e-12, $"the curve sped up at {v}");
            lastStep = c - last;
            last = c;
        }

        foreach (var v in new[] { 140.0, 160.0, 180.0 })
        {
            _out.WriteLine($"curve at {v}: {ClusterFSkillClamps.Curve(ClusterFSkillClamps.Progress(v, 120.0)):0.0000}");
        }

        var pm = Player(new Point3D(1600, 1600, 0));
        var bat = new Mongbat();

        try
        {
            // Past 200 (a worn bonus over the cap, D-113): held at the value at 200.
            SetSkill(pm, SkillName.Bushido, 200.0);
            pm.AddSkillMod(new DefaultSkillMod(SkillName.Bushido, "P71Worn", true, 15.0) { ObeyCap = false });
            Assert.Equal(215.0, pm.Skills.Bushido.Value);
            Assert.Equal(0.95, ClusterFSkillClamps.NerveStrikeChance(pm), 12);
            Assert.Equal(23, ClusterFSkillClamps.StableSlots(pm, 5 + 3 * 12, 215.0, 215.0, 215.0));
            Assert.Equal((int)(1800 * 1.10), ClusterFSkillClamps.FireHornScaled(pm, 2150));

            // Creatures read pinned.
            SetSkill(bat, SkillName.Stealth, 200.0);
            Assert.Equal(40, ClusterFSkillClamps.StealthSteps(bat, 40));
            Assert.Equal(200.0, ClusterFSkillClamps.BandageHealSkill(bat, 200.0));
            Assert.Equal(38, ClusterFSkillClamps.StableSlots(bat, 38, 200.0, 200.0, 200.0));
            Assert.Equal(4200, ClusterFSkillClamps.FireHornScaled(bat, 2000));

            // The switch off: pinned for players too.
            ClusterFSkillClamps.ScalingEnabled = false;
            Assert.Equal(PinnedNerveStrike(215.0), ClusterFSkillClamps.NerveStrikeChance(pm), 12);
            Assert.Equal(38, ClusterFSkillClamps.StableSlots(pm, 38, 200.0, 200.0, 200.0));
            Assert.Equal(4200, ClusterFSkillClamps.FireHornScaled(pm, 2000));
            Assert.Equal(200.0, ClusterFSkillClamps.BandageHealSkill(pm, 200.0));
            _out.WriteLine($"switch off: {ClusterFSkillClamps.DescribeScaling()}");
        }
        finally
        {
            ClusterFSkillClamps.ScalingEnabled = true;
            bat.Delete();
            pm.Delete();
        }

        _out.WriteLine($"defaults: {ClusterFSkillClamps.DescribeScaling()}");
    }
}
