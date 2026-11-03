// ChocolatieringVerification.cs
//
// cc-P785: chocolatiering in Cooking. Notes in shard-migration notes/cc-P785-chocolate-recipes.md. Each fact was seen
// red first (the notes' red table).
//
// Facts:
//   1. The table: the defaults validate (19 recipes: sweet cocoa butter, three nutcrackers, five figures in three
//      chocolates) and round-trip as JSON; a recipe making anything but the three chocolatiering items, a resource or a
//      provisioner row that is a deed, a power scroll or a weapon, and an unknown setting are each refused whole.
//   2. Every recipe is in Cooking's Chocolatiering group with the table's skills and resources (sweet cocoa butter at an
//      oven), beside pinned's cocoa butter, cocoa liquor and three chocolates.
//   3. Each recipe makes its item from its resources and uses them up: pinned's five (cocoa butter and liquor from pulp,
//      dark, milk and white chocolate) and our fourteen others, each figure in its shape and chocolate.
//   4. The nutcrackers are unlocked: dark, milk and white each make a wrapped nutcracker of that kind, and each sits in
//      an ordinary Cooking recipe band (free or a book), not behind a recipe scroll.
//   5. The provisioner sells the five molds, sugar, vanilla and foil sheets at the table's prices, once each, also after
//      its lists are rebuilt (as pinned does on every world load).

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ChocolatieringVerification : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly List<IEntity> _made = [];

    private static readonly Point3D Kitchen = new(1420, 1700, 0);

    public ChocolatieringVerification(ITestOutputHelper output)
    {
        _out = output;
        if (DefCooking.CraftSystem == null)
        {
            DefCooking.Initialize();
        }

        CookingCraftRegistrations.Register();
        ChocolatieringTable.Current = ChocolatieringTable.Defaults;
        ChocolatieringRecipes.Register();

        // A stone oven beside the kitchen spot: pinned checks it when the resources are used (CraftItem.ConsumeRes).
        var oven = new Item(0x92B) { Movable = false };
        oven.MoveToWorld(new Point3D(Kitchen.X + 1, Kitchen.Y, Kitchen.Z), Map.Trammel);
        _made.Add(oven);
    }

    public void Dispose()
    {
        foreach (var e in _made)
        {
            e?.Delete();
        }
    }

    private static CraftSystem Cooking => DefCooking.CraftSystem;

    private PlayerMobile NewCook()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Cook" };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Kitchen, Map.Trammel);
        pm.Skills.Cooking.CapFixedPoint = 1500;
        pm.Skills.Cooking.Base = 150.0; // above every entry's max: no craft can fail
        _made.Add(pm);
        return pm;
    }

    private static Item Make(Type type, int amount)
    {
        if (type == typeof(BaseBeverage))
        {
            return new Pitcher(BeverageType.Milk);
        }

        var item = type.CreateInstance<Item>() ?? throw new InvalidOperationException(type.Name);
        if (item.Stackable)
        {
            item.Amount = amount;
        }

        return item;
    }

    /// <summary>Gives the cook a craft entry's resources, makes it once, and returns what was made.</summary>
    private Item? Craft(CraftItem entry, PlayerMobile cook)
    {
        var tool = new Skillet();
        cook.Backpack.DropItem(tool);
        foreach (var res in entry.Resources)
        {
            if (res.ItemType == typeof(BaseBeverage))
            {
                cook.Backpack.DropItem(new Pitcher(BeverageType.Milk));
                continue;
            }

            var first = Make(res.ItemType, res.Amount);
            cook.Backpack.DropItem(first);
            for (var i = first.Amount; i < res.Amount; i++)
            {
                cook.Backpack.DropItem(Make(res.ItemType, 1));
            }
        }

        var before = cook.Backpack.Items.ToHashSet();
        entry.CompleteCraft(1, false, cook, Cooking, entry.Resources[0].ItemType, tool, null);
        var made = cook.Backpack.Items.FirstOrDefault(i => !before.Contains(i) && entry.ItemType.IsInstanceOfType(i));

        foreach (var res in entry.Resources.Where(r => r.ItemType != typeof(BaseBeverage)))
        {
            var left = cook.Backpack.GetAmount(res.ItemType) - (res.ItemType.IsInstanceOfType(made) ? made!.Amount : 0);
            Assert.True(left == 0, $"{entry.ItemType.Name}: {left} {res.ItemType.Name} left after the craft");
        }

        return made;
    }

    // ---------------------------------------------------------------- 1. the table

    [Fact]
    public void TheTableValidatesAndRefusesAnythingButKitchenGoods()
    {
        var t = ChocolatieringTable.Defaults;
        var errors = new List<string>();
        ChocolatieringTable.Validate(t, errors);
        Assert.Empty(errors);
        Assert.Equal(19, t.Recipes.Count);
        Assert.Single(t.Recipes, r => r.ItemType == typeof(SweetCocoaButter));
        Assert.Equal(3, t.Recipes.Count(r => r.ItemType == typeof(ChocolateNutcracker)));
        Assert.Equal(15, t.Recipes.Count(r => r.ItemType == typeof(ChocolateFigure)));
        Assert.Equal(8, t.Provisioner.Count);

        var back = ChocolatieringTable.Parse(ChocolatieringTable.ToJson(t), "round-trip", errors);
        Assert.Empty(errors);
        Assert.NotNull(back);
        Assert.Equal(t.Recipes.Select(r => (r.Key, r.MinSkill, r.MaxSkill, r.Resources.Count)),
            back!.Recipes.Select(r => (r.Key, r.MinSkill, r.MaxSkill, r.Resources.Count)));

        // A recipe that makes a power scroll; a resource that is a deed; a vendor row that is a weapon.
        var bad1 = ChocolatieringTable.BuildDefaults();
        bad1.Recipes[0].Item = "PowerScroll";
        var bad2 = ChocolatieringTable.BuildDefaults();
        bad2.Recipes[1].Resources.Add(new ChocoResource { Type = "HolidayTreeDeed", Amount = 1 });
        var bad3 = ChocolatieringTable.BuildDefaults();
        bad3.Provisioner.Add(new ChocoVendorRow { Type = "Longsword", Price = 10, Amount = 1 });
        var bad4 = ChocolatieringTable.BuildDefaults();
        bad4.Provisioner.Add(new ChocoVendorRow { Type = "StatCapScroll", Price = 10, Amount = 1 });

        foreach (var (bad, word) in new[] { (bad1, "PowerScroll"), (bad2, "HolidayTreeDeed"), (bad3, "Longsword"), (bad4, "StatCapScroll") })
        {
            var errs = new List<string>();
            Assert.Null(ChocolatieringTable.Parse(ChocolatieringTable.ToJson(bad), "bad", errs));
            Assert.Contains(errs, e => e.Contains(word));
            _out.WriteLine(string.Join("; ", errs));
        }

        Assert.Null(ChocolatieringTable.Parse("{\"recipez\": []}", "unknown", new List<string>()));
    }

    // ---------------------------------------------------------------- 2. every recipe is in Cooking

    [Fact]
    public void EveryRecipeIsInCookingsChocolatieringGroup()
    {
        var t = ChocolatieringTable.Current;
        foreach (var r in t.Recipes)
        {
            var entry = ChocolatieringRecipes.CraftItemFor(r.Key);
            Assert.True(entry != null, $"{r.Key} is not in Cooking");
            Assert.Contains(entry, Cooking.CraftItems);
            Assert.Equal(r.ItemType, entry!.ItemType);
            Assert.Equal(r.MinSkill, entry.Skills[0].MinSkill);
            Assert.Equal(r.MaxSkill, entry.Skills[0].MaxSkill);
            Assert.Equal(SkillName.Cooking, entry.Skills[0].SkillToMake);
            Assert.Equal(r.Resources.Select(x => (x.ItemType, x.Amount)), entry.Resources.Select(x => (x.ItemType, x.Amount)));
            Assert.Equal(r.NeedOven, entry.NeedOven);

            var group = Cooking.CraftGroups.First(g => g.CraftItems.Contains(entry));
            Assert.Equal(ChocolatieringRecipes.Group, group.NameNumber);
        }

        Assert.True(ChocolatieringRecipes.CraftItemFor("sweetcocoabutter")!.NeedOven);

        // Registering again adds nothing.
        var count = Cooking.CraftItems.Count;
        Assert.Equal(0, ChocolatieringRecipes.Register());
        Assert.Equal(count, Cooking.CraftItems.Count);

        // Pinned's own chocolatiering is there beside it.
        foreach (var type in new[] { typeof(CocoaButter), typeof(CocoaLiquor), typeof(DarkChocolate), typeof(MilkChocolate), typeof(WhiteChocolate) })
        {
            Assert.NotNull(Cooking.CraftItems.SearchFor(type));
        }
    }

    // ---------------------------------------------------------------- 3. each recipe makes its item

    [Fact]
    public void EachRecipeMakesItsItemFromItsResources()
    {
        // Pinned's five: cocoa butter and liquor from cocoa pulp, then the three chocolates.
        foreach (var type in new[] { typeof(CocoaButter), typeof(CocoaLiquor), typeof(DarkChocolate), typeof(MilkChocolate), typeof(WhiteChocolate) })
        {
            var cook = NewCook();
            var made = Craft(Cooking.CraftItems.SearchFor(type), cook);
            _out.WriteLine($"{type.Name}: {made?.GetType().Name}");
            Assert.IsType(type, made);
        }

        // Ours.
        foreach (var r in ChocolatieringTable.Current.Recipes)
        {
            var cook = NewCook();
            var made = Craft(ChocolatieringRecipes.CraftItemFor(r.Key)!, cook);
            _out.WriteLine($"{r.Key}: {made?.GetType().Name} {made?.Name}");
            Assert.NotNull(made);
            Assert.IsType(r.ItemType, made);

            if (made is ChocolateFigure figure)
            {
                Assert.Equal(r.FigureKind, figure.Figure);
                Assert.Equal(r.ChocolateKind, figure.Chocolate);
                Assert.Equal(ChocolateFigure.ArtOf(r.FigureKind), figure.ItemID);
                Assert.Equal((int)r.ChocolateKind, figure.Hue);
            }
        }
    }

    // ---------------------------------------------------------------- 4. the nutcrackers are unlocked

    [Fact]
    public void TheThreeNutcrackersAreMadeEachOfItsKindInAnOrdinaryRecipeBand()
    {
        var kinds = new[]
        {
            ("nutcracker.dark", ChocolateNutcracker.ChocolateType.Dark),
            ("nutcracker.milk", ChocolateNutcracker.ChocolateType.Milk),
            ("nutcracker.white", ChocolateNutcracker.ChocolateType.White)
        };

        foreach (var (key, kind) in kinds)
        {
            var entry = ChocolatieringRecipes.CraftItemFor(key);
            Assert.NotNull(entry);
            Assert.Equal(typeof(ChocolateNutcracker), entry!.ItemType);
            Assert.Equal(new[] { typeof(SweetCocoaButter), typeof(GoldFoil), typeof(CocoaLiquor) }, entry.Resources.Select(r => r.ItemType));
            Assert.Null(entry.Recipe); // no pinned recipe scroll

            var gate = ClusterFRecipeGate.Classify(Cooking, entry);
            _out.WriteLine($"{key}: {gate}");
            Assert.True(gate.Kind is RecipeKind.Free or RecipeKind.Book or RecipeKind.Exempt, $"{key} is {gate.Kind}");

            var made = Craft(entry, NewCook()) as ChocolateNutcracker;
            Assert.NotNull(made);
            Assert.Equal(kind, made!.Type);
            Assert.True(made.Wrapped);
        }
    }

    // ---------------------------------------------------------------- 5. the provisioner

    [Fact]
    public void TheProvisionerSellsTheMoldsAndSuppliesAtTheTablesPrices()
    {
        var p = new Provisioner();
        p.MoveToWorld(Kitchen, Map.Trammel);
        _made.Add(p);

        void Check()
        {
            var rows = p.GetBuyInfo().OfType<GenericBuyInfo>().ToList();
            foreach (var v in ChocolatieringTable.Current.Provisioner)
            {
                var row = Assert.Single(rows, b => b.Type == v.ItemType);
                Assert.Equal(v.Price, row.Price);
            }
        }

        Check();
        foreach (var fig in ChocolatieringTable.FigureNames)
        {
            Assert.Contains(ChocolatieringTable.Current.Provisioner, v => v.Type == ChocolatieringTable.MoldTypeName(fig));
        }

        // Its lists rebuilt, as on every world load (pinned's AfterDeserialization) or a map change: still once each.
        p.MoveToWorld(Kitchen, Map.Felucca);
        Check();
    }
}
