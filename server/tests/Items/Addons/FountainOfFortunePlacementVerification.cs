// FountainOfFortunePlacementVerification.cs
//
// cc-P27, Part A (bug-list D14, Q-021): ClusterFSeedFountainOfFortune places the Fountain of Fortune at OSI's point,
// Ter Mur 1121, 957, -42. Notes in shard-migration notes/cc-P27-defect-batch-1.md.
//
// Facts:
//   1. A dry run places nothing and says it would place.
//   2. Run twice, the seeder places exactly one fountain, at the point, on Ter Mur; the second run places none. A
//      fountain already a few tiles away counts as present.
//   3. A LuckyCoin used on a placed fountain reaches its success path (a coin is spent, the daily cooldown is set),
//      thrown from the pool's edge at 1117, 952: the nearest shore tile, three tiles from the fountain's corner.
//      The existing facts call FountainOfFortune.OnTarget directly; this one goes through the coin's own target.

using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class FountainOfFortunePlacementVerification
{
    private static readonly Point3D OsiPoint = new(1121, 957, -42);

    // Standable shore tile nearest the fountain (notes/cc-P27-tools/fountain_fit.py and fountain_reach.py): three
    // tiles from the component at offset -2, -2 (1119, 955), the coin's target range.
    private static readonly Point3D Shore = new(1117, 952, -42);

    private readonly ITestOutputHelper _out;

    public FountainOfFortunePlacementVerification(ITestOutputHelper output) => _out = output;

    private static List<FountainOfFortune> FountainsNearThePoint() =>
        World.Items.Values.OfType<FountainOfFortune>()
            .Where(f => !f.Deleted && f.Map == Map.TerMur && Utility.InRange(f.Location, OsiPoint, 10))
            .ToList();

    private static void DeleteFountainsNearThePoint()
    {
        foreach (var f in FountainsNearThePoint())
        {
            f.Delete();
        }
    }

    [Fact]
    public void ADryRunPlacesNothing()
    {
        try
        {
            Assert.Empty(FountainsNearThePoint());

            var report = ClusterFFountainOfFortuneSeeder.Seed(dryRun: true);
            _out.WriteLine(string.Join("\n", report));

            Assert.Empty(FountainsNearThePoint());
            Assert.Contains(report, l => l.Contains("would place") && l.Contains("(1121, 957, -42)"));
        }
        finally
        {
            DeleteFountainsNearThePoint();
        }
    }

    [Fact]
    public void RunTwiceItPlacesOneFountainAtTheOsiPoint()
    {
        try
        {
            Assert.Empty(FountainsNearThePoint());

            var first = ClusterFFountainOfFortuneSeeder.Seed(dryRun: false);
            var second = ClusterFFountainOfFortuneSeeder.Seed(dryRun: false);
            _out.WriteLine(string.Join("\n", first.Concat(second)));

            var fountain = Assert.Single(FountainsNearThePoint());
            Assert.Equal(OsiPoint, fountain.Location);
            Assert.Equal(Map.TerMur, fountain.Map);
            Assert.Equal(16, fountain.Components.Count);
            Assert.All(fountain.Components, c => Assert.Equal(Map.TerMur, c.Map));
            Assert.Contains(first, l => l.Contains("placed at"));
            Assert.Contains(second, l => l.Contains("already there"));

            // One a few tiles away, placed by hand, counts as present too.
            fountain.Delete();
            var nearby = new FountainOfFortune();
            nearby.MoveToWorld(new Point3D(OsiPoint.X + 3, OsiPoint.Y - 2, OsiPoint.Z), Map.TerMur);

            var third = ClusterFFountainOfFortuneSeeder.Seed(dryRun: false);
            _out.WriteLine(string.Join("\n", third));

            Assert.Same(nearby, Assert.Single(FountainsNearThePoint()));
            Assert.Contains(third, l => l.Contains("already there"));
        }
        finally
        {
            DeleteFountainsNearThePoint();
        }
    }

    [Fact]
    public void ALuckyCoinThrownFromTheShoreReachesTheSuccessPath()
    {
        PlayerMobile pm = null;

        try
        {
            ClusterFFountainOfFortuneSeeder.Seed(dryRun: false);
            var fountain = Assert.Single(FountainsNearThePoint());

            var corner = fountain.Components.Single(c => c.Location == new Point3D(1119, 955, -42));

            pm = new PlayerMobile { Player = true };
            pm.AddItem(new Backpack());
            pm.MoveToWorld(Shore, Map.TerMur);

            var coin = new LuckyCoin(2);
            pm.Backpack.DropItem(coin);

            Assert.False(fountain.IsCoolingDown(pm));

            coin.OnDoubleClick(pm);
            Assert.NotNull(pm.Target);
            pm.Target.Invoke(pm, corner);

            _out.WriteLine($"from {pm.Location} at {corner.Location}: coin.Amount={coin.Amount} " +
                           $"cooling={fountain.IsCoolingDown(pm)}");

            // The failure path ("That is not sacred waters", out of range, out of sight) spends nothing and sets
            // no cooldown. Only the fountain's OnTarget does both.
            Assert.Equal(1, coin.Amount);
            Assert.True(fountain.IsCoolingDown(pm));
        }
        finally
        {
            pm?.Delete();
            DeleteFountainsNearThePoint();
        }
    }
}
