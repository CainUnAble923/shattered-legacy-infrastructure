// cc-P53 Part C (deviation D-113, Chase 2026-10-04): item and temporary skill bonuses count above the skill's cap; the
// skill's own Base still stops at the cap.
//
// Pinned clamps a SkillMod built with ObeyCap at Cap (Skills.cs:317-327). An item's "Skill +X" builds its mods that way
// (AosSkillBonuses.AddTo, Misc/AOS.cs:1357-1358), and so does Animal Form (AnimalForm.cs:237, :245).
// server/patches/PlayerMobile-skill-bonus-above-cap.patch sends every mod a player gets through
// ClusterFSkillBonusAboveCap.Admit, which clears ObeyCap. Facts:
//   1. A +15 ring on a capped skill reads cap + 15; taking it off returns to the cap.
//   2. A temporary mod built as Animal Form builds its own (ObeyCap set) reads cap + 20; removing it returns to the cap.
//   3. Skill gain at the cap, with the bonus worn, does not raise Base; one gain step below the cap stops at the cap.
//   4. With the switch off, pinned's clamp comes back (what fact 1 sees without the patch).
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh.

using Server;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SkillBonusAboveCapVerification
{
    private const double Cap = 200.0;

    private readonly ITestOutputHelper _out;

    public SkillBonusAboveCapVerification(ITestOutputHelper output) => _out = output;

    private static PlayerMobile CappedSmith()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(new Point3D(1240, 1240, 0), Map.Trammel);
        pm.Skills.Cap = 58 * 2000; // total cap well clear, so only the individual cap decides
        pm.Skills.Blacksmith.Cap = Cap;
        pm.Skills.Blacksmith.Base = Cap;
        pm.Skills.Stealth.Cap = Cap;
        pm.Skills.Stealth.Base = Cap;
        return pm;
    }

    private static GoldRing PlusFifteenBlacksmithy()
    {
        var ring = new GoldRing();
        ring.SkillBonuses.SetValues(0, SkillName.Blacksmith, 15.0);
        return ring;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void APlusFifteenItemOnACappedSkillReadsCapPlusFifteenAndBackOnRemoval()
    {
        Assert.True(ClusterFSkillBonusAboveCap.Enabled);
        var pm = CappedSmith();
        var ring = PlusFifteenBlacksmithy();

        try
        {
            Assert.Equal(Cap, pm.Skills.Blacksmith.Value);

            Assert.True(pm.EquipItem(ring));
            _out.WriteLine($"worn: Base {pm.Skills.Blacksmith.Base} Cap {pm.Skills.Blacksmith.Cap} Value {pm.Skills.Blacksmith.Value}");
            Assert.Equal(Cap + 15.0, pm.Skills.Blacksmith.Value);
            Assert.Equal(Cap, pm.Skills.Blacksmith.Base);

            pm.Backpack.DropItem(ring);
            _out.WriteLine($"removed: Value {pm.Skills.Blacksmith.Value}");
            Assert.Equal(Cap, pm.Skills.Blacksmith.Value);
        }
        finally
        {
            ring.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ATemporaryEffectLikewiseGoesAboveTheCapAndComesBack()
    {
        var pm = CappedSmith();

        try
        {
            // Exactly Animal Form's construction (pinned AnimalForm.cs:237).
            var mod = new DefaultSkillMod(SkillName.Stealth, "StealthAnimalForm", true, 20.0) { ObeyCap = true };
            pm.AddSkillMod(mod);
            _out.WriteLine($"in form: Stealth {pm.Skills.Stealth.Value}");
            Assert.Equal(Cap + 20.0, pm.Skills.Stealth.Value);

            pm.RemoveSkillMod(mod);
            Assert.Equal(Cap, pm.Skills.Stealth.Value);

            // A timed one, as a potion or spell would build it, with the cap flag set.
            var timed = new TimedSkillMod(SkillName.Stealth, "P53Timed", true, 10.0, System.TimeSpan.FromMinutes(5))
                { ObeyCap = true };
            pm.AddSkillMod(timed);
            Assert.Equal(Cap + 10.0, pm.Skills.Stealth.Value);
            timed.Remove();
            Assert.Equal(Cap, pm.Skills.Stealth.Value);
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void SkillGainAtTheCapStillDoesNotRaiseBase()
    {
        var pm = CappedSmith();
        var ring = PlusFifteenBlacksmithy();

        try
        {
            pm.EquipItem(ring);
            var skill = pm.Skills.Blacksmith;
            Assert.Equal(Cap + 15.0, skill.Value);

            for (var i = 0; i < 50; i++)
            {
                SkillCheck.Gain(pm, skill);
            }

            Assert.Equal(Cap, skill.Base);

            // One tenth below the cap: a gain step lands on the cap and goes no further (SkillCheck.cs:280).
            skill.Base = Cap - 0.1;
            for (var i = 0; i < 50; i++)
            {
                SkillCheck.Gain(pm, skill);
            }

            _out.WriteLine($"after gains: Base {skill.Base} Value {skill.Value}");
            Assert.Equal(Cap, skill.Base);
            Assert.Equal(Cap + 15.0, skill.Value);
        }
        finally
        {
            ring.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void WithTheSwitchOffPinnedsClampReturns()
    {
        var pm = CappedSmith();
        var ring = PlusFifteenBlacksmithy();
        ClusterFSkillBonusAboveCap.Enabled = false;

        try
        {
            pm.EquipItem(ring);
            Assert.Equal(Cap, pm.Skills.Blacksmith.Value);

            // Below the cap the bonus still applies up to the cap, as pinned.
            pm.Skills.Blacksmith.Base = 190.0;
            Assert.Equal(Cap, pm.Skills.Blacksmith.Value);
        }
        finally
        {
            ClusterFSkillBonusAboveCap.Enabled = true;
            ring.Delete();
            pm.Delete();
        }
    }
}
