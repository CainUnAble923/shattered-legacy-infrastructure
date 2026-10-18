// cc-P142 (decisions 3 and 4): what a successful monster steal yields, by tier. OSI has one table: a special thief item
// (StealingHandler.SpecialItemList, the first nine) from Stealing 100. Ours (every number a decision for Chase, notes
// "For Chase"):
//
//   Minor tier, Stealing 60 to 99.9 (decision 3): weaker and smaller things, never an OSI special.
//       35  a coin pouch: 20 + fame / 40 gold, give or take a quarter (fame 1: ~20; fame 30,000: ~770)
//       25  Den scrip, 1 to 3, straight into the thief's Thieves' Den purse
//       15  lockpicks, 2 to 5
//       15  a lesser potion (heal, cure, refresh, agility, strength or night sight)
//       10  bandages, 5 to 10
//   Special tier, Stealing 100 and up (decision 4), rolled in this order on one draw:
//        1%  a loop scroll fragment (rarer than a box: a box's rare pool pays one in seven of 2 to 25 percent a slot)
//        4%  a recipe scroll: three draws in four from the Den's table (the three Den recipes and the Shadow Cloak
//            schematics), the fourth from the Band V pool of every craft (recipes "alongside boxes, bosses and quests")
//       15%  a stolen-only component (StolenComponents.cs), the ones the creature's fame level allows
//       80%  OSI's special item, as cc-P127 rolled it
//   Family flavour (decision 4, last bullet), on top of either tier: a Burrows creature that carries its family part
//   (the P24 part the family drops, BurrowsCommon.cs) gives one more at 10%.

using System;
using System.Collections.Generic;
using Server.Engines.Craft;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;

namespace Server.Engines.CreatureStealing;

public static class StealLootTables
{
    // Minor tier weights (sum 100).
    public const int MinorGoldWeight = 35;
    public const int MinorScripWeight = 25;
    public const int MinorLockpickWeight = 15;
    public const int MinorPotionWeight = 15;
    public const int MinorBandageWeight = 10;

    // Special tier draw thresholds on one RandomDouble.
    public const double FragmentChance = 0.01;
    public const double RecipeChance = 0.04;
    public const double ComponentChance = 0.15;
    public const double DenRecipeShare = 0.75;

    public const double FamilyPartChance = 0.10;

    /// <summary>The Den's recipe keys: the three Den recipes and the Shadow Cloak schematics I to III.</summary>
    public static readonly string[] DenRecipeKeys =
    {
        ClusterFRecipeStore.RecipeKey(ThievesDenCraftRegistrations.ShadowsilkCloakRecipe),
        ClusterFRecipeStore.RecipeKey(ThievesDenCraftRegistrations.ThiefsSmokeBombRecipe),
        ClusterFRecipeStore.RecipeKey(ThievesDenCraftRegistrations.PickpocketsCharmRecipe),
        ClusterFRecipeStore.RecipeKey(MuleModuleCrafts.RecipeId(MuleModuleId.ShadowCloak, 1)),
        ClusterFRecipeStore.RecipeKey(MuleModuleCrafts.RecipeId(MuleModuleId.ShadowCloak, 2)),
        ClusterFRecipeStore.RecipeKey(MuleModuleCrafts.RecipeId(MuleModuleId.ShadowCloak, 3))
    };

    public const string DenRewardTable = "thieves";

    public enum MinorKind
    {
        Gold,
        Scrip,
        Lockpicks,
        Potion,
        Bandages
    }

    public enum SpecialKind
    {
        Fragment,
        Recipe,
        Component,
        OsiSpecial
    }

    /// <summary>The minor-tier pick for a weight roll in [0, 100).</summary>
    public static MinorKind MinorPick(int roll)
    {
        if ((roll -= MinorGoldWeight) < 0)
        {
            return MinorKind.Gold;
        }

        if ((roll -= MinorScripWeight) < 0)
        {
            return MinorKind.Scrip;
        }

        if ((roll -= MinorLockpickWeight) < 0)
        {
            return MinorKind.Lockpicks;
        }

        return roll - MinorPotionWeight < 0 ? MinorKind.Potion : MinorKind.Bandages;
    }

    /// <summary>The special-tier pick for a draw in [0, 1).</summary>
    public static SpecialKind SpecialPick(double draw) =>
        draw < FragmentChance ? SpecialKind.Fragment :
        draw < FragmentChance + RecipeChance ? SpecialKind.Recipe :
        draw < FragmentChance + RecipeChance + ComponentChance ? SpecialKind.Component :
        SpecialKind.OsiSpecial;

    public static int GoldFor(int fame) => 20 + Math.Clamp(fame, 1, 30000) / 40;

    /// <summary>Gives the minor-tier prize and says what it was. Returns the item, or null for scrip (paid into the purse).</summary>
    public static Item GiveMinor(PlayerMobile thief, BaseCreature from, MinorKind kind)
    {
        Item item = null;

        switch (kind)
        {
            case MinorKind.Scrip when thief.Account != null:
                {
                    var scrip = Utility.RandomMinMax(1, 3);
                    ClusterFAccountPersistence.GetOrCreateGuild(thief).AddCurrency(ThievesDenStealRules.GuildKey, scrip);
                    thief.SendMessage(54, $"You pocket {scrip} Thieves' Den scrip from the creature's hoard.");
                    return null;
                }
            case MinorKind.Gold:
            case MinorKind.Scrip: // no account to pay into (never a player): gold instead
                {
                    var gold = GoldFor(from.Fame);
                    item = new Gold(Utility.RandomMinMax(gold * 3 / 4, gold * 5 / 4));
                    break;
                }
            case MinorKind.Lockpicks:
                item = new Lockpick(Utility.RandomMinMax(2, 5));
                break;
            case MinorKind.Potion:
                item = Utility.Random(6) switch
                {
                    0 => new HealPotion(),
                    1 => new LesserCurePotion(),
                    2 => new RefreshPotion(),
                    3 => new AgilityPotion(),
                    4 => new StrengthPotion(),
                    _ => new NightSightPotion()
                };
                break;
            default:
                item = new Bandage(Utility.RandomMinMax(5, 10));
                break;
        }

        thief.SendMessage(54, kind switch
        {
            MinorKind.Gold => $"You lift {item.Amount} gold from the creature.",
            MinorKind.Lockpicks => $"You lift {item.Amount} lockpicks from the creature.",
            MinorKind.Potion => "You lift a small potion from the creature.",
            _ => $"You lift {item.Amount} bandages from the creature."
        });
        thief.AddToBackpack(item);
        return item;
    }

    /// <summary>The component types a creature of this fame level can yield, in order.</summary>
    public static List<Type> ComponentsFor(int fameLevel)
    {
        var list = new List<Type> { typeof(ShadowsilkThread), typeof(EmberAsh) };

        if (fameLevel >= 10)
        {
            list.Add(typeof(Whisperleaf));
        }

        if (fameLevel >= 20)
        {
            list.Add(typeof(ThiefsWire));
        }

        if (fameLevel >= 30)
        {
            list.Add(typeof(LuckyFang));
        }

        return list;
    }

    public static Item Component(BaseCreature from)
    {
        var types = ComponentsFor(StealingHandler.FameLevel(from));
        return types[Utility.Random(types.Count)].CreateInstance<Item>();
    }

    /// <summary>
    /// A recipe scroll: DenRecipeShare of the draws from the Den's table (Configuration/recipe-gating.json's "thieves"
    /// table when it has one, else the built-in keys), the rest from every craft's Band V pool. Null only when both are empty.
    /// </summary>
    public static Item RecipeScroll(double share = -1.0)
    {
        if (share < 0.0)
        {
            share = Utility.RandomDouble();
        }

        if (share < DenRecipeShare)
        {
            var fromTable = ClusterFRecipeRewards.CreateFromTable(DenRewardTable, RecipeRewardSource.Steal);
            if (fromTable != null)
            {
                return fromTable;
            }

            return ClusterFRecipeRewards.CreateScroll(DenRecipeKeys[Utility.Random(DenRecipeKeys.Length)]);
        }

        return ClusterFRecipeRewards.CreateFromTable("bandV", RecipeRewardSource.Steal)
               ?? ClusterFRecipeRewards.CreateScroll(DenRecipeKeys[Utility.Random(DenRecipeKeys.Length)]);
    }

    /// <summary>OSI's special item: ServUO's Random(Length - 2), the lantern and the tool box never (cc-P127).</summary>
    public static Item OsiSpecial() =>
        StealingHandler.SpecialItemList[Utility.Random(StealingHandler.SpecialItemList.Length - 2)].CreateInstance<Item>();

    /// <summary>Makes the special-tier prize for a kind (the message is the caller's).</summary>
    public static Item MakeSpecial(BaseCreature from, SpecialKind kind) => kind switch
    {
        SpecialKind.Fragment => new LoopScrollFragment(),
        SpecialKind.Recipe => RecipeScroll() ?? OsiSpecial(),
        SpecialKind.Component => Component(from),
        _ => OsiSpecial()
    };

    /// <summary>The creature's family part, if it carries one (the Burrows families pack theirs), else null.</summary>
    public static Type FamilyPartOf(BaseCreature from)
    {
        var pack = from?.Backpack;

        if (pack == null)
        {
            return null;
        }

        foreach (var item in pack.Items)
        {
            if (item is BurrowsPartBase)
            {
                return item.GetType();
            }
        }

        return null;
    }

    /// <summary>One more of the creature's family part at FamilyPartChance; the item given, or null.</summary>
    public static Item TryFamilyPart(PlayerMobile thief, BaseCreature from, double roll = -1.0)
    {
        var type = FamilyPartOf(from);

        if (type == null || (roll < 0.0 ? Utility.RandomDouble() : roll) >= FamilyPartChance)
        {
            return null;
        }

        var part = type.CreateInstance<Item>();

        if (part == null)
        {
            return null;
        }

        thief.SendMessage(54, $"A {part.Name ?? "part"} comes away in your hand as well.");
        thief.AddToBackpack(part);
        return part;
    }
}
