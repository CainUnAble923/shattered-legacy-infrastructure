// SalvageBagIngotReturnVerification.cs
//
// cc-P23, D42: the Smith Guild salvage bag returned more ingots than the item cost. Our bag copies
// pinned's return, ((4 + Mining) * cost - 4) * 0.0068 (Items/Containers/SalvageBag.cs:90-108), with
// pinned's Mining clamp of 100 removed. Unclamped it passes the item's cost at Mining about 143 and
// gives 51 ingots for a 25-ingot item at 300. The fix clamps the result to cost - 1 instead of the
// skill. Notes in shard-migration notes/cc-P23-live-defect-sweep.md.
//
// Facts (the brief's numbering):
//   1. At Mining 100 a 25-ingot player-made item returns fewer than 25.
//   2. At Mining 300 the same item returns at most 24. The regression test for the dupe.
//   3. At Mining 300 a 2-ingot item returns at least pinned's 2 and never more than its cost.
//   4. An item not player-made returns the flat 2.
// 1, 2 and 4 run the real salvage path (TryResmelt) on a real PlateChest, 25 ingots in pinned
// DefBlacksmithy.cs:202. No blacksmith recipe costs 2 ingots, so 3 calls the return directly.

using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SalvageBagIngotReturnVerification
{
    private readonly ITestOutputHelper _out;

    public SalvageBagIngotReturnVerification(ITestOutputHelper output)
    {
        _out = output;

        if (DefBlacksmithy.CraftSystem == null)
        {
            DefBlacksmithy.Initialize();
        }

        BlacksmithyCraftRegistrations.Register();
    }

    private static int RecipeCost<T>() where T : Item =>
        DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(T)).Resources[0].Amount;

    // Salvages one iron PlateChest through the bag at `mining` and returns the ingots it gave back.
    private int Salvage(double mining, bool playerMade)
    {
        var pm = new PlayerMobile();
        pm.AddItem(new Backpack());
        pm.Skills.Mining.Cap = 300.0;
        pm.Skills.Mining.Base = mining;

        var bag = new SmithGuildSalvageBag();
        pm.Backpack.DropItem(bag);

        var chest = new PlateChest { PlayerConstructed = playerMade };
        bag.DropItem(chest);

        try
        {
            Assert.True(bag.TryResmelt(pm, chest, CraftResource.Iron));
            Assert.True(chest.Deleted);

            var ingots = pm.Backpack.GetAmount(typeof(IronIngot));
            _out.WriteLine($"Mining {mining}, player-made {playerMade}: {ingots} ingots back");
            return ingots;
        }
        finally
        {
            chest.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void TheTestItemCostsTwentyFiveIngots()
    {
        Assert.Equal(25, RecipeCost<PlateChest>());
    }

    [Fact]
    public void AtMining100ATwentyFiveIngotItemReturnsFewerThan25()
    {
        var back = Salvage(100.0, true);
        Assert.True(back < 25, $"{back}");
        Assert.Equal(17, back); // ((4 + 100) * 25 - 4) * 0.0068 = 17.65, pinned's own answer
    }

    [Fact]
    public void AtMining300ATwentyFiveIngotItemReturnsAtMost24()
    {
        var back = Salvage(300.0, true);
        Assert.True(back <= 24, $"returned {back} ingots for a 25-ingot item: the D42 dupe");
    }

    [Fact]
    public void NoMiningValueReturnsAsMuchAsTheItemCost()
    {
        // Every recipe the bag can resmelt, at every Mining from 0 to 400 in steps of 0.1.
        var item = new PlateChest { PlayerConstructed = true };

        try
        {
            var costs = DefBlacksmithy.CraftSystem.CraftItems
                .Select(c => c.Resources.Count > 0 ? c.Resources[0].Amount : 0)
                .Where(a => a >= 2)
                .Distinct()
                .OrderBy(a => a)
                .ToList();

            _out.WriteLine($"blacksmith recipe costs the bag can see: {string.Join(", ", costs)}");

            foreach (var cost in costs)
            {
                for (var fixedPoint = 0; fixedPoint <= 4000; fixedPoint++)
                {
                    var back = SmithGuildSalvageBag.IngotReturn(item, fixedPoint / 10.0, cost);
                    var ceiling = cost == 2 ? 2 : cost - 1;
                    Assert.True(back <= ceiling, $"cost {cost} at Mining {fixedPoint / 10.0} returned {back}");
                }
            }
        }
        finally
        {
            item.Delete();
        }
    }

    [Fact]
    public void AtMining300ATwoIngotItemReturnsPinnedsMinimumAndNeverMoreThanItsCost()
    {
        var item = new PlateChest { PlayerConstructed = true };

        try
        {
            var back = SmithGuildSalvageBag.IngotReturn(item, 300.0, 2);
            _out.WriteLine($"2-ingot item at Mining 300: {back}");
            Assert.True(back >= SmithGuildSalvageBag.MinimumIngotReturn, $"{back}");
            Assert.True(back <= 2, $"{back}");
        }
        finally
        {
            item.Delete();
        }
    }

    [Fact]
    public void AnItemNotPlayerMadeReturnsTheFlatTwo()
    {
        Assert.Equal(2, Salvage(300.0, false));
        Assert.Equal(2, Salvage(100.0, false));
    }
}
