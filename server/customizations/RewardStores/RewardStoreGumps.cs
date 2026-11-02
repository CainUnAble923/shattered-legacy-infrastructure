// cc-P760: the reward stores' window and confirm step, the League shop's own (LeagueShopGumps.cs) for every store, so the
// shard's stores look and work alike:
//
// RewardStoreGump         CleanUpBritanniaRewardGump's scroll frame, five rows a page, the item art greyed until the row
//                         opens. Each row shows its price in the store's currency and what opens it, in cc-P169's
//                         requirement colours (RequirementStyle): green when met, red when not. A Titles button opens the
//                         League title picker, where a bought title is chosen.
// RewardStoreConfirmGump  the confirm step: the row's requirements as a cc-P169 RequirementList, then Buy.
//
// Every press checks again server side (RewardStore.Purchase), so a stale page buys nothing.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Engines.LeagueRewards;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.RewardStores;

public class RewardStoreGump : Gump
{
    public const int RowHeight = LeagueShopGump.RowHeight;
    public const int RowsPerPage = LeagueShopGump.RowsPerPage;
    public const int BtnTitles = 2;
    public const int BtnRowBase = 200;

    public RewardStoreDef Store { get; }

    public IReadOnlyList<RewardStoreRow> Rows { get; }

    public RewardStoreGump(PlayerMobile pm, RewardStoreDef store) : base(50, 50)
    {
        pm.CloseGump<RewardStoreGump>();

        Store = store;
        Rows = store.Rows();

        Closable = true;
        Disposable = true;
        Draggable = true;
        Resizable = false;

        AddPage(0);

        AddImage(0, 0, 0x1F40);
        AddImageTiled(20, 37, 300, 308, 0x1F42);
        AddImage(20, 325, 0x1F43);
        AddImage(35, 8, 0x39);
        AddImageTiled(65, 8, 257, 10, 0x3A);
        AddImage(290, 8, 0x3B);
        AddImage(32, 33, 0x2635);
        AddImageTiled(70, 55, 230, 2, 0x23C5);

        // Dark text on the scroll, as the League shop.
        AddLabel(70, 35, 0, store.Title);
        AddLabel(40, 62, 0, $"{store.Currency.Name}:");
        AddLabel(40, 78, 0x64, store.Currency.Balance(pm).ToString("N0"));
        AddLabel(180, 62, 0, "You:");
        AddLabel(180, 78, 0x64, store.ProgressText(pm));
        AddImageTiled(35, 98, 270, 2, 0x23C5);
        AddLabel(35, 101, 0, store.Blurb);

        AddButton(35, 337, 4011, 4012, BtnTitles);
        AddLabel(69, 337, 0, "Titles");

        if (Rows.Count == 0)
        {
            AddLabel(35, 130, 0, "Nothing is on sale here now.");
        }

        var pages = Math.Max(1, (Rows.Count + RowsPerPage - 1) / RowsPerPage);

        for (var page = 0; page < pages; page++)
        {
            AddPage(page + 1);

            var offset = 122;

            for (var i = page * RowsPerPage; i < Math.Min(Rows.Count, (page + 1) * RowsPerPage); i++)
            {
                var entry = Rows[i];
                var row = Row.Of(pm, store, entry);

                if (row.CanBuy)
                {
                    AddButton(35, offset + RowHeight / 2 - 5, 0x837, 0x838, BtnRowBase + i);
                }

                AddItem(55, offset + 4, entry.ItemID, row.Unlocked ? entry.Hue : LeagueShopGump.LockedArtHue);
                AddLabel(110, offset + 2, row.Unlocked ? 0 : RequirementStyle.DisabledHue, entry.Label);
                AddLabel(110, offset + 20, row.PriceHue, $"{entry.Price:N0} {store.Currency.Name}");
                AddLabel(230, offset + 20, row.OpensHue, row.OpensText);

                offset += RowHeight;
            }

            if (page > 0)
            {
                AddButton(150, 335, 0x15E3, 0x15E7, 0, GumpButtonType.Page, page);
                AddHtmlLocalized(170, 335, 60, 20, 1074880, 0x1); // Previous
            }

            if (page < pages - 1)
            {
                AddButton(300, 335, 0x15E1, 0x15E5, 0, GumpButtonType.Page, page + 2);
                AddHtmlLocalized(240, 335, 60, 20, 1072854, 0x1); // <div align=right>Next</div>
            }
        }
    }

    /// <summary>How one row draws for this buyer: open or locked, the price's and the opening's words and colours.</summary>
    public readonly record struct Row(bool Unlocked, bool Owned, bool CanBuy, int PriceHue, string OpensText, int OpensHue)
    {
        public static Row Of(PlayerMobile pm, RewardStoreDef store, RewardStoreRow entry)
        {
            var unlocked = RewardStore.IsUnlocked(pm, store, entry);
                var owned = RewardStore.IsOwned(pm, entry);
            var affordable = store.Currency.Balance(pm) >= entry.Price;

            var opensText = owned ? "Owned"
                : !unlocked ? (entry.Condition != null && !entry.Condition(pm) && entry.ConditionText != null
                    ? entry.ConditionText
                    : store.OpensText(entry.OpensAt))
                : "Open";
            var opensHue = unlocked ? RequirementStyle.MetHue : RequirementStyle.NotMetHue;
            var priceHue = !unlocked || owned ? RequirementStyle.DisabledHue
                : affordable ? RequirementStyle.MetHue : RequirementStyle.NotMetHue;

            return new Row(unlocked, owned, unlocked && !owned && affordable, priceHue, opensText, opensHue);
        }
    }

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        if (state.Mobile is not PlayerMobile pm)
        {
            return;
        }

        if (info.ButtonID == BtnTitles)
        {
            pm.SendGump(new LeagueTitleGump(pm));
            return;
        }

        var index = info.ButtonID - BtnRowBase;
        if (index < 0 || index >= Rows.Count)
        {
            return;
        }

        var store = Store.Refresh();
        if (!store.CanShop(pm, out var why))
        {
            pm.SendMessage(0x22, why);
            return;
        }

        // The row by its key, as the store sells it now: the confirm step and the purchase check everything again.
        if (store.Find(Rows[index].Key) is { } row)
        {
            pm.SendGump(new RewardStoreConfirmGump(pm, store, row));
        }
        else
        {
            pm.SendMessage(0x22, "That is no longer for sale here.");
            pm.SendGump(new RewardStoreGump(pm, store));
        }
    }
}

public class RewardStoreConfirmGump : GuildStyledGump
{
    public const int BtnBuy = 1;
    public const int BtnBack = 2;

    private const int W = 400;
    private const int H = 240;

    public RewardStoreDef Store { get; }

    public RewardStoreRow Entry { get; }

    public RequirementList Requirements { get; }

    public RewardStoreConfirmGump(PlayerMobile pm, RewardStoreDef store, RewardStoreRow entry) : base(80, 80)
    {
        pm.CloseGump<RewardStoreConfirmGump>();
        Store = store;
        Entry = entry;

        Closable = true;
        Disposable = true;

        var opensMet = store.Progress(pm) >= entry.OpensAt;

        Requirements = new RequirementList()
            .Check(opensMet, entry.OpensAt > 0 ? store.OpensText(entry.OpensAt).Replace("Requires", "Has") : "Open to all",
                store.OpensText(entry.OpensAt));

        if (entry.Condition != null)
        {
            var met = entry.Condition(pm);
            Requirements.Check(met, (entry.ConditionText ?? "").Replace("Requires", "Has"), entry.ConditionText ?? "Not yet");
        }

        Requirements.Amount(store.Currency.Name, store.Currency.Balance(pm), entry.Price);

        if (entry.IsTitle)
        {
            Requirements.Check(!RewardStore.IsOwned(pm, entry), "Title not yet held", "You already hold this title");
        }

        Requirements.Info(store.SpendNote);

        AddBackground(0, 0, W, H, 9270);
        RequirementStyle.AddSolidPanel(this, W, H);

        AddLabel(20, 16, RequirementStyle.HeadingHue, $"Buy from {store.Title}?");
        AddItem(20, 44, entry.ItemID, entry.Hue);
        AddLabel(80, 46, 0x481, entry.Label);
        AddLabel(80, 64, RequirementStyle.InfoHue, $"Price: {entry.Price:N0} {store.Currency.Name}");

        var y = Requirements.Draw(this, 20, 92);
        Requirements.DrawFooter(this, 20, y + 4, BtnBuy, $"Buy for {entry.Price:N0} {store.Currency.Name}");

        AddButton(W - 90, H - 34, 4014, 4015, BtnBack);
        AddLabel(W - 56, H - 32, RequirementStyle.InfoHue, "Back");

    }

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        if (state.Mobile is not PlayerMobile pm)
        {
            return;
        }

        if (info.ButtonID == BtnBuy)
        {
            var store = Store.Refresh();
            RewardStore.Purchase(pm, store, Entry);
            if (store.CanShop(pm, out _))
            {
                pm.SendGump(new RewardStoreGump(pm, store));
            }
        }
        else if (info.ButtonID == BtnBack)
        {
            var store = Store.Refresh();
            if (store.CanShop(pm, out var why))
            {
                pm.SendGump(new RewardStoreGump(pm, store));
            }
            else
            {
                pm.SendMessage(0x22, why);
            }
        }
    }
}
