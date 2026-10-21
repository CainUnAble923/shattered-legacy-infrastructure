// cc-P199: ServUO pub57 Scripts/Items/Containers/RewardBag.cs. BaseRewardBag, the bag ServUO's quest reward bags
// derive from: a hue from the reward-bag hue table and ItemAmount pieces of magic loot (armor, ranged weapon, weapon or
// jewelry, each with one to five properties, 5% of slots a talisman instead). Our six ported bags (the three imbuing bags
// and the three museum bags) all keep ServUO's ItemAmount of 0, so for them only the hue matters.
//
// Left out of this file: ServUO's SmallTrinketBag, TrinketBag, TreasureBag and LargeTreasureBag (RewardBag.cs:65-203).
// They are the ML quest bags "a small bag of trinkets" (1072268), "a bag of trinkets" (1072341), "a bag of treasure"
// (1072583) and "a large bag of treasure" (1072706), which pinned already has as SmallBagOfTrinkets, BagOfTrinkets,
// BagOfTreasure and LargeBagOfTreasure (Engines/ML Quests/Items/RewardBags.cs:62-99), the ones pinned's ML quests give.
//
// The helpers below are what the bags need from ServUO code pinned does not have:
//   RewardBagHue, Armor, RangedWeapon, Weapon, Jewlery, ApplyMods: ServUO's Server.Engines.Quests.BaseReward
//     (Services/MondainsLegacyQuests/BaseReward.cs:15-19, 49-95, 143-175). Pinned's BaseReward is an unrelated ML quest
//     class, so these sit here rather than under that name.
//   ApplyMods: ServUO runs RunicReforging.GenerateRandomItem(item, 0, 10, 850) when Core.SA and RandomItemGenerator is
//     enabled (BaseReward.cs:70-73; enabled from High Seas on, RandomItemGenerator.cs:14). Neither exists in pinned or
//     ours, so this always takes ServUO's other branch, BaseRunicTool with 1-5 properties at 10-100 intensity
//     (BaseReward.cs:76-84).
//   DropItemStacked: ServUO's Container.DropItemStacked (Scripts/Items/Containers/Container.cs:104-118), which pinned's
//     Container lacks. Same loop, without ServUO's XmlSpawner "taken" flag (ItemFlags.SetTaken, an XmlSpawner field).
//   RandomRareGem, RandomMLResource: ServUO's Loot (Misc/Loot.cs:172-185, 880-888), not in pinned's Loot.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BaseRewardBag : Bag
{
    // BaseReward.cs:15-19, pairs of from/to.
    private static readonly int[] _rewardBagHues =
    {
        0x385, 0x3E9, 0x4B0, 0x4E6, 0x514, 0x54A, 0x578, 0x5AE, 0x5DC, 0x612, 0x640, 0x676, 0x6A5, 0x6DA, 0x708, 0x774
    };

    // Loot.cs:172-175.
    private static readonly Type[] _rareGemTypes =
    {
        typeof(BlueDiamond), typeof(DarkSapphire), typeof(EcruCitrine), typeof(FireRuby), typeof(PerfectEmerald),
        typeof(Turquoise), typeof(WhitePearl), typeof(BrilliantAmber)
    };

    // Loot.cs:179-183.
    private static readonly Type[] _mlResources =
    {
        typeof(BlueDiamond), typeof(DarkSapphire), typeof(EcruCitrine), typeof(FireRuby), typeof(PerfectEmerald),
        typeof(Turquoise), typeof(WhitePearl), typeof(BrilliantAmber), typeof(LuminescentFungi), typeof(BarkFragment),
        typeof(SwitchItem), typeof(ParasiticPlant)
    };

    public BaseRewardBag()
    {
        Hue = RewardBagHue();

        while (Items.Count < ItemAmount)
        {
            Item item;

            if (0.05 > Utility.RandomDouble())
            {
                item = Loot.RandomTalisman();
            }
            else
            {
                item = Utility.Random(4) switch
                {
                    0 => Armor(),
                    1 => RangedWeapon(),
                    2 => Weapon(),
                    _ => Jewlery()
                };
            }

            // ServUO drops whatever came back; a null here would throw there. Skipping it just rolls again.
            if (item != null)
            {
                DropItem(item);
            }
        }
    }

    public virtual int ItemAmount => 0;

    public static int RewardBagHue()
    {
        if (Utility.RandomDouble() < 0.005)
        {
            return 0;
        }

        var row = Utility.Random(_rewardBagHues.Length / 2) * 2;

        return Utility.RandomMinMax(_rewardBagHues[row], _rewardBagHues[row + 1]);
    }

    public static void ApplyMods(Item item)
    {
        if (item == null)
        {
            return;
        }

        var attributeCount = Utility.RandomMinMax(1, 5);

        // ServUO's order (BaseReward.cs:77-84); its BaseRanged case is a BaseWeapon and never reached.
        if (item is BaseJewel jewel)
        {
            BaseRunicTool.ApplyAttributesTo(jewel, false, 0, attributeCount, 10, 100);
        }
        else if (item is BaseWeapon weapon)
        {
            BaseRunicTool.ApplyAttributesTo(weapon, false, 0, attributeCount, 10, 100);
        }
        else if (item is BaseArmor armor)
        {
            BaseRunicTool.ApplyAttributesTo(armor, false, 0, attributeCount, 10, 100);
        }
    }

    public static Item Jewlery()
    {
        Item item = Loot.RandomJewelry();
        ApplyMods(item);
        return item;
    }

    public static Item RangedWeapon()
    {
        Item item = Loot.RandomRangedWeapon(false, true);
        ApplyMods(item);
        return item;
    }

    public static Item Armor()
    {
        Item item = Loot.RandomArmor(false, true);
        ApplyMods(item);
        return item;
    }

    public static Item Weapon()
    {
        Item item = Loot.RandomWeapon(false, true);
        ApplyMods(item);
        return item;
    }

    public static Item RandomRareGem() => Loot.Construct(_rareGemTypes);

    public static Item RandomMLResource() => Loot.Construct(_mlResources);

    public static void DropItemStacked(Container container, Item dropped)
    {
        var list = container.Items;

        for (var i = 0; i < list.Count; ++i)
        {
            var item = list[i];

            if (item is not Container && item.StackWith(null, dropped, false))
            {
                return;
            }
        }

        container.DropItem(dropped);
    }
}
