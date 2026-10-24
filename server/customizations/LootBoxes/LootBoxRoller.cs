// cc-P79 (F-40): what goes in a box, rolled the moment it is earned. Notes: shard-migration notes/cc-P79-loot-boxes-fanfare.md.
//
// Slots (LootTable.Slots: 2/3/3/4/5/6): one theme slot, one currency slot (gold, plus Renown from tier 2), the rest from
// the general pool by the tier's weights. Scaling: the character's level (ClusterFLootBoxes.LevelOf, a placeholder until
// F-12 levels exist) sets magic gear intensity and potion strength; the related skill sets the resource tier of the theme
// slot. Box gear is plain quality (never exceptional) with a few modest properties, so it stays below an exceptional
// crafted piece of the same material; no item on the shard is levelable, so none of it is.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;

namespace Server.LootBoxes;

public static class LootBoxRoller
{
    public static string ThemeTitle(BoxTheme theme) => theme switch
    {
        BoxTheme.Miner => "Miner's",
        BoxTheme.Woodsman => "Woodsman's",
        BoxTheme.Artisan => "Artisan's",
        BoxTheme.Hunter => "Hunter's",
        BoxTheme.Explorer => "Explorer's",
        BoxTheme.Tamer => "Tamer's",
        BoxTheme.Scholar => "Scholar's",
        _ => theme.ToString()
    };

    // -- Names and hues --

    /// <summary>
    /// The box's name and hue: a resource theme by its ladder (Miner's metal, Woodsman's wood, Hunter's leather, Artisan's
    /// by its craft), the rest by rarity. Tier 1 to 6 picks the ladder step.
    /// </summary>
    public static (string Name, int Hue) NameAndHue(BoxTheme theme, int tier, SkillName? craft)
    {
        var t = LootTable.Current;
        var ladder = theme switch
        {
            BoxTheme.Miner => t.MetalLadder,
            BoxTheme.Woodsman => t.WoodLadder,
            BoxTheme.Hunter => t.LeatherLadder,
            BoxTheme.Artisan => craft switch
            {
                SkillName.Carpentry or SkillName.Fletching => t.WoodLadder,
                SkillName.Tailoring => t.LeatherLadder,
                _ => t.MetalLadder
            },
            _ => t.Rarity
        };

        var step = ladder[Math.Clamp(tier, 1, 6) - 1];
        return (step.Name, LootTable.HueOf(step));
    }

    // -- Resource tiers by skill --

    private static readonly (CraftResource Res, double Skill)[] Metals =
    [
        (CraftResource.Iron, 0), (CraftResource.DullCopper, 65), (CraftResource.ShadowIron, 70), (CraftResource.Copper, 75),
        (CraftResource.Bronze, 80), (CraftResource.Gold, 85), (CraftResource.Agapite, 90), (CraftResource.Verite, 95),
        (CraftResource.Valorite, 99), (CraftResource.Platinum, ClusterFMetalTiers.Platinum), (CraftResource.Toxic, ClusterFMetalTiers.Toxic),
        (CraftResource.Blaze, ClusterFMetalTiers.Blaze), (CraftResource.Frost, ClusterFMetalTiers.Frost),
        (CraftResource.Obsidian, ClusterFMetalTiers.Obsidian), (CraftResource.Mythril, ClusterFMetalTiers.Mythril),
        (CraftResource.Adamantium, ClusterFMetalTiers.Adamantium), (CraftResource.Celestial, ClusterFMetalTiers.Celestial)
    ];

    private static readonly (CraftResource Res, double Skill)[] Woods =
    [
        (CraftResource.RegularWood, 0), (CraftResource.OakWood, 65), (CraftResource.AshWood, 80), (CraftResource.YewWood, 95),
        (CraftResource.Heartwood, 100), (CraftResource.Bloodwood, 100), (CraftResource.Frostwood, 100),
        (CraftResource.Ironwood, ClusterFWoodTiers.Ironwood), (CraftResource.Ghostwood, ClusterFWoodTiers.Ghostwood),
        (CraftResource.Emberbark, ClusterFWoodTiers.Emberbark), (CraftResource.Frostbark, ClusterFWoodTiers.Frostbark),
        (CraftResource.Shadowbark, ClusterFWoodTiers.Shadowbark), (CraftResource.Runewood, ClusterFWoodTiers.Runewood),
        (CraftResource.Voidwood, ClusterFWoodTiers.Voidwood), (CraftResource.Starwood, ClusterFWoodTiers.Starwood)
    ];

    private static readonly (CraftResource Res, double Skill)[] Leathers =
    [
        (CraftResource.RegularLeather, 0), (CraftResource.SpinedLeather, 65), (CraftResource.HornedLeather, 80),
        (CraftResource.BarbedLeather, 99)
    ];

    /// <summary>The best resource of a ladder this skill can work (a ladder entry per skill floor).</summary>
    public static CraftResource ResourceFor((CraftResource Res, double Skill)[] ladder, double skill)
    {
        var best = ladder[0].Res;
        foreach (var (res, need) in ladder)
        {
            if (skill >= need)
            {
                best = res;
            }
        }

        return best;
    }

    // The ladder's resource for a general-pool roll: by tier, not skill (tier 6 reaches the top).
    private static CraftResource ResourceForTier((CraftResource Res, double Skill)[] ladder, int tier) =>
        ladder[Math.Clamp((int)Math.Round((ladder.Length - 1) * (tier - 1) / 5.0), 0, ladder.Length - 1)].Res;

    /// <summary>One stack of a resource: index 0 of its types is the ingot, log or leather; 1 the ore, board or hide.</summary>
    public static Item? ResourceStack(CraftResource res, int typeIndex, int amount)
    {
        var info = CraftResources.GetInfo(res);
        if (info == null || info.ResourceTypes.Length == 0)
        {
            return null;
        }

        var type = info.ResourceTypes[Math.Min(typeIndex, info.ResourceTypes.Length - 1)];
        if (type.CreateInstance<Item>() is not { } item)
        {
            return null;
        }

        if (item.Stackable)
        {
            item.Amount = Math.Max(1, amount);
        }

        return item;
    }

    // -- The roll --

    public sealed class Rolled
    {
        public string BoxName = "";
        public int Hue;
        public int Gold;
        public int Renown;
        public readonly List<(Item Item, bool Rare)> Items = new();
        public readonly List<BoxPool> Pools = new();

        // cc-P82: the tier the contents rolled at (the box's own, or one higher on a Fortune's Favor roll).
        public int QualityTier;
    }

    /// <summary>Rolls a box's whole contents for this character now. Nothing is rolled again at opening.</summary>
    public static Rolled Roll(PlayerMobile pm, BoxTheme theme, int tier, SkillName? relatedSkill, string? sourceName = null)
    {
        var t = LootTable.Current;
        tier = Math.Clamp(tier, 1, 6);
        var level = ClusterFLootBoxes.LevelOf(pm);
        var account = pm.Account?.Username ?? "";
        var craft = theme == BoxTheme.Artisan ? relatedSkill ?? BestCraft(pm) : relatedSkill;
        var r = new Rolled();

        (r.BoxName, r.Hue) = NameAndHue(theme, tier, craft);
        r.Gold = t.Gold[tier - 1];
        r.Renown = t.Renown[tier - 1];

        // cc-P82 Fortune's Favor: the contents may roll one quality tier higher. The number of slots and the currency
        // slot's gold stay this box's own tier's, so the number of items never changes; what fills each slot (and a
        // theme stack's size, as tier always set it) follows the quality tier.
        var quality = ClusterFImbueEffects.FortuneQualityTier(pm, tier);
        r.QualityTier = quality;

        // The theme slot.
        foreach (var item in ThemeSlot(pm, theme, quality, craft, account, sourceName, r))
        {
            r.Items.Add((item, false));
        }

        // The general pool: every slot past theme and currency.
        for (var i = 2; i < t.SlotsFor(tier); i++)
        {
            var pool = PickPool(quality);
            r.Pools.Add(pool);
            var item = PoolItem(pool, pm, quality, level, account, theme);
            if (item != null)
            {
                r.Items.Add((item, pool == BoxPool.Rare));
            }
        }

        return r;
    }

    public static BoxPool PickPool(int tier)
    {
        var t = LootTable.Current;
        var total = 0;
        foreach (var pool in Enum.GetValues<BoxPool>())
        {
            total += t.Weight(pool, tier);
        }

        var roll = Utility.Random(Math.Max(1, total));
        foreach (var pool in Enum.GetValues<BoxPool>())
        {
            roll -= t.Weight(pool, tier);
            if (roll < 0)
            {
                return pool;
            }
        }

        return BoxPool.Consumables;
    }

    private static SkillName BestCraft(PlayerMobile pm)
    {
        SkillName[] crafts =
        [
            SkillName.Blacksmith, SkillName.Tailoring, SkillName.Carpentry, SkillName.Fletching, SkillName.Tinkering,
            SkillName.Alchemy, SkillName.Inscribe, SkillName.Cooking
        ];

        var best = crafts[0];
        foreach (var s in crafts)
        {
            if (pm.Skills[s].Base > pm.Skills[best].Base)
            {
                best = s;
            }
        }

        return best;
    }

    private static List<Item> ThemeSlot(PlayerMobile pm, BoxTheme theme, int tier, SkillName? craft, string account, string? sourceName, Rolled r)
    {
        var list = new List<Item>();
        void Add(Item? i)
        {
            if (i != null)
            {
                list.Add(i);
            }
        }

        switch (theme)
        {
            case BoxTheme.Miner:
                Add(ResourceStack(ResourceFor(Metals, pm.Skills[SkillName.Mining].Base), 1, 10 + 10 * tier));
                Add(Gems(tier));
                if (Utility.RandomDouble() < 0.1 * tier)
                {
                    Add(new LootBoxPlaceholder("Ore Satchel Upgrade"));
                }

                break;
            case BoxTheme.Woodsman:
                Add(ResourceStack(ResourceFor(Woods, pm.Skills[SkillName.Lumberjacking].Base), 0, 10 + 10 * tier));
                if (Utility.RandomDouble() < 0.1 * tier)
                {
                    Add(new LootBoxPlaceholder("Grove Ledger Page"));
                }

                break;
            case BoxTheme.Artisan:
                var skill = craft ?? SkillName.Blacksmith;
                Add(CraftResourcesFor(skill, pm.Skills[skill].Base, tier));
                Add(CraftRecipe(skill, tier));
                break;
            case BoxTheme.Hunter:
                Add(ResourceStack(ResourceForTier(Leathers, tier), 1, 10 + 5 * tier)); // hides: the monster part
                Add(SlayerWeapon(tier));
                break;
            case BoxTheme.Explorer:
                Add(new TreasureMap(Math.Clamp(tier, 1, 6), Map.Trammel));
                Add(new BlankMap());
                break;
            case BoxTheme.Tamer:
                Add(new LootBoxPlaceholder("Companion Treat"));
                Add(RewardMounts.LootBoxMount(theme, tier, Utility.RandomDouble(), Utility.Random(1000))); // cc-P365
                break;
            case BoxTheme.Scholar:
                Add(Reagents(tier));
                Add(Loot.RandomScroll(0, Math.Clamp(tier * 8, 8, 63), SpellbookType.Regular));
                break;
            case BoxTheme.Boss:
                Add(new LootBoxPlaceholder($"Trophy of {sourceName ?? "a boss"}"));
                Add(RewardMounts.LootBoxMount(theme, tier, Utility.RandomDouble(), Utility.Random(1000))); // cc-P365
                break;
            case BoxTheme.Loop:
                Add(new LoopScrollFragment());
                Add(new BoundCosmeticSash(account, $"Loop {ClusterFLootBoxes.CurrentLoop(pm)}", LootTable.HueOf(LootTable.Current.Rarity[5])));
                break;
            case BoxTheme.League:
                r.Renown += 50 * tier;
                Add(new LootBoxPlaceholder("League Shop Discount Token"));
                break;
        }

        return list;
    }

    private static Item? PoolItem(BoxPool pool, PlayerMobile pm, int tier, int level, string account, BoxTheme theme) => pool switch
    {
        BoxPool.Consumables => Consumable(level, tier),
        BoxPool.Resources => GeneralResource(tier),
        BoxPool.MagicGear => MagicGear(level, tier),
        BoxPool.Recipes => Recipe(tier),
        BoxPool.Cosmetics => new BoundCosmeticSash(account, "Box", UniqueHue()),
        BoxPool.Rare => RareItem(pm, tier, account, theme),
        _ => null
    };

    // -- Pool items --

    private static Item? Consumable(int level, int tier)
    {
        if (Utility.RandomBool())
        {
            return new Bandage(10 + 10 * tier);
        }

        Type[] potions = level < 30
            ? [typeof(LesserHealPotion), typeof(LesserCurePotion), typeof(RefreshPotion)]
            : level < 70
                ? [typeof(HealPotion), typeof(CurePotion), typeof(RefreshPotion)]
                : [typeof(GreaterHealPotion), typeof(GreaterCurePotion), typeof(TotalRefreshPotion), typeof(GreaterStrengthPotion), typeof(GreaterAgilityPotion)];

        var potion = potions[Utility.Random(potions.Length)].CreateInstance<Item>();
        if (potion is { Stackable: true })
        {
            potion.Amount = 2 + tier;
        }

        return potion;
    }

    private static Item? GeneralResource(int tier) => Utility.Random(4) switch
    {
        0 => ResourceStack(ResourceForTier(Metals, tier), 0, 10 * tier),
        1 => ResourceStack(ResourceForTier(Woods, tier), 1, 10 * tier),
        2 => ResourceStack(ResourceForTier(Leathers, tier), 0, 10 * tier),
        _ => Gems(tier)
    };

    private static Item Gems(int tier)
    {
        var gem = Loot.RandomGem();
        gem.Amount = tier;
        return gem;
    }

    private static Item Reagents(int tier)
    {
        var reg = Loot.RandomReagent();
        reg.Amount = 10 * tier;
        return reg;
    }

    /// <summary>
    /// A random weapon, armor piece, shield or jewel, plain quality, with 1 to 4 properties at an intensity set by level
    /// (at most 60 percent): below an exceptional crafted piece of its material.
    /// </summary>
    public static Item? MagicGear(int level, int tier)
    {
        var item = Loot.RandomArmorOrShieldOrWeaponOrJewelry();
        var props = Math.Clamp(1 + tier / 2, 1, 4);
        var max = Math.Clamp(20 + level / 4, 20, 60);
        var min = Math.Max(10, max / 3);

        switch (item)
        {
            case BaseWeapon w:
                BaseRunicTool.ApplyAttributesTo(w, props, min, max);
                break;
            case BaseArmor a:
                BaseRunicTool.ApplyAttributesTo(a, props, min, max);
                break;
            case BaseJewel j:
                BaseRunicTool.ApplyAttributesTo(j, props, min, max);
                break;
        }

        return item;
    }

    private static Item? SlayerWeapon(int tier)
    {
        var weapon = Loot.RandomWeapon();
        if (weapon == null)
        {
            return null;
        }

        var slayers = Enum.GetValues<SlayerName>();
        weapon.Slayer = slayers[Utility.RandomMinMax(1, slayers.Length - 1)];
        return weapon;
    }

    // F-43's reward hook first ("recipe scroll from table"), then a stock ML recipe scroll.
    private static Item? Recipe(int tier)
    {
        var fromTable = ClusterFRecipeRewards.CreateFromTable("bandV", RecipeRewardSource.LootBox);
        if (fromTable != null && Utility.RandomBool())
        {
            return fromTable;
        }

        var ids = new List<int>();
        foreach (var id in Engines.Craft.Recipe.Recipes.Keys)
        {
            if (id < 9000)
            {
                ids.Add(id);
            }
        }

        if (ids.Count == 0)
        {
            return fromTable;
        }

        fromTable?.Delete();
        return new RecipeScroll(ids[Utility.Random(ids.Count)]);
    }

    private static Item? CraftResourcesFor(SkillName skill, double value, int tier) => skill switch
    {
        SkillName.Carpentry or SkillName.Fletching => ResourceStack(ResourceFor(Woods, value), 1, 10 + 10 * tier),
        SkillName.Tailoring => ResourceStack(ResourceFor(Leathers, value), 0, 10 + 10 * tier),
        SkillName.Alchemy => Reagents(tier),
        SkillName.Inscribe => new BlankScroll(10 * tier),
        SkillName.Cooking => new RawRibs(5 * tier),
        _ => ResourceStack(ResourceFor(Metals, value), 0, 10 + 10 * tier)
    };

    // The Artisan's recipe: a scroll for one of the craft's own scroll-only designs, else its band book for the tier.
    private static Item? CraftRecipe(SkillName skill, int tier)
    {
        var keys = ClusterFRecipeRewards.BandVKeys(skill);
        if (keys.Count > 0 && tier >= 5)
        {
            return ClusterFRecipeRewards.CreateScroll(keys[Utility.Random(keys.Count)]);
        }

        var system = ClusterFRecipeGate.SystemFor(skill);
        if (system == null)
        {
            return Recipe(tier);
        }

        var books = ClusterFRecipeGate.BooksFor(system);
        return books.Count > 0
            ? new PatternBook(skill, books[Math.Clamp(tier - 1, 0, books.Count - 1)])
            : Recipe(tier);
    }

    private static int UniqueHue() => Utility.RandomList(0x0489, 0x0493, 0x04F2, 0x0501, 0x0515, 0x052D, 0x0555, 0x0581);

    private static Item? RareItem(PlayerMobile pm, int tier, string account, BoxTheme theme) => Utility.Random(7) switch
    {
        0 => new LoopScrollFragment(),
        1 => new LootBoxPlaceholder("Unique Title", account),
        2 => new LootBoxPlaceholder("Unique Hue", account),
        3 => MuleSchematic(tier),
        4 => new LootBoxPlaceholder("Companion Item"),
        5 => new EpicComponent(Utility.RandomList("Starforged Filament", "Voidglass Shard", "Heartsteel Ember", "Loopwoven Thread")),
        _ => tier > 1 ? BoxInBox(pm, theme, tier - 1) : new EpicComponent("Loopwoven Thread")
    };

    private static Item MuleSchematic(int tier)
    {
        var modules = Enum.GetValues<MuleModuleId>();
        var id = modules[Utility.RandomMinMax(1, modules.Length - 1)];
        var moduleTier = Math.Clamp(tier - 2, 1, MuleModuleData.MaxTier);
        return ClusterFRecipeRewards.CreateScroll(ClusterFRecipeStore.RecipeKey(MuleModuleCrafts.RecipeId(id, moduleTier)));
    }

    /// <summary>A box one tier lower, same theme, rolled now like any box.</summary>
    public static LootBox BoxInBox(PlayerMobile pm, BoxTheme theme, int tier) =>
        ClusterFLootBoxes.MakeBox(pm, theme, tier, null, "a box in a box", null);

    /// <summary>A plain name for the reveal: the item's name, else its type name in words ("GreaterHealPotion" to "Greater Heal Potion").</summary>
    public static string Describe(Item item)
    {
        var name = item.Name ?? item.DefaultName;
        if (string.IsNullOrEmpty(name))
        {
            var sb = new System.Text.StringBuilder();
            foreach (var ch in item.GetType().Name)
            {
                if (char.IsUpper(ch) && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(ch);
            }

            name = sb.ToString();
        }

        return item.Amount > 1 ? $"{item.Amount} x {name}" : name;
    }
}
