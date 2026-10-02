// CleanUpBritanniaBarrelVerification.cs
//
// cc-P33 (F-3), stage 2: trash containers and "Appraise for Cleanup", ported from ServUO pub57
// Items/Containers/{BaseTrash,CleanupTrashBarrel,TrashBarrel,TrashChest}.cs. Ours: CleanupTrashBarrel and the
// bookkeeping in customizations Services/CleanUpBritannia/CleanUpTrash.cs; pinned's TrashBarrel and TrashChest reach it
// through server/patches/Trash{Barrel,Chest}-clean-up-britannia.patch. Notes in shard-migration
// notes/cc-P33-clean-up-britannia.md.
//
// Facts:
//   1. Appraise for Cleanup reports the number the barrel pays: for each item, the appraisal's value is what the
//      character's points rise by when the item goes in a Clean Up barrel, and the cliloc follows ServUO's four cases.
//   2. A Clean Up barrel refuses an item with no turn-in value (it stays where it was) and pays for one with value at
//      once.
//   3. Pinned's trash barrel (the patch) records each drop, pays nothing until it empties, then pays each dropper for
//      their own items.
//   4. Pinned's trash chest (the patch) pays at once, and refuses a blessed item with no value instead of destroying it.
//   5. All three offer "Appraise for Cleanup" (cliloc 1151298) on their context menu.

using System.Linq;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Points;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CleanUpBritanniaBarrelVerification
{
    private static readonly Point3D Spot = new(1410, 1750, 0);

    private readonly ITestOutputHelper _out;

    public CleanUpBritanniaBarrelVerification(ITestOutputHelper output)
    {
        _out = output;
        CleanUpCraftHost.EnsureCraftSystems();
    }

    private static PlayerMobile Player(int dx = 0)
    {
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y, Spot.Z), Map.Trammel);
        return pm;
    }

    private static T Place<T>(T container) where T : Item
    {
        container.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, Spot.Z), Map.Trammel);
        return container;
    }

    private static double Points(Mobile m) => CleanUpBritanniaData.Instance.GetPoints(m);

    // -- 1 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void AppraiseReportsTheNumberTheBarrelPays()
    {
        var pm = Player();
        var barrel = Place(new CleanupTrashBarrel());

        try
        {
            Item[] items =
            [
                new ValoriteIngot(7),                                   // 70
                new PlateChest { Resource = CraftResource.Agapite },    // 25 x 5 = 125
                new PowerScroll(SkillName.Magery, 110),                 // 100
                new Gold(250),                                          // 2.5
                new Lockpick(10),                                       // 1
                new IronIngot(3)                                        // 0.3
            ];

            foreach (var item in items)
            {
                pm.Backpack.DropItem(item);
                var (number, args) = AppraiseforCleanupTarget.Appraise(item);
                var expected = CleanUpBritanniaData.GetPoints(item);

                var before = Points(pm);
                Assert.True(barrel.OnDragDrop(pm, item), $"{item.GetType().Name} refused");
                var paid = Points(pm) - before;

                _out.WriteLine($"{item.GetType().Name}: appraised {number} '{args}', barrel paid {paid}");
                Assert.True(item.Deleted);
                Assert.Equal(expected, paid, 9);

                switch (number)
                {
                    case 1151274: Assert.Equal(paid.ToString(), args); Assert.True(paid > 1); break;
                    case 1151273: Assert.Equal(1.0, paid, 9); break;
                    case 1151272: Assert.True(paid is > 0 and < 1); break;
                    default: Assert.Fail($"unexpected cliloc {number}"); break;
                }
            }

            Assert.Equal((1151271, ""), AppraiseforCleanupTarget.Appraise(new Candle()));
        }
        finally
        {
            barrel.Delete();
            pm.Delete();
        }
    }

    // -- 2 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void ACleanUpBarrelRefusesWhatHasNoValueAndPaysAtOnce()
    {
        var pm = Player();
        var barrel = Place(new CleanupTrashBarrel());

        try
        {
            var candle = new Candle();
            pm.Backpack.DropItem(candle);
            Assert.False(barrel.OnDragDrop(pm, candle));
            Assert.False(candle.Deleted);

            var ingots = new ValoriteIngot(2);
            pm.Backpack.DropItem(ingots);
            var before = Points(pm);
            Assert.True(barrel.OnDragDrop(pm, ingots));

            Assert.True(ingots.Deleted);
            Assert.Empty(barrel.Items);
            Assert.Equal(20.0, Points(pm) - before, 9);
            Assert.Equal(0, CleanUpTrash.PendingCount(barrel));
        }
        finally
        {
            barrel.Delete();
            pm.Delete();
        }
    }

    // -- 3 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void PinnedsTrashBarrelPaysEachDropperWhenItEmpties()
    {
        var a = Player();
        var b = Player(1);
        var barrel = Place(new TrashBarrel());

        try
        {
            var aBefore = Points(a);
            var bBefore = Points(b);

            var aItem = new BronzeIngot(10); // 15
            var bItem = new Diamond(5);      // 1.5
            var junk = new Candle();         // nothing
            a.Backpack.DropItem(aItem);
            b.Backpack.DropItem(bItem);
            a.Backpack.DropItem(junk);

            Assert.True(barrel.OnDragDrop(a, aItem));
            Assert.True(barrel.OnDragDrop(b, bItem));
            Assert.True(barrel.OnDragDrop(a, junk)); // a plain barrel takes anything, as pinned's does

            // Nothing paid yet: pinned's barrel empties after three minutes or at 50 items.
            Assert.Equal(aBefore, Points(a), 9);
            Assert.Equal(bBefore, Points(b), 9);
            Assert.Equal(2, CleanUpTrash.PendingCount(barrel));

            barrel.Empty(501479); // Emptying the trashcan!

            Assert.True(aItem.Deleted && bItem.Deleted && junk.Deleted);
            Assert.Equal(15.0, Points(a) - aBefore, 9);
            Assert.Equal(1.5, Points(b) - bBefore, 9);
            Assert.Equal(0, CleanUpTrash.PendingCount(barrel));
        }
        finally
        {
            barrel.Delete();
            a.Delete();
            b.Delete();
        }
    }

    // -- 4 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void PinnedsTrashChestPaysAtOnceAndRefusesBlessedJunk()
    {
        var pm = Player();
        var chest = Place(new TrashChest());

        try
        {
            var ingots = new GoldIngot(4); // 10
            pm.Backpack.DropItem(ingots);
            var before = Points(pm);
            Assert.True(chest.OnDragDrop(pm, ingots));
            Assert.True(ingots.Deleted);
            Assert.Equal(10.0, Points(pm) - before, 9);

            var blessed = new Candle { LootType = LootType.Blessed };
            pm.Backpack.DropItem(blessed);
            Assert.False(chest.OnDragDrop(pm, blessed));
            Assert.False(blessed.Deleted);

            // A plain item with no value is still thrown away, as pinned's chest does.
            var junk = new Candle();
            pm.Backpack.DropItem(junk);
            Assert.True(chest.OnDragDrop(pm, junk));
            Assert.True(junk.Deleted);
        }
        finally
        {
            chest.Delete();
            pm.Delete();
        }
    }

    // -- 5 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void EveryTrashContainerOffersAppraiseForCleanup()
    {
        var pm = Player();
        Container[] containers = [new CleanupTrashBarrel(), new TrashBarrel(), new TrashChest()];

        try
        {
            foreach (var container in containers)
            {
                Place(container);
                var list = PooledRefList<ContextMenuEntry>.Create();

                try
                {
                    container.GetContextMenuEntries(pm, ref list);
                    var numbers = new System.Collections.Generic.List<int>();
                    for (var i = 0; i < list.Count; i++)
                    {
                        numbers.Add(list[i].Number);
                    }

                    _out.WriteLine($"{container.GetType().Name}: {string.Join(", ", numbers)}");
                    Assert.Contains(1151298, numbers);
                }
                finally
                {
                    list.Dispose();
                }
            }
        }
        finally
        {
            foreach (var c in containers)
            {
                c.Delete();
            }

            pm.Delete();
        }
    }
}
