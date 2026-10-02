// MeditationStartChanceVerification.cs
//
// cc-P29 Part B, D45: active Meditation's start chance must not get worse as the mana pool grows. OSI's formula,
// (50 + (Meditation - (ManaMax - Mana)) * 2) / 100 (pinned UOContent/Skills/Meditation.cs:76, ours Meditation.cs),
// takes the absolute mana deficit, so with no stat ceiling (p25-decisions item 3) a big pool must be nearly full
// before a trance can start. Ours now scales the deficit to a 150-mana pool above 150 (OSI's effective Int cap,
// pinned PlayerMobile.cs:594); at or below 150 it is OSI's formula exactly. Notes in shard-migration
// notes/cc-P29-defect-batch-2.md, Part B.
//
// Facts:
//   1. At OSI-scale pools (100, 125, 150) the chance is OSI's, for every mana level and Meditation 50, 100, 120.
//   2. A pool of 500 at half mana, Meditation 120, has the same chance as a pool of 150 at half mana (1.4).
//      Before cc-P29 it was -2.1: no chance at all.
//   3. For a fixed fraction of the pool missing, the chance never falls as the pool grows from 150 to 2,000.

using Server.SkillHandlers;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

public class MeditationStartChanceVerification
{
    private readonly ITestOutputHelper _out;

    public MeditationStartChanceVerification(ITestOutputHelper output) => _out = output;

    private static double Osi(double skill, int mana, int manaMax) => (50.0 + (skill - (manaMax - mana)) * 2) / 100;

    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    public void AtOsiScalePoolsTheChanceIsOsis(int pool)
    {
        foreach (var skill in new[] { 50.0, 100.0, 120.0 })
        {
            for (var mana = 0; mana <= pool; mana++)
            {
                Assert.Equal(Osi(skill, mana, pool), Meditation.StartChance(skill, mana, pool), 9);
            }
        }
    }

    [Fact]
    public void AFiveHundredPoolAtHalfManaStartsLikeAOneFiftyPoolAtHalfMana()
    {
        var big = Meditation.StartChance(120.0, 250, 500);
        var osi = Meditation.StartChance(120.0, 75, 150);
        _out.WriteLine($"pool 500 at 250: {big:F3}; pool 150 at 75: {osi:F3}; OSI formula at 500: {Osi(120.0, 250, 500):F3}");

        Assert.Equal(1.4, osi, 9);
        Assert.Equal(osi, big, 9);
    }

    [Fact]
    public void ForAFixedFractionMissingTheChanceNeverFallsAsThePoolGrows()
    {
        foreach (var skill in new[] { 50.0, 100.0, 120.0 })
        {
            foreach (var missing in new[] { 0.1, 0.25, 0.5, 1.0 })
            {
                var previous = double.MaxValue;
                for (var pool = 150; pool <= 2000; pool += 50)
                {
                    var mana = pool - (int)(pool * missing);
                    var chance = Meditation.StartChance(skill, mana, pool);
                    // 0.02 allows for the whole-mana rounding of the deficit, at most 3 / pool.
                    Assert.True(previous == double.MaxValue || chance >= previous - 0.02,
                        $"Med {skill}, {missing:P0} missing: pool {pool} chance {chance:F3}, previous {previous:F3}");
                    previous = chance;
                }
            }
        }
    }
}
