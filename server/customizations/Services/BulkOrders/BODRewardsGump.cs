// cc-P122: the 2016 bulk-order reward gumps, ported from ServUO Scripts/Gumps/BaseRewardGump.cs (the collection list and
// its confirm step), Scripts/Services/BulkOrders/Rewards/RewardsGump.cs and ConfirmBankPointsGump.cs
// (and the ConfirmCallbackGump it extends, Scripts/Gumps/ConfirmCallbackGump.cs), onto pinned's Gump.
//
// The layout is ServUO's (background, title 1157082, the points line, ten-row pages with Previous and Next), except that
// each row is a fixed 30 pixels: ServUO sizes rows by the art's bounds (ItemBounds), which pinned only has after generating
// its bounds file. A placeholder reward (BODRewardCollections) is listed with its price in grey and "not on the shard
// yet", and has no button.

using System;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.BulkOrders;

public class BODRewardsGump : Gump
{
    public const int ButtonOffset = 200;
    private const int RowHeight = 30;

    private readonly Mobile _owner;
    private readonly PlayerMobile _user;
    private readonly BODType _type;

    /// <summary>
    /// Points to spend now: a turn-in's pending points, or (0) the banked points. ServUO's UsingBanked is points == 0.
    /// </summary>
    public BODRewardsGump(Mobile owner, PlayerMobile user, BODType type, int points = 0) : base(50, 50)
    {
        user.CloseGump<BODRewardsGump>();

        _owner = owner;
        _user = user;
        _type = type;
        UsingBanked = points == 0;
        Points = points == 0 ? BulkOrderSystem.GetPoints(user, type) : points;

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

        AddHtmlLocalized(70, 35, 270, 20, 1157082, 0x1); // title
        AddHtmlLocalized(50, 65, 150, 20, 1072843, 0x1); // Your Reward Points:
        AddLabel(230, 65, 0x64, Points.ToString("0.000000"));
        AddImageTiled(35, 85, 270, 2, 0x23C5);
        AddHtmlLocalized(35, 90, 270, 20, 1072844, 0x1); // Please Choose a Reward:

        var collection = BODRewardCollections.Get(type);
        var page = 1;
        var index = 0;

        while (index < collection.Count)
        {
            AddPage(page);

            var offset = 110;

            while (offset + RowHeight <= 330 && index < collection.Count)
            {
                var item = collection[index];
                var affordable = Points >= item.Points;

                if (affordable && !item.IsPlaceholder)
                {
                    AddButton(35, offset + 5, 0x837, 0x838, ButtonOffset + index);
                }

                AddItem(55, offset, item.ItemID, affordable && !item.IsPlaceholder ? item.Hue : 0x3E9);

                if (item.Tooltip > 0)
                {
                    AddTooltip(item.Tooltip);
                }

                if (item.IsPlaceholder)
                {
                    AddLabel(110, offset + 5, 0x3B2, $"{item.Points:N0}  not on the shard yet");
                }
                else
                {
                    AddLabel(110, offset + 5, affordable ? 0x64 : 0x21, item.Points.ToString("N0"));
                }

                offset += RowHeight;
                index++;
            }

            if (page > 1)
            {
                AddButton(150, 335, 0x15E3, 0x15E7, 0, GumpButtonType.Page, page - 1);
                AddHtmlLocalized(170, 335, 60, 20, 1074880, 0x1); // Previous
            }

            page++;

            if (index < collection.Count)
            {
                AddButton(300, 335, 0x15E1, 0x15E5, 0, GumpButtonType.Page, page);
                AddHtmlLocalized(240, 335, 60, 20, 1072854, 0x1); // <div align=right>Next</div>
            }
        }
    }

    public double Points { get; }

    public bool UsingBanked { get; }

    public BODType BODType => _type;

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm || pm != _user || info.ButtonID < ButtonOffset)
        {
            return;
        }

        var collection = BODRewardCollections.Get(_type);
        var index = info.ButtonID - ButtonOffset;

        if (index >= collection.Count)
        {
            return;
        }

        var item = collection[index];

        if (item.IsPlaceholder || item.Points > CurrentPoints())
        {
            pm.SendLocalizedMessage(1073122); // You don't have enough points for that!
            return;
        }

        pm.SendGump(new BODConfirmRewardGump(this, index));
    }

    private double CurrentPoints() => UsingBanked ? BulkOrderSystem.GetPoints(_user, _type) : Points;

    /// <summary>ServUO RewardsGump.OnConfirmed: the item to the pack, then the points spent (or the pending cleared).</summary>
    public void OnConfirmed(int index)
    {
        var collection = BODRewardCollections.Get(_type);

        if (index < 0 || index >= collection.Count)
        {
            return;
        }

        var reward = collection[index];

        // Re-checked at the click: points may have been spent, or the turn-in's pending points claimed, meanwhile.
        if (reward.IsPlaceholder || CurrentPoints() < reward.Points ||
            !UsingBanked && BulkOrderSystem.GetContext(_user)?.GetPendingRewardFor(_type) != (int)Points)
        {
            _user.SendLocalizedMessage(1073122); // You don't have enough points for that!
            return;
        }

        var item = reward.Construct();

        if (item == null)
        {
            return;
        }

        if (_user.Backpack?.TryDropItem(_user, item, false) != true)
        {
            _user.SendLocalizedMessage(1074361); // The reward could not be given.  Make sure you have room in your pack.
            item.Delete();

            _user.SendGump(new BODRewardsGump(_owner, _user, _type, UsingBanked ? 0 : (int)Points));
            return;
        }

        _user.SendLocalizedMessage(1073621); // Your reward has been placed in your backpack.
        _user.PlaySound(0x5A7);

        if (UsingBanked)
        {
            BulkOrderSystem.DeductPoints(_user, _type, reward.Points);
        }
        else
        {
            BulkOrderSystem.RemovePending(_user, _type);
        }
    }

    public Mobile Owner => _owner;
}

/// <summary>ServUO's aConfirmRewardGump (BaseRewardGump.cs:213-245): 1074974 / 1074975, within 5 tiles of the vendor.</summary>
public class BODConfirmRewardGump : Gump
{
    private readonly BODRewardsGump _list;
    private readonly int _index;

    public BODConfirmRewardGump(BODRewardsGump list, int index) : base(120, 50)
    {
        _list = list;
        _index = index;

        Closable = false;

        AddPage(0);
        AddBackground(0, 0, 270, 120, 0x13BE);
        AddHtmlLocalized(10, 10, 250, 20, 1074974, 0x7FFF); // Confirm Selection
        AddHtmlLocalized(10, 35, 250, 40, 1074975, 0x7FFF); // Are you sure you wish to select this?

        AddButton(20, 85, 0xFA5, 0xFA7, 1);
        AddHtmlLocalized(55, 87, 75, 20, 1011036, 0x7FFF); // OKAY

        AddButton(140, 85, 0xFA5, 0xFA7, 0);
        AddHtmlLocalized(175, 87, 75, 20, 1011012, 0x7FFF); // CANCEL
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (info.ButtonID == 1 && _list.Owner != null && from.InRange(_list.Owner.Location, 5))
        {
            _list.OnConfirmed(_index);
        }
    }
}

/// <summary>
/// ServUO's ConfirmBankPointsGump: after a turn-in with points mode Enabled, bank the points (2% small, 20% large) and open
/// the banked rewards, or claim them now (the rewards gump with the turn-in's points). It cannot be closed unanswered.
/// </summary>
public class BODConfirmBankPointsGump : Gump
{
    public const int BtnBank = 1;
    public const int BtnClaim = 2;

    private readonly PlayerMobile _user;
    private readonly Mobile _owner;
    private readonly BODType _type;
    private readonly int _points;
    private readonly double _banked;

    public BODConfirmBankPointsGump(PlayerMobile user, Mobile owner, BODType type, int points, double banked)
        : base(20, 20)
    {
        user.CloseGump<BODConfirmBankPointsGump>();

        _user = user;
        _owner = owner;
        _type = type;
        _points = points;
        _banked = banked;

        Closable = false;

        AddPage(0);
        AddImageTiled(0, 0, 348, 262, 0xA8E);
        AddAlphaRegion(0, 0, 348, 262);
        AddImage(0, 15, 0x27A8);
        AddImageTiled(0, 30, 17, 200, 0x27A7);
        AddImage(0, 230, 0x27AA);
        AddImage(15, 0, 0x280C);
        AddImageTiled(30, 0, 300, 17, 0x280A);
        AddImage(315, 0, 0x280E);
        AddImage(15, 244, 0x280C);
        AddImageTiled(30, 244, 300, 17, 0x280A);
        AddImage(315, 244, 0x280E);
        AddImage(330, 15, 0x27A8);
        AddImageTiled(330, 30, 17, 200, 0x27A7);
        AddImage(330, 230, 0x27AA);

        AddHtmlLocalized(25, 25, 200, 20, 1157076, 0x7D00); // title
        AddHtmlLocalized(25, 55, 300, 120, 1157077, $"{banked:0.000000}\t{points}", 0x7FFF); // body

        AddItem(115, 110, 0x2258, BulkOrderSystem.GetBodHue(type));

        AddButton(90, 205, 0xFA5, 0xFA7, BtnBank);
        AddHtmlLocalized(125, 207, 100, 20, 1074976, 0x7FFF);

        AddButton(200, 205, 0xFA5, 0xFA7, BtnClaim);
        AddHtmlLocalized(235, 207, 100, 20, 1074977, 0x7FFF);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm || pm != _user)
        {
            return;
        }

        if (info.ButtonID == BtnBank)
        {
            // Only once for this turn-in: the pending points must still be the ones this gump was opened for.
            if (BulkOrderSystem.GetContext(pm)?.GetPendingRewardFor(_type) == _points)
            {
                BulkOrderSystem.SetPoints(pm, _type, _banked);
                BulkOrderSystem.RemovePending(pm, _type);
            }

            pm.SendGump(new BODRewardsGump(_owner, pm, _type));
        }
        else
        {
            pm.SendGump(new BODRewardsGump(_owner, pm, _type, _points));
        }
    }

    public double Banked => _banked;

    public int PointsOffered => _points;
}
