// CivicContractPointsVerification.cs
//
// cc-P414: civic waste bundles are retired; the Custodians' Civic Contracts count Clean Up points instead
// (customizations ClusterFWorkOrderSystem.cs, GuildContractLedgerGump.cs, ClusterFCustodianSystem.cs, SanitationWarden.cs,
// CleanedDebris.cs). Notes in shard-migration notes/cc-P414-retire-civic-waste-bundles.md.
//
// Facts:
//   1. Cleaning drops no bundles: a trash bag dump of valued items pays Clean Up points and puts no CleanedDebris in the
//      pack (it used to give 1 per 5 valued items).
//   2. A Civic Contract fills from Clean Up points earned after it was taken and not before; turning it in pays the
//      same gold as before and takes no points.
//   3. The same points never fill two open contracts: a turn-in claims its points from the others.
//   4. An old bundle stack handed to the Sanitation Warden turns in once for Clean Up points (10 a bundle) and is deleted.
//   5. A save with bundles in a backpack loads: the bundle stack comes back whole (the type and its serialization stay).
//   6. A contract taken before cc-P414 (a version 1 entry, no baseline) loads and is finished with points earned from the
//      first look on, not with older points; a version 2 entry keeps its baseline through a save.
//   7. No vendor item can be bought and cleaned up to fill a Civic Contract for profit: every vendor's buy list, priced
//      at its base price (no price scalar), costs more gold per Clean Up point than any Civic Contract pays (P33's open item 2).
//   8. Not a rule: the sizing's measurement (pinned's loot packs, points per valued item), printed for the notes.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Accounting;
using Server.Engines.Points;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CivicContractPointsVerification
{
    private static readonly Point3D Spot = new(1432, 1770, 0);

    private readonly ITestOutputHelper _out;

    public CivicContractPointsVerification(ITestOutputHelper output)
    {
        _out = output;
        CleanUpCraftHost.EnsureCraftSystems();
        CleanUpCraftHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        ClusterFWorkOrderSystem.Configure();
    }

    private sealed class Member : IDisposable
    {
        public readonly Account Account = new($"p414{Guid.NewGuid():N}"[..16], "p414-test-only");
        public readonly PlayerMobile Pm;

        public Member()
        {
            Pm = new PlayerMobile { Player = true };
            Pm.AddItem(new Backpack());
            Pm.RawStr = 100;
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);
            ClusterFGuildSystem.Join(Pm, ClusterFGuildSystem.GetDef("custodians"));
            SanitationWarden.OnCustodiansJoined(Pm);
        }

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreateGuild(Pm);

        public double Lifetime => CleanUpBritanniaData.Instance.GetLifetimePoints(Pm);

        public int Gold => Pm.Backpack.GetAmount(typeof(Gold));

        public void Dispose()
        {
            foreach (var bag in TrashBag.OwnedBy(Pm))
            {
                bag.Delete();
            }

            CleanUpBritanniaData.Instance.RemoveEntry(Pm);
            Pm.Delete();
        }
    }

    private static WorkOrderDef Order(string key) => ClusterFWorkOrderSystem.Get(key)!;

    // -- 1 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void CleaningDropsNoBundles()
    {
        using var a = new Member();
        var bag = TrashBag.OwnedBy(a.Pm).Single();

        for (var i = 0; i < 10; i++)
        {
            bag.DropItem(new Longsword()); // 1.2 points each: ten valued items, two bundles before cc-P414
        }

        var paid = TrashBagGump.DumpBag(a.Pm, bag);

        Assert.True(paid > 0, $"paid {paid}");
        Assert.Null(a.Pm.Backpack.FindItemByType<CleanedDebris>());
        Assert.Null(typeof(ClusterFCustodianSystem).GetMethod("GiveBundles", BindingFlags.Public | BindingFlags.Static));
    }

    // -- 2 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void AContractFillsFromPointsEarnedAfterItWasTakenAndNotBefore()
    {
        using var a = new Member();
        var def = Order("custodians.refuse_collection");
        Assert.Equal(50, Assert.IsType<WorkOrderCleanUpRequirement>(Assert.Single(def.Requirements)).Amount);

        ClusterFCustodianSystem.AwardPoints(a.Pm, 40); // before the contract: these never count
        var entry = ClusterFWorkOrderSystem.Accept(a.Pm, a.Guild, def);
        Assert.Equal(a.Lifetime, entry.CleanUpBaseline);
        Assert.False(def.RequirementsMet(a.Pm, entry));

        ClusterFCustodianSystem.AwardPoints(a.Pm, 49.5);
        Assert.False(def.RequirementsMet(a.Pm, entry));
        Assert.Equal(49.5, ClusterFWorkOrderSystem.CleanUpProgress(a.Pm, entry), 9);

        ClusterFCustodianSystem.AwardPoints(a.Pm, 0.5);
        Assert.True(def.RequirementsMet(a.Pm, entry));

        var points = CleanUpBritanniaData.Instance.GetPoints(a.Pm);
        var lifetime = a.Lifetime;
        var gold = a.Gold;
        Assert.True(GuildContractLedgerGump.TurnIn(a.Pm, a.Guild, "custodians", entry, true));
        Assert.Equal(gold + 100, a.Gold);
        Assert.Equal(points, CleanUpBritanniaData.Instance.GetPoints(a.Pm), 9);   // nothing taken: still spendable
        Assert.Equal(lifetime, a.Lifetime, 9);
        Assert.DoesNotContain(entry, a.Guild.ActiveWorkOrders);
        Assert.Contains(entry, a.Guild.CompletedWorkOrders);
    }

    // -- 3 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSamePointsNeverFillTwoContracts()
    {
        using var a = new Member();
        var small = Order("custodians.refuse_collection");    // 50
        var larger = Order("custodians.sanitation_detail");   // 150
        var first = ClusterFWorkOrderSystem.Accept(a.Pm, a.Guild, small);
        var second = ClusterFWorkOrderSystem.Accept(a.Pm, a.Guild, larger);

        ClusterFCustodianSystem.AwardPoints(a.Pm, 150);
        Assert.True(small.RequirementsMet(a.Pm, first));
        Assert.True(larger.RequirementsMet(a.Pm, second));

        Assert.True(GuildContractLedgerGump.TurnIn(a.Pm, a.Guild, "custodians", first, true));
        Assert.Equal(100, ClusterFWorkOrderSystem.CleanUpProgress(a.Pm, second), 9); // the 50 the small one took are gone
        Assert.False(larger.RequirementsMet(a.Pm, second));

        ClusterFCustodianSystem.AwardPoints(a.Pm, 50);
        Assert.True(larger.RequirementsMet(a.Pm, second));
    }

    // -- 4 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnOldBundleTurnsInOnceForPointsAndIsDeleted()
    {
        using var a = new Member();
        var warden = new SanitationWarden();
        try
        {
            warden.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
            var bundles = new CleanedDebris { Amount = 3 };
            a.Pm.Backpack.DropItem(bundles);
            var before = a.Lifetime;

            Assert.True(warden.OnDragDrop(a.Pm, bundles));
            Assert.True(bundles.Deleted);
            Assert.Equal(before + 3 * ClusterFCustodianSystem.PointsPerBundle, a.Lifetime, 9);
            Assert.Equal(10.0, ClusterFCustodianSystem.PointsPerBundle);

            // Once: the same stack pays nothing again.
            Assert.Equal(0, ClusterFCustodianSystem.RedeemBundles(a.Pm, bundles));
            Assert.Equal(before + 30, a.Lifetime, 9);
            Assert.Null(a.Pm.Backpack.FindItemByType<CleanedDebris>());
        }
        finally
        {
            warden.Delete();
        }
    }

    // -- 5 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void ASaveWithBundlesInABackpackLoads()
    {
        using var a = new Member();
        var bundles = new CleanedDebris { Amount = 7 };
        a.Pm.Backpack.DropItem(bundles);

        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        bundles.Serialize(writer);
        writer.Flush();

        var copy = new CleanedDebris(bundles.Serial);
        try
        {
            copy.Deserialize(new BufferReader(buffer));
            Assert.False(copy.Deleted);
            Assert.Equal(7, copy.Amount);
            Assert.Equal("civic waste bundle", copy.Name);
            Assert.True(copy.Stackable);
        }
        finally
        {
            copy.Delete();
            bundles.Delete();
        }
    }

    // -- 6 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void AContractTakenBeforeTheChangeIsFinishedWithPointsFromTheFirstLook()
    {
        using var a = new Member();
        ClusterFCustodianSystem.AwardPoints(a.Pm, 60); // earned before the update: does not count

        // A version 1 entry, as the pre-cc-P414 build wrote it (WorkOrderEntry.Serialize, version 1).
        var buffer = new byte[4096];
        var writer = new BufferWriter(buffer, true);
        writer.Write(1);
        writer.Write("custodians.refuse_collection");
        writer.Write("custodians");
        writer.Write(DateTime.UtcNow.AddDays(-2));
        writer.Write(false);
        writer.Write(0);
        writer.Flush();

        var old = new WorkOrderEntry(new BufferReader(buffer));
        Assert.Null(old.CleanUpBaseline);
        a.Guild.ActiveWorkOrders.Add(old);
        var def = Order(old.DefKey);

        Assert.False(def.RequirementsMet(a.Pm, old));
        Assert.Equal(a.Lifetime, old.CleanUpBaseline);
        ClusterFCustodianSystem.AwardPoints(a.Pm, 50);
        Assert.True(def.RequirementsMet(a.Pm, old));

        // Version 2 keeps the baseline.
        var buffer2 = new byte[4096];
        var writer2 = new BufferWriter(buffer2, true);
        old.Serialize(writer2);
        writer2.Flush();
        var back = new WorkOrderEntry(new BufferReader(buffer2));
        Assert.Equal(old.CleanUpBaseline, back.CleanUpBaseline);
        Assert.Equal(old.DefKey, back.DefKey);
    }

    // -- 7 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void NoVendorItemFillsACivicContractForProfit()
    {
        // The most gold a Civic Contract pays for one Clean Up point.
        var civic = ClusterFWorkOrderSystem.GetForGuild("custodians").ToList();
        Assert.Equal(5, civic.Count);
        var goldPerPoint = civic.Max(d => (double)d.GoldReward / d.Requirements.OfType<WorkOrderCleanUpRequirement>().Sum(r => r.Amount));
        _out.WriteLine($"most gold per Clean Up point a Civic Contract pays: {goldPerPoint:0.##}");

        var rows = new List<(string Vendor, string Item, int Price, double Points)>();
        var skipped = 0;
        var sbTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(asm =>
            {
                try { return (IEnumerable<Type>)asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
            })
            .Where(t => t is { IsAbstract: false } && typeof(SBInfo).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) != null)
            .ToList();

        foreach (var sbType in sbTypes)
        {
            List<GenericBuyInfo> buy;
            try
            {
                buy = ((SBInfo)Activator.CreateInstance(sbType)!).BuyInfo;
            }
            catch
            {
                skipped++;
                continue;
            }

            foreach (var info in buy ?? [])
            {
                if (info?.Type == null || !typeof(Item).IsAssignableFrom(info.Type) || info.Price <= 0)
                {
                    continue;
                }

                Item item = null;
                try
                {
                    item = info.GetEntity() as Item;
                    if (item == null)
                    {
                        continue;
                    }

                    var points = CleanUpBritanniaData.GetPoints(item) / Math.Max(1, item.Amount);
                    if (points > 0)
                    {
                        rows.Add((sbType.Name, info.Type.Name, info.Price, points));
                    }
                }
                catch
                {
                    skipped++;
                }
                finally
                {
                    item?.Delete();
                }
            }
        }

        var worst = rows.OrderBy(r => r.Price / r.Points).Take(10).ToList();
        _out.WriteLine($"{sbTypes.Count} vendor lists, {rows.Count} priced items with Clean Up value, {skipped} skipped");
        foreach (var r in worst)
        {
            _out.WriteLine($"  {r.Item} from {r.Vendor}: {r.Price} gp for {r.Points:0.###} points = {r.Price / r.Points:0.##} gp a point");
        }

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.True(r.Price / r.Points > goldPerPoint,
            $"{r.Item} from {r.Vendor}: {r.Price} gp for {r.Points} points beats a Civic Contract's {goldPerPoint:0.##} gp a point"));
    }

    // -- 8: the sizing, measured -----------------------------------------------------------------------------------

    /// <summary>
    /// Not a rule: the measurement behind the sizing (notes, section "Sizing"). Each of pinned's loot packs (the era's
    /// Poor to FilthyRich, what monster corpses carry) rolled 300 times into a bag: the average Clean Up points of an
    /// item with value (gold left out: Custodians never clean gold), and of one roll's whole contents (a corpse, which
    /// counted as one valued item towards a bundle, before its creature's fame base).
    /// </summary>
    [Fact]
    public void SizingProbeLootPointsPerValuedItem()
    {
        var killer = new PlayerMobile { Player = true };
        try
        {
            (string Name, LootPack Pack)[] packs =
            [
                ("Poor", LootPack.Poor), ("Meager", LootPack.Meager), ("Average", LootPack.Average),
                ("Rich", LootPack.Rich), ("FilthyRich", LootPack.FilthyRich)
            ];

            var allValued = 0;
            foreach (var (name, pack) in packs)
            {
                double itemPoints = 0, rollPoints = 0;
                int valued = 0, rollsWithValue = 0;
                for (var i = 0; i < 300; i++)
                {
                    var bag = new Bag();
                    try
                    {
                        pack.Generate(killer, bag, false, 0);
                        double roll = 0;
                        foreach (var item in bag.Items.ToList())
                        {
                            if (item is Gold)
                            {
                                continue;
                            }

                            var points = CleanUpBritanniaData.GetPoints(item);
                            if (points > 0)
                            {
                                itemPoints += points;
                                valued++;
                                roll += points;
                            }
                        }

                        if (roll > 0)
                        {
                            rollPoints += roll;
                            rollsWithValue++;
                        }
                    }
                    finally
                    {
                        bag.Delete();
                    }
                }

                allValued += valued;
                _out.WriteLine(
                    $"{name}: {valued} valued items in 300 rolls, {itemPoints / Math.Max(1, valued):0.##} points each; " +
                    $"a roll with value holds {rollPoints / Math.Max(1, rollsWithValue):0.##} points ({rollsWithValue} of 300)");
            }

            Assert.True(allValued > 0);
        }
        finally
        {
            killer.Delete();
        }
    }
}
