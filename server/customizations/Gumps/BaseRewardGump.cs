// ServUO: Gumps/BaseRewardGump.cs (cc-P194, ported for the Void Pool reward vendor; ServUO's community
// collections, Clean Up and the Huntmaster use it too).
//
// A reward list: the player's points at the top, then one row per CollectionItem (its picture, its price in the
// points colour when affordable, a select button when affordable), paged as the rows fill, and a confirmation
// before the reward is made and the points taken. GetPoints is the system's; OnConfirmed may be overridden to
// build the reward differently (the Void Pool builds hued cauldron deeds).
//
// Conversion notes: the pinned legacy Gump has the same Add* calls; OnResponse takes an "in RelayInfo";
// the confirmation is pinned's BaseConfirmGump (Gumps/BaseConfirmGump.cs), whose Confirm(Mobile) is the callback.

using System;
using System.Collections.Generic;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Gumps;

public abstract class BaseRewardGump : Gump
{
    protected BaseRewardGump(Mobile owner, PlayerMobile user, List<CollectionItem> col, int title, double points = -1.0)
        : base(50, 50)
    {
        user.CloseGump<BaseRewardGump>();

        Owner = owner;
        User = user;
        Collection = col;
        Title = title;

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

        Index = 0;
        Page = 1;

        Points = points == -1 ? GetPoints(user) : points;

        AddHtmlLocalized(70, 35, 270, 20, Title, 0x1);
        AddHtmlLocalized(50, 65, 150, 20, PointsName, 0x1);
        AddPoints();
        AddImageTiled(35, 85, 270, 2, 0x23C5);
        AddHtmlLocalized(35, 90, 270, 20, RewardLabel, 0x1);

        while (Collection != null && Index < Collection.Count)
        {
            DisplayRewardPage();
        }
    }

    public int Index { get; private set; }
    public Mobile Owner { get; }
    public PlayerMobile User { get; }
    public int Page { get; private set; }
    public int Title { get; }

    public double Points { get; protected set; }
    public List<CollectionItem> Collection { get; protected set; }

    public virtual int PointsName => 1072843; // Your Reward Points:
    public virtual int RewardLabel => 1072844; // Please Choose a Reward:

    public virtual int GetYOffset(int id) => 10;

    protected virtual void AddPoints()
    {
        AddLabel(230, 65, 0x64, ((int)Points).ToString());
    }

    public void DisplayRewardPage()
    {
        AddPage(Page);

        var offset = 110;
        var next = 0;
        var max = GetMax();

        while (offset + next < 320 && Index < Collection.Count)
        {
            var item = Collection[Index];
            var height = Math.Max(item.Height, 20);

            if (Points >= item.Points)
            {
                AddButton(35, offset + height / 2 - 5, 0x837, 0x838, 200 + Index);
                AddTooltip(item.Tooltip);
            }

            var y = offset - item.Y;

            if (item.Height < 20)
            {
                y += (20 - item.Height) / 2;
            }

            Item i = null;

            if (Owner.Backpack != null && item.Type != null)
            {
                i = Owner.Backpack.FindItemByType(item.Type);
            }

            var hue = GetItemHue(i, item);

            AddItem(55 - item.X + max / 2 - item.Width / 2, y, item.ItemID, hue);

            if (i != null)
            {
                AddItemProperty(i.Serial);
            }
            else if (item.Tooltip > 0)
            {
                AddTooltip(item.Tooltip);
            }

            AddLabel(80 + max, offset + height / 2 - 10, Points >= item.Points ? 0x64 : 0x21, item.Points.ToString("N0"));

            offset += GetYOffset(item.ItemID) + height;
            Index++;

            next = Index < Collection.Count ? Math.Max(Collection[Index].Height, 20) : 0;
        }

        if (Page > 1)
        {
            AddButton(150, 335, 0x15E3, 0x15E7, 0, GumpButtonType.Page, Page - 1);
            AddHtmlLocalized(170, 335, 60, 20, 1074880, 0x1); // Previous
        }

        Page++;

        if (Index < Collection.Count)
        {
            AddButton(300, 335, 0x15E1, 0x15E5, 0, GumpButtonType.Page, Page);
            AddHtmlLocalized(240, 335, 60, 20, 1072854, 0x1); // <div align=right>Next</div>
        }
    }

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        var from = state.Mobile;

        if (info.ButtonID >= 200 && from is PlayerMobile)
        {
            var index = info.ButtonID - 200;

            if (index < 0 || index >= Collection.Count)
            {
                return;
            }

            var item = Collection[index];
            var points = GetPoints(from);

            if (item != null && item.Points <= points)
            {
                from.SendGump(new ConfirmRewardGump(Owner, item, index, OnConfirmed));
            }
            else
            {
                from.SendLocalizedMessage(1073122); // You don't have enough points for that!
            }
        }
    }

    public abstract double GetPoints(Mobile m);

    public virtual void RemovePoints(double points)
    {
    }

    public virtual void OnConfirmed(CollectionItem citem, int index)
    {
        if (Activator.CreateInstance(citem.Type) is not Item item)
        {
            return;
        }

        if (User.Backpack == null || !User.Backpack.TryDropItem(User, item, false))
        {
            User.SendLocalizedMessage(1074361); // The reward could not be given.  Make sure you have room in your pack.
            item.Delete();
        }
        else
        {
            OnItemCreated(item);

            User.SendLocalizedMessage(1073621); // Your reward has been placed in your backpack.
            RemovePoints(citem.Points);
            User.PlaySound(0x5A7);
        }
    }

    public virtual void OnItemCreated(Item item)
    {
    }

    public virtual int GetItemHue(Item i, CollectionItem item) => Points >= item.Points ? item.Hue : 0x3E9;

    public virtual int GetMax()
    {
        var max = 0;

        if (Collection != null)
        {
            for (var i = 0; i < Collection.Count; i++)
            {
                if (max < Collection[i].Width)
                {
                    max = Collection[i].Width;
                }
            }
        }

        return max;
    }
}

// ServUO: aConfirmRewardGump.
public class ConfirmRewardGump : BaseConfirmGump
{
    public ConfirmRewardGump(Mobile owner, CollectionItem item, int index, Action<CollectionItem, int> callback)
    {
        Item = item;
        Index = index;
        Owner = owner;

        ConfirmCallback = callback;
    }

    public override int TitleNumber => 1074974; // Are you sure you want to select this reward?
    public override int LabelNumber => 1074975; // Are you sure you wish to select this?

    public Mobile Owner { get; set; }
    public int Index { get; set; }
    public CollectionItem Item { get; set; }

    public Action<CollectionItem, int> ConfirmCallback { get; set; }

    public override void Confirm(Mobile from)
    {
        if (from.InRange(Owner.Location, 5) && Item != null)
        {
            ConfirmCallback?.Invoke(Item, Index);
        }
    }
}
