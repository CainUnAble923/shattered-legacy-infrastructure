// CleanUpPropertyWeightVerification.cs
//
// cc-P175 Part A (Chase 2026-10-18: "include magic properties adding value"). Clean Up Britannia pays crafted gear and
// loot its material value plus total property weight / 30, ServUO's rule (CleanUpBritanniaData.cs:811-816), the weight
// read from the shard's imbue catalogue (ImbueCatalogue.GetTotalWeight): each property the Imbuing Table sees counts 100
// at its Full value, a weapon's Damage Increase left out (ServUO's id 12). Part D: the extended woods resolve.
//
// Facts:
//   1. A magic longsword and plate chest pay their plain twin plus weight / 30 (exact numbers below).
//   2. The anti-farm measurement for imbuing: for every catalogue row on its kind of item, at its Full value and at its
//      top value, the Clean Up points the property adds are no more than its imbuing gold (and any ingredient) would pay
//      trashed directly (gold is 0.01 a coin, CleanUpBritanniaData.cs Gold). Essences and Essence Shards pay nothing.
//   3. The anti-farm measurement for runic crafting: for every runic hammer and sewing kit, the most a craft's properties
//      add (2,000 rolls of pinned's own BaseRunicTool.ApplyAttributesTo) against what one use of the tool is worth
//      trashed (CleanUpBritanniaData.cs, RunicHammer / RunicSewingKit). ServUO's rule (properties on crafted gear count)
//      lets a dull copper runic craft add 6 points for a 5-point use; this build counts none on crafted gear
//      (PlayerConstructed), so a craft adds 0. Both are printed.
//   4. Trash vs unravel for a few typical loot items (printed for the notes; a disenchant pays Essence Shards, not
//      points, so the two are not one currency).
//   5. The eight extended woods resolve: CraftResources.GetType is Wood and GetInfo names the wood.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Points;
using Server.Items;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CleanUpPropertyWeightVerification
{
    private readonly ITestOutputHelper _out;

    public CleanUpPropertyWeightVerification(ITestOutputHelper output)
    {
        _out = output;
        CleanUpCraftHost.EnsureCraftSystems(); // the crafted-gear rate reads the craft tables, as cc-P33's facts do
    }

    private static double Points(Item item) => CleanUpBritanniaData.GetPoints(item);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void AMagicItemPaysItsPlainTwinPlusWeightOverThirty()
    {
        var plain = new Longsword();
        var magic = new Longsword();
        magic.WeaponAttributes.HitLightning = 50; // Full 50: 100
        magic.Attributes.WeaponSpeed = 30;        // Full 60: 50
        magic.Attributes.WeaponDamage = 50;       // Damage Increase on a weapon: left out (ServUO id 12)
        var plainChest = new PlateChest();
        var magicChest = new PlateChest();
        magicChest.Attributes.Luck = 100;         // Full 100: 100
        magicChest.Attributes.BonusStr = 8;       // Full 8: 100
        magicChest.ArmorAttributes.MageArmor = 1; // a bool: 100
        try
        {
            Assert.Equal(150, CleanUpBritanniaData.PropertyWeight(magic));
            Assert.Equal(300, CleanUpBritanniaData.PropertyWeight(magicChest));
            Assert.Equal(0, CleanUpBritanniaData.PropertyWeight(plain));

            _out.WriteLine($"longsword: plain {Points(plain)}, magic {Points(magic)}; plate chest: plain {Points(plainChest)}, magic {Points(magicChest)}");
            Assert.Equal(Points(plain) + 150 / 30, Points(magic));
            Assert.Equal(Points(plainChest) + 300 / 30, Points(magicChest));
            Assert.True(Points(magic) > Points(plain));

            // The same properties on a crafted piece (pinned's craft marks it PlayerConstructed) pay its materials only: an
            // exceptional plate chest's 15 resist points (BaseArmor.cs:557-575) and a runic's rolls come from the craft.
            var crafted = new PlateChest { PlayerConstructed = true, Quality = ArmorQuality.Exceptional };
            crafted.DistributeBonuses(15);
            crafted.Attributes.Luck = 100;
            Assert.Equal(0, CleanUpBritanniaData.PropertyWeight(crafted));
            Assert.Equal(Points(plainChest), Points(crafted));
            crafted.PlayerConstructed = false;
            Assert.True(CleanUpBritanniaData.PropertyWeight(crafted) >= 175, $"unmarked: {CleanUpBritanniaData.PropertyWeight(crafted)}");
            crafted.Delete();
        }
        finally
        {
            plain.Delete();
            magic.Delete();
            plainChest.Delete();
            magicChest.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    private static Item BaseFor(ImbuePropertyDef def)
    {
        Item[] candidates = [new Longsword(), new Bow(), new PlateChest(), new GoldRing(), new Robe()];
        var hit = candidates.FirstOrDefault(i => (def.AppliesNow & ImbueCatalogue.GetItemFlag(i)) != 0 && def.Allows?.Invoke(i) != false);
        foreach (var c in candidates.Where(c => c != hit))
        {
            c.Delete();
        }

        return hit;
    }

    [Fact]
    public void NoImbuedPropertyPaysMoreThanItsImbuingCost()
    {
        var worst = (Name: "", Gain: 0.0, Cost: 0.0);
        var rows = 0;
        var bad = new List<string>();
        foreach (var def in ImbueCatalogue.All)
        {
            foreach (var value in new[] { def.FullMax, def.TopValue }.Distinct())
            {
                var plain = BaseFor(def);
                if (plain == null)
                {
                    continue;
                }

                var imbued = (Item)Activator.CreateInstance(plain.GetType())!;
                try
                {
                    def.Set(imbued, value);
                    var got = def.Get(imbued);
                    if (got == 0)
                    {
                        continue;
                    }

                    var gain = Points(imbued) - Points(plain);
                    var ingredient = ImbueCatalogue.CustomIngredient(def);
                    var ingredientPoints = 0.0;
                    if (ingredient != null)
                    {
                        var sample = ingredient.Type.CreateInstance<Item>();
                        ingredientPoints = Points(sample) * ingredient.Amount;
                        sample.Delete();
                    }

                    var cost = def.GoldFor(got) * 0.01 + ingredientPoints;
                    rows++;
                    if (gain / Math.Max(cost, 0.0001) > worst.Gain / Math.Max(worst.Cost, 0.0001))
                    {
                        worst = ($"{def.Name} {got}", gain, cost);
                    }

                    if (gain > cost)
                    {
                        bad.Add($"{def.Name} at {got}: +{gain} points for {cost} points of gold and ingredients");
                    }
                }
                finally
                {
                    imbued.Delete();
                    plain.Delete();
                }
            }
        }

        _out.WriteLine($"{rows} row-values measured; closest: {worst.Name}, +{worst.Gain} points for {worst.Cost} points of inputs");
        Assert.True(rows > 50, $"only {rows} measured");
        Assert.True(bad.Count == 0, string.Join("; ", bad));
    }

    // ---------------------------------------------------------------- 3

    public static IEnumerable<object[]> Runics() =>
        new[] { CraftResource.DullCopper, CraftResource.ShadowIron, CraftResource.Copper, CraftResource.Bronze, CraftResource.Gold,
                CraftResource.Agapite, CraftResource.Verite, CraftResource.Valorite }
            .Select(r => new object[] { r, false })
            .Concat(new[] { CraftResource.SpinedLeather, CraftResource.HornedLeather, CraftResource.BarbedLeather }
                .Select(r => new object[] { r, true }));

    [Theory]
    [MemberData(nameof(Runics))]
    public void ARunicCraftsPropertiesPayNoMoreThanTheUseOfTheTool(CraftResource resource, bool sewing)
    {
        BaseRunicTool tool = sewing ? new RunicSewingKit(resource, 1) : new RunicHammer(resource, 1);
        try
        {
            var useValue = Points(tool);
            var most = 0.0;    // ServUO's rule: the properties count on crafted gear too
            var crafted = 0.0; // this build: a crafted item (PlayerConstructed, as the craft marks it) pays its materials
            for (var i = 0; i < 2000; i++)
            {
                if (sewing)
                {
                    var plain = new LeatherChest { Resource = resource };
                    var made = new LeatherChest { Resource = resource, Crafter = "p175" };
                    tool.ApplyAttributesTo(made);
                    most = Math.Max(most, Points(made) - Points(plain));
                    made.PlayerConstructed = true;
                    crafted = Math.Max(crafted, Points(made) - Points(plain));
                    plain.Delete();
                    made.Delete();
                }
                else
                {
                    var plain = new Longsword { Resource = resource };
                    var made = new Longsword { Resource = resource, Crafter = "p175" };
                    tool.ApplyAttributesTo(made);
                    most = Math.Max(most, Points(made) - Points(plain));
                    made.PlayerConstructed = true;
                    crafted = Math.Max(crafted, Points(made) - Points(plain));
                    plain.Delete();
                    made.Delete();
                }
            }

            _out.WriteLine($"| {resource} {(sewing ? "sewing kit" : "hammer")} | one use {useValue} | most a craft's properties add, ServUO's rule {most} | this build {crafted} |");
            Assert.True(crafted <= useValue, $"{resource}: a runic craft's properties add {crafted}, one use is worth {useValue}");
            Assert.Equal(0.0, crafted);
        }
        finally
        {
            tool.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TrashAndUnravelValuesForTypicalLoot()
    {
        var items = new (string Name, Item Item)[]
        {
            ("Longsword, 35 SSI 40 HLD", new Longsword()), ("Plate chest, 10 phys 8 str 50 luck", new PlateChest()),
            ("Gold ring, 15 SDI 10 DCI", new GoldRing()), ("Bow, 50 hit fireball 15 HCI", new Bow())
        };
        ((Longsword)items[0].Item).Attributes.WeaponSpeed = 35;
        ((Longsword)items[0].Item).WeaponAttributes.HitLowerDefend = 40;
        ((PlateChest)items[1].Item).PhysicalBonus = 10;
        ((PlateChest)items[1].Item).Attributes.BonusStr = 8;
        ((PlateChest)items[1].Item).Attributes.Luck = 50;
        ((GoldRing)items[2].Item).Attributes.SpellDamage = 15;
        ((GoldRing)items[2].Item).Attributes.DefendChance = 10;
        ((Bow)items[3].Item).WeaponAttributes.HitFireball = 50;
        ((Bow)items[3].Item).Attributes.AttackChance = 15;

        _out.WriteLine("| Item | weight | trash points | disenchant shards | extract one: shards + essence |");
        foreach (var (name, item) in items)
        {
            var active = ImbueCatalogue.GetActiveProperties(item);
            var shards = active.Sum(p => Math.Max(1, p.Def.ShardBase / 2));
            var extract = active.Count == 0 ? 0 : Math.Max(1, active[0].Def.ShardBase / 4);
            _out.WriteLine($"| {name} | {CleanUpBritanniaData.PropertyWeight(item)} | {Points(item)} | {shards} | {extract} + 1 |");
            Assert.True(Points(item) > 0);
            item.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Theory]
    [InlineData(CraftResource.Ironwood)]
    [InlineData(CraftResource.Ghostwood)]
    [InlineData(CraftResource.Emberbark)]
    [InlineData(CraftResource.Frostbark)]
    [InlineData(CraftResource.Shadowbark)]
    [InlineData(CraftResource.Runewood)]
    [InlineData(CraftResource.Voidwood)]
    [InlineData(CraftResource.Starwood)]
    public void AnExtendedWoodResolves(CraftResource wood)
    {
        Assert.Equal(CraftResourceType.Wood, CraftResources.GetType(wood));
        var info = CraftResources.GetInfo(wood);
        Assert.NotNull(info);
        Assert.Equal(wood, info.Resource);
        Assert.Equal(CraftResource.RegularWood, CraftResources.GetStart(wood));
    }
}
