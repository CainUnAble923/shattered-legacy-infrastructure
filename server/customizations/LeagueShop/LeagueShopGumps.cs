// cc-P229 (F-13): the League shop's window, its confirm step, and the title picker.
//
// LeagueShopGump      shaped like CleanUpBritanniaRewardGump (Services/CleanUpBritannia/CleanUpBritanniaRewards.cs) so the
//                     shard's stores feel alike: the same scroll frame, five rows a page, the item art greyed until it can
//                     be bought. Each row shows its Renown price and its AP; a locked row reads "Requires 500 AP". Prices
//                     and AP use cc-P169's requirement colours (RequirementStyle): green when met, red when not.
// LeagueShopConfirmGump  the confirm step: the row's requirements as a cc-P169 RequirementList, then Buy.
// LeagueTitleGump     the title picker: every League title, the held ones choosable, the rest saying how to get them.
// LeagueHallGump      cc-P243: the Hall page, house decor (LeagueHall.cs), drawn as the shop's own rows, in AP tier order.
//
// Opened from the League Registrar (LeagueRegistrarGump, "League Shop" and "Titles") and from the achievements gump's
// footer. Every press checks again server side (LeagueShop.Purchase, LeagueTitles.Choose), so a stale page buys nothing.

#nullable enable

using System;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.LeagueRewards;

public class LeagueShopGump : Gump
{
    public const int RowHeight = 40;
    public const int RowsPerPage = 5;
    public const int BtnTitles = 2;
    public const int BtnHall = 3; // cc-P243
    public const int BtnRowBase = 200;

    // Item art drawn in this hue on a locked row (CleanUpBritanniaRewardGump's grey, ServUO GetItemHue).
    public const int LockedArtHue = 0x3E9;

    public LeagueShopGump(PlayerMobile pm) : base(50, 50)
    {
        pm.CloseGump<LeagueShopGump>();

        Closable = true;
        Disposable = true;
        Draggable = true;
        Resizable = false;

        var data = LeagueShop.DataOf(pm) ?? new ClusterFAccountData();

        AddPage(0);

        AddImage(0, 0, 0x1F40);
        AddImageTiled(20, 37, 300, 308, 0x1F42);
        AddImage(20, 325, 0x1F43);
        AddImage(35, 8, 0x39);
        AddImageTiled(65, 8, 257, 10, 0x3A);
        AddImage(290, 8, 0x3B);
        AddImage(32, 33, 0x2635);
        AddImageTiled(70, 55, 230, 2, 0x23C5);

        // Dark text on the scroll, as the Clean Up store's cliloc lines (colour 0x1): cc-P169's ice white heading is for
        // the dark panels and would not read on parchment.
        AddLabel(70, 35, 0, "The League Shop");
        AddLabel(50, 65, 0, "Renown:");
        AddLabel(110, 65, 0x64, data.Renown.ToString("N0"));
        AddLabel(180, 65, 0, "AP:");
        AddLabel(210, 65, 0x64, data.AchievementPoints.ToString("N0"));
        AddImageTiled(35, 85, 270, 2, 0x23C5);
        AddLabel(35, 90, 0, "AP opens a row for good. Renown buys.");

        AddButton(35, 337, 4011, 4012, BtnTitles);
        AddLabel(69, 337, 0, "Titles");
        AddButton(105, 337, 4011, 4012, BtnHall);
        AddLabel(139, 337, 0, "Hall");

        var stock = LeagueShop.Stock;
        var pages = (stock.Length + RowsPerPage - 1) / RowsPerPage;

        for (var page = 0; page < pages; page++)
        {
            AddPage(page + 1);

            var offset = 110;

            for (var i = page * RowsPerPage; i < Math.Min(stock.Length, (page + 1) * RowsPerPage); i++)
            {
                var entry = stock[i];
                var row = Row.Of(data, entry);

                if (row.CanBuy)
                {
                    AddButton(35, offset + RowHeight / 2 - 5, 0x837, 0x838, BtnRowBase + i);
                }

                AddItem(55, offset + 4, entry.ItemID, row.Unlocked ? entry.Hue : LockedArtHue);
                AddLabel(110, offset + 2, row.Unlocked ? 0 : RequirementStyle.DisabledHue, entry.Label);
                AddLabel(110, offset + 20, row.PriceHue, $"{entry.Renown:N0} Renown");
                AddLabel(200, offset + 20, row.ApHue, row.ApText);

                offset += RowHeight + 2;
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

    /// <summary>How one row draws for this account: open or locked, the price's and the AP's words and colours.</summary>
    public readonly record struct Row(bool Unlocked, bool Owned, bool CanBuy, int PriceHue, string ApText, int ApHue)
    {
        public static Row Of(ClusterFAccountData data, LeagueShopEntry entry)
        {
            var unlocked = LeagueShop.IsUnlocked(data, entry);
            var owned = LeagueShop.IsOwned(data, entry);
            var affordable = data.Renown >= entry.Renown;

            var apText = owned ? "Owned"
                : !unlocked ? $"Requires {entry.ApRequired:N0} AP"
                : entry.ApRequired > 0 ? $"{entry.ApRequired:N0} AP" : "Open to all";
            var apHue = unlocked ? RequirementStyle.MetHue : RequirementStyle.NotMetHue;
            var priceHue = !unlocked || owned ? RequirementStyle.DisabledHue
                : affordable ? RequirementStyle.MetHue : RequirementStyle.NotMetHue;

            return new Row(unlocked, owned, unlocked && !owned && affordable, priceHue, apText, apHue);
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

        if (info.ButtonID == BtnHall)
        {
            pm.SendGump(new LeagueHallGump(pm));
            return;
        }

        var index = info.ButtonID - BtnRowBase;
        if (index < 0 || index >= LeagueShop.Stock.Length)
        {
            return;
        }

        pm.SendGump(new LeagueShopConfirmGump(pm, LeagueShop.Stock[index]));
    }
}

public class LeagueShopConfirmGump : GuildStyledGump
{
    public const int BtnBuy = 1;
    public const int BtnBack = 2;

    private const int W = 400;
    private const int H = 230;

    public LeagueShopEntry Entry { get; }

    public RequirementList Requirements { get; }

    /// <summary>cc-P243: a Hall row; Buy and Back return to the Hall page.</summary>
    public bool FromHall { get; }

    public LeagueShopConfirmGump(PlayerMobile pm, LeagueShopEntry entry, bool fromHall = false) : base(80, 80)
    {
        pm.CloseGump<LeagueShopConfirmGump>();
        Entry = entry;
        FromHall = fromHall;

        Closable = true;
        Disposable = true;

        var data = LeagueShop.DataOf(pm) ?? new ClusterFAccountData();

        Requirements = new RequirementList()
            .Amount("Achievement Points", data.AchievementPoints, entry.ApRequired)
            .Amount("Renown", data.Renown, entry.Renown);

        if (entry.IsTitle)
        {
            Requirements.Check(!LeagueShop.IsOwned(data, entry), "Title not yet held", "You already hold this title");
        }

        Requirements.Info("Renown spent never lowers your League rank.");

        AddBackground(0, 0, W, H, 9270);
        RequirementStyle.AddSolidPanel(this, W, H);

        AddLabel(20, 16, RequirementStyle.HeadingHue, "Buy from the League Shop?");
        AddItem(20, 44, entry.ItemID, entry.Hue);
        AddLabel(80, 46, 0x481, entry.Label);
        AddLabel(80, 64, RequirementStyle.InfoHue, $"Price: {entry.Renown:N0} Renown");

        var y = Requirements.Draw(this, 20, 92);
        Requirements.DrawFooter(this, 20, y + 4, BtnBuy, $"Buy for {entry.Renown:N0} Renown");

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
            LeagueShop.Purchase(pm, Entry);
            SendBack(pm);
        }
        else if (info.ButtonID == BtnBack)
        {
            SendBack(pm);
        }
    }

    private void SendBack(PlayerMobile pm)
    {
        if (FromHall)
        {
            pm.SendGump(new LeagueHallGump(pm));
        }
        else
        {
            pm.SendGump(new LeagueShopGump(pm));
        }
    }
}

/// <summary>
/// cc-P243: the Hall page. The shop's frame and rows (LeagueShopGump.Row: price and AP in cc-P169's colours, the art greyed
/// until AP opens the row), the rows in AP tier order (LeagueHall.Stock), Buy through the same confirm step.
/// </summary>
public class LeagueHallGump : GuildStyledGump
{
    public const int BtnShop = 3;
    public const int BtnRowBase = 300;

    public LeagueShopEntry[] Rows { get; }

    public LeagueHallGump(PlayerMobile pm) : base(50, 50)
    {
        pm.CloseGump<LeagueHallGump>();

        Closable = true;
        Disposable = true;
        Draggable = true;
        Resizable = false;

        var data = LeagueShop.DataOf(pm) ?? new ClusterFAccountData();
        Rows = LeagueHall.Stock();

        AddPage(0);

        AddImage(0, 0, 0x1F40);
        AddImageTiled(20, 37, 300, 308, 0x1F42);
        AddImage(20, 325, 0x1F43);
        AddImage(35, 8, 0x39);
        AddImageTiled(65, 8, 257, 10, 0x3A);
        AddImage(290, 8, 0x3B);
        AddImage(32, 33, 0x2635);
        AddImageTiled(70, 55, 230, 2, 0x23C5);

        AddLabel(70, 35, 0, "The League Hall");
        AddLabel(50, 65, 0, "Renown:");
        AddLabel(110, 65, 0x64, data.Renown.ToString("N0"));
        AddLabel(180, 65, 0, "AP:");
        AddLabel(210, 65, 0x64, data.AchievementPoints.ToString("N0"));
        AddImageTiled(35, 85, 270, 2, 0x23C5);
        AddLabel(35, 90, 0, "Decor for a house you own. Deeds stay yours.");

        AddButton(35, 337, 4014, 4015, BtnShop);
        AddLabel(69, 337, 0, "Shop");

        const int rowsPerPage = LeagueShopGump.RowsPerPage;
        var pages = (Rows.Length + rowsPerPage - 1) / rowsPerPage;

        for (var page = 0; page < pages; page++)
        {
            AddPage(page + 1);

            var offset = 110;

            for (var i = page * rowsPerPage; i < Math.Min(Rows.Length, (page + 1) * rowsPerPage); i++)
            {
                var entry = Rows[i];
                var row = LeagueShopGump.Row.Of(data, entry);

                if (row.CanBuy)
                {
                    AddButton(35, offset + LeagueShopGump.RowHeight / 2 - 5, 0x837, 0x838, BtnRowBase + i);
                }

                AddItem(55, offset + 4, entry.ItemID, row.Unlocked ? entry.Hue : LeagueShopGump.LockedArtHue);
                AddLabel(110, offset + 2, row.Unlocked ? 0 : RequirementStyle.DisabledHue, entry.Label);
                AddLabel(110, offset + 20, row.PriceHue, $"{entry.Renown:N0} Renown");
                AddLabel(200, offset + 20, row.ApHue, row.ApText);

                offset += LeagueShopGump.RowHeight + 2;
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

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        if (state.Mobile is not PlayerMobile pm)
        {
            return;
        }

        if (info.ButtonID == BtnShop)
        {
            pm.SendGump(new LeagueShopGump(pm));
            return;
        }

        var index = info.ButtonID - BtnRowBase;
        if (index < 0 || index >= Rows.Length)
        {
            return;
        }

        // The row by its key, priced from the settings now: the confirm step and the purchase check everything again.
        if (LeagueHall.FindEntry(Rows[index].Key) is { } entry)
        {
            pm.SendGump(new LeagueShopConfirmGump(pm, entry, true));
        }
    }
}

public class LeagueTitleGump : GuildStyledGump
{
    public const int BtnNone = 99;
    public const int BtnTitleBase = 100;
    public const int BtnShop = 3;

    private const int W = 420;
    private const int RowH = 24;

    public LeagueTitleGump(PlayerMobile pm) : base(90, 70)
    {
        pm.CloseGump<LeagueTitleGump>();

        Closable = true;
        Disposable = true;

        var data = LeagueShop.DataOf(pm) ?? new ClusterFAccountData();
        LeagueTitles.Sync(data);
        var shown = LeagueTitles.ShownKey(data, pm);

        var titles = LeagueTitles.All;
        var listed = 0;
        foreach (var t in titles)
        {
            if (!t.HiddenUntilHeld || data.LeagueTitles.Owned.Contains(t.Key))
            {
                listed++;
            }
        }

        var h = 120 + (listed + 1) * RowH;

        AddBackground(0, 0, W, h, 9270);
        RequirementStyle.AddSolidPanel(this, W, h);

        AddLabel(20, 16, RequirementStyle.HeadingHue, "League Titles");
        AddLabel(20, 36, RequirementStyle.InfoHue, $"Shown after your name. AP: {data.AchievementPoints:N0}");
        AddImageTiled(10, 58, W - 20, 2, 9304);

        var y = 68;

        AddButton(20, y, shown == null ? 4012 : 4011, 4012, BtnNone);
        AddLabel(56, y + 2, shown == null ? RequirementStyle.MetHue : RequirementStyle.HeadingHue, "No League title");
        y += RowH;

        for (var i = 0; i < titles.Length; i++)
        {
            var t = titles[i];
            var held = data.LeagueTitles.Owned.Contains(t.Key);
            // cc-P262: a hidden achievement's title is not listed until it is held, so the picker never gives it away.
            if (t.HiddenUntilHeld && !held)
            {
                continue;
            }

            if (held)
            {
                var isShown = shown == t.Key;
                AddButton(20, y, isShown ? 4012 : 4011, 4012, BtnTitleBase + i);
                AddLabel(56, y + 2, isShown ? RequirementStyle.MetHue : RequirementStyle.HeadingHue, t.Display);
                if (isShown)
                {
                    AddLabel(280, y + 2, RequirementStyle.MetHue, "Shown");
                }
            }
            else
            {
                AddImage(20, y, RequirementStyle.ActionArt, RequirementStyle.DisabledHue);
                AddLabel(56, y + 2, RequirementStyle.DisabledHue, t.Display);
                AddLabel(280, y + 2, RequirementStyle.NotMetHue,
                    t.FreeAtAp >= 0 ? $"Requires {t.FreeAtAp:N0} AP" : "League Shop");
            }

            y += RowH;
        }

        AddImageTiled(10, h - 40, W - 20, 2, 9304);
        AddButton(20, h - 32, 4011, 4012, BtnShop);
        AddLabel(56, h - 30, RequirementStyle.HeadingHue, "League Shop");
    }

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        if (state.Mobile is not PlayerMobile pm || info.ButtonID == 0)
        {
            return;
        }

        if (info.ButtonID == BtnShop)
        {
            pm.SendGump(new LeagueShopGump(pm));
            return;
        }

        if (info.ButtonID == BtnNone)
        {
            LeagueTitles.Choose(pm, null);
            pm.SendMessage(0x44, "System: No League title shown.");
        }
        else
        {
            var index = info.ButtonID - BtnTitleBase;
            if (index < 0 || index >= LeagueTitles.All.Length)
            {
                return;
            }

            var t = LeagueTitles.All[index];
            if (LeagueTitles.Choose(pm, t.Key))
            {
                pm.SendMessage(0x44, $"System: You are now {pm.Name} {t.Display}.");
            }
            else
            {
                pm.SendMessage(0x22, "You do not hold that title.");
            }
        }

        pm.SendGump(new LeagueTitleGump(pm));
    }
}
