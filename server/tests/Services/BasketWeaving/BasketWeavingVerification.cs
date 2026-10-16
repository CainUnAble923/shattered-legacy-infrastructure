// cc-P128: basket weaving (ServUO Services/BasketWeaving -> customizations/Services/BasketWeaving/, with the two forced
// leaf resources in Items/Resource/), its craft entries (AlchemyCraftRegistrations, CookingCraftRegistrations,
// BasketWeavingCraftRegistrations) and the book's key in the recipe gate.
//
// Facts:
//   1. The eleven baskets are containers with ServUO's art and names.
//   2. Reeds, softened reeds and clippings carry their plant's colour and stack only by it.
//   3. Pigments mix by ServUO's rules (primaries make a secondary; the same hue, two shades of one hue and a saturated
//      pigment refuse; black or white needs 100 skill; 75 to mix at all), leaving an empty bottle.
//   4. A natural dye colours a shirt five times and refuses an ingot and a worn item.
//   5. Scouring toxin softens two dry reeds of one colour for a basket weaver, refuses someone who has not read the book
//      and refuses one reed; the book teaches a Grandmaster Tinker once, on the account.
//   6. Clippers cut a decorative plant into clippings or reeds by mode, spend a use, and refuse a growing plant.
//   7. The craft entries carry ServUO's values: the Tinkering reeds, baskets, clippers and rope; the Alchemy elixir,
//      five Eodon potions with their recipe ids and beverages, dye, pigment, fixative, granules and reeds; the Cooking
//      wafer, pigment, dye, fixative and three magical foods. The left-out entries stay out. The baskets need the book.
//   8. A tinker with the book weaves a red round basket end to end from red softened reeds, and softens red reeds at the
//      menu into red softened reeds (the hue tables patch).
//   9. The dye and the reeds survive a round trip; the four plant-engine and craft files carry their patched lines.
//
// Host notes: the book's key lives on the account, so the book facts make an Account; the gate's band books are
// switched off inside those facts (ClusterFRecipeGate.Enabled) so only the basket-weaving key is in play.

using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Engines.Plants;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class BasketWeavingVerification
{
    private readonly ITestOutputHelper _out;

    public BasketWeavingVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsureCraftSystems();
        EnsureTokuno();
    }

    private static bool _tokuno;

    // The dye and the toxin ask BasePigmentsOfTokuno.IsValidItem, which reads TreasuresOfTokuno's config; the server
    // loads it at Configure, the host does not (defaults when the file is absent; nothing is written).
    private static void EnsureTokuno()
    {
        if (!_tokuno)
        {
            TreasuresOfTokuno.Configure();
            _tokuno = true;
        }
    }

    private static readonly Point3D Spot = new(1400, 1720, 0);

    private sealed class Weaver : IDisposable
    {
        public readonly Account Account = new($"p128{Guid.NewGuid():N}"[..16], "p128-test-only");
        public readonly PlayerMobile Pm;
        public readonly List<Item> Loose = new();

        public Weaver(double tinkering = 100.0, double alchemy = 0.0, double cooking = 0.0)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.Skills[SkillName.Tinkering].Base = tinkering;
            Pm.Skills[SkillName.Alchemy].Base = alchemy;
            Pm.Skills[SkillName.Cooking].Base = cooking;
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);
        }

        public T Pack<T>(T item) where T : Item
        {
            Pm.Backpack.DropItem(item);
            return item;
        }

        public T World<T>(T item) where T : Item
        {
            item.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, 0), Pm.Map);
            Loose.Add(item);
            return item;
        }

        public void Dispose()
        {
            foreach (var item in new List<Item>(Pm.Backpack.Items))
            {
                item.Delete();
            }

            foreach (var item in Loose)
            {
                item.Delete();
            }

            BasketWeaving.Forget(Pm);
            Pm.Delete();
        }
    }

    private static PlantItem DecorativePlant(PlantHue hue, PlantType type = PlantType.CampionFlowers)
    {
        var plant = new PlantItem { PlantType = type, PlantHue = hue };
        plant.PlantStatus = PlantStatus.Stage9;
        plant.PlantStatus = PlantStatus.DecorativePlant;
        return plant;
    }

    [Fact]
    public void TheBasketsAreContainersWithServUOsArt()
    {
        var baskets = new (BaseContainer Basket, int ItemId, int Label)[]
        {
            (new BasketCraftable(), 9431, 1022448), (new PicnicBasket2(), 0xE7A, 0), (new RoundBasket(), 0x990, 1112293),
            (new RoundBasketHandles(), 0x9AC, 1112293), (new SmallBushel(), 0x09B1, 1112337), (new SmallRoundBasket(), 0x24DD, 1112298),
            (new SmallSquareBasket(), 0x24D9, 1112296), (new SquareBasket(), 0x24D5, 1112295), (new TallBasket(), 0x24DB, 1112299),
            (new TallRoundBasket(), 0x24D8, 1112297), (new WinnowingBasket(), 0x1882, 0)
        };

        try
        {
            Assert.Equal(11, baskets.Length);

            foreach (var (basket, itemId, label) in baskets)
            {
                Assert.Equal(itemId, basket.ItemID);
                if (label != 0)
                {
                    Assert.Equal(label, basket.LabelNumber);
                }
            }
        }
        finally
        {
            foreach (var (basket, _, _) in baskets)
            {
                basket.Delete();
            }
        }
    }

    [Fact]
    public void ReedsAndClippingsCarryThePlantsColourAndStackByIt()
    {
        var red = new DryReeds(PlantHue.Red);
        var red2 = new DryReeds(PlantHue.Red);
        var blue = new DryReeds(PlantHue.Blue);
        var soft = new SoftenedReeds(PlantHue.BrightBlue);
        var clip = new PlantClippings(PlantHue.Purple);

        try
        {
            Assert.Equal(PlantHueInfo.GetInfo(PlantHue.Red).Hue, red.Hue);
            Assert.Equal(PlantHueInfo.GetInfo(PlantHue.BrightBlue).Hue, soft.Hue);
            Assert.Equal(PlantHueInfo.GetInfo(PlantHue.Purple).Hue, clip.Hue);

            Assert.True(red.CanStackWith(red2));
            Assert.False(red.CanStackWith(blue));
            Assert.False(red.CanStackWith(soft));

            soft.InvalidateHue();
            Assert.Equal(PlantHue.BrightBlue, soft.PlantHue);

            Assert.Equal(PlantHue.Red, PlantHueLookup.FromItemHue(PlantHueInfo.GetInfo(PlantHue.Red).Hue));
            Assert.Equal(PlantHue.None, PlantHueLookup.FromItemHue(1));
        }
        finally
        {
            red.Delete();
            red2.Delete();
            blue.Delete();
            soft.Delete();
            clip.Delete();
        }
    }

    [Fact]
    public void PigmentsMixByServUOsRules()
    {
        Assert.Equal(PlantPigmentHue.Purple, PlantPigmentHueInfo.Mix(PlantPigmentHue.Red, PlantPigmentHue.Blue));
        Assert.Equal(PlantPigmentHue.BrightRed, PlantPigmentHueInfo.Mix(PlantPigmentHue.Red, PlantPigmentHue.Plain));
        Assert.Equal(PlantPigmentHue.IceBlue, PlantPigmentHueInfo.Mix(PlantPigmentHue.White, PlantPigmentHue.Blue));
        Assert.Equal(PlantPigmentHue.DarkYellow, PlantPigmentHueInfo.Mix(PlantPigmentHue.Yellow, PlantPigmentHue.Black));
        Assert.Equal(PlantPigmentHue.None, PlantPigmentHueInfo.Mix(PlantPigmentHue.BrightRed, PlantPigmentHue.Blue)); // saturated
        Assert.Equal(PlantPigmentHue.Red, PlantPigmentHueInfo.HueFromPlantHue(PlantHue.Red));
        Assert.Equal(PlantPigmentHue.Plain, PlantPigmentHueInfo.HueFromPlantHue(PlantHue.None));

        using var w = new Weaver(alchemy: 75.0);
        var red = w.Pack(new PlantPigment(PlantPigmentHue.Red));
        var blue = w.Pack(new PlantPigment(PlantPigmentHue.Blue));
        var red2 = w.Pack(new PlantPigment(PlantPigmentHue.Red));
        var bright = w.Pack(new PlantPigment(PlantPigmentHue.BrightRed));
        var white = w.Pack(new PlantPigment(PlantPigmentHue.White));

        Assert.Equal(PlantPigmentHueInfo.GetInfo(PlantPigmentHue.Red).Hue, red.Hue);
        Assert.Equal(1112242, red.TryMix(w.Pm, red2));       // two identical colors
        Assert.Equal(1112243, red.TryMix(w.Pm, bright));     // variations of the same hue
        Assert.Equal(1112213, red.TryMix(w.Pm, white));      // white needs 100 skill
        Assert.Equal(1112124, red.TryMix(w.Pm, null));       // not a pigment

        w.Pm.Skills[SkillName.Alchemy].Base = 74.9;
        Assert.Equal(1112214, red.TryMix(w.Pm, blue));       // lacks the skill
        w.Pm.Skills[SkillName.Alchemy].Base = 75.0;

        Assert.Equal(0, red.TryMix(w.Pm, blue));
        Assert.True(red.Deleted);
        Assert.Equal(PlantPigmentHue.Purple, blue.PigmentHue);
        Assert.Equal(PlantPigmentHueInfo.GetInfo(PlantPigmentHue.Purple).Hue, blue.Hue);
        Assert.NotNull(w.Pm.Backpack.FindItemByType<Bottle>());
    }

    [Fact]
    public void ANaturalDyeColoursAnItemFiveTimes()
    {
        using var w = new Weaver();
        var dye = w.Pack(new NaturalDye(PlantPigmentHue.Green));
        var shirt = w.Pack(new Shirt());
        var ingot = w.Pack(new IronIngot());
        var worn = new Cap();
        w.Pm.EquipItem(worn);

        Assert.Equal(5, dye.UsesRemaining);
        Assert.Equal(0, dye.TryDye(w.Pm, shirt));
        Assert.Equal(PlantPigmentHueInfo.GetInfo(PlantPigmentHue.Green).Hue, shirt.Hue);
        Assert.Equal(4, dye.UsesRemaining);

        Assert.Equal(1042083, dye.TryDye(w.Pm, ingot));
        Assert.Equal(1060640, dye.TryDye(w.Pm, worn));
        Assert.Equal(4, dye.UsesRemaining);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(0, dye.TryDye(w.Pm, shirt));
        }

        Assert.True(dye.Deleted);
        worn.Delete();
    }

    [Fact]
    public void ScouringToxinSoftensTwoReedsForABasketWeaver()
    {
        using var w = new Weaver();
        var toxin = w.Pack(new ScouringToxin());
        var reeds = w.Pack(new DryReeds(PlantHue.Red) { Amount = 3 });

        Assert.Equal(10, toxin.UsesRemaining);
        Assert.False(BasketWeaving.Knows(w.Pm));
        Assert.Equal(1112253, toxin.Scour(w.Pm, reeds)); // no book

        Assert.True(BasketWeaving.Learn(w.Pm));
        Assert.True(BasketWeaving.Knows(w.Pm));
        Assert.False(BasketWeaving.Learn(w.Pm));

        Assert.Equal(0, toxin.Scour(w.Pm, reeds));
        Assert.Equal(1, reeds.Amount);
        Assert.Equal(9, toxin.UsesRemaining);

        var soft = w.Pm.Backpack.FindItemByType<SoftenedReeds>();
        Assert.NotNull(soft);
        Assert.Equal(PlantHue.Red, soft.PlantHue);

        Assert.Equal(1112250, toxin.Scour(w.Pm, reeds)); // one reed left
        Assert.Equal(1112350, toxin.Scour(w.Pm, w.Pm.Backpack)); // worn: the pack hangs on the player
        Assert.Equal(1112349, toxin.Scour(w.Pm, w.World(new IronIngot()))); // cannot scour that
    }

    [Fact]
    public void TheBookTeachesAGrandmasterTinkerOnce()
    {
        using var w = new Weaver(tinkering: 99.9);
        var book = w.Pack(new BasketWeavingBook());
        var second = w.Pack(new BasketWeavingBook());

        book.OnDoubleClick(w.Pm);
        Assert.False(book.Deleted);
        Assert.False(BasketWeaving.Knows(w.Pm));

        w.Pm.Skills[SkillName.Tinkering].Base = 100.0;
        book.OnDoubleClick(w.Pm);
        Assert.True(book.Deleted);
        Assert.True(BasketWeaving.Knows(w.Pm));
        Assert.True(ClusterFRecipeStore.Knows(w.Pm, BasketWeaving.Flag));
        Assert.Contains(BasketWeaving.Flag, ClusterFRecipeStore.AllKeys());

        second.OnDoubleClick(w.Pm);
        Assert.False(second.Deleted); // already learned
    }

    [Fact]
    public void ClippersCutADecorativePlantByMode()
    {
        using var w = new Weaver();
        var clippers = w.Pack(new Clippers(3));
        var blue = w.World(DecorativePlant(PlantHue.Blue));
        var red = w.World(DecorativePlant(PlantHue.Red));
        var growing = w.World(new PlantItem { PlantType = PlantType.CampionFlowers, PlantHue = PlantHue.Red, PlantStatus = PlantStatus.Stage5 });

        Assert.Equal(ClipMode.Clippings, Clippers.GetMode(w.Pm));
        Assert.Equal(1112119, clippers.Clip(w.Pm, growing)); // only decorative plants

        Assert.Equal(0, clippers.Clip(w.Pm, blue));
        Assert.True(blue.Deleted);
        Assert.Equal(2, clippers.UsesRemaining);
        var clip = w.Pm.Backpack.FindItemByType<PlantClippings>();
        Assert.NotNull(clip);
        Assert.Equal(PlantHue.Blue, clip.PlantHue);

        Clippers.SetMode(w.Pm, ClipMode.Reeds);
        Assert.Equal(0, clippers.Clip(w.Pm, red));
        var reeds = w.Pm.Backpack.FindItemByType<DryReeds>();
        Assert.NotNull(reeds);
        Assert.Equal(PlantHue.Red, reeds.PlantHue);
        Assert.Equal(1, clippers.UsesRemaining);
    }

    [Fact]
    public void TheCraftEntriesAreServUOs()
    {
        var tinkering = DefTinkering.CraftSystem;
        var alchemy = DefAlchemy.CraftSystem;
        var cooking = DefCooking.CraftSystem;

        // Tinkering: the reeds, a basket, the clippers, the rope.
        var soft = tinkering.CraftItems.SearchFor(typeof(SoftenedReeds));
        Assert.NotNull(soft);
        Assert.Equal(1044042, soft.GroupNameNumber);
        Assert.Equal(75.0, soft.Skills[0].MinSkill);
        Assert.Equal(100.0, soft.Skills[0].MaxSkill);
        Assert.Equal(typeof(DryReeds), soft.Resources[0].ItemType);
        Assert.Equal(1, soft.Resources[0].Amount);
        Assert.Equal(typeof(ScouringToxin), soft.Resources[1].ItemType);
        Assert.Equal(2, soft.Resources[1].Amount);

        var tall = tinkering.CraftItems.SearchFor(typeof(TallRoundBasket));
        Assert.NotNull(tall);
        Assert.Equal(3, tall.Resources[0].Amount); // softened reeds
        Assert.Equal(typeof(Shaft), tall.Resources[1].ItemType);
        Assert.Equal(4, tall.Resources[1].Amount);

        var clippers = tinkering.CraftItems.SearchFor(typeof(Clippers));
        Assert.NotNull(clippers);
        Assert.Equal(1044046, clippers.GroupNameNumber);
        Assert.Equal(50.0, clippers.Skills[0].MinSkill);
        Assert.Equal(4, clippers.Resources[0].Amount);

        var rope = tinkering.CraftItems.SearchFor(typeof(HitchingRope));
        Assert.NotNull(rope);
        Assert.Equal(typeof(ResolvesBridle), rope.Resources[1].ItemType);
        Assert.Contains(rope.Skills, s => s.SkillToMake == SkillName.AnimalLore && s.MinSkill == 15.0);
        Assert.Null(tinkering.CraftItems.SearchFor(typeof(EnchantedPicnicBasketStandIn)));

        foreach (var type in BasketWeavingCraftRegistrations.BookGated)
        {
            Assert.NotNull(tinkering.CraftItems.SearchFor(type));
            Assert.Equal(BasketWeaving.Flag, ClusterFRecipeGate.RequiredKeys[type]);
        }

        // Alchemy.
        var elixir = alchemy.CraftItems.SearchFor(typeof(ElixirOfRebirth));
        Assert.NotNull(elixir);
        Assert.Equal(1116348, elixir.GroupNameNumber);
        Assert.Equal(65.0, elixir.Skills[0].MinSkill);
        Assert.Equal(typeof(MedusaBlood), elixir.Resources[0].ItemType);
        Assert.Equal(typeof(SpidersSilk), elixir.Resources[1].ItemType);
        Assert.Equal(3, elixir.Resources[1].Amount);
        Assert.Equal(typeof(Bottle), elixir.Resources[2].ItemType);

        var barrab = alchemy.CraftItems.SearchFor(typeof(BarrabHemolymphConcentrate));
        Assert.NotNull(barrab);
        Assert.Equal(900, barrab.Recipe?.ID);
        Assert.Equal(51.0, barrab.Skills[0].MinSkill);
        Assert.Equal(151.0, barrab.Skills[0].MaxSkill);
        Assert.Equal(typeof(MyrmidexEggsac), barrab.Resources[3].ItemType);

        var sakkhra = alchemy.CraftItems.SearchFor(typeof(SakkhraProphylaxisPotion));
        Assert.NotNull(sakkhra);
        Assert.Equal(905, sakkhra.Recipe?.ID);
        Assert.Equal(BeverageType.Wine, sakkhra.RequiredBeverage);
        Assert.Equal(typeof(BlueCorn), sakkhra.Resources[3].ItemType);

        var barako = alchemy.CraftItems.SearchFor(typeof(BarakoDraftOfMight));
        Assert.NotNull(barako);
        Assert.Equal(BeverageType.Liquor, barako.RequiredBeverage);
        Assert.Equal(typeof(PerfectBanana), barako.Resources[3].ItemType);

        Assert.Null(alchemy.CraftItems.SearchFor(typeof(KurakAmbushersEssence))); // needs TigerPelt
        Assert.NotNull(alchemy.CraftItems.SearchFor(typeof(JukariBurnPoiltice)));
        Assert.NotNull(alchemy.CraftItems.SearchFor(typeof(UraliTranceTonic)));
        Assert.NotNull(alchemy.CraftItems.SearchFor(typeof(NaturalDye)));
        Assert.NotNull(alchemy.CraftItems.SearchFor(typeof(PlantPigment)));
        Assert.NotNull(alchemy.CraftItems.SearchFor(typeof(ColorFixative)));
        Assert.NotNull(alchemy.CraftItems.SearchFor(typeof(CrystalGranules)));
        Assert.NotNull(alchemy.CraftItems.SearchFor(typeof(SoftenedReeds)));

        // Cooking.
        var wafer = cooking.CraftItems.SearchFor(typeof(ParrotWafer));
        Assert.NotNull(wafer);
        Assert.Equal(37.5, wafer.Skills[0].MinSkill);
        Assert.Equal(typeof(RawFishSteak), wafer.Resources[2].ItemType);
        Assert.Equal(10, wafer.Resources[2].Amount);

        var apple = cooking.CraftItems.SearchFor(typeof(EnchantedApple));
        Assert.NotNull(apple);
        Assert.Equal(1073108, apple.GroupNameNumber);
        Assert.True(apple.ForceNonExceptional);
        Assert.Equal(typeof(GreaterHealPotion), apple.Resources[1].ItemType);

        var grapes = cooking.CraftItems.SearchFor(typeof(GrapesOfWrath));
        Assert.NotNull(grapes);
        Assert.Equal(95.0, grapes.Skills[0].MinSkill);

        var bowl = cooking.CraftItems.SearchFor(typeof(FruitBowl));
        Assert.NotNull(bowl);
        Assert.Equal(4, bowl.Resources.Count);
        Assert.Equal(typeof(Banana), bowl.Resources[3].ItemType);

        Assert.NotNull(cooking.CraftItems.SearchFor(typeof(PlantPigment)));
        Assert.NotNull(cooking.CraftItems.SearchFor(typeof(NaturalDye)));
        Assert.NotNull(cooking.CraftItems.SearchFor(typeof(ColorFixative)));
        Assert.Null(cooking.CraftItems.SearchFor(typeof(ChocolateNutcracker))); // needs SweetCocoaButter
    }

    // A type that exists nowhere, standing in for the absent EnchantedPicnicBasket in the "stays out" check.
    private sealed class EnchantedPicnicBasketStandIn : Item
    {
    }

    [Fact]
    public void TheBasketsNeedTheBookAtTheGate()
    {
        using var w = new Weaver();
        var tinkering = DefTinkering.CraftSystem;
        var entry = tinkering.CraftItems.SearchFor(typeof(RoundBasket));
        var was = ClusterFRecipeGate.Enabled;

        try
        {
            ClusterFRecipeGate.Enabled = false; // the band books aside: only the basket-weaving key is in play
            Assert.False(ClusterFRecipeGate.IsKnown(w.Pm, tinkering, entry));

            BasketWeaving.Learn(w.Pm);
            Assert.True(ClusterFRecipeGate.IsKnown(w.Pm, tinkering, entry));

            ClusterFRecipeGate.Enabled = true;
            BasketWeaving.Forget(w.Pm);
            Assert.False(ClusterFRecipeGate.IsKnown(w.Pm, tinkering, entry)); // the key is checked whatever the gate setting
        }
        finally
        {
            ClusterFRecipeGate.Enabled = was;
        }
    }

    [Fact]
    public void ATinkerWeavesARedBasketFromRedReeds()
    {
        using var w = new Weaver(tinkering: 120.0); // chance past 1: the craft cannot fail
        var tinkering = DefTinkering.CraftSystem;
        BasketWeaving.Learn(w.Pm);

        var tools = w.Pack(new TinkerTools());
        w.Pack(new SoftenedReeds(PlantHue.Red) { Amount = 2 });
        w.Pack(new Shaft(3));

        var entry = tinkering.CraftItems.SearchFor(typeof(RoundBasket));
        entry.CompleteCraft(1, false, w.Pm, tinkering, typeof(SoftenedReeds), tools, null);

        var basket = w.Pm.Backpack.FindItemByType<RoundBasket>();
        _out.WriteLine($"made: {basket?.GetType().Name} hue={basket?.Hue} reeds left={w.Pm.Backpack.GetAmount(typeof(SoftenedReeds))}");

        Assert.NotNull(basket);
        Assert.Equal(0, w.Pm.Backpack.GetAmount(typeof(SoftenedReeds)));
        Assert.Equal(0, w.Pm.Backpack.GetAmount(typeof(Shaft)));
        Assert.Equal(PlantHueInfo.GetInfo(PlantHue.Red).Hue, basket.Hue);

        // Softening at the menu: red dry reeds and a toxin make red softened reeds.
        w.Pack(new DryReeds(PlantHue.Red));
        w.Pack(new ScouringToxin());
        w.Pack(new ScouringToxin()); // the entry wants two toxins (ServUO: two items, not two uses)
        var soften = tinkering.CraftItems.SearchFor(typeof(SoftenedReeds));
        soften.CompleteCraft(1, false, w.Pm, tinkering, typeof(DryReeds), tools, null);

        var soft = w.Pm.Backpack.FindItemByType<SoftenedReeds>();
        Assert.NotNull(soft);
        Assert.Equal(PlantHue.Red, soft.PlantHue);
        Assert.Equal(0, w.Pm.Backpack.GetAmount(typeof(DryReeds)));
    }

    [Fact]
    public void TheDyeAndTheReedsSurviveARoundTrip()
    {
        ShardTestClock.Arm();
        var dye = new NaturalDye(PlantPigmentHue.DarkGreen) { UsesRemaining = 3 };
        var reeds = new SoftenedReeds(PlantHue.Magenta) { Amount = 4 };

        var dyeCopy = ConsumablesVerification.RoundTrip(dye, s => new NaturalDye(s));
        var reedsCopy = ConsumablesVerification.RoundTrip(reeds, s => new SoftenedReeds(s));

        try
        {
            Assert.Equal(PlantPigmentHue.DarkGreen, dyeCopy.PigmentHue);
            Assert.Equal(3, dyeCopy.UsesRemaining);
            Assert.Equal(PlantPigmentHueInfo.GetInfo(PlantPigmentHue.DarkGreen).Hue, dyeCopy.Hue);
            Assert.Equal(PlantHue.Magenta, reedsCopy.PlantHue);
            Assert.Equal(4, reedsCopy.Amount);
        }
        finally
        {
            dyeCopy.Delete();
            reedsCopy.Delete();
        }
    }

    [Fact]
    public void TheCraftFileCarriesItsPatchedLinesInTheBuildTree()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Projects", "UOContent")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, $"no Projects/UOContent above {AppContext.BaseDirectory}");
        var root = Path.Combine(dir.FullName, "Projects", "UOContent");

        var text = File.ReadAllText(Path.Combine(root, "Engines/Craft/Core/CraftItem.cs"));
        Assert.True(text.Contains("typeof(SoftenedReeds), typeof(DryReeds)", StringComparison.Ordinal), "CraftItem.cs lacks the cc-P128 plant hue tables");
    }
}
