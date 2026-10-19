// CleanUpBritanniaStoreVerification.cs
//
// cc-P33 (F-3), stage 3: the one Clean Up Britannia store (customizations Services/CleanUpBritannia/
// CleanUpBritanniaRewards.cs), opened by the Cleanup Officer (TheCleanupOfficer.cs, ported) and the Sanitation Warden
// (SanitationWardenGump.cs). Notes in shard-migration notes/cc-P33-clean-up-britannia.md.
//
// Facts:
//   1. OSI's section is the 109 of ServUO's 134 rewards that exist here (49 from cc-P33, 73 ported by cc-P175), at
//      ServUO's prices
//      (Services/CleanUpBritannia/CleanUpBritanniaRewards.cs), by price tier, sorted by price.
//   2. Ours, after OSI's: 100 bandages for 15, 5 refresh potions for 3, 3 greater heal potions for 5, sorted by price,
//      and each delivers what it says.
//   3. Buying spends exactly the price and never the lifetime total; too few points buys nothing and changes nothing.
//   4. Both NPCs open the store: the Officer on double-click within 5 tiles, the Warden's gump button within 5 tiles
//      of a Warden or an Officer, and neither from further away.
//   5. [ClusterFPlaceCleanUp: the dry run places nothing, a tile that cannot hold it gets nothing, a real run places
//      one Officer on the caller's tile and one barrel east of it, and a re-run places nothing twice. The test host has
//      no map files, so the tile check is supplied (ClusterFCleanUpPlacement.Place's fits).
//   6. cc-P175 Part C: each of the 73 rewards cc-P175 ported is in the store at OSI's price (ServUO
//      CleanUpBritanniaRewards.cs, the line beside each), can be bought, and survives a save (the reader stops where the
//      writer did, and the item reads back with its art, hue and cliloc).

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

        Assert.Equal(122, osi.Count);
        Assert.All(osi, r => Assert.False(r.Ours));

        // ServUO's tiers, counted over the 122 that exist here.
        var tiers = osi.GroupBy(r => r.Points).ToDictionary(g => g.Key, g => g.Count());
        _out.WriteLine(string.Join(", ", tiers.Select(t => $"{t.Value} at {t.Key:N0}")));
        Assert.Equal(11, tiers.Count);
        Assert.Equal(5, tiers[1000]);    // cc-P175: the five named robes (:18-22)
        Assert.Equal(7, tiers[5000]);    // cc-P175: lily pads 2, mushrooms 4, Nocturne earrings (:24-30)
        Assert.Equal(37, tiers[10000]);  // Knights 10, Scout 9, Sorcerer 8 (ServUO :45-72); cc-P175: Sherry, 9 tiles (:32-42)
        Assert.Equal(3, tiers[15000]);   // cc-P175: yucca, table lamp, bamboo (:75-77)
        Assert.Equal(10, tiers[20000]);  // Scroll of Alacrity (:80); cc-P175: horse barding, boots 5, 3 instruments (:79-90)
        Assert.Equal(22, tiers[50000]);  // Bestial 8, Virtuoso 8 (:111-128); cc-P175: tapestries 2, chests 2, dragon head, nest (:92-98)
        Assert.Equal(2, tiers[75000]);   // cc-P175: fire pit, Presentation Stone (:130-131)
        Assert.Equal(2, tiers[80000]);   // cc-P175: beehive (:132); archery butte (:133)
        Assert.Equal(4, tiers[150000]);  // Novo Bleue, Etoile Bleue, Soleil Rouge, Lune Rouge (:135-138)
        Assert.Equal(24, tiers[250000]); // cc-P175: the 24 pigments (:142-165)
        Assert.Equal(6, tiers[300000]);  // cc-P175: the six charms (:167-172)

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

        Assert.Equal(osi, CleanUpBritanniaRewards.Store.Take(122));

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

        Assert.Equal(CleanUpBritanniaRewards.Store.Skip(122).ToArray(), ours.ToArray());
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

    // -- 6 ---------------------------------------------------------------------------------------------------------

    // ServUO CleanUpBritanniaRewards.cs, line and price, for the 73 cc-P175 ported (36 items, 24 pigments, 13 addons and the hive).
    public static readonly (Type Type, int Line, double Price)[] Ported =
    [
        (typeof(HumansAndElvesRobe), 18, 1000), (typeof(GargoylesAreOurFriendsRobe), 19, 1000), (typeof(WeArePiratesRobe), 20, 1000),
        (typeof(FollowerOfBaneRobe), 21, 1000), (typeof(QueenDawnForeverRobe), 22, 1000), (typeof(LillyPad), 24, 5000),
        (typeof(LillyPads), 25, 5000), (typeof(Mushrooms1), 26, 5000), (typeof(Mushrooms2), 27, 5000), (typeof(Mushrooms3), 28, 5000),
        (typeof(Mushrooms4), 29, 5000), (typeof(NocturneEarrings), 30, 5000), (typeof(SherryTheMouseStatue), 32, 10000),
        (typeof(YuccaTree), 75, 15000), (typeof(TableLamp), 76, 15000), (typeof(Bamboo), 77, 15000), (typeof(SnakeSkinBoots), 82, 20000),
        (typeof(BootsOfTheLavaLizard), 83, 20000), (typeof(BootsOfTheIceWyrm), 84, 20000), (typeof(BootsOfTheCrystalHydra), 85, 20000),
        (typeof(BootsOfTheThrasher), 86, 20000), (typeof(NaturesTears), 88, 20000), (typeof(PrimordialDecay), 89, 20000),
        (typeof(ArachnidDoom), 90, 20000), (typeof(SophisticatedElvenTapestry), 92, 50000), (typeof(OrnateElvenTapestry), 93, 50000),
        (typeof(ChestOfDrawers), 94, 50000), (typeof(FootedChestOfDrawers), 95, 50000), (typeof(NestWithEggs), 98, 50000),
        (typeof(PresentationStone), 131, 75000), (typeof(LuckyCharm), 167, 300000), (typeof(SoldiersMedal), 168, 300000),
        (typeof(DuelistsEdge), 169, 300000), (typeof(NecromancersPhylactery), 170, 300000), (typeof(WizardsCurio), 171, 300000),
        (typeof(MysticsMemento), 172, 300000),
        (typeof(IntenseTealPigment), 142, 250000),
        (typeof(TyrianPurplePigment), 143, 250000),
        (typeof(MottledSunsetBluePigment), 144, 250000),
        (typeof(MossyGreenPigment), 145, 250000),
        (typeof(VibrantOcherPigment), 146, 250000),
        (typeof(OliveGreenPigment), 147, 250000),
        (typeof(PolishedBronzePigment), 148, 250000),
        (typeof(GlossyBluePigment), 149, 250000),
        (typeof(BlackAndGreenPigment), 150, 250000),
        (typeof(DeepVioletPigment), 151, 250000),
        (typeof(AuraOfAmberPigment), 152, 250000),
        (typeof(MurkySeagreenPigment), 153, 250000),
        (typeof(ShadowyBluePigment), 154, 250000),
        (typeof(GleamingFuchsiaPigment), 155, 250000),
        (typeof(GlossyFuchsiaPigment), 156, 250000),
        (typeof(DeepBluePigment), 157, 250000),
        (typeof(VibranSeagreenPigment), 158, 250000),
        (typeof(MurkyAmberPigment), 159, 250000),
        (typeof(VibrantCrimsonPigment), 160, 250000),
        (typeof(ReflectiveShadowPigment), 161, 250000),
        (typeof(StarBluePigment), 162, 250000),
        (typeof(MotherOfPearlPigment), 163, 250000),
        (typeof(LiquidSunshinePigment), 164, 250000),
        (typeof(DarkVoidPigment), 165, 250000),
        (typeof(ChaosTileDeed), 34, 10000),
        (typeof(HonestyVirtueTileDeed), 35, 10000),
        (typeof(CompassionVirtueTileDeed), 36, 10000),
        (typeof(ValorVirtueTileDeed), 37, 10000),
        (typeof(SpiritualityVirtueTileDeed), 38, 10000),
        (typeof(HonorVirtueTileDeed), 39, 10000),
        (typeof(HumilityVirtueTileDeed), 40, 10000),
        (typeof(SacrificeVirtueTileDeed), 41, 10000),
        (typeof(JusticeVirtueTileDeed), 42, 10000),
        (typeof(HorseBardingDeed), 79, 20000),
        (typeof(DragonHeadAddonDeed), 97, 50000),
        (typeof(FirePitDeed), 130, 75000),
        (typeof(Beehive), 132, 80000)
    ];

    [Fact]
    public void EveryPortedRewardIsInTheStoreAtOsisPriceCanBeBoughtAndSurvivesASave()
    {
        Assert.Equal(73, Ported.Length);
        using var s = new Shopper();
        var cub = CleanUpBritanniaData.Instance;

        foreach (var (type, line, price) in Ported)
        {
            var reward = CleanUpBritanniaRewards.SortedOsi.SingleOrDefault(r => r.Type == type);
            Assert.True(reward != null, $"{type.Name} (ServUO :{line}) is not in the store");
            Assert.Equal(price, reward.Points);

            cub.AwardPoints(s.Pm, price, message: false);
            Assert.Equal(CleanUpBritanniaRewards.PurchaseResult.Bought, CleanUpBritanniaRewards.Purchase(s.Pm, reward));
            Assert.Equal(0.0, cub.GetPoints(s.Pm), 9);

            var bought = s.Pm.Backpack.Items.Single(i => i.GetType() == type);
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            bought.Serialize(writer);
            writer.Flush();

            var copy = (Item)Activator.CreateInstance(type, bought.Serial)!;
            var reader = new BufferReader(buffer);
            copy.Deserialize(reader);
            Assert.Equal(writer.Position, reader.Position);
            Assert.Equal(bought.ItemID, copy.ItemID);
            Assert.Equal(bought.Hue, copy.Hue);
            Assert.Equal(bought.LabelNumber, copy.LabelNumber);

            bought.Delete();
        }
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
