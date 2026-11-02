// cc-P760: the reward stores' shared core. Notes: shard-migration notes/cc-P760-reward-stores.md; the plan Chase approved
// (2026-11-01) is notes/cc-P760-stores-plan.md.
//
// Every store is built on the League shop's pattern (LeagueShop/, cc-P229):
//   - rows: what a row is, what opens it, its price, what it hands over (RewardStoreRow);
//   - one window: the League shop's scroll frame, five rows a page, the art greyed until the row opens (RewardStoreGump);
//   - a confirm step, then a server-side buy that checks everything again, hands the items over all or none, and then
//     takes exactly the price (RewardStore.Purchase);
//   - spending never touches a lifetime total: each currency (IRewardCurrency) spends a balance only.
//
// The stores: the guild stores (GuildStores.cs), the Festival store (FestivalStore.cs) and the Achievement store
// (AchievementStore.cs). The League shop keeps its own code and its own tests (it gained rows in LeagueShop.cs).
//
// Prices are placeholders, every one in one table: Configuration/reward-stores.json (RewardStorePrices), a row's price
// and what opens it by the row's key. A missing file is written with the built-in defaults; a bad file is refused whole.
//
// Guardrails (F-12, F-13), for every store, checked by RewardStoreVerification over every row: nothing grants skill or stat
// points; no power scrolls, bless deeds, or artifacts or gear with item properties; art only from registry.csv.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.LeagueRewards;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.RewardStores;

public enum RewardStoreResult
{
    Bought,
    NoAccount,
    Unavailable,
    Locked,
    AlreadyOwned,
    NotEnough,
    NoRoom
}

/// <summary>A balance a store spends. Spending takes from the balance only, never from a lifetime total.</summary>
public interface IRewardCurrency
{
    string Name { get; }

    int Balance(PlayerMobile pm);

    /// <summary>Takes exactly <paramref name="amount"/>. False, and nothing taken, when the balance is short.</summary>
    bool TrySpend(PlayerMobile pm, int amount);
}

/// <summary>One store row: what it is, what opens it, its price, and what it hands over.</summary>
public sealed class RewardStoreRow
{
    public RewardStoreRow(string key, string label, int price, int opensAt, int itemID, int hue = 0,
        Func<PlayerMobile, List<Item>>? create = null, string? titleKey = null)
    {
        Key = key;
        Label = label;
        DefaultPrice = price;
        DefaultOpensAt = opensAt;
        ItemID = itemID;
        Hue = hue;
        Create = create;
        TitleKey = titleKey;
    }

    public string Key { get; }
    public string Label { get; }
    public int DefaultPrice { get; }
    public int DefaultOpensAt { get; }
    public int ItemID { get; }
    public int Hue { get; }

    /// <summary>The items handed over, made for this buyer now. Null for a title.</summary>
    public Func<PlayerMobile, List<Item>>? Create { get; }

    /// <summary>The LeagueTitles key a title row grants (account-wide, chosen in the Titles window). Null for items.</summary>
    public string? TitleKey { get; }

    /// <summary>An extra condition besides OpensAt (the Achievement store's "every Discovery achievement"), and its words.</summary>
    public Func<PlayerMobile, bool>? Condition { get; init; }

    public string? ConditionText { get; init; }

    public bool IsTitle => TitleKey != null;

    /// <summary>The price in force: the price table's, else the default.</summary>
    public int Price => RewardStorePrices.Current.PriceOf(this);

    /// <summary>What opens the row (a rank index, an AP total, 0 for open to all), from the price table.</summary>
    public int OpensAt => RewardStorePrices.Current.OpensAtOf(this);
}

/// <summary>A store: its name, its currency, its rows and what opens a row.</summary>
public abstract class RewardStoreDef
{
    public abstract string Key { get; }

    public abstract string Title { get; }

    /// <summary>One line under the balance, about the store.</summary>
    public abstract string Blurb { get; }

    public abstract IRewardCurrency Currency { get; }

    /// <summary>The rows on sale now.</summary>
    public abstract IReadOnlyList<RewardStoreRow> Rows();

    /// <summary>Every row the store can ever sell (the price table lists each), whatever is on sale now.</summary>
    public virtual IReadOnlyList<RewardStoreRow> AllRows() => Rows();

    /// <summary>The buyer's measure against a row's OpensAt: a rank index, an AP total.</summary>
    public abstract int Progress(PlayerMobile pm);

    /// <summary>How a locked row reads: "Requires Master rank", "Requires 500 AP".</summary>
    public abstract string OpensText(int opensAt);

    /// <summary>The measure's name and the buyer's value, for the window's header ("Rank: Journeyman").</summary>
    public abstract string ProgressText(PlayerMobile pm);

    /// <summary>The confirm step's last line: what spending never lowers.</summary>
    public abstract string SpendNote { get; }

    /// <summary>Whether the buyer may shop here now (at the guildmaster, beside the peddler, in season).</summary>
    public virtual bool CanShop(PlayerMobile pm, out string why)
    {
        why = "";
        return true;
    }

    /// <summary>The row on sale now with this key, or null.</summary>
    public RewardStoreRow? Find(string key) => Rows().FirstOrDefault(r => r.Key == key);

    /// <summary>A fresh copy of this store for a reopened window (the stores carry their guildmaster or peddler).</summary>
    public virtual RewardStoreDef Refresh() => this;
}

public static class RewardStore
{
    public static ClusterFAccountData? DataOf(Mobile m) => LeagueShop.DataOf(m);

    public static bool IsUnlocked(PlayerMobile pm, RewardStoreDef store, RewardStoreRow row) =>
        store.Progress(pm) >= row.OpensAt && (row.Condition?.Invoke(pm) ?? true);

    public static bool IsOwned(PlayerMobile pm, RewardStoreRow row) =>
        row.TitleKey != null && DataOf(pm) is { } data && data.LeagueTitles.Owned.Contains(row.TitleKey);

    /// <summary>Why this buyer cannot buy the row now, or Bought when they can. Opens, then ownership, then the price.</summary>
    public static RewardStoreResult Check(PlayerMobile pm, RewardStoreDef store, RewardStoreRow row)
    {
        if (DataOf(pm) == null)
        {
            return RewardStoreResult.NoAccount;
        }

        if (store.Find(row.Key) == null)
        {
            return RewardStoreResult.Unavailable;
        }

        if (!IsUnlocked(pm, store, row))
        {
            return RewardStoreResult.Locked;
        }

        if (IsOwned(pm, row))
        {
            return RewardStoreResult.AlreadyOwned;
        }

        return store.Currency.Balance(pm) < row.Price ? RewardStoreResult.NotEnough : RewardStoreResult.Bought;
    }

    /// <summary>
    /// Buys the row: every check again, server side, then the items into the pack (all or none), then exactly the price.
    /// Nothing is taken on a refusal, and a failed spend takes the items back.
    /// </summary>
    public static RewardStoreResult Purchase(PlayerMobile pm, RewardStoreDef store, RewardStoreRow row)
    {
        if (!store.CanShop(pm, out var why))
        {
            pm.SendMessage(0x22, why);
            return RewardStoreResult.Unavailable;
        }

        // The row as the store sells it now: a stale window (the season changed, the row left) buys nothing.
        var current = store.Find(row.Key);
        var check = current == null ? RewardStoreResult.Unavailable : Check(pm, store, current);
        if (check != RewardStoreResult.Bought)
        {
            pm.SendMessage(0x22, RefusalLine(check, pm, store, current ?? row));
            return check;
        }

        row = current!;
        var data = DataOf(pm)!;
        var price = row.Price;

        if (row.IsTitle)
        {
            if (!store.Currency.TrySpend(pm, price))
            {
                pm.SendMessage(0x22, RefusalLine(RewardStoreResult.NotEnough, pm, store, row));
                return RewardStoreResult.NotEnough;
            }

            data.LeagueTitles.Owned.Add(row.TitleKey!);
            pm.SendMessage(0x44, $"System: Title unlocked: \"{LeagueTitles.Display(row.TitleKey!)}\". Choose it in Titles.");
            pm.PlaySound(0x5A7);
            return RewardStoreResult.Bought;
        }

        var items = row.Create!(pm);
        var pack = pm.Backpack;
        if (pack == null || items.Count == 0 || !CanHoldAll(pm, pack, items))
        {
            DeleteAll(items);
            pm.SendLocalizedMessage(1074361); // The reward could not be given.  Make sure you have room in your pack.
            return RewardStoreResult.NoRoom;
        }

        if (!store.Currency.TrySpend(pm, price))
        {
            DeleteAll(items);
            pm.SendMessage(0x22, RefusalLine(RewardStoreResult.NotEnough, pm, store, row));
            return RewardStoreResult.NotEnough;
        }

        foreach (var item in items)
        {
            pack.DropItem(item);
            item.InvalidateProperties();
        }

        pm.SendLocalizedMessage(1073621); // Your reward has been placed in your backpack.
        pm.PlaySound(0x5A7);
        return RewardStoreResult.Bought;
    }

    public static string RefusalLine(RewardStoreResult result, PlayerMobile pm, RewardStoreDef store, RewardStoreRow row) =>
        result switch
        {
            RewardStoreResult.NoAccount    => "You have no account record.",
            RewardStoreResult.Unavailable  => "That is no longer for sale here.",
            RewardStoreResult.Locked       => row.Condition != null && !row.Condition(pm) && row.ConditionText != null
                ? row.ConditionText
                : store.OpensText(row.OpensAt),
            RewardStoreResult.AlreadyOwned => "You already hold that title.",
            RewardStoreResult.NotEnough    =>
                $"That costs {row.Price:N0} {store.Currency.Name} (you have {store.Currency.Balance(pm):N0}).",
            RewardStoreResult.NoRoom       => "Make room in your pack first.",
            _                              => ""
        };

    // CleanUpBritanniaRewards.CanHoldAll, as LeagueShop: several items, checked together before any is given.
    public static bool CanHoldAll(PlayerMobile pm, Container pack, List<Item> items)
    {
        var weight = items.Sum(i => i.TotalWeight + i.PileWeight);
        return pack.CheckHold(pm, items[0], false, true, items.Count - 1, weight - items[0].TotalWeight - items[0].PileWeight);
    }

    private static void DeleteAll(List<Item> items)
    {
        foreach (var item in items)
        {
            item.Delete();
        }
    }

    // -- Helpers the stock tables use --

    public static Func<PlayerMobile, List<Item>> One(Func<Item> make) => _ => [make()];

    public static Func<PlayerMobile, List<Item>> One(Func<PlayerMobile, Item> make) => pm => [make(pm)];

    /// <summary>A stack of a stackable item (Amount), or the item alone.</summary>
    public static Func<PlayerMobile, List<Item>> Stack<T>(int amount) where T : Item =>
        _ =>
        {
            // Loot.Construct: most stock resources have only an optional-amount constructor (no new()).
            var item = Loot.Construct<T>(typeof(T));
            if (item.Stackable)
            {
                item.Amount = amount;
            }

            return [item];
        };

    /// <summary>A tool with this many uses (pinned BaseTool.UsesRemaining), or the item alone.</summary>
    public static Func<PlayerMobile, List<Item>> Tool<T>(int uses) where T : Item =>
        _ =>
        {
            var item = Loot.Construct<T>(typeof(T));
            if (item is IUsesRemaining tool)
            {
                tool.UsesRemaining = uses;
            }

            return [item];
        };

    /// <summary>Several of a non-stacking item in one bag.</summary>
    public static Func<PlayerMobile, List<Item>> Bagged<T>(string bagName, int count) where T : Item =>
        _ =>
        {
            var bag = new Bag { Name = bagName };
            for (var i = 0; i < count; i++)
            {
                bag.DropItem(Loot.Construct<T>(typeof(T)));
            }

            return [bag];
        };
}
