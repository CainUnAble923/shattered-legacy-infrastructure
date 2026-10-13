// cc-P78 (F-43): recipe-gated crafting, the gate. Notes: shard-migration notes/cc-P78-recipe-gating.md.
//
// One question, asked in two places: does this account know this craft item?
//   - The craft menu asks it for every item in the shown category (server/patches/CraftGump-recipe-gating.patch, in
//     CraftGump.CreateItemList) and hides what is not known, adding hint lines for the hidden ones.
//   - CraftItem.Craft asks it before anything is made (the same patch, at pinned's recipe check), so Make Last, Last
//     Ten, Make X, a forged button id and the T2A menus are all held to it. Repair, Smelt, Enhance and resource
//     conversion never reach CraftItem.Craft (pinned CraftGump.OnResponse Misc cases 1, 5 and 8), so they are never gated.
//
// An item is, by its craft system and its own data (Classify):
//   Exempt     not gated: Cartography (no bands; region maps are learned by exploring, F-6), Masonry and Glassblowing
//              (pinned already gates the whole system on its book: DefMasonry.cs:40, DefGlassblowing.cs:38; the book is
//              stored on the account by ClusterFRecipeStore.Sync), stock Inscription spell scrolls (gated by owning the
//              spell, DefInscription.cs:62-71).
//   Free       minimum main skill 30 or less (RecipeGatingTable.FreeUpTo).
//   OwnRecipe  the item has a pinned recipe (CraftItem.Recipe: OSI's ML recipes, and our mule schematics). It stays a
//              single scroll, whatever its band: key "recipe:<id>".
//   Book       Band I to IV: key "book:<MainSkill>:<band>", after the merge rule.
//   Scroll     Band V and up: key "item:<MainSkill>:<item type name>".
// A Book item is also known by its own item key, so a single scroll can teach one pattern early.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.Craft;

public enum RecipeKind
{
    Exempt,
    Free,
    OwnRecipe,
    Book,
    Scroll
}

public readonly record struct RecipeClass(RecipeKind Kind, int Band, string? Key, double MinSkill);

/// <summary>One row of the craft menu's item list: a craft item at its own index in the group, or a hint line.</summary>
public readonly record struct CraftListRow(int Index, string? Hint, int HintButton);

public static class ClusterFRecipeGate
{
    // crafting.recipeGating.enabled (default true): off shows the full stock menus and gates nothing but pinned's own
    // recipes, as before cc-P78.
    public static bool Enabled { get; set; } = true;

    // Hint line buttons: HintButtonBase + skill * 16 + band (band 0: the wild line). Far above pinned's own ids
    // (CraftGump.GetButtonID: 1 + type + index * 7).
    public const int HintButtonBase = 1_000_000;

    public static void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("crafting.recipeGating.enabled", true);
    }

    // -- The gated craft systems: one per craft skill with a menu, keyed by its main skill ------------------------

    /// <summary>
    /// Whether a craft system is gated by bands. Not: Cartography, Masonry, Glassblowing (see the header), and any
    /// system this table does not know.
    /// </summary>
    public static bool IsBandGated(CraftSystem? system) =>
        system != null && (
            system == DefBlacksmithy.CraftSystem || system == DefTailoring.CraftSystem ||
            system == DefCarpentry.CraftSystem || system == DefBowFletching.CraftSystem ||
            system == DefTinkering.CraftSystem || system == DefAlchemy.CraftSystem ||
            system == DefInscription.CraftSystem || system == DefCooking.CraftSystem);

    /// <summary>The gated systems, for counts and "learn all". Systems not yet initialized are left out.</summary>
    public static List<CraftSystem> GatedSystems()
    {
        var list = new List<CraftSystem>();
        foreach (var s in new[]
                 {
                     DefBlacksmithy.CraftSystem, DefTailoring.CraftSystem, DefCarpentry.CraftSystem,
                     DefBowFletching.CraftSystem, DefTinkering.CraftSystem, DefAlchemy.CraftSystem,
                     DefInscription.CraftSystem, DefCooking.CraftSystem
                 })
        {
            if (s != null)
            {
                list.Add(s);
            }
        }

        return list;
    }

    public static CraftSystem? SystemFor(SkillName skill)
    {
        foreach (var s in GatedSystems())
        {
            if (s.MainSkill == skill)
            {
                return s;
            }
        }

        return null;
    }

    /// <summary>The item's minimum skill in its system's main skill (pinned CraftItem.cs:884-889 finds it the same way).</summary>
    public static double MinMainSkill(CraftSystem system, CraftItem item)
    {
        foreach (var s in item.Skills)
        {
            if (s.SkillToMake == system.MainSkill)
            {
                return s.MinSkill;
            }
        }

        return item.Skills.Count > 0 ? item.Skills[0].MinSkill : 0.0;
    }

    public static RecipeClass Classify(CraftSystem system, CraftItem item)
    {
        var min = MinMainSkill(system, item);

        if (!IsBandGated(system))
        {
            return new RecipeClass(RecipeKind.Exempt, 0, null, min);
        }

        if (item.Recipe != null)
        {
            return new RecipeClass(RecipeKind.OwnRecipe, RecipeGatingTable.Current.BandOf(min), ClusterFRecipeStore.RecipeKey(item.Recipe.ID), min);
        }

        if (system == DefInscription.CraftSystem && typeof(SpellScroll).IsAssignableFrom(item.ItemType))
        {
            return new RecipeClass(RecipeKind.Exempt, 0, null, min);
        }

        var table = RecipeGatingTable.Current;
        var band = table.BandOf(min);

        if (band == 0)
        {
            return new RecipeClass(RecipeKind.Free, 0, null, min);
        }

        if (table.IsBookBand(band))
        {
            var merged = MergedBand(system, band);
            return new RecipeClass(RecipeKind.Book, merged, ClusterFRecipeStore.BookKey(system.MainSkill, merged), min);
        }

        return new RecipeClass(RecipeKind.Scroll, band, ClusterFRecipeStore.ItemKey(system.MainSkill, item.ItemType), min);
    }

    // -- The merge rule ------------------------------------------------------------------------------------

    // Per system: its item count when counted (our additive registrations grow the lists at ServerStarted, so a count
    // taken before then is redone) and the items per raw band.
    private static readonly Dictionary<CraftSystem, (int Items, int[] Counts)> _bookCounts = new();

    public static void ClearCache() => _bookCounts.Clear();

    /// <summary>Items per raw band (index = band number) that a book would teach: not free, not exempt, no own recipe.</summary>
    public static int[] RawBookBandCounts(CraftSystem system)
    {
        if (_bookCounts.TryGetValue(system, out var cached) && cached.Items == system.CraftItems.Count)
        {
            return cached.Counts;
        }

        var table = RecipeGatingTable.Current;
        var counts = new int[table.Bands.Length + 1];

        foreach (var item in system.CraftItems)
        {
            if (item.Recipe != null ||
                system == DefInscription.CraftSystem && typeof(SpellScroll).IsAssignableFrom(item.ItemType))
            {
                continue;
            }

            var band = table.BandOf(MinMainSkill(system, item));
            if (band > 0 && table.IsBookBand(band))
            {
                counts[band]++;
            }
        }

        _bookCounts[system] = (system.CraftItems.Count, counts);
        return counts;
    }

    /// <summary>
    /// The book a raw band's items are taught by: a book band with fewer than MergeBelow items folds into the next book
    /// band, repeatedly; the last book band never folds (the next band is scrolls).
    /// </summary>
    public static int MergedBand(CraftSystem system, int band)
    {
        var table = RecipeGatingTable.Current;
        var counts = RawBookBandCounts(system);
        var last = table.LastBookBand;

        while (band < last && table.IsBookBand(band) && counts[band] < table.MergeBelow)
        {
            band++;
        }

        return band;
    }

    /// <summary>The books a skill has after the merge rule, lowest first.</summary>
    public static List<int> BooksFor(CraftSystem system)
    {
        var books = new List<int>();
        var table = RecipeGatingTable.Current;
        var counts = RawBookBandCounts(system);

        for (var band = 1; band <= table.LastBookBand; band++)
        {
            if (counts[band] > 0)
            {
                var merged = MergedBand(system, band);
                if (!books.Contains(merged))
                {
                    books.Add(merged);
                }
            }
        }

        return books;
    }

    // -- Known ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Whether this crafter may see and make the item. With gating off: free, except a pinned recipe item, which needs
    /// its recipe on the character or the account (pinned's own rule, made account-wide). Staff see everything.
    /// </summary>
    public static bool IsKnown(Mobile? from, CraftSystem system, CraftItem item)
    {
        if (from is not PlayerMobile pm || pm.AccessLevel >= AccessLevel.GameMaster)
        {
            return true;
        }

        var c = Classify(system, item);

        if (c.Kind == RecipeKind.OwnRecipe)
        {
            return ClusterFRecipeStore.Knows(pm, c.Key!);
        }

        if (!Enabled)
        {
            return true;
        }

        return c.Kind switch
        {
            RecipeKind.Book => ClusterFRecipeStore.Knows(pm, c.Key!) ||
                               ClusterFRecipeStore.Knows(pm, ClusterFRecipeStore.ItemKey(system.MainSkill, item.ItemType)),
            RecipeKind.Scroll => ClusterFRecipeStore.Knows(pm, c.Key!),
            _ => true
        };
    }

    // -- The menu rows and their hint lines ---------------------------------------------------------------------

    /// <summary>
    /// The rows the craft menu draws for one category: each known item at its own index in the group (so the menu's
    /// button ids still name the right item), then the hint lines for the hidden ones. A book hint names its count, the
    /// owning guild and the rank that sells the book; it shows once the crafter's skill reaches the lowest band floor
    /// among its items. One wild line covers every scroll-only item (Band V and up, and pinned recipes), with no count.
    /// </summary>
    public static List<CraftListRow> ItemRows(Mobile? from, CraftSystem system, CraftGroup group)
    {
        var rows = new List<CraftListRow>();
        var items = group.CraftItems;

        if (from is PlayerMobile pm0 && pm0.AccessLevel < AccessLevel.GameMaster)
        {
            ClusterFRecipeStore.Sync(pm0);
            ClusterFGuildPromotion.UpdateForSkill(pm0, system.MainSkill);
        }

        var hiddenBooks = new SortedDictionary<int, (int Count, double Floor)>();
        var wildFloor = double.MaxValue;
        var table = RecipeGatingTable.Current;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (IsKnown(from, system, item))
            {
                rows.Add(new CraftListRow(i, null, 0));
                continue;
            }

            var c = Classify(system, item);
            var floor = table.FloorOf(table.BandOf(c.MinSkill));

            if (c.Kind == RecipeKind.Book)
            {
                hiddenBooks.TryGetValue(c.Band, out var h);
                hiddenBooks[c.Band] = (h.Count + 1, h.Count == 0 ? floor : Math.Min(h.Floor, floor));
            }
            else
            {
                wildFloor = Math.Min(wildFloor, c.Kind == RecipeKind.OwnRecipe && c.Band == 0 ? 0.0 : floor);
            }
        }

        var skill = from?.Skills[system.MainSkill].Value ?? 0.0;

        foreach (var (band, (count, floor)) in hiddenBooks)
        {
            if (skill >= floor)
            {
                rows.Add(new CraftListRow(-1, BookHint(system.MainSkill, band, count), HintButtonBase + (int)system.MainSkill * 16 + band));
            }
        }

        if (wildFloor < double.MaxValue && skill >= wildFloor)
        {
            rows.Add(new CraftListRow(-1, WildHint, HintButtonBase + (int)system.MainSkill * 16));
        }

        return rows;
    }

    public const string WildHint = "Some designs are found only in the wild.";

    public static string BookHint(SkillName skill, int band, int count)
    {
        var guildKey = RecipeOwners.GuildFor(skill);
        var guild = guildKey != null ? ClusterFGuildSystem.GetDef(guildKey)?.Name ?? guildKey : "no guild";
        var rank = guildKey != null ? ClusterFGuildPromotion.RankNameForIndex(guildKey, SellRank(band)) : "?";
        return $"{count} more pattern{(count == 1 ? "" : "s")}: {guild}, rank {rank}.";
    }

    /// <summary>The rank index whose guild shop sells a band's book: one band ahead, so Band I at Initiate (0).</summary>
    public static int SellRank(int band) => Math.Clamp(band - 1, 0, ClusterFGuildSystem.RankTop);

    /// <summary>Draws a hint row at its menu row (pinned's item rows: y 60 + row * 20).</summary>
    public static void AddHintRow(ref DynamicGumpBuilder builder, CraftListRow row, int index)
    {
        builder.AddButton(220, 60 + index * 20, 4011, 4012, row.HintButton);
        builder.AddLabel(255, 63 + index * 20, 0x3B2, row.Hint ?? "");
    }

    /// <summary>A hint line's button: opens the help page and keeps the craft menu open. False for any other button.</summary>
    public static bool OnHintButton(Mobile from, CraftSystem system, BaseTool tool, int buttonID)
    {
        if (buttonID < HintButtonBase || buttonID >= HintButtonBase + 64 * 16)
        {
            return false;
        }

        var id = buttonID - HintButtonBase;
        var skill = (SkillName)(id / 16);
        var band = id % 16;

        from.SendGump(new CraftGump(from, system, tool, null));
        from.SendGump(new RecipeHintGump(skill, band));
        return true;
    }
}

/// <summary>One owning guild per craft skill with a menu (F-43, Chase 2026-10-11): it issues the skill's books and
/// scrolls, shop rows, hint lines and BODs, and its rank gates them.</summary>
public static class RecipeOwners
{
    // Masonry follows Carpentry and Glassblowing follows Alchemy: their systems' main skills are Carpentry and Alchemy
    // (pinned DefMasonry.cs:18, DefGlassblowing.cs:18). Imbuing is not a craft system and is left to the Imbuing rework.
    public static readonly IReadOnlyDictionary<SkillName, string> BySkill = new Dictionary<SkillName, string>
    {
        [SkillName.Blacksmith] = "smithing",
        [SkillName.Tailoring] = "tailoring",
        [SkillName.Carpentry] = "foresters",
        [SkillName.Fletching] = "foresters",
        [SkillName.Tinkering] = "tinkers",
        [SkillName.Alchemy] = ClusterFAlembicGuild.Key,
        [SkillName.Inscribe] = "arcane",
        [SkillName.Cooking] = "maritime",
        [SkillName.Cartography] = "maritime",
    };

    public static string? GuildFor(SkillName skill) => BySkill.TryGetValue(skill, out var key) ? key : null;

    /// <summary>The craft skills a guild owns, in table order (empty for a guild that owns none).</summary>
    public static List<SkillName> SkillsOf(string guildKey)
    {
        var list = new List<SkillName>();
        foreach (var (skill, key) in BySkill)
        {
            if (key.Equals(guildKey, StringComparison.OrdinalIgnoreCase))
            {
                list.Add(skill);
            }
        }

        return list;
    }

    /// <summary>The band-gated craft skills a guild owns (Cartography owns no bands).</summary>
    public static List<SkillName> GatedSkillsOf(string guildKey) =>
        SkillsOf(guildKey).FindAll(s => ClusterFRecipeGate.SystemFor(s) != null);

    public static bool IsCraftGuild(string guildKey) => SkillsOf(guildKey).Count > 0;
}

/// <summary>The help page a hint line opens: who teaches the hidden patterns, where, and at what rank.</summary>
public class RecipeHintGump : Gump
{
    // A placeholder until the wiki is published (wiki publishing waits for launch).
    public const string WikiLink = "Recipes on the wiki: link to come.";

    public RecipeHintGump(SkillName skill, int band) : base(120, 120)
    {
        var lines = Lines(skill, band);

        AddPage(0);
        AddBackground(0, 0, 420, 70 + lines.Count * 22, 9270);
        AddAlphaRegion(10, 10, 400, 50 + lines.Count * 22);
        AddLabel(24, 18, 1154, band == 0 ? "Designs found in the wild" : "Pattern books");

        for (var i = 0; i < lines.Count; i++)
        {
            AddHtml(24, 44 + i * 22, 380, 22, $"<BASEFONT COLOR=#DDDDDD>{lines[i]}</BASEFONT>", false, false);
        }
    }

    public static List<string> Lines(SkillName skill, int band)
    {
        var table = RecipeGatingTable.Current;
        var skillName = (int)skill < SkillInfo.Table.Length ? SkillInfo.Table[(int)skill].Name : skill.ToString();

        if (band == 0)
        {
            return
            [
                "No guild teaches these. They come from bosses, treasure, bulk orders and quests.",
                "Ask at the League of Extraordinary Citizens' Field Office in New Haven.",
                WikiLink
            ];
        }

        var key = RecipeOwners.GuildFor(skill);
        var def = key != null ? ClusterFGuildSystem.GetDef(key) : null;
        if (def == null)
        {
            return [$"No guild teaches {skillName} patterns yet.", WikiLink];
        }

        var posts = GuildLocations.For(def.Key);
        var hall = posts.Count > 0 ? $"{posts[0].Hall}, {posts[0].Town}" : "no hall yet";

        return
        [
            $"The {def.Name} teaches {skillName} patterns. Its hall: {hall}.",
            $"The Band {table.BandName(band)} book is sold at rank {ClusterFGuildPromotion.RankNameForIndex(def.Key, ClusterFRecipeGate.SellRank(band))} " +
            $"and given at rank {ClusterFGuildPromotion.RankNameForIndex(def.Key, Math.Min(band, ClusterFGuildSystem.RankTop))}.",
            "Joining is free. A book teaches your whole account.",
            WikiLink
        ];
    }
}
