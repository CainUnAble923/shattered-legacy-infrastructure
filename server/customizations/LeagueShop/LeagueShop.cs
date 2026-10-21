// cc-P229 (F-13): the League reward shop. AP opens a row and never closes it; Renown is the price. Notes: shard-migration
// notes/cc-P229-league-reward-shop.md.
//
// The stock is F-13's starter table exactly (shard-migration docs/feature-queue.md, "F-13", approved by Chase 2026-09-30):
// 14 rows, 0 to 1,750 AP, in its order. Prices get tuned in play testing, so they live in one table here.
//
// Paying: ClusterFAccountData.SpendRenown, which takes from the balance only. LifetimeRenown, and so League rank
// (ClusterFLeagueRanks.Evaluate reads LifetimeRenown), never moves on a purchase.
//
// Guardrails (F-13, and F-12's "nothing grants skill or stat points"): nothing here has item properties, and there are
// no power scrolls, artifacts, bless deeds, skill or stat items. The loot boxes draw only from the rows' own kinds
// (LeagueShop.BoxPool).

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;

namespace Server.Engines.LeagueRewards;

public enum LeagueShopKind
{
    Title,
    Cosmetic,
    LootBox,
    Convenience,
    Decor
}

/// <summary>One shop row: what it is, the AP that opens it, the Renown it costs, and what it hands over.</summary>
public sealed class LeagueShopEntry
{
    public LeagueShopEntry(string key, string label, int ap, int renown, LeagueShopKind kind, int itemID, int hue,
        Func<PlayerMobile, List<Item>>? create = null, string? titleKey = null)
    {
        Key = key;
        Label = label;
        ApRequired = ap;
        Renown = renown;
        Kind = kind;
        ItemID = itemID;
        Hue = hue;
        Create = create;
        TitleKey = titleKey;
    }

    public string Key { get; }
    public string Label { get; }
    public int ApRequired { get; }
    public int Renown { get; }
    public LeagueShopKind Kind { get; }
    public int ItemID { get; }
    public int Hue { get; }

    /// <summary>The items handed over, made for this buyer now. Null for a title.</summary>
    public Func<PlayerMobile, List<Item>>? Create { get; }

    /// <summary>The LeagueTitles key a title row grants. Null for an item.</summary>
    public string? TitleKey { get; }

    public bool IsTitle => TitleKey != null;
}

public enum LeagueShopResult
{
    Bought,
    NoAccount,
    NeedsAp,
    NotEnoughRenown,
    AlreadyOwned,
    NoRoom
}

public static class LeagueShop
{
    // The League's hues, sold nowhere else (cc-P229: no item or hue table in pinned or ours names them; listed in the
    // notes). All three are above the 1,001 a cloth dye tub reaches.
    public const int LeagueHue = 1323;  // 0x52B, hues.mul "Blues #22": the tabard and the pigment
    public const int CloakHue = 1713;   // 0x6B1, "Yellows #12": the League cloak
    public const int RegaliaHue = 1223; // 0x4C7, "Pinks #22": the regalia

    // Stock art (classic art, cited in the notes): a deed for a title, P79's loot box, pinned's own item art otherwise.
    public const int TitleArt = 0x14EF;
    public const int BoxArt = 0x9AA;

    public static readonly LeagueShopEntry[] Stock =
    [
        new("title.registered", "Title: Registered Adventurer", 0, 25, LeagueShopKind.Title, TitleArt, LeagueHue,
            titleKey: LeagueTitles.RegisteredAdventurer),
        new("tabard", "League tabard", 0, 50, LeagueShopKind.Cosmetic, 0x1FFD, LeagueHue, One(_ => new LeagueTabard())),
        new("box.bronze", "System's Loot Box, Bronze", 0, 40, LeagueShopKind.LootBox, BoxArt, 0,
            pm => [MakeBox(pm, BoxTier.Bronze)]),
        new("hairdeed", "Hair restyling deed", 100, 75, LeagueShopKind.Convenience, 0x14F0, 0,
            One(_ => new HairRestylingDeed())),
        new("rune", "League rune (blessed)", 100, 60, LeagueShopKind.Convenience, 0x1F14, 0,
            One(_ => MakeLeagueRune())),
        new("banner", "League banner", 250, 150, LeagueShopKind.Decor, LeagueBanner.SouthArt, LeagueHue,
            One(_ => new LeagueBanner())),
        new("cloak", "League cloak", 250, 200, LeagueShopKind.Cosmetic, 0x1515, CloakHue, One(_ => new LeagueCloak())),
        new("box.silver", "System's Loot Box, Silver", 250, 150, LeagueShopKind.LootBox, BoxArt, 0,
            pm => [MakeBox(pm, BoxTier.Silver)]),
        new("trophy", "Achievement trophy statuette", 500, 250, LeagueShopKind.Decor, LeagueTrophyStatuette.Art, 0,
            One(pm => new LeagueTrophyStatuette(pm))),
        new("pigment", "League-hue pigment (one use)", 500, 300, LeagueShopKind.Cosmetic, 0xEFF, LeagueHue,
            One(_ => new LeaguePigment())),
        new("title.veteran", "Title: Veteran of the League", 500, 200, LeagueShopKind.Title, TitleArt, LeagueHue,
            titleKey: LeagueTitles.VeteranOfTheLeague),
        new("box.gold", "System's Loot Box, Gold", 1000, 400, LeagueShopKind.LootBox, BoxArt, 0,
            pm => [MakeBox(pm, BoxTier.Gold)]),
        new("regalia", "League regalia (3 pieces)", 1750, 1000, LeagueShopKind.Cosmetic, 0x1F03, RegaliaHue,
            _ => [new LeagueRegaliaRobe(), new LeagueRegaliaCloak(), new LeagueRegaliaSandals()]),
        new("title.favorite", "Title: the System's Favorite", 1750, 500, LeagueShopKind.Title, TitleArt, LeagueHue,
            titleKey: LeagueTitles.SystemsFavorite),
    ];

    private static Func<PlayerMobile, List<Item>> One(Func<PlayerMobile, Item> make) => pm => [make(pm)];

    public static LeagueShopEntry? Find(string key) => Stock.FirstOrDefault(e => e.Key == key);

    public static ClusterFAccountData? DataOf(Mobile m) =>
        m.Account is IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct) : null;

    /// <summary>AP opens the row, and AP is a lifetime score, so an open row stays open.</summary>
    public static bool IsUnlocked(ClusterFAccountData data, LeagueShopEntry entry) => data.AchievementPoints >= entry.ApRequired;

    public static bool IsOwned(ClusterFAccountData data, LeagueShopEntry entry) =>
        entry.TitleKey != null && data.LeagueTitles.Owned.Contains(entry.TitleKey);

    /// <summary>Why this account cannot buy the row now, or Bought when it can. Checks AP, then ownership, then Renown.</summary>
    public static LeagueShopResult Check(ClusterFAccountData data, LeagueShopEntry entry)
    {
        if (!IsUnlocked(data, entry))
        {
            return LeagueShopResult.NeedsAp;
        }

        if (IsOwned(data, entry))
        {
            return LeagueShopResult.AlreadyOwned;
        }

        return data.Renown < entry.Renown ? LeagueShopResult.NotEnoughRenown : LeagueShopResult.Bought;
    }

    /// <summary>
    /// Buys the row: every check again, server side, then the items into the pack (all or none), then exactly the price.
    /// Nothing is taken on a refusal.
    /// </summary>
    public static LeagueShopResult Purchase(PlayerMobile pm, LeagueShopEntry entry)
    {
        var data = DataOf(pm);
        if (data == null)
        {
            return LeagueShopResult.NoAccount;
        }

        var check = Check(data, entry);
        if (check != LeagueShopResult.Bought)
        {
            pm.SendMessage(0x22, RefusalLine(check, data, entry));
            return check;
        }

        if (entry.IsTitle)
        {
            data.LeagueTitles.Owned.Add(entry.TitleKey!);
            data.SpendRenown(entry.Renown);
            pm.SendMessage(0x44, $"System: Title unlocked: \"{LeagueTitles.Display(entry.TitleKey!)}\". Choose it in Titles.");
            pm.PlaySound(0x5A7);
            return LeagueShopResult.Bought;
        }

        var items = entry.Create!(pm);
        var pack = pm.Backpack;
        if (pack == null || !CanHoldAll(pm, pack, items))
        {
            foreach (var item in items)
            {
                item.Delete();
            }

            pm.SendLocalizedMessage(1074361); // The reward could not be given.  Make sure you have room in your pack.
            return LeagueShopResult.NoRoom;
        }

        foreach (var item in items)
        {
            pack.DropItem(item);
            item.InvalidateProperties();
        }

        data.SpendRenown(entry.Renown);
        pm.SendLocalizedMessage(1073621); // Your reward has been placed in your backpack.
        if (entry.Kind == LeagueShopKind.LootBox)
        {
            pm.SendMessage(0x44, BoxLines[Utility.Random(BoxLines.Length)]);
        }

        pm.PlaySound(0x5A7);
        return LeagueShopResult.Bought;
    }

    public static string RefusalLine(LeagueShopResult result, ClusterFAccountData data, LeagueShopEntry entry) => result switch
    {
        LeagueShopResult.NeedsAp         => $"Requires {entry.ApRequired:N0} AP (you have {data.AchievementPoints:N0}).",
        LeagueShopResult.AlreadyOwned    => "You already hold that title.",
        LeagueShopResult.NotEnoughRenown => $"That costs {entry.Renown:N0} Renown (you have {data.Renown:N0}).",
        LeagueShopResult.NoRoom          => "Make room in your pack first.",
        _                                => ""
    };

    // CleanUpBritanniaRewards.CanHoldAll: several items, checked together before any is given.
    private static bool CanHoldAll(PlayerMobile pm, Container pack, List<Item> items)
    {
        var weight = items.Sum(i => i.TotalWeight + i.PileWeight);
        return pack.CheckHold(pm, items[0], false, true, items.Count - 1, weight - items[0].TotalWeight - items[0].PileWeight);
    }

    // -- The League rune --

    /// <summary>A blessed recall rune marked one step in front of the League Registrar (she faces west).</summary>
    public static RecallRune MakeLeagueRune()
    {
        var at = ClusterFNewHavenSeeder.RegistrarAnchor;
        return new RecallRune
        {
            Target = new Point3D(at.X - 1, at.Y, at.Z),
            TargetMap = Map.Trammel,
            Marked = true,
            Description = "the League Registrar, New Haven",
            LootType = LootType.Blessed
        };
    }

    // -- The System's loot boxes --

    public enum BoxTier
    {
        Bronze,
        Silver,
        Gold
    }

    // P79's LootBox item (bound, opened in a guarded town or a house, its reveal and fanfare), filled from the shop's own
    // kinds instead of P79's roll: that roll includes magic gear (item properties), strength and agility potions, and
    // Renown that would count toward League rank, which F-13's guardrails rule out. Each box draws only from rows its
    // own AP opens, so no box hands over a row early. Box tier (P79's 1 to 6) sets the fanfare: Gold's is the major one.
    public static (string Name, int P79Tier, int Draws, int MaxAp) BoxInfo(BoxTier tier) => tier switch
    {
        BoxTier.Bronze => ("System's Bronze Loot", 1, 1, 0),
        BoxTier.Silver => ("System's Silver Loot", 2, 2, 250),
        _              => ("System's Gold Loot", 4, 3, 1000)
    };

    /// <summary>
    /// What a box can hold: the cosmetic, decor and convenience rows its AP opens (never a title, never a box), plus the
    /// League sash (P79's BoundCosmeticSash in the League hue) so the Bronze box has two kinds.
    /// </summary>
    public static List<Func<PlayerMobile, Item>> BoxPool(BoxTier tier)
    {
        var maxAp = BoxInfo(tier).MaxAp;
        var pool = new List<Func<PlayerMobile, Item>>
        {
            pm => new BoundCosmeticSash(pm.Account?.Username ?? "", "League", LeagueHue)
        };

        foreach (var entry in Stock)
        {
            if (entry.Create != null && entry.Kind != LeagueShopKind.LootBox && entry.ApRequired <= maxAp)
            {
                var e = entry;
                pool.Add(pm => e.Create!(pm)[0]);
            }
        }

        return pool;
    }

    public static readonly string[] BoxLines =
    [
        "System: You paid Renown for a box. Of things. The economy thanks you.",
        "System: Congratulations on your purchase. No refunds. No returns. No regrets, probably.",
        "System: A box! Inside it: more stuff you will put in a different box.",
        "System: Opening boxes is not a personality. But it is a start."
    ];

    public static LootBox MakeBox(PlayerMobile pm, BoxTier tier)
    {
        var (name, p79Tier, draws, _) = BoxInfo(tier);
        var pool = BoxPool(tier);
        var box = new LootBox(BoxTheme.League, p79Tier, pm.Account?.Username ?? "", "the League shop");
        var contents = new List<(Item, bool)>();

        for (var i = 0; i < draws; i++)
        {
            contents.Add((pool[Utility.Random(pool.Count)](pm), false));
        }

        box.Fill(name, box.Hue, 0, 0, contents);
        return box;
    }
}

/// <summary>
/// The League titles (cc-P229, F-13). Three are bought in the shop; the AP titles unlock free at their threshold and stay
/// unlocked. A character shows one, through pinned's Mobile.Title (Server/Mobiles/Mobile.cs:1032), which
/// Mobile.AddNameProperties draws after the name (:3360-3371) and the click name likewise (:8081): no core edit.
/// </summary>
public static class LeagueTitles
{
    public const string RegisteredAdventurer = "registered-adventurer";
    public const string VeteranOfTheLeague = "veteran-of-the-league";
    public const string SystemsFavorite = "systems-favorite";

    // HiddenUntilHeld (cc-P262): a title a hidden achievement grants; the picker lists it only once the account holds it.
    public sealed record TitleDef(string Key, string Display, int FreeAtAp, bool HiddenUntilHeld = false);

    // cc-P262: Iron Soles' title (Accessories/AccessoryAchievements.cs grants it).
    public const string Unshod = "unshod";

    // FreeAtAp -1: bought only. The AP titles' names and thresholds are ours (F-13 asks for them, names none); listed
    // in the notes for Chase. They follow the shop's own AP tiers: every achievement not retired gives 1,955 AP
    // (counted by LeagueShopVerification on 2026-10-21; F-13's 2,505 predates cc-P55's retirements), so 1,750 is the top.
    public static readonly TitleDef[] All =
    [
        new(RegisteredAdventurer, "the Registered Adventurer", -1),
        new(VeteranOfTheLeague, "the Veteran of the League", -1),
        new(SystemsFavorite, "the System's Favorite", -1),
        new("ap-100", "the Noted", 100),
        new("ap-250", "the Accomplished", 250),
        new("ap-500", "the Distinguished", 500),
        new("ap-1000", "the Renowned", 1000),
        new("ap-1750", "the Illustrious", 1750),
        new(Unshod, "the Unshod", -1, HiddenUntilHeld: true),
        // cc-P263: the "Noted by the System" titles (NotedRewards), given by their achievements.
        new(NotedRewards.LarryTitle, "Larry", -1, HiddenUntilHeld: true),
        new(NotedRewards.SupervisorTitle, "the Supervisor", -1, HiddenUntilHeld: true),
        new(NotedRewards.BraveTitle, "the Brave (citation needed)", -1, HiddenUntilHeld: true),
    ];

    public static TitleDef? Find(string key) => All.FirstOrDefault(t => t.Key == key);

    public static string Display(string key) => Find(key)?.Display ?? key;

    /// <summary>Adds every AP title the account's AP has reached. They are never removed, so a later AP change keeps them.</summary>
    public static void Sync(ClusterFAccountData data)
    {
        foreach (var t in All)
        {
            if (t.FreeAtAp >= 0 && data.AchievementPoints >= t.FreeAtAp)
            {
                data.LeagueTitles.Owned.Add(t.Key);
            }
        }
    }

    public static bool Holds(ClusterFAccountData data, string key)
    {
        Sync(data);
        return data.LeagueTitles.Owned.Contains(key);
    }

    /// <summary>The title key this character shows, or null.</summary>
    public static string? ShownKey(ClusterFAccountData data, Mobile m) =>
        data.LeagueTitles.Shown.TryGetValue((uint)m.Serial, out var key) ? key : null;

    /// <summary>Shows a held title on this character (key null: none). False when the account does not hold it.</summary>
    public static bool Choose(PlayerMobile pm, string? key)
    {
        var data = LeagueShop.DataOf(pm);
        if (data == null)
        {
            return false;
        }

        if (key == null)
        {
            if (data.LeagueTitles.Shown.Remove((uint)pm.Serial))
            {
                pm.Title = null;
            }

            return true;
        }

        if (!Holds(data, key))
        {
            return false;
        }

        data.LeagueTitles.Shown[(uint)pm.Serial] = key;
        pm.Title = Display(key);
        return true;
    }

    /// <summary>Puts the chosen title back on the character at login, so the account record is what shows.</summary>
    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct || ClusterFAccountPersistence.Get(acct) is not { } data)
        {
            return;
        }

        Sync(data);

        if (ShownKey(data, pm) is { } key && data.LeagueTitles.Owned.Contains(key))
        {
            pm.Title = Display(key);
        }
    }
}
