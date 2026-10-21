// TestCrystalVerification.cs
//
// cc-P190: the Test Crystal, a per-character skill gain booster for the test shard (customizations
// TestCenter/TestCrystal.cs, server/patches/SkillCheck-test-crystal.patch). Notes: shard-migration
// notes/cc-P190-cont-test-crystal.md.
//
// The gain roll is pinned's CheckSkill (SkillCheck.cs:108-150): a use gains when gc >= RandomDouble(), where
//   gc = (((Cap - Total) / Cap + (skill.Cap - Base) / skill.Cap) / 2 + (1 - chance) * 0.5 on a success) / 2 x GainFactor
// and the patch multiplies gc by the owner's crystal strength right after GainFactor. With every roll held at r, a use
// gains exactly when r <= gc, so holding r just under and just over a computed gc reads the chance itself.
// The setup: skill cap 100, Base 80 (window 0-100, so chance 0.8 and a success at any r <= 0.8), total cap 1000 and no
// other skill, so gc = ((0.2 + 0.2) / 2 + 0.2 x 0.5) / 2 = 0.15 x GainFactor.
//
// Facts:
//   C1. A crystal that is Off gives exactly the base chance: gain at 0.999 x gc, none at 1.001 x gc.
//   C2. At 5x its owner gains at 0.999 x 5gc and not at 1.001 x 5gc; a second character in the same place, rolling the
//       same numbers, gets the base chance.
//   C3. With the Test Center off the crystal multiplies nothing, and double-clicking it deletes it.
//   C4. The owner and setting survive a save and load (and the loaded crystal is the one that counts).
//   C5. [TCFill's refill puts one crystal, Off and bound to the character, in the bank, and never a second.
//   C6. Only the offered strengths are kept; anything else is Off.

using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TestCrystalVerification
{
    private readonly ITestOutputHelper _out;

    public TestCrystalVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureSkillChecks();
    }

    private sealed class TestCenterSwitch : IDisposable
    {
        private static readonly MethodInfo Setter =
            typeof(TestCenter).GetProperty(nameof(TestCenter.Enabled))!.GetSetMethod(true)!;

        private readonly bool _was;

        public TestCenterSwitch(bool on)
        {
            _was = TestCenter.Enabled;
            Setter.Invoke(null, [on]);
        }

        public void Dispose() => Setter.Invoke(null, [_was]);
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

    // Pinned's own location handler alone, so one use is one roll; every roll held at r.
    private sealed class RawRoll : IDisposable
    {
        private readonly SkillCheckLocationHandler _location = Mobile.SkillCheckLocationHandler;

        public RawRoll(double r)
        {
            Mobile.SkillCheckLocationHandler = SkillCheck.Mobile_SkillCheckLocation;
            BuiltInRng.Generator = new ConstantRandom { Value = r };
        }

        public void Dispose()
        {
            BuiltInRng.Reset();
            Mobile.SkillCheckLocationHandler = _location;
        }
    }

    private static readonly Point3D Spot = new(1220, 1220, 0); // as YoungPlayerVerification

    private static PlayerMobile Player()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.StrLock = StatLockType.Locked;
        pm.DexLock = StatLockType.Locked;
        pm.IntLock = StatLockType.Locked;
        pm.Skills.Cap = 1000;
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    // The base gc for Tactics at 80.0 in the setup above.
    private static double BaseChance(PlayerMobile pm) => 0.15 * pm.Skills[SkillName.Tactics].Info.GainFactor;

    // One use of Tactics at 80.0 (window 0-100) with every roll held at r: did it gain?
    private static bool Gains(PlayerMobile pm, double r)
    {
        var tactics = pm.Skills[SkillName.Tactics];
        tactics.Cap = 100.0;
        tactics.Base = 80.0;

        using (new RawRoll(r))
        {
            pm.CheckSkill(SkillName.Tactics, 0.0, 100.0);
        }

        return tactics.Base > 80.0;
    }

    private static TestCrystal Give(PlayerMobile pm, int strength)
    {
        var crystal = new TestCrystal(pm);
        pm.BankBox.DropItem(crystal);
        crystal.SetStrength(strength);
        return crystal;
    }

    [Fact]
    public void C1_ACrystalThatIsOffGivesExactlyTheBaseChance()
    {
        using var tc = new TestCenterSwitch(true);
        var pm = Player();

        try
        {
            Give(pm, 1);
            var gc = BaseChance(pm);
            _out.WriteLine($"base gc {gc:F6}");

            Assert.Equal(1.0, TestCrystal.GainScalar(pm));
            Assert.True(Gains(pm, gc * 0.999));
            Assert.False(Gains(pm, gc * 1.001));
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void C2_AtFiveTimesOnlyItsOwnerGainsFiveTimesTheChance()
    {
        using var tc = new TestCenterSwitch(true);
        var owner = Player();
        var other = Player();

        try
        {
            Give(owner, 5);
            var gc = BaseChance(owner);

            Assert.Equal(5.0, TestCrystal.GainScalar(owner));
            Assert.True(Gains(owner, 5 * gc * 0.999));
            Assert.False(Gains(owner, 5 * gc * 1.001));

            Assert.Equal(1.0, TestCrystal.GainScalar(other));
            Assert.False(Gains(other, 5 * gc * 0.999));
            Assert.True(Gains(other, gc * 0.999));
            Assert.False(Gains(other, gc * 1.001));
        }
        finally
        {
            owner.Delete();
            other.Delete();
        }
    }

    [Fact]
    public void C3_WithTheTestCenterOffItDoesNothingAndCrumbles()
    {
        var pm = Player();
        TestCrystal crystal;

        try
        {
            using (new TestCenterSwitch(true))
            {
                crystal = Give(pm, 5);
            }

            using (new TestCenterSwitch(false))
            {
                var gc = BaseChance(pm);
                Assert.Equal(1.0, TestCrystal.GainScalar(pm));
                Assert.False(Gains(pm, 5 * gc * 0.999));
                Assert.True(Gains(pm, gc * 0.999));

                crystal.OnDoubleClick(pm);
                Assert.True(crystal.Deleted);
                Assert.Null(TestCrystal.EnsureFor(pm)); // and none is handed out
            }
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void C4_TheOwnerAndSettingSurviveASaveAndLoad()
    {
        using var tc = new TestCenterSwitch(true);
        var pm = Player();

        try
        {
            var original = Give(pm, 10);

            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            original.Serialize(writer);
            writer.Flush();

            // A restart: the crystal comes back from its bytes.
            original.Delete();
            Assert.Equal(1.0, TestCrystal.GainScalar(pm));

            var copy = new TestCrystal(original.Serial);
            copy.Deserialize(new BufferReader(buffer));

            Assert.Same(pm, copy.Owner);
            Assert.Equal(10, copy.Strength);
            Assert.Same(pm.BankBox, copy.Parent);
            Assert.Same(copy, TestCrystal.For(pm));
            Assert.Equal(10.0, TestCrystal.GainScalar(pm));
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void C5_TheRefillPutsOneCrystalOffAndBoundInTheBankAndNeverASecond()
    {
        using var tc = new TestCenterSwitch(true);
        var pm = Player();

        try
        {
            TestCenterKit.PlaceKit(pm);
            TestCenterKit.PlaceKit(pm);

            var crystal = Assert.Single(pm.BankBox.Items.OfType<TestCrystal>());
            Assert.Same(pm, crystal.Owner);
            Assert.Equal(1, crystal.Strength);
            Assert.Equal(LootType.Blessed, crystal.LootType);
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void C6_OnlyTheOfferedStrengthsAreKept()
    {
        using var tc = new TestCenterSwitch(true);
        var pm = Player();

        try
        {
            var crystal = Give(pm, 1);
            Assert.Equal(new[] { 1, 2, 5, 10, 25 }, TestCrystal.Strengths);

            foreach (var s in TestCrystal.Strengths)
            {
                crystal.SetStrength(s);
                Assert.Equal(s, crystal.Strength);
            }

            crystal.SetStrength(3);
            Assert.Equal(1, crystal.Strength);
            crystal.SetStrength(1000);
            Assert.Equal(1, crystal.Strength);
        }
        finally
        {
            pm.Delete();
        }
    }
}
