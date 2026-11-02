// cc-P79 (F-40): what goes in a box, rolled the moment it is earned. Notes: shard-migration notes/cc-P79-loot-boxes-fanfare.md.
// cc-P763: the contents come from the table's entries (LootTable.Pools, Themes, Seasons; notes/cc-P759-loot-plan.md and
// notes/cc-P763-cont-loot-boxes-diversified.md), and the rarity rolled at the earn adds to them.
//
// Slots (LootTable.Slots: 2/3/3/4/5/6 by grade, plus the rarity's ExtraSlots): one theme slot, one currency slot (gold,
// plus Renown from grade 2, both times the rarity's stack multiplier), the rest from the general pools by the grade's
// weights, the rarity's PoolShift added to Rare and Cosmetics. An Epic box adds its theme's Epic list, a Legendary its
// Legendary list; a box earned while a season is on adds one item from that season's pool.
//
// Scaling: the character's level (ClusterFLootBoxes.LevelOf, a placeholder until F-12 levels exist) sets magic gear
// intensity and potion strength; the related skill sets the resource tier of the theme slot. Box gear is plain quality
// (never exceptional) with a few modest properties, so it stays below an exceptional crafted piece of the same material;
// no item on the shard is levelable, so none of it is.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Engines.Craft;
using Server.Engines.Plants;
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
        BoxTheme.Fisher => "Fisher's",
        BoxTheme.Sailor => "Sailor's",
        BoxTheme.Herbalist => "Herbalist's",
        BoxTheme.Delver => "Delver's",
        BoxTheme.LeagueDive => "League Dive",
        _ => theme.ToString()
    };

    // -- Names and hues --

    /// <summary>The grade's step name: a resource theme by its ladder (Artisan's and Society's by the craft), the rest by grade.</summary>
    public static string GradeName(BoxTheme theme, int tier, SkillName? craft)
    {
        var t = LootTable.Current;
        var ladder = t.ThemeOf(theme).Ladder switch
        {
            "Metal" => t.MetalLadder,
            "Wood" => t.WoodLadder,
            "Leather" => t.LeatherLadder,
            "Craft" => craft switch
            {
                SkillName.Carpentry or SkillName.Fletching or SkillName.Lumberjacking => t.WoodLadder,
                SkillName.Tailoring => t.LeatherLadder,
                _ => t.MetalLadder
            },
            _ => t.Grades
        };

        return ladder[Math.Clamp(tier, 1, 6) - 1].Name;
    }

    /// <summary>
    /// cc-P763: the box's name and hue. The name puts the season (when one is on), the rarity, the grade and the theme
    /// together ("Halloween Epic Fine Explorer's"; the box adds " Box"); the hue is the rarity's (grey, blue, purple, orange).
    /// </summary>
    public static (string Name, int Hue) NameAndHue(BoxTheme theme, int tier, SkillName? craft,
        BoxRarity rarity = BoxRarity.Common, string? seasonLabel = null)
    {
        var step = LootTable.Current.RarityOf(rarity);
        var name = $"{step.Name} {GradeName(theme, tier, craft)} {ThemeTitle(theme)}";
        if (!string.IsNullOrEmpty(seasonLabel))
        {
            name = $"{seasonLabel} {name}";
        }

        return (name, step.Hue);
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

    // The ladder's resource for a general-pool roll: by grade, not skill (grade 6 reaches the top).
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

        // cc-P82: the grade the contents rolled at (the box's own, or one higher on a Fortune's Favor roll).
        public int QualityTier;

        // cc-P763: the rarity rolled at the earn, and the season whose slot it got (null out of season).
        public BoxRarity Rarity;
        public string? Season;
    }

    // Everything one roll needs.
    private sealed class Ctx
    {
        public required PlayerMobile Pm;
        public required BoxTheme Theme;
        public required int Grade;
        public required BoxRarity Rarity;
        public required SkillName? Craft;
        public required string Account;
        public required string? SourceName;
        public required int Level;
        public required Rolled R;
        public required int BoxGrade;

        public double Stack => LootTable.Current.RarityOf(Rarity).StackMultiplier;
    }

    /// <summary>
    /// Rolls a box's whole contents for this character now. Nothing is rolled again at opening. cc-P763: the rarity and
    /// the season are the earn's (ClusterFLootBoxes.MakeBox rolls them); a caller that names neither gets a Common box
    /// with no season slot, as every box was before.
    /// </summary>
    public static Rolled Roll(PlayerMobile pm, BoxTheme theme, int tier, SkillName? relatedSkill, string? sourceName = null,
        BoxRarity rarity = BoxRarity.Common, string? season = null)
    {
        var t = LootTable.Current;
        tier = Math.Clamp(tier, 1, 6);
        var craft = theme is BoxTheme.Artisan or BoxTheme.Society ? relatedSkill ?? BestCraft(pm) : relatedSkill;
        var step = t.RarityOf(rarity);
        var seasonDef = season != null && t.Seasons.TryGetValue(season, out var s) ? s : null;
        var r = new Rolled { Rarity = rarity, Season = seasonDef != null ? season : null };

        (r.BoxName, r.Hue) = NameAndHue(theme, tier, craft, rarity, seasonDef?.Label);
        r.Gold = (int)Math.Round(t.Gold[tier - 1] * step.StackMultiplier);
        r.Renown = (int)Math.Round(t.Renown[tier - 1] * step.StackMultiplier);

        // cc-P82 Fortune's Favor: the contents may roll one quality grade higher. The number of slots and the currency
        // slot's gold stay this box's own grade's, so the number of items never changes; what fills each slot (and a
        // theme stack's size) follows the quality grade.
        var quality = ClusterFImbueEffects.FortuneQualityTier(pm, tier);
        r.QualityTier = quality;

        var c = new Ctx
        {
            Pm = pm, Theme = theme, Grade = quality, Rarity = rarity, Craft = craft, Account = pm.Account?.Username ?? "",
            SourceName = sourceName, Level = ClusterFLootBoxes.LevelOf(pm), R = r, BoxGrade = tier
        };

        var def = t.ThemeOf(theme);

        // The theme slot.
        foreach (var item in GiveList(def.Slot, c))
        {
            r.Items.Add((item, false));
        }

        // The general pool: every slot past theme and currency, and the rarity's extra slots.
        var poolSlots = t.SlotsFor(tier) - 2 + step.ExtraSlots;
        for (var i = 0; i < poolSlots; i++)
        {
            var pool = PickPool(quality, rarity);
            r.Pools.Add(pool);
            var item = PoolItem(pool, c);
            if (item != null)
            {
                r.Items.Add((item, pool == BoxPool.Rare));
            }
        }

        // cc-P763: the rarity's guaranteed list.
        var guaranteed = rarity switch
        {
            BoxRarity.Epic => def.Epic,
            BoxRarity.Legendary => def.Legendary,
            _ => []
        };

        foreach (var item in GiveList(guaranteed, c))
        {
            r.Items.Add((item, true));
        }

        // cc-P763: the season's slot.
        if (seasonDef != null && PickEntry(seasonDef.Items, c) is { } seasonal && Make(seasonal, c) is { } seasonItem)
        {
            r.Items.Add((seasonItem, false));
        }

        return r;
    }

    /// <summary>A pool by the grade's weights; cc-P763: a rarity above Common adds its PoolShift to Rare and Cosmetics.</summary>
    public static BoxPool PickPool(int tier, BoxRarity rarity = BoxRarity.Common)
    {
        var t = LootTable.Current;
        var shift = t.RarityOf(rarity).PoolShift;

        int WeightOf(BoxPool pool) => t.Weight(pool, tier) + (pool is BoxPool.Rare or BoxPool.Cosmetics ? shift : 0);

        var total = 0;
        foreach (var pool in Enum.GetValues<BoxPool>())
        {
            total += WeightOf(pool);
        }

        var roll = Utility.Random(Math.Max(1, total));
        foreach (var pool in Enum.GetValues<BoxPool>())
        {
            roll -= WeightOf(pool);
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

    // -- Entries --

    /// <summary>A theme slot or list: each entry with no group (a Chance entry by its chance), and one per group by weight.</summary>
    private static List<Item> GiveList(PoolEntry[] list, Ctx c)
    {
        var made = new List<Item>();
        var groups = new Dictionary<string, List<PoolEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in list)
        {
            if (!e.OfferedAt(c.Grade, c.Rarity))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(e.Group))
            {
                if (!groups.TryGetValue(e.Group, out var g))
                {
                    groups[e.Group] = g = new List<PoolEntry>();
                }

                g.Add(e);
                continue;
            }

            if (e.Chance != null && Utility.Random(100) >= e.Chance[Math.Clamp((int)c.Rarity, 0, 3)])
            {
                continue;
            }

            if (Make(e, c) is { } item)
            {
                made.Add(item);
            }
        }

        foreach (var g in groups.Values)
        {
            if (PickEntry(g.ToArray(), c) is { } e && Make(e, c) is { } item)
            {
                made.Add(item);
            }
        }

        return made;
    }

    /// <summary>One entry by weight among those offered at this grade and rarity; null when none is.</summary>
    private static PoolEntry? PickEntry(PoolEntry[] entries, Ctx c, Func<PoolEntry, bool>? allow = null)
    {
        var total = 0;
        foreach (var e in entries)
        {
            if (e.OfferedAt(c.Grade, c.Rarity) && (allow == null || allow(e)))
            {
                total += e.Weight;
            }
        }

        if (total <= 0)
        {
            return null;
        }

        var roll = Utility.Random(total);
        foreach (var e in entries)
        {
            if (!e.OfferedAt(c.Grade, c.Rarity) || allow != null && !allow(e))
            {
                continue;
            }

            roll -= e.Weight;
            if (roll < 0)
            {
                return e;
            }
        }

        return null;
    }

    /// <summary>A pool's item. A pick that makes nothing (a box in a box at grade 1) is picked again; bandages at the last.</summary>
    private static Item? PoolItem(BoxPool pool, Ctx c, Func<PoolEntry, bool>? allow = null)
    {
        var entries = LootTable.Current.PoolEntries(pool);
        for (var tries = 0; tries < 4; tries++)
        {
            if (PickEntry(entries, c, allow) is { } e && Make(e, c) is { } item)
            {
                return item;
            }
        }

        return new Bandage(10 + 10 * c.Grade);
    }

    /// <summary>The amount an entry gives: Min to Max, plus PerGrade per grade, times the rarity's stack multiplier.</summary>
    private static int AmountOf(PoolEntry e, Ctx c)
    {
        var n = e.Max > e.Min ? Utility.RandomMinMax(e.Min, e.Max) : e.Min;
        n += e.PerGrade * c.Grade;
        return Math.Max(1, (int)Math.Round(n * c.Stack));
    }

    /// <summary>The specials an entry may name (made in code below); every other Item is a type name.</summary>
    public static readonly HashSet<string> Specials = new(StringComparer.OrdinalIgnoreCase)
    {
        "@Ore", "@Boards", "@Hides", "@Metal", "@Wood", "@Leather", "@Gems", "@CraftResources", "@PatternBook",
        "@CraftTool", "@RunicTool", "@SlayerWeapon", "@TreasureMap", "@Mount", "@Reagents", "@Scroll", "@Trophy",
        "@LoopSash", "@LeagueCloak", "@LeagueTitle", "@Potion", "@MagicGear", "@Recipe", "@BoxSash", "@BoxInBox",
        "@MuleSchematic", "@BagOfHolding", "@ReagentBag", "@Decor", "@RenownBonus", "@RarePick", "@Seed", "@RunicMallet"
    };

    /// <summary>Why an entry cannot be made, or null when it can (LootTable.Parse refuses a file with any).</summary>
    public static string? CheckEntry(PoolEntry e)
    {
        if (e.Item.StartsWith('@'))
        {
            if (!Specials.Contains(e.Item))
            {
                return $"'{e.Item}' is not a special ({string.Join(", ", Specials)}).";
            }

            return e.Item.ToLowerInvariant() switch
            {
                "@decor" when e.ItemId is < 1 or > 0xFFFF || string.IsNullOrWhiteSpace(e.Name) => "@Decor needs an itemId and a name.",
                "@bagofholding" when e.Arg is < 1 or > 5 => "@BagOfHolding needs arg 1 to 5 (the bag's size).",
                "@treasuremap" when e.Arg is < 0 or > 6 => "@TreasureMap's arg is a map level 1 to 6 (0: the grade).",
                "@runictool" when e.Arg is < 0 or > 1 => "@RunicTool's arg is 0 (the lowest material) or 1 (one step up).",
                _ => null
            };
        }

        var type = AssemblyHandler.FindTypeByName(e.Item);
        if (type == null || !typeof(Item).IsAssignableFrom(type) || type.IsAbstract)
        {
            return $"'{e.Item}' is not an item type.";
        }

        // Made as CreateInstance makes it: a constructor whose every parameter is optional counts (Bandage(int amount = 1)).
        return ActivatorExtensions.GetConstructor(type, null, Type.EmptyTypes, out _) == null
            ? $"'{e.Item}' cannot be made with no arguments."
            : null;
    }

    /// <summary>Makes an entry's item for this roll, or null (a mount chance that missed, a Renown bonus, no recipe).</summary>
    private static Item? Make(PoolEntry e, Ctx c)
    {
        Item? item;
        if (!e.Item.StartsWith('@'))
        {
            item = AssemblyHandler.FindTypeByName(e.Item)?.CreateInstance<Item>();
            if (item == null)
            {
                return null;
            }

            // A stackable takes the entry's amount; a non-stackable is one (the stack multiplier never makes copies).
            if (item.Stackable)
            {
                item.Amount = AmountOf(e, c);
            }

            item.Personalize(c.Pm);
        }
        else
        {
            item = MakeSpecial(e, c);
        }

        if (item != null && e.Hue != 0)
        {
            item.Hue = e.Hue;
        }

        return item;
    }

    // A Noted joke item is personalized to its opener, as the Dented Box's is.
    private static void Personalize(this Item item, PlayerMobile pm)
    {
        if (item is INotedJoke joke)
        {
            joke.Personalize(pm);
        }
    }

    private static Item? MakeSpecial(PoolEntry e, Ctx c)
    {
        var t = LootTable.Current;
        var pm = c.Pm;
        var craft = e.Skill != null && Enum.TryParse<SkillName>(e.Skill, true, out var named) ? named : c.Craft ?? SkillName.Blacksmith;

        switch (e.Item.ToLowerInvariant())
        {
            case "@ore": return ResourceStack(ResourceFor(Metals, pm.Skills[SkillName.Mining].Base), 1, AmountOf(e, c));
            case "@boards": return ResourceStack(ResourceFor(Woods, pm.Skills[SkillName.Lumberjacking].Base), 1, AmountOf(e, c));
            case "@hides": return ResourceStack(ResourceForTier(Leathers, c.Grade), 1, AmountOf(e, c));
            case "@metal": return ResourceStack(ResourceForTier(Metals, c.Grade), 0, AmountOf(e, c));
            case "@wood": return ResourceStack(ResourceForTier(Woods, c.Grade), 1, AmountOf(e, c));
            case "@leather": return ResourceStack(ResourceForTier(Leathers, c.Grade), 0, AmountOf(e, c));
            case "@gems": return Gems(AmountOf(e, c));
            case "@reagents": return Reagents(AmountOf(e, c));
            case "@craftresources": return CraftResourcesFor(craft, pm.Skills[craft].Base, AmountOf(e, c));
            case "@patternbook": return CraftRecipe(craft, c.Grade, c);
            case "@crafttool": return CraftTool(craft);
            case "@runictool": return RunicTool(craft, e.Arg) ?? PoolItem(BoxPool.Rare, c, NoRecursion);
            case "@slayerweapon": return SlayerWeapon(e.Arg, c.Level);
            case "@treasuremap": return new TreasureMap(e.Arg > 0 ? e.Arg : Math.Clamp(c.Grade, 1, 6), Map.Trammel);
            case "@mount": return RewardMounts.LootBoxMount(c.Theme, c.Grade, Utility.RandomDouble(), Utility.Random(1000)); // cc-P365
            case "@scroll": return Loot.RandomScroll(0, Math.Clamp(c.Grade * 8, 8, 63), SpellbookType.Regular);
            case "@trophy": return Trophy(c.SourceName);
            case "@loopsash":
                return new BoundCosmeticSash(c.Account, $"Loop {ClusterFLootBoxes.CurrentLoop(pm)}", LootTable.HueOf(t.Grades[5]));
            case "@leaguecloak":
                return new BoundCosmeticSash(c.Account, "League", ClusterFLeagueRanks.LabelHue(Math.Max(1, ClusterFLeagueRanks.GetRank(pm))));
            case "@leaguetitle": return LeagueTitleDeed.ForAccount(pm) ?? PoolItem(BoxPool.Rare, c, NoTitle);
            case "@potion": return Potion(c.Level, AmountOf(e, c));
            case "@magicgear": return MagicGear(c.Level, c.Grade);
            case "@recipe": return Recipe(pm, c.Grade);
            case "@boxsash": return new BoundCosmeticSash(c.Account, "Box", UniqueHue());
            case "@boxinbox": return c.BoxGrade > 1 ? BoxInBox(pm, c.Theme, c.BoxGrade - 1) : null;
            case "@muleschematic": return MuleSchematic(c.Grade);
            case "@bagofholding": return new BagOfHolding(e.Arg);
            case "@reagentbag": return new ReagentBag(t.ReagentBagCut[Math.Clamp((int)c.Rarity, 0, 3)]);
            case "@decor": return new LootDecor(e.ItemId, e.AltId, e.Name ?? "a decoration");
            case "@renownbonus":
                c.R.Renown += Math.Max(t.Renown[c.BoxGrade - 1] * e.Arg / 100, 25 * c.Grade);
                return null;
            case "@rarepick": return PoolItem(BoxPool.Rare, c, NoRecursion);
            case "@runicmallet": return new RunicMalletAndChisel(e.Arg == 1 ? CraftResource.ShadowIron : CraftResource.DullCopper); // masonry's runic
            case "@seed":
            {
                var seed = e.Arg == 1 ? Seed.RandomPeculiarSeed(Utility.RandomMinMax(1, 4)) : new Seed();
                if (seed.Stackable)
                {
                    seed.Amount = AmountOf(e, c);
                }

                return seed;
            }
            default: return null;
        }
    }

    private static bool NoRecursion(PoolEntry e) => !e.Item.Equals("@RarePick", StringComparison.OrdinalIgnoreCase);

    private static bool NoTitle(PoolEntry e) => NoRecursion(e) && !e.Item.Equals("@LeagueTitle", StringComparison.OrdinalIgnoreCase);

    // -- Specials --

    private static Item Gems(int amount)
    {
        var gem = Loot.RandomGem();
        gem.Amount = Math.Max(1, amount);
        return gem;
    }

    private static Item Reagents(int amount)
    {
        var reg = Loot.RandomReagent();
        reg.Amount = Math.Max(1, amount);
        return reg;
    }

    private static Item? Potion(int level, int amount)
    {
        Type[] potions = level < 30
            ? [typeof(LesserHealPotion), typeof(LesserCurePotion), typeof(RefreshPotion)]
            : level < 70
                ? [typeof(HealPotion), typeof(CurePotion), typeof(RefreshPotion)]
                : [typeof(GreaterHealPotion), typeof(GreaterCurePotion), typeof(TotalRefreshPotion), typeof(GreaterStrengthPotion), typeof(GreaterAgilityPotion)];

        var potion = potions[Utility.Random(potions.Length)].CreateInstance<Item>();
        if (potion is { Stackable: true })
        {
            potion.Amount = Math.Max(1, amount);
        }

        return potion;
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

    // A slayer weapon; cc-P763: an Epic one carries extra properties at the gear's intensity.
    private static Item? SlayerWeapon(int extraProps, int level)
    {
        var weapon = Loot.RandomWeapon();
        if (weapon == null)
        {
            return null;
        }

        var slayers = Enum.GetValues<SlayerName>();
        weapon.Slayer = slayers[Utility.RandomMinMax(1, slayers.Length - 1)];
        if (extraProps > 0)
        {
            var max = Math.Clamp(20 + level / 4, 20, 60);
            BaseRunicTool.ApplyAttributesTo(weapon, extraProps, Math.Max(10, max / 3), max);
        }

        return weapon;
    }

    // cc-P763: a boss's trophy, from the registered trophy art, named for the boss.
    private static Item Trophy(string? bossName)
    {
        var pair = LootTable.Current.Trophies[Utility.Random(LootTable.Current.Trophies.Length)];
        return new LootDecor(pair[0], pair.Length > 1 ? pair[1] : 0, $"Trophy of {bossName ?? "a boss"}");
    }

    /// <summary>
    /// cc-P763: a recipe the opener can use: F-43's reward hook first ("recipe scroll from table"), else a stock recipe
    /// scroll of a craft where the opener has 50 or more skill; with no such craft, a plain resource of the grade.
    /// </summary>
    private static Item? Recipe(PlayerMobile pm, int tier)
    {
        var ids = new List<int>();
        foreach (var (id, recipe) in Engines.Craft.Recipe.Recipes)
        {
            if (id < 9000 && recipe.CraftSystem != null && pm.Skills[recipe.CraftSystem.MainSkill].Base >= 50)
            {
                ids.Add(id);
            }
        }

        if (ids.Count == 0)
        {
            return GeneralResource(tier);
        }

        if (Utility.RandomBool() && ClusterFRecipeRewards.CreateFromTable("bandV", RecipeRewardSource.LootBox) is { } fromTable)
        {
            return fromTable;
        }

        return new RecipeScroll(ids[Utility.Random(ids.Count)]);
    }

    /// <summary>cc-P763: whether a stock recipe scroll's craft is one the opener has 50 or more in (the test's check).</summary>
    public static bool CanUseRecipe(PlayerMobile pm, int recipeId) =>
        Engines.Craft.Recipe.Recipes.TryGetValue(recipeId, out var r) && r.CraftSystem != null &&
        pm.Skills[r.CraftSystem.MainSkill].Base >= 50;

    private static Item? GeneralResource(int tier) => Utility.Random(4) switch
    {
        0 => ResourceStack(ResourceForTier(Metals, tier), 0, 10 * tier),
        1 => ResourceStack(ResourceForTier(Woods, tier), 1, 10 * tier),
        2 => ResourceStack(ResourceForTier(Leathers, tier), 0, 10 * tier),
        _ => Gems(tier)
    };

    private static Item? CraftResourcesFor(SkillName skill, double value, int amount) => skill switch
    {
        SkillName.Carpentry or SkillName.Fletching or SkillName.Lumberjacking => ResourceStack(ResourceFor(Woods, value), 1, amount),
        SkillName.Tailoring => ResourceStack(ResourceFor(Leathers, value), 0, amount),
        SkillName.Alchemy => Reagents(amount),
        SkillName.Inscribe => new BlankScroll(Math.Max(1, amount)),
        SkillName.Cooking => new RawRibs(Math.Max(1, amount / 2)),
        SkillName.Imbuing => Gems(Math.Max(1, amount / 10)),
        _ => ResourceStack(ResourceFor(Metals, value), 0, amount)
    };

    // cc-P763: the craft's own tool, for a Society box.
    private static Item CraftTool(SkillName skill) => skill switch
    {
        SkillName.Blacksmith => new SmithHammer(),
        SkillName.Tailoring => new SewingKit(),
        SkillName.Carpentry or SkillName.Lumberjacking => new DovetailSaw(),
        SkillName.Fletching => new FletcherTools(),
        SkillName.Alchemy => new MortarPestle(),
        SkillName.Inscribe => new ScribesPen(),
        SkillName.Cooking => new Skillet(),
        SkillName.Cartography => new MapmakersPen(),
        _ => new TinkerTools()
    };

    /// <summary>
    /// cc-P763: the craft's runic tool, its lowest material (step 0) or one up (step 1). Crafts with no runic tool in
    /// stock (Tinkering, Inscription, Alchemy, Cooking, Cartography, Imbuing) give null, and the caller a Rare pick.
    /// </summary>
    public static Item? RunicTool(SkillName skill, int step) => skill switch
    {
        SkillName.Blacksmith => new RunicHammer(step == 0 ? CraftResource.DullCopper : CraftResource.ShadowIron),
        SkillName.Tailoring => new RunicSewingKit(step == 0 ? CraftResource.SpinedLeather : CraftResource.HornedLeather),
        SkillName.Carpentry or SkillName.Lumberjacking => new RunicDovetailSaw(step == 0 ? CraftResource.OakWood : CraftResource.AshWood),
        SkillName.Fletching => new RunicFletcherTool(step == 0 ? CraftResource.OakWood : CraftResource.AshWood),
        _ => null
    };

    // The Artisan's recipe: a scroll for one of the craft's own scroll-only designs, else its band book for the grade.
    private static Item? CraftRecipe(SkillName skill, int tier, Ctx c)
    {
        var keys = ClusterFRecipeRewards.BandVKeys(skill);
        if (keys.Count > 0 && tier >= 5)
        {
            return ClusterFRecipeRewards.CreateScroll(keys[Utility.Random(keys.Count)]);
        }

        var system = ClusterFRecipeGate.SystemFor(skill);
        if (system == null)
        {
            return Recipe(c.Pm, tier);
        }

        var books = ClusterFRecipeGate.BooksFor(system);
        return books.Count > 0
            ? new PatternBook(skill, books[Math.Clamp(tier - 1, 0, books.Count - 1)])
            : Recipe(c.Pm, tier);
    }

    private static int UniqueHue() => Utility.RandomList(0x0489, 0x0493, 0x04F2, 0x0501, 0x0515, 0x052D, 0x0555, 0x0581);

    private static Item MuleSchematic(int tier)
    {
        var modules = Enum.GetValues<MuleModuleId>();
        var id = modules[Utility.RandomMinMax(1, modules.Length - 1)];
        var moduleTier = Math.Clamp(tier - 2, 1, MuleModuleData.MaxTier);
        return ClusterFRecipeRewards.CreateScroll(ClusterFRecipeStore.RecipeKey(MuleModuleCrafts.RecipeId(id, moduleTier)));
    }

    /// <summary>A box one grade lower, same theme, rolled now like any box.</summary>
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
