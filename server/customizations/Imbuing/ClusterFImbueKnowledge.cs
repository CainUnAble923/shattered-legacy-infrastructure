// cc-P82: the Artificers' Order Imbuing rework, what an account knows. Design: shard-migration
// docs/artificers-imbuing-design.md. Notes: shard-migration notes/cc-P82-artificers-imbuing.md.
//
// Known imbue properties are account-wide and kept through loops, like recipes, so they live in the recipe store's own
// set (ClusterFAccountData.LearnedRecipes, cc-P78), as keys:
//     imbue:<property>:<tier>     the account knows <property> up to <tier> (Basic, Refined, Full, Overcharged)
//     imbue:book:<id>             the account has read formula book <id>
//     imbue:formulary:master      the account has read the Master Formulary (rank 5): the item budget
// A property's known tier is the highest such key; a free property (the ladder's "free") is known at Basic by every
// account without a key. <property> is a knowledge key: a catalogue property's name, or a family that several catalogue
// rows share (ImbueFamilies: the slayers, the elemental damages, the +skill rows). The 140+ path's single grant API is
// Grant(account data, property, tier): quests, commissions, loot boxes and bosses call it.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Accounting;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;

namespace Server;

public enum ImbueTier
{
    None = 0,
    Basic = 1,
    Refined = 2,
    Full = 3,
    Overcharged = 4
}

/// <summary>Knowledge keys that cover several catalogue rows, and the grant-only keys (no catalogue row yet).</summary>
internal static class ImbueFamilies
{
    public const string ElementalDamage = "Elemental Damage";
    public const string JewelrySkill = "Jewelry Skill Bonus";
    public const string CrafterSkill = "Crafter Skill Bonus";
    public const string Slayer = "Slayer";
    public const string SuperSlayer = "Super Slayer";

    /// <summary>
    /// Grantable, but no catalogue row: nothing in pinned ModernUO or here stores or runs them yet (Splintering, Reactive
    /// Paralyze and Soul Charge are OSI properties stock Imbuing cannot place; Casting Focus has no AosAttribute). Known
    /// is recorded; the menu shows them once a row exists.
    /// </summary>
    public static readonly string[] GrantOnly = ["Splintering", "Reactive Paralyze", "Soul Charge", "Casting Focus"];

    public static bool IsKnowledgeKey(string key) =>
        GrantOnly.Contains(key, StringComparer.OrdinalIgnoreCase) ||
        ImbueCatalogue.All.Exists(d => d.KnowledgeKey.Equals(key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every knowledge key there is: each catalogue row's, then the grant-only ones.</summary>
    public static List<string> AllKeys()
    {
        var keys = new List<string>();
        foreach (var d in ImbueCatalogue.All)
        {
            if (!keys.Contains(d.KnowledgeKey, StringComparer.OrdinalIgnoreCase))
            {
                keys.Add(d.KnowledgeKey);
            }
        }

        keys.AddRange(GrantOnly);
        return keys;
    }

    public static ImbuableItem ParseApplies(string text)
    {
        var flags = ImbuableItem.None;
        foreach (var part in (text ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse<ImbuableItem>(part, true, out var f) && f is ImbuableItem.Weapon or ImbuableItem.Armor or
                    ImbuableItem.Jewel or ImbuableItem.Clothing)
            {
                flags |= f;
            }
            else
            {
                return ImbuableItem.None;
            }
        }

        return flags;
    }
}

public static class ClusterFImbueKnowledge
{
    public const string Prefix = "imbue:";
    public const string MasterKey = "imbue:formulary:master";

    // cc-P95 (D89): after SkillsInfo.Configure (default priority 50). The table's defaults read the catalogue, whose
    // skill rows read SkillInfo.Table; AssemblyHandler.Invoke runs equal priorities in no promised order.
    [CallPriority(100)]
    public static void Configure()
    {
        foreach (var line in ImbuingTable.LoadOrWriteDefaults(ImbuingTable.FilePath))
        {
            Console.WriteLine($"[Imbuing] {line}");
        }

        CommandSystem.Register("ImbueKnowledge", AccessLevel.GameMaster, OnCommand);
    }

    public static string TierKey(string property, ImbueTier tier) => $"{Prefix}{property}:{tier}";

    public static string BookKey(string id) => $"{Prefix}book:{id}";

    // -- What the account knows ----------------------------------------------------------------------------------

    /// <summary>The ladder entry for a knowledge key: 0 (free) to 4, or -1 for the 140+ path (or no entry).</summary>
    public static int LearnedAtRank(string key) =>
        ImbuingTable.Current.Ladder.TryGetValue(key, out var at) && ImbuingTable.TryRankOf(at, out var r) ? r : -1;

    public static bool IsFree(string key) => LearnedAtRank(key) == 0;

    /// <summary>The highest tier this account knows the property at (a free property is Basic at least).</summary>
    public static ImbueTier KnownTier(ClusterFAccountData? data, string key)
    {
        var best = IsFree(key) ? ImbueTier.Basic : ImbueTier.None;
        if (data == null)
        {
            return best;
        }

        for (var t = ImbueTier.Overcharged; t > best; t--)
        {
            if (data.LearnedRecipes.Contains(TierKey(key, t)))
            {
                return t;
            }
        }

        return best;
    }

    public static ImbueTier KnownTier(Mobile m, string key) => KnownTier(ClusterFRecipeStore.DataOf(m), key);

    /// <summary>
    /// The single grant API (the 140+ path, books, staff): the account knows the property up to at least this tier.
    /// Fortune's Favor has no Overcharged. Returns true when that raised what the account knew. Never lowers.
    /// </summary>
    public static bool Grant(ClusterFAccountData data, string key, ImbueTier tier)
    {
        key = Canonical(key) ?? throw new ArgumentException($"'{key}' is not an imbue property or family.", nameof(key));
        if (tier is < ImbueTier.Basic or > ImbueTier.Overcharged || tier == ImbueTier.Overcharged && !HasOvercharged(key))
        {
            return false;
        }

        if (KnownTier(data, key) >= tier)
        {
            return false;
        }

        data.LearnedRecipes.Add(TierKey(key, tier));
        return true;
    }

    /// <summary>The grant API for a character: its account learns, and the character is told.</summary>
    public static bool Grant(PlayerMobile pm, string key, ImbueTier tier, bool tell = true)
    {
        if (ClusterFRecipeStore.DataOf(pm) is not { } data || !Grant(data, key, tier))
        {
            return false;
        }

        if (tell)
        {
            pm.SendMessage(0x44, $"Your account now knows {Canonical(key)} at {tier}.");
        }

        return true;
    }

    /// <summary>Staff: forgets the property from this tier up (Basic forgets it; a free property keeps Basic). Returns keys removed.</summary>
    public static int Revoke(ClusterFAccountData data, string key, ImbueTier fromTier)
    {
        key = Canonical(key) ?? key;
        var removed = 0;
        for (var t = (ImbueTier)Math.Max(1, (int)fromTier); t <= ImbueTier.Overcharged; t++)
        {
            removed += data.LearnedRecipes.Remove(TierKey(key, t)) ? 1 : 0;
        }

        return removed;
    }

    /// <summary>The key as the catalogue or grant list spells it, or null.</summary>
    public static string? Canonical(string key) =>
        ImbueFamilies.AllKeys().Find(k => k.Equals(key?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool HasOvercharged(string key)
    {
        var t = ImbuingTable.Current;
        if (t.CustomOf(key) is { } c)
        {
            return c.Overcharged > 0;
        }

        // A family or a row has it when any of its rows does; a grant-only key may (its rows do not exist yet).
        var rows = ImbueCatalogue.All.FindAll(d => d.KnowledgeKey.Equals(key, StringComparison.OrdinalIgnoreCase));
        return rows.Count == 0 || rows.Exists(d => ImbueTiers.MaxFor(d, ImbueTier.Overcharged) > d.FullMax);
    }

    public static bool HasMasterFormulary(ClusterFAccountData? data) => data?.LearnedRecipes.Contains(MasterKey) == true;

    // -- The item budget (rank 5) ----------------------------------------------------------------------------------

    public static int MaxSlots(ClusterFAccountData? data)
    {
        var t = ImbuingTable.Current;
        return t.BaseSlots + (!t.BudgetMode && HasMasterFormulary(data) ? t.MasterExtraSlots : 0);
    }

    /// <summary>The item's total intensity budget in percent of Full, or 0 when the budget is off (slot mode).</summary>
    public static int IntensityBudget(ClusterFAccountData? data)
    {
        var t = ImbuingTable.Current;
        if (!t.BudgetMode)
        {
            return 0;
        }

        return t.BaseSlots * 100 + (HasMasterFormulary(data) ? t.MasterBudgetPercent : 0);
    }

    // -- Books -------------------------------------------------------------------------------------------------------

    /// <summary>Reads a book into the account: every grant, and the Master Formulary's key. Returns lines to say.</summary>
    public static List<string> ReadBook(ClusterFAccountData data, ImbueBookConfig book)
    {
        var said = new List<string>();
        foreach (var g in book.Grants)
        {
            if (Enum.TryParse<ImbueTier>(g.Tier, true, out var tier) && Grant(data, g.Property, tier))
            {
                said.Add($"{Canonical(g.Property)}: {tier}");
            }
        }

        if (book.MasterFormulary && data.LearnedRecipes.Add(MasterKey))
        {
            said.Add(ImbuingTable.Current.BudgetMode
                ? $"the item intensity budget rises by {ImbuingTable.Current.MasterBudgetPercent}%"
                : $"items take {MaxSlots(data)} imbued properties");
        }

        data.LearnedRecipes.Add(BookKey(book.Id));
        return said;
    }

    // -- Hints -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// One hint line per category of this item's catalogue that has anything locked (an unknown property or a tier past
    /// the known one), saying where more comes from. Null when nothing in the category is locked.
    /// </summary>
    internal static string? HintFor(ClusterFAccountData? data, string group, List<ImbuePropertyDef> rows)
    {
        var lockedProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lockedTiers = 0;
        var lowestRank = int.MaxValue;
        var path140 = false;

        foreach (var def in rows)
        {
            if (def.Group != group)
            {
                continue;
            }

            var known = KnownTier(data, def.KnowledgeKey);
            var top = ImbueTiers.TopTier(def);
            if (known >= top)
            {
                continue;
            }

            if (known == ImbueTier.None)
            {
                lockedProps.Add(def.KnowledgeKey);
            }
            else
            {
                lockedTiers++;
            }

            // Where the next tier comes from: a book while the ladder still reaches it, else the 140+ path.
            var learned = LearnedAtRank(def.KnowledgeKey);
            var nextRank = learned < 0 ? -1 : learned + (int)known; // Basic at L, Refined at L + 1, Full at L + 2
            if (nextRank is >= 1 and <= 5 && known + 1 <= ImbueTier.Full)
            {
                lowestRank = Math.Min(lowestRank, nextRank);
            }
            else
            {
                path140 = true;
            }
        }

        if (lockedProps.Count == 0 && lockedTiers == 0)
        {
            return null;
        }

        var what = new List<string>();
        if (lockedProps.Count > 0)
        {
            what.Add($"{lockedProps.Count} more propert{(lockedProps.Count == 1 ? "y" : "ies")}");
        }

        if (lockedTiers > 0)
        {
            what.Add($"{lockedTiers} higher tier{(lockedTiers == 1 ? "" : "s")}");
        }

        var where = new List<string>();
        if (lowestRank != int.MaxValue)
        {
            where.Add($"formula books from Artificers' rank {ClusterFGuildPromotion.RankNameForIndex("artificers", lowestRank)}");
        }

        if (path140)
        {
            where.Add("the 140+ path");
        }

        return $"{group}: {string.Join(" and ", what)} locked. Learn more: {string.Join(", ", where)}.";
    }

    // -- Describe (the recipe store's lists) ---------------------------------------------------------------------------

    public static string Describe(string key)
    {
        if (key == MasterKey)
        {
            return "the Master Formulary";
        }

        var rest = key[Prefix.Length..];
        if (rest.StartsWith("book:", StringComparison.Ordinal))
        {
            return ImbuingTable.Current.Book(rest[5..])?.Name ?? rest;
        }

        var colon = rest.LastIndexOf(':');
        return colon > 0 ? $"imbue {rest[..colon]} ({rest[(colon + 1)..]})" : key;
    }

    // -- Staff ---------------------------------------------------------------------------------------------------------

    [Usage("ImbueKnowledge [reload]")]
    [Description("Views and grants or revokes the imbue properties a targeted player's account knows. 'reload' reads Configuration/imbuing.json again.")]
    [ShardCommand(CommandCategory.Grant, Shard = CommandShard.Any)] // staff remedies on live too; every change is logged
    private static void OnCommand(CommandEventArgs e)
    {
        if (e.Length >= 1 && e.GetString(0).Equals("reload", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var line in ImbuingTable.Reload(ImbuingTable.FilePath))
            {
                e.Mobile.SendMessage(line);
            }

            return;
        }

        e.Mobile.SendMessage("Target a player to view their account's imbue knowledge.");
        e.Mobile.Target = new KnowledgeTarget();
    }

    private sealed class KnowledgeTarget : Targeting.Target
    {
        public KnowledgeTarget() : base(-1, false, Targeting.TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is PlayerMobile { Account: IAccount acct })
            {
                from.SendGump(new Gumps.ImbueKnowledgeGump(acct, 0));
            }
            else
            {
                from.SendMessage("That is not a player with an account.");
            }
        }
    }
}

/// <summary>A catalogue row's values by tier.</summary>
internal static class ImbueTiers
{
    /// <summary>The value this tier sets (0: the row has no such tier).</summary>
    public static int MaxFor(ImbuePropertyDef def, ImbueTier tier)
    {
        var t = ImbuingTable.Current;
        var full = def.FullMax;

        if (def.IsBool)
        {
            return tier >= ImbueTier.Basic ? 1 : 0;
        }

        return tier switch
        {
            ImbueTier.Basic => Math.Max(1, (int)Math.Round(full * t.BasicFraction)),
            ImbueTier.Refined => Math.Max(1, (int)Math.Round(full * t.RefinedFraction)),
            ImbueTier.Full => full,
            ImbueTier.Overcharged => def.IsCustom
                ? t.CustomOf(def.Name)?.Overcharged ?? 0
                : t.OverchargedMax.TryGetValue(def.Name, out var over) && over > full ? over : 0,
            _ => 0
        };
    }

    /// <summary>The highest tier the row has (Basic for an on/off property).</summary>
    public static ImbueTier TopTier(ImbuePropertyDef def) =>
        def.IsBool ? ImbueTier.Basic : MaxFor(def, ImbueTier.Overcharged) > def.FullMax ? ImbueTier.Overcharged : ImbueTier.Full;

    /// <summary>The tier a value sits in (the lowest tier whose value reaches it).</summary>
    public static ImbueTier TierOf(ImbuePropertyDef def, int value)
    {
        for (var t = ImbueTier.Basic; t <= TopTier(def); t++)
        {
            if (value <= MaxFor(def, t))
            {
                return t;
            }
        }

        return ImbueTier.Overcharged;
    }
}
