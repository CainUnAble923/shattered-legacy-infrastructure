// CleanUpBritanniaStoreVerification.cs
//
// cc-P33 (F-3), stage 3: the one Clean Up Britannia store (customizations Services/CleanUpBritannia/
// CleanUpBritanniaRewards.cs), opened by the Cleanup Officer (TheCleanupOfficer.cs, ported) and the Sanitation Warden
// (SanitationWardenGump.cs). Notes in shard-migration notes/cc-P33-clean-up-britannia.md.
//
// Facts:
//   1. OSI's section is the 49 of ServUO's 134 rewards that exist here, at ServUO's prices
//      (Services/CleanUpBritannia/CleanUpBritanniaRewards.cs), by price tier, sorted by price.
//   2. Ours, after OSI's: 100 bandages for 15, 5 refresh potions for 3, 3 greater heal potions for 5, sorted by price,
//      and each delivers what it says.
//   3. Buying spends exactly the price and never the lifetime total; too few points buys nothing and changes nothing.
//   4. Both NPCs open the store: the Officer on double-click within 5 tiles, the Warden's gump button within 5 tiles
//      of a Warden or an Officer, and neither from further away.
//   5. [ClusterFPlaceCleanUp: the dry run places nothing, a tile that cannot hold it gets nothing, a real run places
//      one Officer on the caller's tile and one barrel east of it, and a re-run places nothing twice. The test host has
//      no map files, so the tile check is supplied (ClusterFCleanUpPlacement.Place's fits).

using System;
using System.Linq;
using Server;
using Server.Engines.CleanUpBritannia;
using Server.Engines.Points;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CleanUpBritanniaStoreVerification
{
    private static readonly Point3D Spot = new(1420, 1760, 0);

    private readonly ITestOutputHelper _out;

    public CleanUpBritanniaStoreVerification(ITestOutputHelper output) => _out = output;

    private sealed class Shopper : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Shopper()
        {
            Pm = new PlayerMobile { Player = true };
            Pm.AddItem(new Backpack());
            Pm.RawStr = 100;
            Pm.MoveToWorld(Spot, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            CleanUpBritanniaData.Instance.RemoveEntry(Pm);
            Pm.Delete();
        }
    }

    // -- 1 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void OsisSectionIsServUOsAvailableRewardsAtServUOsPricesSortedByPrice()
    {
        var osi = CleanUpBritanniaRewards.SortedOsi;

        Assert.Equal(49, osi.Count);
        Assert.All(osi, r => Assert.False(r.Ours));

        // ServUO's tiers, counted over the 49 that exist here.
        var tiers = osi.GroupBy(r => r.Points).ToDictionary(g => g.Key, g => g.Count());
        _out.WriteLine(string.Join(", ", tiers.Select(t => $"{t.Value} at {t.Key:N0}")));
        Assert.Equal(5, tiers.Count);
        Assert.Equal(27, tiers[10000]);  // Knights 10, Scout 9, Sorcerer 8 (ServUO :45-72)
        Assert.Equal(1, tiers[20000]);   // Scroll of Alacrity (:80)
        Assert.Equal(16, tiers[50000]);  // Bestial 8, Virtuoso 8 (:111-128)
        Assert.Equal(1, tiers[80000]);   // Archery butte (:133)
        Assert.Equal(4, tiers[150000]);  // Novo Bleue, Etoile Bleue, Soleil Rouge, Lune Rouge (:135-138)

        double Price(Type t) => osi.Single(r => r.Type == t).Points;
        Assert.Equal(10000, Price(typeof(KnightsPlateChest)));
        Assert.Equal(10000, Price(typeof(SorcererHat)));
        Assert.Equal(20000, Price(typeof(ScrollofAlacrity)));
        Assert.Equal(50000, Price(typeof(BestialHelm)));
        Assert.Equal(80000, Price(typeof(ArcheryButteDeed)));
        Assert.Equal(150000, Price(typeof(LuneRouge)));

        // Sorted by price, and the store lists OSI's section first.
        for (var i = 1; i < osi.Count; i++)
        {
            Assert.True(osi[i - 1].Points <= osi[i].Points, $"{osi[i - 1].Type.Name} before {osi[i].Type.Name}");
        }

        Assert.Equal(osi, CleanUpBritanniaRewards.Store.Take(49));

        // Every reward builds.
        foreach (var reward in CleanUpBritanniaRewards.Store)
        {
            foreach (var item in reward.Create())
            {
                Assert.IsType(reward.Type, item);
                item.Delete();
            }
        }
    }

    // -- 2 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void OurSuppliesAreLastAtTheApprovedPricesAndDeliverWhatTheySay()
    {
        var ours = CleanUpBritanniaRewards.SortedCustodian;

        Assert.Equal(CleanUpBritanniaRewards.Store.Skip(49).ToArray(), ours.ToArray());
        Assert.Equal([3.0, 5.0, 15.0], ours.Select(r => r.Points).ToArray());
        Assert.All(ours, r => Assert.True(r.Ours));

        using var s = new Shopper();
        CleanUpBritanniaData.Instance.AwardPoints(s.Pm, 23, message: false);

        foreach (var reward in ours)
        {
            Assert.Equal(CleanUpBritanniaRewards.PurchaseResult.Bought, CleanUpBritanniaRewards.Purchase(s.Pm, reward));
        }

        Assert.Equal(0.0, CleanUpBritanniaData.Instance.GetPoints(s.Pm), 9);

        var bandages = s.Pm.Backpack.Items.OfType<Bandage>().ToList();
        Assert.Single(bandages);
        Assert.Equal(100, bandages[0].Amount);
        Assert.Equal(5, s.Pm.Backpack.Items.OfType<RefreshPotion>().Count());
        Assert.Equal(3, s.Pm.Backpack.Items.OfType<GreaterHealPotion>().Count());
    }

    // -- 3 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void BuyingSpendsExactlyThePriceAndNeverTheLifetime()
    {
        using var s = new Shopper();
        var cub = CleanUpBritanniaData.Instance;
        var scroll = CleanUpBritanniaRewards.SortedOsi.Single(r => r.Type == typeof(ScrollofAlacrity));

        cub.AwardPoints(s.Pm, 20000, message: false);
        Assert.Equal(CleanUpBritanniaRewards.PurchaseResult.Bought, CleanUpBritanniaRewards.Purchase(s.Pm, scroll));

        Assert.Equal(0.0, cub.GetPoints(s.Pm), 9);
        Assert.Equal(20000.0, cub.GetLifetimePoints(s.Pm), 9);
        Assert.Single(s.Pm.Backpack.Items.OfType<ScrollofAlacrity>());

        var count = s.Pm.Backpack.Items.Count;
        Assert.Equal(CleanUpBritanniaRewards.PurchaseResult.NotEnoughPoints, CleanUpBritanniaRewards.Purchase(s.Pm, scroll));
        Assert.Equal(count, s.Pm.Backpack.Items.Count);
        Assert.Equal(0.0, cub.GetPoints(s.Pm), 9);
    }

    // -- 4 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void BothNpcsOpenTheStoreAndNeitherFromAfar()
    {
        using var s = new Shopper();
        var officer = new TheCleanupOfficer();
        var warden = new SanitationWarden();

        try
        {
            officer.MoveToWorld(new Point3D(Spot.X + 2, Spot.Y, Spot.Z), Map.Trammel);
            officer.OnDoubleClick(s.Pm);
            Assert.NotNull(s.Pm.FindGump<CleanUpBritanniaRewardGump>());
            s.Pm.CloseGump<CleanUpBritanniaRewardGump>();

            // The Warden's button finds the Officer near...
            Assert.True(SanitationWardenGump.OpenStore(s.Pm));
            s.Pm.CloseGump<CleanUpBritanniaRewardGump>();

            // ...and a Warden alone.
            officer.Internalize();
            warden.MoveToWorld(new Point3D(Spot.X, Spot.Y + 3, Spot.Z), Map.Trammel);
            Assert.True(SanitationWardenGump.OpenStore(s.Pm));
            Assert.NotNull(s.Pm.FindGump<CleanUpBritanniaRewardGump>());
            s.Pm.CloseGump<CleanUpBritanniaRewardGump>();

            // From afar: nothing opens.
            warden.MoveToWorld(new Point3D(Spot.X + 20, Spot.Y, Spot.Z), Map.Trammel);
            officer.MoveToWorld(new Point3D(Spot.X + 20, Spot.Y + 1, Spot.Z), Map.Trammel);
            Assert.False(SanitationWardenGump.OpenStore(s.Pm));
            officer.OnDoubleClick(s.Pm);
            Assert.Null(s.Pm.FindGump<CleanUpBritanniaRewardGump>());
        }
        finally
        {
            officer.Delete();
            warden.Delete();
        }
    }

    // -- 5 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void ThePlacementCommandsDryRunPlacesNothingAndARerunPlacesNothingTwice()
    {
        var at = new Point3D(1440, 1780, 0);
        var map = Map.Trammel;

        System.Collections.Generic.List<IEntity> Placed()
        {
            var found = new System.Collections.Generic.List<IEntity>();
            foreach (var m in map.GetMobilesInRange(at, 20))
            {
                if (m is TheCleanupOfficer) found.Add(m);
            }

            foreach (var i in map.GetItemsInRange(at, 20))
            {
                if (i is CleanupTrashBarrel) found.Add(i);
            }

            return found;
        }

        try
        {
            var dry = ClusterFCleanUpPlacement.Place(map, at, true, _ => true);
            _out.WriteLine(dry);
            Assert.Contains("would place", dry);
            Assert.Empty(Placed());

            var blocked = ClusterFCleanUpPlacement.Place(map, at, false, _ => false);
            _out.WriteLine(blocked);
            Assert.Contains("cannot hold", blocked);
            Assert.Empty(Placed());

            var real = ClusterFCleanUpPlacement.Place(map, at, false, _ => true);
            _out.WriteLine(real);
            var placed = Placed();
            Assert.Equal(2, placed.Count);
            Assert.Contains(placed, e => e is TheCleanupOfficer && e.Location == at);
            Assert.Contains(placed, e => e is CleanupTrashBarrel && e.Location == new Point3D(at.X + 1, at.Y, at.Z));

            var again = ClusterFCleanUpPlacement.Place(map, at, false, _ => true);
            _out.WriteLine(again);
            Assert.Contains("already at", again);
            Assert.Equal(2, Placed().Count);
        }
        finally
        {
            foreach (var e in Placed())
            {
                e.Delete();
            }
        }
    }
}
