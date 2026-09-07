// Proves the test route can construct creatures - stock ModernUO ones and ported ones - with
// nothing in the test but the collection attribute.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// THE DEFECT THIS EXISTS FOR (Q-026, found by S5):
//
//   Every public BaseCreature constructor calls NPCSpeeds.GetSpeeds
//   (Projects/UOContent/Mobiles/BaseCreature.cs:344), which falls back to
//   _speedsByLevel[SpeedLevel.Medium] and throws KeyNotFoundException while the speed table is
//   empty. The real server fills the table from Data/npc-speeds.json through
//   AssemblyHandler.Invoke("Configure") (Projects/Server/Main.cs:441). UOContentFixture calls a
//   hand-picked subset of Configure methods and NPCSpeeds is not among them, so before S8 no
//   test could build a creature at all. ModernUO's own tests work around it by going through the
//   Serial deserialization constructor - see the comment at
//   UOContent.Tests/Tests/Engines/Pathing/BitmapAStarAlgorithmTests.cs:356 - which is not
//   available to a test that needs a real creature.
//
//   Q-026 recorded the cause as "the file is not under Core.BaseDirectory in the test host".
//   That was wrong and it is worth being exact about, because it is the difference between a
//   one-line fix and a build-system change: UOContent.Tests.csproj already copies
//   Distribution/Data into the output directory, and Core.BaseDirectory in the test host IS
//   that directory. Measured 2026-09-07: 272 files including npc-speeds.json. The table was
//   there all along; nothing ever asked for it.
//
//   The route's fix is server/patches/UOContentFixture-npc-speeds.patch, one call added to the
//   upstream fixture. See notes/s8-test-route.md section 2 for why it is a patch and not a
//   helper of ours.
//
//   apply-patches.sh FAILS THE BUILD for a test file that registers speeds by hand, so S5's
//   workaround cannot come back as the example people copy.

using System;
using Server;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CreatureConstructionVerification
{
    private readonly ITestOutputHelper _out;

    public CreatureConstructionVerification(ITestOutputHelper output) => _out = output;

    [Fact]
    public void AStockModernUoCreatureConstructs()
    {
        // No scaffolding above this line. That is the whole assertion: before S8 this threw
        // KeyNotFoundException in the constructor.
        var wolf = new GreyWolf();

        _out.WriteLine($"{wolf.GetType().Name}: active={wolf.ActiveSpeed} passive={wolf.PassiveSpeed}");

        Assert.NotNull(wolf);
        Assert.True(wolf.ActiveSpeed > 0, "the speed table did not reach the constructor");

        wolf.Delete();
    }

    [Fact]
    public void APortedCreatureConstructs()
    {
        // CC3's Eodon port. Not in any "types" list in npc-speeds.json, so it takes the
        // SpeedLevel.Medium fallback - the exact lookup that threw before S8.
        var saurosaurus = new Saurosaurus();

        _out.WriteLine($"{saurosaurus.GetType().Name}: active={saurosaurus.ActiveSpeed} " +
                       $"passive={saurosaurus.PassiveSpeed} hits={saurosaurus.HitsMax}");

        Assert.Equal(0.25, saurosaurus.ActiveSpeed);
        Assert.Equal(0.5, saurosaurus.PassiveSpeed);

        // S5's quest creature, from a different port, through the same route.
        var minion = new MinionOfScelestus();
        Assert.Equal(0.25, minion.ActiveSpeed);

        saurosaurus.Delete();
        minion.Delete();
    }

    [Fact]
    public void TheRealSpeedTableIsLoaded_NotAHandRegisteredMediumRow()
    {
        // This is the assertion that separates the route's fix from S5's workaround, and it is
        // the reason the workaround had to go rather than merely being moved somewhere shared.
        //
        // S5 registered one row - SpeedLevel.Medium, 0.25/0.5, an empty type set - which is
        // enough to stop the constructor throwing and wrong for every creature the real table
        // classifies differently. Data/npc-speeds.json registers 351 creature types across four
        // levels. A Rat is Slow and an AirElemental is Fast, and under the workaround
        // both would have silently come out Medium.
        //
        // Counted from pinned Distribution/Data/npc-speeds.json: five rows, and 351 type names
        // across four of them - VerySlow 0, Slow 35, Medium 219, Fast 70, VeryFast 27.
        var rat = new Rat();
        var wolf = new GreyWolf();
        var air = new AirElemental();

        _out.WriteLine($"Rat {rat.ActiveSpeed}/{rat.PassiveSpeed}  " +
                       $"GreyWolf {wolf.ActiveSpeed}/{wolf.PassiveSpeed}  " +
                       $"AirElemental {air.ActiveSpeed}/{air.PassiveSpeed}");

        // Values verbatim from pinned Distribution/Data/npc-speeds.json.
        Assert.Equal(0.3, rat.ActiveSpeed);    // Slow
        Assert.Equal(0.6, rat.PassiveSpeed);

        Assert.Equal(0.25, wolf.ActiveSpeed);  // Medium, by type rather than by fallback
        Assert.Equal(0.5, wolf.PassiveSpeed);

        Assert.Equal(0.2, air.ActiveSpeed);    // Fast
        Assert.Equal(0.4, air.PassiveSpeed);

        rat.Delete();
        wolf.Delete();
        air.Delete();
    }

    [Fact]
    public void AConstructedCreatureIsUsableAndNotJustConstructible()
    {
        // Constructing is not the bar; CC6 needs creatures that can be placed, damaged and
        // killed. Drive enough of that to prove the object is real.
        var wolf = new GreyWolf
        {
            Map = Map.Felucca,
            Location = new Point3D(1000, 1000, 0)
        };

        Assert.True(wolf.HitsMax > 0);
        Assert.False(wolf.Deleted);
        Assert.Equal(Map.Felucca, wolf.Map);

        var before = wolf.Hits;
        wolf.Damage(5, null);

        _out.WriteLine($"GreyWolf hits {before} -> {wolf.Hits} of {wolf.HitsMax}, alive={wolf.Alive}");

        Assert.Equal(before - 5, wolf.Hits);
        Assert.True(wolf.Alive);

        wolf.Delete();
        Assert.True(wolf.Deleted);
    }
}
