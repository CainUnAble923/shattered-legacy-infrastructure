// cc-P199: the Containers batch (ServUO Items/Containers -> customizations/Items/Containers/): EssenceBox,
// GargoyleWoodenChest, JaacarBox, BaseRewardBag (RewardBag.cs), the three imbuing bags, the three museum bags, the three
// dusty backpacks (RewardBackpack.cs), ScrollBox/2/3, ShipsStrongbox and the three unknown skeletons.
//
// Facts:
//   1. The essence box, gargoyle chest and Jaacar box: art, label or name, hue, gump, weight and ServUO's contents.
//   2. The three imbuing bags: label, a reward-bag hue, and exactly one of ServUO's ingredients for that bag.
//   3. The three museum bags: label, ServUO's gold, and gem and ML-resource totals inside ServUO's loop bounds.
//   4. The three dusty backpacks: label, hue 1127, level x 2000 gold, 3/3/5 pieces of equipment, 2-3 gems (rare above
//      level 1) and 20 residue / 10 essence / one relic fragment.
//   5. The three scroll boxes: name, hue, the Imbuing power scroll of each; the runic mallet roll gives only ServUO's
//      resource and uses pairs.
//   6. A ship's strongbox at levels 0, 2, 4 and 5: hue before the clamp, contents after it, 400-stone capacity.
//   7. The three unknown skeletons: art in 0xECA-0xED2, name, gump 0x9, 35 stones, and each one's kit.
//   8. DropItemStacked joins an existing stack, keeps different types apart and never stacks into a nested bag.
//   9. RewardBagHue only gives 0 or a hue inside one of ServUO's eight ranges.
//  10. Every ported container survives a serialization round trip with its hue, gump and contents.
//
// Host notes: no tile data, so every weight asserted is a DefaultWeight override. Every fact deletes what it made.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class P199ContainersVerification
{
    private static readonly Type[] RareGems =
    {
        typeof(BlueDiamond), typeof(DarkSapphire), typeof(EcruCitrine), typeof(FireRuby), typeof(PerfectEmerald),
        typeof(Turquoise), typeof(WhitePearl), typeof(BrilliantAmber)
    };

    private static readonly Type[] MLResources = RareGems.Concat(new[]
    {
        typeof(LuminescentFungi), typeof(BarkFragment), typeof(SwitchItem), typeof(ParasiticPlant)
    }).ToArray();

    private static int AmountOf(Container c, IEnumerable<Type> types)
    {
        var set = new HashSet<Type>(types);
        return c.Items.Where(i => set.Contains(i.GetType())).Sum(i => i.Amount);
    }

    private static int AmountOf<T>(Container c) where T : Item => c.Items.OfType<T>().Sum(i => i.Amount);

    private static bool IsRewardBagHue(int hue)
    {
        int[] ranges = { 0x385, 0x3E9, 0x4B0, 0x4E6, 0x514, 0x54A, 0x578, 0x5AE, 0x5DC, 0x612, 0x640, 0x676, 0x6A5, 0x6DA, 0x708, 0x774 };

        if (hue == 0)
        {
            return true;
        }

        for (var i = 0; i < ranges.Length; i += 2)
        {
            if (hue >= ranges[i] && hue <= ranges[i + 1])
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void TheEssenceBoxGargoyleChestAndJaacarBoxAreServUOs()
    {
        var essence = new EssenceBox();
        var chest = new GargoyleWoodenChest();
        var jaacar = new JaacarBox();

        try
        {
            Assert.Equal(0x9AA, essence.ItemID);
            Assert.Equal(1113770, essence.LabelNumber);
            Assert.Equal(2306, essence.Hue);
            Assert.True(essence.Movable);
            Assert.Equal(4.0, essence.Weight);
            var essenceItem = Assert.Single(essence.Items);
            Assert.Contains(essenceItem.GetType(), new[]
            {
                typeof(EssencePrecision), typeof(EssenceAchievement), typeof(EssenceBalance), typeof(EssenceControl),
                typeof(EssenceDiligence), typeof(EssenceDirection), typeof(EssenceFeeling), typeof(EssenceOrder),
                typeof(EssencePassion), typeof(EssencePersistence), typeof(EssenceSingularity)
            });
            Assert.Equal(1, essenceItem.Amount);

            Assert.Equal(0x4025, chest.ItemID);
            Assert.Equal(0x42, chest.GumpID);
            Assert.Equal(2.0, chest.Weight);
            Assert.Empty(chest.Items);

            Assert.Equal(0x9AA, jaacar.ItemID);
            Assert.Equal("Jaacar Reward Box", jaacar.Name);
            Assert.Equal(1266, jaacar.Hue);
            var scroll = Assert.IsType<RecipeScroll>(Assert.Single(jaacar.Items));
            Assert.Equal(500, scroll.RecipeID);
        }
        finally
        {
            essence.Delete();
            chest.Delete();
            jaacar.Delete();
        }
    }

    [Fact]
    public void TheImbuingBagsHoldOneOfTheirIngredients()
    {
        var made = new List<Item>();

        try
        {
            for (var n = 0; n < 20; n++)
            {
                var average = new AverageImbuingBag();
                var meager = new MeagerImbuingBag();
                var valuable = new ValuableImbuingBag();
                made.AddRange(new Item[] { average, meager, valuable });

                Assert.Equal(1113768, average.LabelNumber);
                Assert.Equal(1112994, meager.LabelNumber);
                Assert.Equal(1113769, valuable.LabelNumber);
                Assert.Equal(0xE76, average.ItemID);

                foreach (var bag in new BaseRewardBag[] { average, meager, valuable })
                {
                    Assert.True(IsRewardBagHue(bag.Hue), $"hue {bag.Hue:X}");
                    Assert.Equal(0, bag.ItemAmount);
                }

                Assert.Contains(Assert.Single(average.Items).GetType(), new[] { typeof(SpiderCarapace), typeof(SilverSnakeSkin) });
                Assert.Contains(
                    Assert.Single(meager.Items).GetType(),
                    new[] { typeof(SlithTongue), typeof(GoblinBlood), typeof(ReflectiveWolfEye), typeof(RaptorTeeth) }
                );
                Assert.Contains(Assert.Single(valuable.Items).GetType(), new[] { typeof(UndyingFlesh), typeof(DaemonClaw) });
            }
        }
        finally
        {
            made.ForEach(i => i.Delete());
        }
    }

    [Fact]
    public void TheMuseumBagsHoldServUOsGoldGemsAndResources()
    {
        var made = new List<Item>();

        try
        {
            for (var n = 0; n < 10; n++)
            {
                var cases = new (BaseRewardBag Bag, int Label, int Gold, int GemMin, int GemMax, int ResMin, int ResMax)[]
                {
                    (new BulgingMuseumBag(), 1112995, 10000, 10, 15, 5, 7),
                    (new DustyMuseumBag(), 1112994, 6000, 7, 9, 3, 5),
                    (new MeagerMuseumBag(), 1112993, 3000, 5, 7, 1, 3)
                };

                foreach (var (bag, label, gold, gemMin, gemMax, resMin, resMax) in cases)
                {
                    made.Add(bag);
                    Assert.Equal(label, bag.LabelNumber);
                    Assert.True(IsRewardBagHue(bag.Hue));
                    Assert.Equal(gold, AmountOf<Gold>(bag));
                    Assert.InRange(AmountOf(bag, Loot.GemTypes), gemMin, gemMax);
                    Assert.InRange(AmountOf(bag, MLResources), resMin, resMax);

                    // Gold, gems and resources only: no Ter Mur book, nothing else.
                    Assert.All(
                        bag.Items,
                        i => Assert.True(i is Gold || Loot.GemTypes.Contains(i.GetType()) || MLResources.Contains(i.GetType()))
                    );

                    // Stacked: one stack per type.
                    Assert.Equal(bag.Items.Count, bag.Items.Select(i => i.GetType()).Distinct().Count());
                }
            }
        }
        finally
        {
            made.ForEach(i => i.Delete());
        }
    }

    [Fact]
    public void TheDustyBackpacksScaleByLevel()
    {
        var made = new List<Item>();

        try
        {
            for (var n = 0; n < 5; n++)
            {
                var cases = new (BaseRewardBackpack Pack, int Label, int Level, int Equipment)[]
                {
                    (new DustyAdventurersBackpack(), 1113189, 1, 3),
                    (new DustyExplorersBackpack(), 1113190, 2, 3),
                    (new DustyHuntersBackpack(), 1113191, 3, 5)
                };

                foreach (var (pack, label, level, equipment) in cases)
                {
                    made.Add(pack);
                    Assert.Equal(0xE75, pack.ItemID);
                    Assert.Equal(label, pack.LabelNumber);
                    Assert.Equal(level, pack.Level);
                    Assert.Equal(1127, pack.Hue);
                    Assert.Equal(level * 2000, AmountOf<Gold>(pack));
                    Assert.Equal(
                        equipment,
                        pack.Items.Count(i => i is BaseWeapon or BaseArmor or BaseJewel or BaseHat)
                    );

                    var gems = level == 1 ? Loot.GemTypes : RareGems;
                    Assert.InRange(AmountOf(pack, gems), 2, 3);

                    switch (level)
                    {
                        case 1:
                            Assert.Equal(20, AmountOf<MagicalResidue>(pack));
                            break;
                        case 2:
                            Assert.Equal(10, AmountOf<EnchantedEssence>(pack));
                            break;
                        case 3:
                            Assert.Equal(1, AmountOf<RelicFragment>(pack));
                            break;
                    }
                }
            }
        }
        finally
        {
            made.ForEach(i => i.Delete());
        }
    }

    [Fact]
    public void TheScrollBoxesHoldTheirImbuingScrollAndServUOsMallets()
    {
        var made = new List<Item>();

        try
        {
            for (var n = 0; n < 20; n++)
            {
                var one = new ScrollBox();
                var two = new ScrollBox2();
                var three = new ScrollBox3();
                made.AddRange(new Item[] { one, two, three });

                foreach (var (box, hue) in new (WoodenBox, int)[] { (one, 1151), (two, 1266), (three, 1159) })
                {
                    Assert.Equal("Reward Scroll Box", box.Name);
                    Assert.Equal(hue, box.Hue);
                    Assert.Equal(0x9AA, box.ItemID);
                    Assert.InRange(box.Items.Count, 1, 2);
                }

                double Value(Container c)
                {
                    var ps = Assert.Single(c.Items.OfType<PowerScroll>());
                    Assert.Equal(SkillName.Imbuing, ps.Skill);
                    return ps.Value;
                }

                Assert.Equal(115.0, Value(one));
                Assert.Equal(120.0, Value(two));
                Assert.Contains(Value(three), new[] { 105.0, 110.0 });
            }

            var table = new Dictionary<CraftResource, int>
            {
                [CraftResource.DullCopper] = 50, [CraftResource.ShadowIron] = 45, [CraftResource.Copper] = 40,
                [CraftResource.Bronze] = 35, [CraftResource.Gold] = 30, [CraftResource.Agapite] = 25,
                [CraftResource.Verite] = 20, [CraftResource.Valorite] = 15
            };

            for (var n = 0; n < 200; n++)
            {
                var mallet = Assert.IsType<RunicMalletAndChisel>(ScrollBox.RandomRunicMalletAndChisel());
                made.Add(mallet);
                Assert.True(table.TryGetValue(mallet.Resource, out var uses), $"resource {mallet.Resource}");
                Assert.Equal(uses, mallet.UsesRemaining);
            }
        }
        finally
        {
            made.ForEach(i => i.Delete());
        }
    }

    [Fact]
    public void AShipsStrongboxHoldsItsLevelsTreasure()
    {
        var made = new List<Item>();

        try
        {
            foreach (var (asked, level, hue) in new[] { (0, 1, 0x836), (2, 2, 0x836), (4, 4, 0x481), (5, 4, 0x481) })
            {
                var box = new ShipsStrongbox(asked);
                made.Add(box);

                Assert.Equal(0xE80, box.ItemID);
                Assert.Equal(1149959, box.LabelNumber);
                Assert.Equal(400, box.MaxWeight);
                Assert.Equal(hue, box.Hue);
                Assert.Equal(10000 * level, AmountOf<Gold>(box));
                Assert.Equal(20 * level, AmountOf(box, Loot.GemTypes));
                Assert.InRange(AmountOf(box, RareGems), level * 5, level * 5 + 4);
                Assert.Equal(level * 100, AmountOf<GoldIngot>(box) + AmountOf<CopperIngot>(box));
                Assert.Equal(box.Items.Count, box.Items.Select(i => i.GetType()).Distinct().Count());
            }

            for (var n = 0; n < 10; n++)
            {
                var random = new ShipsStrongbox();
                made.Add(random);
                Assert.Equal(0x836, random.Hue);
                Assert.Contains(AmountOf<Gold>(random), new[] { 10000, 20000, 30000 });
            }
        }
        finally
        {
            made.ForEach(i => i.Delete());
        }
    }

    [Fact]
    public void TheUnknownSkeletonsCarryTheirKits()
    {
        var made = new List<Item>();

        try
        {
            for (var n = 0; n < 10; n++)
            {
                var bard = new UnknownBardSkeleton();
                var rogue = new UnknownRogueSkeleton();
                var mage = new UnknownMageSkeleton();
                made.AddRange(new Item[] { bard, rogue, mage });

                foreach (var (s, name) in new (BaseContainer, string)[]
                         {
                             (bard, "An Unknown Bard's Skeleton"), (rogue, "An Unknown Rogue's Skeleton"),
                             (mage, "An Unknown Mage's Skeleton")
                         })
                {
                    Assert.InRange(s.ItemID, 0xECA, 0xED2);
                    Assert.Equal(name, s.Name);
                    Assert.Equal(0x9, s.GumpID);
                    Assert.Equal(35.0, s.Weight);
                }

                Assert.InRange(AmountOf<Gold>(bard), 200, 400);
                Assert.Single(bard.Items.OfType<Doublet>());
                Assert.Single(bard.Items.OfType<JesterHat>());
                Assert.InRange(AmountOf<Bandage>(bard), 10, 20);
                Assert.Equal(1, bard.Items.Count(i => i is Kilt or ShortPants));
                Assert.Contains(
                    Assert.Single(bard.Items.OfType<BeverageBottle>()).Content,
                    new[] { BeverageType.Ale, BeverageType.Wine, BeverageType.Liquor }
                );
                Assert.Single(bard.Items.OfType<BaseInstrument>());
                Assert.Equal(7, bard.Items.Count);

                Assert.Single(rogue.Items.OfType<LeatherChest>());
                Assert.Single(rogue.Items.OfType<LeatherGloves>());
                Assert.Single(rogue.Items.OfType<LeatherArms>());
                Assert.Equal(50, Assert.Single(rogue.Items.OfType<Shovel>()).UsesRemaining);
                Assert.InRange(AmountOf<Lockpick>(rogue), 1, 4);
                Assert.Equal(1, rogue.Items.Count(i => i is Torch or Lantern));
                Assert.Equal(2, rogue.Items.Count(i => i is BaseWeapon)); // the dagger and the random weapon
                Assert.Single(rogue.Items.OfType<TreasureMap>());
                Assert.Equal(9, rogue.Items.Count);

                Assert.Single(mage.Items.OfType<Robe>());
                Assert.Single(mage.Items.OfType<Sandals>());
                Assert.Single(mage.Items.OfType<BaseJewel>());
                Assert.Equal(1, mage.Items.Count(i => i is QuarterStaff or GnarledStaff));
                var regs = mage.Items.Where(i => Loot.RegTypes.Contains(i.GetType())).ToList();
                Assert.Equal(3, regs.Count);
                Assert.All(regs, r => Assert.InRange(r.Amount, 15, 20));
                var scrolls = mage.Items.OfType<SpellScroll>().ToList();
                Assert.InRange(scrolls.Count, 0, 3);
                Assert.All(scrolls, s => Assert.InRange(s.Amount, 1, 2));
            }
        }
        finally
        {
            made.ForEach(i => i.Delete());
        }
    }

    [Fact]
    public void DropItemStackedJoinsAStackButNotANestedBag()
    {
        var box = new WoodenBox();

        try
        {
            var inner = new Bag();
            box.DropItem(inner);
            inner.DropItem(new Ruby());

            BaseRewardBag.DropItemStacked(box, new Ruby());
            BaseRewardBag.DropItemStacked(box, new Ruby(2));
            BaseRewardBag.DropItemStacked(box, new Amber());

            var rubies = box.Items.OfType<Ruby>().ToList();
            Assert.Single(rubies);
            Assert.Equal(3, rubies[0].Amount);
            Assert.Single(box.Items.OfType<Amber>());
            Assert.Equal(1, AmountOf<Ruby>(inner));
            Assert.Equal(3, box.Items.Count);
        }
        finally
        {
            box.Delete();
        }
    }

    [Fact]
    public void RewardBagHueComesFromServUOsTable()
    {
        for (var n = 0; n < 2000; n++)
        {
            var hue = BaseRewardBag.RewardBagHue();
            Assert.True(IsRewardBagHue(hue), $"hue {hue:X}");
        }
    }

    [Fact]
    public void EveryPortedContainerSurvivesARoundTripWithItsContents()
    {
        var chest = new GargoyleWoodenChest();
        chest.DropItem(new Gold(5));

        var originals = new List<(Container Item, Func<Serial, Container> Make)>
        {
            (new EssenceBox(), s => new EssenceBox(s)),
            (chest, s => new GargoyleWoodenChest(s)),
            (new JaacarBox(), s => new JaacarBox(s)),
            (new AverageImbuingBag(), s => new AverageImbuingBag(s)),
            (new MeagerImbuingBag(), s => new MeagerImbuingBag(s)),
            (new ValuableImbuingBag(), s => new ValuableImbuingBag(s)),
            (new BulgingMuseumBag(), s => new BulgingMuseumBag(s)),
            (new DustyMuseumBag(), s => new DustyMuseumBag(s)),
            (new MeagerMuseumBag(), s => new MeagerMuseumBag(s)),
            (new DustyAdventurersBackpack(), s => new DustyAdventurersBackpack(s)),
            (new DustyExplorersBackpack(), s => new DustyExplorersBackpack(s)),
            (new DustyHuntersBackpack(), s => new DustyHuntersBackpack(s)),
            (new ScrollBox(), s => new ScrollBox(s)),
            (new ScrollBox2(), s => new ScrollBox2(s)),
            (new ScrollBox3(), s => new ScrollBox3(s)),
            (new ShipsStrongbox(3), s => new ShipsStrongbox(s)),
            (new UnknownBardSkeleton(), s => new UnknownBardSkeleton(s)),
            (new UnknownRogueSkeleton(), s => new UnknownRogueSkeleton(s)),
            (new UnknownMageSkeleton(), s => new UnknownMageSkeleton(s))
        };

        var cleanup = new List<Item>();

        try
        {
            foreach (var (original, make) in originals)
            {
                var type = original.GetType();
                var itemId = original.ItemID;
                var hue = original.Hue;
                var gump = original.GumpID;
                var label = original.LabelNumber;
                var name = original.Name;
                var contents = original.Items.Select(i => i.Serial).ToHashSet();
                cleanup.AddRange(original.Items);
                Assert.NotEmpty(contents);

                var copy = RoundTripWithContents(original, make);
                cleanup.Add(copy);

                Assert.IsType(type, copy);
                Assert.Equal(itemId, copy.ItemID);
                Assert.Equal(hue, copy.Hue);
                Assert.Equal(gump, copy.GumpID);
                Assert.Equal(label, copy.LabelNumber);
                Assert.Equal(name, copy.Name);
                Assert.Equal(contents, copy.Items.Select(i => i.Serial).ToHashSet());
            }
        }
        finally
        {
            foreach (var (original, _) in originals)
            {
                original.Delete();
            }

            cleanup.ForEach(i => i.Delete());
        }
    }

    // ConsumablesVerification.RoundTrip deletes the original, which for a container deletes its contents too, so the
    // contents are lifted out first (still in the world) and the copy finds them again by serial.
    private static Container RoundTripWithContents(Container original, Func<Serial, Container> make)
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        foreach (var child in original.Items.ToList())
        {
            original.RemoveItem(child);
        }

        var serial = original.Serial;
        original.Delete();

        var copy = make(serial);
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }
}
