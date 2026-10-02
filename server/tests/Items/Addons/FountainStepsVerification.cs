// FountainStepsVerification.cs
//
// cc-P29, Part D (bug-list D14): ClusterFSeedFountainSteps lays walkable stepping stones over the rocks in the
// Fountain of Fortune's pool, so a player can reach coin range on foot. Notes in shard-migration
// notes/cc-P29-defect-batch-2.md, Part D.
//
// The test host has no map files and no item tiledata, so it cannot walk the path. The walk is
// notes/cc-P29-tools/steps_walk.py, a port of pinned's movement rules over the client data; only a client can
// confirm it. What is tested here is the placement and the range.
//
// Facts:
//   1. A dry run places nothing and says it would place all twelve.
//   2. Run twice, it places exactly twelve stones, one on each step tile at z -41, unmovable, of the paver art; the
//      second run places none. The steps form one chain from the shore (1122, 966) to the end tile.
//   3. A player standing on the end tile (1121, 959, -40) throws a LuckyCoin at the fountain through the coin's own
//      target and reaches its success path; the end tile is within the coin's range of 3 of a component.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class FountainStepsVerification
{
    private static readonly Point3D OsiPoint = new(1121, 957, -42);
    private static readonly Point2D Shore = new(1122, 966);

    private readonly ITestOutputHelper _out;

    public FountainStepsVerification(ITestOutputHelper output) => _out = output;

    private static List<Item> StepsNearThePool() =>
        World.Items.Values
            .Where(i => !i.Deleted && i.Map == Map.TerMur && ClusterFFountainStepsSeeder.IsStep(i) &&
                        Utility.InRange(i.Location, OsiPoint, 12))
            .ToList();

    private static void Clean()
    {
        foreach (var s in StepsNearThePool())
        {
            s.Delete();
        }

        foreach (var f in World.Items.Values.OfType<FountainOfFortune>()
                     .Where(f => !f.Deleted && f.Map == Map.TerMur && Utility.InRange(f.Location, OsiPoint, 10))
                     .ToList())
        {
            f.Delete();
        }
    }

    private static int Chebyshev(Point2D a, Point2D b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    [Fact]
    public void ADryRunPlacesNothing()
    {
        try
        {
            Clean();

            var report = ClusterFFountainStepsSeeder.Seed(dryRun: true);
            _out.WriteLine(string.Join("\n", report));

            Assert.Empty(StepsNearThePool());
            Assert.Contains(report, l => l.Contains("would place 12, already there 0"));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void RunTwiceItLaysTwelveStonesInOneChain()
    {
        try
        {
            Clean();

            var first = ClusterFFountainStepsSeeder.Seed(dryRun: false);
            var second = ClusterFFountainStepsSeeder.Seed(dryRun: false);
            _out.WriteLine(string.Join("\n", first.Concat(second)));

            var steps = StepsNearThePool();
            Assert.Equal(12, steps.Count);
            Assert.Contains(first, l => l.Contains("placed 12, already there 0"));
            Assert.Contains(second, l => l.Contains("placed 0, already there 12"));

            foreach (var p in ClusterFFountainStepsSeeder.Steps)
            {
                var stone = Assert.Single(steps, s => s.X == p.X && s.Y == p.Y);
                Assert.Equal(ClusterFFountainStepsSeeder.Z, stone.Z);
                Assert.False(stone.Movable);
                Assert.Contains(stone.ItemID, ClusterFFountainStepsSeeder.Art);
            }

            // One chain: every step is next to the shore or to an earlier step.
            var reached = new List<Point2D> { Shore };
            foreach (var p in ClusterFFountainStepsSeeder.Steps)
            {
                Assert.True(reached.Any(r => Chebyshev(r, p) == 1), $"step {p} is not next to the shore or an earlier step");
                reached.Add(p);
            }
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void ALuckyCoinThrownFromTheEndStoneReachesTheSuccessPath()
    {
        PlayerMobile pm = null;

        try
        {
            Clean();
            ClusterFFountainOfFortuneSeeder.Seed(dryRun: false);
            ClusterFFountainStepsSeeder.Seed(dryRun: false);

            var fountain = World.Items.Values.OfType<FountainOfFortune>().Single(f => !f.Deleted && f.Location == OsiPoint);
            var end = ClusterFFountainStepsSeeder.Steps[^1];
            var target = fountain.Components.OrderBy(c => Chebyshev(new Point2D(c.X, c.Y), end)).First();
            var distance = Chebyshev(new Point2D(target.X, target.Y), end);
            _out.WriteLine($"end stone {end}, nearest component {target.Location}, distance {distance}");
            Assert.True(distance <= 3, $"the end stone is {distance} from the fountain");

            pm = new PlayerMobile { Player = true };
            pm.AddItem(new Backpack());
            pm.MoveToWorld(new Point3D(end.X, end.Y, ClusterFFountainStepsSeeder.Z + 1), Map.TerMur);

            var coin = new LuckyCoin(2);
            pm.Backpack.DropItem(coin);
            Assert.False(fountain.IsCoolingDown(pm));

            coin.OnDoubleClick(pm);
            Assert.NotNull(pm.Target);
            pm.Target.Invoke(pm, target);

            _out.WriteLine($"from {pm.Location}: coin.Amount={coin.Amount} cooling={fountain.IsCoolingDown(pm)}");
            Assert.Equal(1, coin.Amount);
            Assert.True(fountain.IsCoolingDown(pm));
        }
        finally
        {
            pm?.Delete();
            Clean();
        }
    }
}
