// cc-P199: the three trap training kits (ServUO Items/Functional -> customizations/Items/Functional/), the circuit gump
// (customizations/Gumps/Traps/CircuitTrap.cs), their two patches (RemoveTrap-trap-training-kits, PuzzleChest-trap-
// training-kit) and the Collector of Oddities who sells them.
//
// Facts:
//   1. Each kit is ServUO's: art 41875, its label, movable; the cylinder kit holds no treasure; a new slider puzzle is a
//      solvable shuffle of its style's eight tiles.
//   2. At Endless Journey, Remove Trap with no Lockpicking and no Detect Hidden still gives a target, and targeting a
//      circuit kit opens its gump with a nine-square grid.
//   3. The circuit grid is 9 below 80 Remove Trap, 16 below 100, 25 from 100.
//   4. Walking the circuit path to the end rolls Remove Trap at 0-100 and clears the path; a wrong step resets progress.
//   5. A slider puzzle one move from solved: the move rolls Remove Trap at 0-100 and shuffles a new puzzle; a tile that
//      is not beside the gap does not move.
//   6. The cylinder kit: the right cylinders roll Remove Trap at 0-100 and set a new solution; a wrong guess hurts no one.
//   7. The three kits survive a serialization round trip with every saved field set.
//   8. The Collector of Oddities sells the three kits at ServUO's 99,375; his placement is a dry run first, then places
//      him once at Trammel 4305,1003.
//   9. The three pinned files carry their patched lines in the build tree (Enhance.cs: the forged metal's aura).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.SkillHandlers;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TrapTrainingKitVerification
{
    private static readonly Point3D Spot = new(1400, 1720, 0);

    public TrapTrainingKitVerification()
    {
        ShardTestHost.EnsureSkillChecks();
    }

    private static PlayerMobile NewPlayer()
    {
        ShardTestClock.Arm();
        var pm = new PlayerMobile { Player = true, Race = Race.Human, RawStr = 100, RawDex = 100, RawInt = 100 };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Spot, Map.Trammel);
        pm.Hits = pm.HitsMax;
        return pm;
    }

    // Records every targeted skill roll; returns success as asked.
    private sealed class SkillRolls : IDisposable
    {
        private readonly SkillCheckTargetHandler _old = Mobile.SkillCheckTargetHandler;
        public readonly List<(SkillName Skill, double Min, double Max)> Rolls = new();

        public SkillRolls()
        {
            Mobile.SkillCheckTargetHandler = (_, skill, _, min, max) =>
            {
                Rolls.Add((skill, min, max));
                return true;
            };
        }

        public void Dispose() => Mobile.SkillCheckTargetHandler = _old;
    }

    private static RelayInfo Button(int id) => new(id, default, default, default, default);

    [Fact]
    public void EachKitIsServUOs()
    {
        var circuit = new CircuitTrapTrainingKit();
        var cylinder = new CylinderTrapTrainingKit();
        var slider = new SliderTrapTrainingKit();

        try
        {
            Assert.Equal(41875, circuit.ItemID);
            Assert.Equal(41875, cylinder.ItemID);
            Assert.Equal(41875, slider.ItemID);
            Assert.Equal(1159014, circuit.LabelNumber);
            Assert.Equal(1159015, cylinder.LabelNumber);
            Assert.Equal(1159016, slider.LabelNumber);
            Assert.True(circuit.Movable && cylinder.Movable && slider.Movable);

            Assert.Equal(1159005, circuit.GumpTitle);
            Assert.Equal(1159006, circuit.GumpDescription);
            Assert.False(circuit.CanDecipher);
            Assert.Equal(1159017, cylinder.Title);

            Assert.Empty(cylinder.Items);
            Assert.NotNull(cylinder.Solution);

            for (var n = 0; n < 50; n++)
            {
                slider.Style = n % 8;
                slider.Reset();
                var start = 0x9CEE + slider.Style * 9;
                var tiles = slider.Order.Where(t => t != 0).OrderBy(t => t).ToArray();

                Assert.Equal(8, tiles.Length);
                Assert.Equal(Enumerable.Range(start, 8), tiles);
                Assert.Single(slider.Order, 0);

                var inversions = 0;
                for (var i = 0; i < 8; i++)
                {
                    for (var j = i + 1; j < 9; j++)
                    {
                        if (slider.Order[i] != 0 && slider.Order[j] != 0 && slider.Order[i] > slider.Order[j])
                        {
                            inversions++;
                        }
                    }
                }

                Assert.True(inversions % 2 == 0, $"style {slider.Style}: an odd shuffle cannot be solved");
            }
        }
        finally
        {
            circuit.Delete();
            cylinder.Delete();
            slider.Delete();
        }
    }

    [Fact]
    public void RemoveTrapNeedsNeitherLockpickingNorDetectHiddenAndOpensTheCircuit()
    {
        Assert.True(Core.EJ, "the shard runs Endless Journey");

        var pm = NewPlayer();
        using var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        var kit = new CircuitTrapTrainingKit();

        try
        {
            pm.Backpack.DropItem(kit);
            Assert.Equal(0, pm.Skills.Lockpicking.Base);
            Assert.Equal(0, pm.Skills.DetectHidden.Base);

            RemoveTrap.OnUse(pm);
            Assert.NotNull(pm.Target);

            pm.Target.Invoke(pm, kit);

            Assert.Equal(CircuitCount.Nine, kit.Count);
            Assert.True(pm.HasGump<CircuitTrapGump>());
            Assert.NotEmpty(kit.Path);
            Assert.Equal(0, kit.Path[0]);
            Assert.Equal(8, kit.Path[^1]);
            Assert.Equal(new[] { 0 }, kit.Progress);
        }
        finally
        {
            ns.Mobile = null;
            pm.NetState = null;
            kit.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void TheCircuitGridFollowsRemoveTrap()
    {
        var pm = NewPlayer();
        var kit = new CircuitTrapTrainingKit();

        try
        {
            (double Skill, CircuitCount Count)[] cases =
            [
                (0.0, CircuitCount.Nine), (79.9, CircuitCount.Nine), (80.0, CircuitCount.Sixteen),
                (99.9, CircuitCount.Sixteen), (100.0, CircuitCount.TwentyFive), (120.0, CircuitCount.TwentyFive)
            ];

            foreach (var (skill, count) in cases)
            {
                pm.Skills.RemoveTrap.Base = skill;
                kit.Path.Clear();
                kit.OnRemoveTrap(pm);
                Assert.Equal(count, kit.Count);
            }

            // A path in progress keeps its grid whatever the skill is now.
            kit.Path.AddRange(new[] { 0, 1, 2, 5, 8 });
            pm.Skills.RemoveTrap.Base = 0;
            kit.OnRemoveTrap(pm);
            Assert.Equal(CircuitCount.TwentyFive, kit.Count);
        }
        finally
        {
            kit.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void WalkingTheCircuitRollsRemoveTrapAndAWrongStepResets()
    {
        var pm = NewPlayer();
        var kit = new CircuitTrapTrainingKit();
        using var rolls = new SkillRolls();

        try
        {
            kit.OnRemoveTrap(pm); // Nine at 0 skill
            kit.Path = new List<int> { 0, 1, 2, 5, 8 };
            kit.Progress = new List<int> { 0 };
            var gump = new CircuitTrapGump(pm, kit);

            gump.OnResponse(null, Button(2)); // right, to 1
            gump.OnResponse(null, Button(2)); // right, to 2
            Assert.Equal(new[] { 0, 1, 2 }, kit.Progress);

            gump.OnResponse(null, Button(4)); // left, back to 1: not the path
            Assert.Empty(kit.Progress);
            Assert.Empty(rolls.Rolls);

            kit.Progress = new List<int> { 0 };
            gump.OnResponse(null, Button(1)); // up, off the board: nothing
            Assert.Equal(new[] { 0 }, kit.Progress);

            gump.OnResponse(null, Button(2));
            gump.OnResponse(null, Button(2));
            gump.OnResponse(null, Button(3)); // down, to 5
            Assert.Equal(new[] { 0, 1, 2, 5 }, kit.Progress);

            gump.OnResponse(null, Button(3)); // down, to 8: the last square
            Assert.Equal(new[] { (SkillName.RemoveTrap, 0.0, 100.0) }, rolls.Rolls);
            Assert.Empty(kit.Path);
            Assert.Empty(kit.Progress);

            Assert.All(CircuitTrapGump.PathsFor(CircuitCount.Sixteen), p => Assert.Equal(15, p[^1]));
            Assert.All(CircuitTrapGump.PathsFor(CircuitCount.TwentyFive), p => Assert.Equal(24, p[^1]));
            Assert.Equal(9, CircuitTrapGump.PathsFor(CircuitCount.Nine).Length);
        }
        finally
        {
            kit.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void TheSliderOneMoveFromSolvedRollsRemoveTrap()
    {
        var pm = NewPlayer();
        var kit = new SliderTrapTrainingKit { Style = 3 };
        using var rolls = new SkillRolls();

        try
        {
            pm.Backpack.DropItem(kit);
            kit.Test = true; // ServUO's test layout: solved but for the first tile and the gap
            var start = 0x9CEE + 3 * 9;
            Assert.Equal(0, kit.Order[1]);
            Assert.Equal(start, kit.Order[0]);

            var gump = new SliderTrapGump(pm, kit);

            gump.OnResponse(null, Button(9)); // the bottom-right tile is not beside the gap
            Assert.Equal(0, kit.Order[1]);
            Assert.Empty(rolls.Rolls);

            gump.OnResponse(null, Button(1)); // the top-left tile slides into the gap: solved
            Assert.Equal(new[] { (SkillName.RemoveTrap, 0.0, 100.0) }, rolls.Rolls);
            Assert.Equal(8, kit.Order.Count(t => t != 0)); // shuffled again

            Assert.True(SliderTrapGump.ValidMove(4, 1));
            Assert.True(SliderTrapGump.ValidMove(4, 7));
            Assert.False(SliderTrapGump.ValidMove(4, 0));
            Assert.False(SliderTrapGump.ValidMove(2, 3));
        }
        finally
        {
            kit.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void TheCylinderKitTrainsWithoutHurting()
    {
        var pm = NewPlayer();
        var kit = new CylinderTrapTrainingKit();
        using var rolls = new SkillRolls();

        try
        {
            pm.Backpack.DropItem(kit);
            var hits = pm.Hits;

            var wrong = new PuzzleChestSolution(kit.Solution);
            wrong.Cylinders[0] = wrong.Cylinders[0] == PuzzleChestCylinder.Red ? PuzzleChestCylinder.Blue : PuzzleChestCylinder.Red;

            kit.SubmitSolution(pm, wrong);
            Assert.Equal(hits, pm.Hits);
            Assert.False(pm.Poisoned);
            Assert.Empty(rolls.Rolls);
            Assert.NotNull(kit.GetLastGuess(pm));

            var old = kit.Solution;
            kit.SubmitSolution(pm, new PuzzleChestSolution(old));
            Assert.Equal(new[] { (SkillName.RemoveTrap, 0.0, 100.0) }, rolls.Rolls);
            Assert.NotSame(old, kit.Solution);
            Assert.Empty(kit.Items);
            Assert.False(kit.CheckLocked(pm));
        }
        finally
        {
            kit.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void TheKitsSurviveARoundTrip()
    {
        ShardTestClock.Arm();
        var circuit = new CircuitTrapTrainingKit();
        var slider = new SliderTrapTrainingKit { Style = 5 };
        var cylinder = new CylinderTrapTrainingKit();

        var pm = NewPlayer();
        pm.Skills.RemoveTrap.Base = 85;
        circuit.OnRemoveTrap(pm);
        circuit.Path = new List<int> { 0, 4, 8, 12, 13, 14, 15 };
        circuit.Progress = new List<int> { 0, 4 };
        var order = slider.Order.ToArray();
        var cylinders = cylinder.Solution.Cylinders.ToArray();

        try
        {
            var circuitCopy = ConsumablesVerification.RoundTrip(circuit, s => new CircuitTrapTrainingKit(s));
            Assert.Equal(CircuitCount.Sixteen, circuitCopy.Count);
            Assert.Equal(new[] { 0, 4, 8, 12, 13, 14, 15 }, circuitCopy.Path);
            Assert.Equal(new[] { 0, 4 }, circuitCopy.Progress);

            var sliderCopy = ConsumablesVerification.RoundTrip(slider, s => new SliderTrapTrainingKit(s));
            Assert.Equal(5, sliderCopy.Style);
            Assert.Equal(order, sliderCopy.Order);

            var cylinderCopy = ConsumablesVerification.RoundTrip(cylinder, s => new CylinderTrapTrainingKit(s));
            Assert.Equal(cylinders, cylinderCopy.Solution.Cylinders);

            circuitCopy.Delete();
            sliderCopy.Delete();
            cylinderCopy.Delete();
        }
        finally
        {
            circuit.Delete();
            slider.Delete();
            cylinder.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void TheCollectorSellsTheKitsAndIsPlacedOnce()
    {
        var info = new SBCollectorOfOddities().BuyInfo;
        Assert.Equal(
            new[] { typeof(CircuitTrapTrainingKit), typeof(CylinderTrapTrainingKit), typeof(SliderTrapTrainingKit) },
            info.Select(i => i.Type).ToArray()
        );
        Assert.All(info, i => Assert.Equal(99375, i.Price));
        Assert.All(info, i => Assert.Equal(41875, i.ItemID));
        Assert.Equal(new[] { "1159014", "1159015", "1159016" }, info.Select(i => i.Name).ToArray());

        var placed = new List<Mobile>();

        try
        {
            var dry = ClusterFCollectorOfOdditiesPlacement.Place(Map.Trammel, true, _ => true);
            Assert.Contains("would place at (4305, 1003, 0)", dry);
            Assert.Empty(Collectors());

            var real = ClusterFCollectorOfOdditiesPlacement.Place(Map.Trammel, false, _ => true);
            Assert.Contains("placed at (4305, 1003, 0)", real);
            placed.AddRange(Collectors());
            var collector = Assert.Single(placed);
            Assert.Equal(ClusterFCollectorOfOdditiesPlacement.Spot, collector.Location);
            Assert.Equal(5, ((BaseCreature)collector).RangeHome);

            var again = ClusterFCollectorOfOdditiesPlacement.Place(Map.Trammel, false, _ => true);
            Assert.Contains("already at", again);

            var blocked = ClusterFCollectorOfOdditiesPlacement.Place(Map.Felucca, false, _ => false);
            Assert.Contains("cannot hold a mobile", blocked);
        }
        finally
        {
            foreach (var m in placed)
            {
                m.Delete();
            }
        }
    }

    private static List<Mobile> Collectors()
    {
        var found = new List<Mobile>();

        foreach (var m in Map.Trammel.GetMobilesInRange(ClusterFCollectorOfOdditiesPlacement.Spot, 15))
        {
            if (m is CollectorOfOddities)
            {
                found.Add(m);
            }
        }

        return found;
    }

    [Fact]
    public void TheThreePinnedFilesCarryTheirPatchedLinesInTheBuildTree()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Projects", "UOContent")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, $"no Projects/UOContent above {AppContext.BaseDirectory}");
        var root = Path.Combine(dir.FullName, "Projects", "UOContent");

        var expected = new (string File, string Line)[]
        {
            ("Skills/RemoveTrap.cs", "else if (targeted is IRemoveTrapTrainingKit trainingKit)"),
            ("Skills/RemoveTrap.cs", "if (!Core.EJ && m.Skills.Lockpicking.Value < 50)"),
            ("Skills/RemoveTrap.cs", "else if (!Core.EJ && m.Skills.DetectHidden.Value < 50)"),
            ("Engines/Khaldun/PuzzleChest.cs", "OnWrongGuess(m);"),
            ("Engines/Khaldun/PuzzleChest.cs", "public void SendPuzzleGump(Mobile from)"),
            ("Engines/Craft/Core/Enhance.cs", "ForgedMetalOfArtifacts.GetNextEnhanceSuccess(auraUser)")
        };

        foreach (var (file, line) in expected)
        {
            var text = File.ReadAllText(Path.Combine(root, file));
            Assert.True(text.Contains(line, StringComparison.Ordinal), $"{file} lacks '{line}': its cc-P199 patch did not apply");
        }
    }
}
