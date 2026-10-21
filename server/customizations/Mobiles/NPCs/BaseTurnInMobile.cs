// ServUO: Mobiles/NPCs/BaseTurnInMobile.cs and Mobiles/NPCs/ShrineHealer.cs (cc-P196, the Agent of the Crown's
// base; ServUO's Khaldun and Doom turn-in NPCs stand on it too).
//
// A turn-in NPC: an invulnerable healer who cannot be moved, with three context entries ServUO's ShrineHealer adds
// (Resurrect, Lock Karma, Tithe Gold; he does not resurrect on approach, CheckResurrect false), a "Turn In" entry
// (enabled while the player's backpack holds something IsRedeemableItem says yes to) opening a tile-button gump of
// those items, each click awarding TurnInPoints and deleting the item, and a "Claim" entry (and a double-click within
// five tiles) opening the reward gump SendRewardGump sends.
//
// Conversion notes: pinned's context entries are ContextMenuEntry(number, range) with OnClick(from, target) and are
// added to a ref PooledRefList; there is no OnClickDisabled, so the disabled Turn In entry says nothing (ServUO
// speaks the cancel line). The tile gump is pinned's BaseImageTileButtonsGump with its ItemTileButtonInfo (both in
// Server.Gumps, the Treasures of Tokuno's), HandleButtonResponse and HandleCancel. BaseHealer here is pinned's
// (Mobiles/Healers/BaseHealer.cs: OfferResurrection, CheckResurrect); its IsInvulnerable is a BaseVendor override.
// PaperdollEntry has no arguments here (ServUO passes the mobile); IsShrineHealer false keeps ServUO's meaning: the
// base entries only.

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;
using Server.Network;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public abstract partial class BaseTurnInMobile : BaseHealer
{
    public BaseTurnInMobile(string title) => Title = title;

    public override bool IsInvulnerable => true;
    public override bool DisallowAllMoves => true;
    public override bool ClickTitle => true;
    public override bool CanTeach => false;

    public virtual int TitleLocalization => 0;
    public virtual int CancelLocalization => 0;
    public virtual int TurnInLocalization => 0;
    public virtual int ClaimLocalization => 1155593; // Claim Rewards

    public virtual bool IsShrineHealer => true;

    public virtual int TurnInPoints => 1;

    // ServUO ShrineHealer: no resurrection on approach; the Resurrect entry does it.
    public override bool CheckResurrect(Mobile m) => false;

    public virtual List<ImageTileButtonInfo> FindRedeemableItems(Mobile m)
    {
        var list = new List<ImageTileButtonInfo>();

        if (m?.Backpack == null)
        {
            return list;
        }

        foreach (var item in m.Backpack.Items)
        {
            if (IsRedeemableItem(item))
            {
                list.Add(new ItemTileButtonInfo(item));
            }
        }

        return list;
    }

    public abstract bool IsRedeemableItem(Item item);

    public abstract void SendRewardGump(Mobile m);

    public virtual void AwardPoints(PlayerMobile pm, Item item, int points)
    {
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from is not PlayerMobile pm)
        {
            return;
        }

        if (IsShrineHealer)
        {
            list.Add(new LockKarmaEntry(pm));
            list.Add(new ResurrectEntry(pm, this));
            list.Add(new TitheEntry(pm));
        }

        if (TurnInLocalization > 0)
        {
            list.Add(new TurnInEntry(pm, this));
        }

        list.Add(new ClaimEntry(pm, this));
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile && from.InRange(Location, 5))
        {
            SendRewardGump(from);
        }
    }

    private class TurnInEntry : ContextMenuEntry
    {
        private readonly List<ImageTileButtonInfo> _buttons;
        private readonly Mobile _mobile;
        private readonly BaseTurnInMobile _vendor;

        public TurnInEntry(Mobile mobile, BaseTurnInMobile vendor) : base(vendor.TurnInLocalization, 2)
        {
            _mobile = mobile;
            _vendor = vendor;
            _buttons = _vendor.FindRedeemableItems(_mobile);

            Enabled = _buttons.Count > 0;
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (_buttons.Count > 0)
            {
                _mobile.SendGump(new TurnInGump(_vendor, _buttons));
            }
        }
    }

    private class ClaimEntry : ContextMenuEntry
    {
        private readonly Mobile _mobile;
        private readonly BaseTurnInMobile _vendor;

        public ClaimEntry(Mobile mobile, BaseTurnInMobile vendor) : base(vendor.ClaimLocalization, 2)
        {
            _mobile = mobile;
            _vendor = vendor;
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (_mobile.CheckAlive())
            {
                _vendor.SendRewardGump(_mobile);
            }
        }
    }

    // ServUO ShrineHealer's three entries.
    private class ResurrectEntry : ContextMenuEntry
    {
        private readonly Mobile _mobile;
        private readonly BaseTurnInMobile _vendor;

        public ResurrectEntry(Mobile mobile, BaseTurnInMobile vendor) : base(6195, 2) // Resurrect
        {
            _mobile = mobile;
            _vendor = vendor;

            Enabled = !_mobile.Alive;
        }

        public override void OnClick(Mobile from, IEntity target) => _vendor.OfferResurrection(_mobile);
    }

    private class LockKarmaEntry : ContextMenuEntry
    {
        private readonly PlayerMobile _mobile;

        public LockKarmaEntry(PlayerMobile mobile) : base(mobile.KarmaLocked ? 6197 : 6196, 2) // Unlock Karma / Lock Karma
            => _mobile = mobile;

        public override void OnClick(Mobile from, IEntity target)
        {
            _mobile.KarmaLocked = !_mobile.KarmaLocked;

            if (_mobile.KarmaLocked)
            {
                _mobile.SendLocalizedMessage(1060192); // Your karma has been locked. Your karma can no longer be raised.
            }
            else
            {
                _mobile.SendLocalizedMessage(1060191); // Your karma has been unlocked. Your karma can be raised again.
            }
        }
    }

    private class TitheEntry : ContextMenuEntry
    {
        private readonly Mobile _mobile;

        public TitheEntry(Mobile mobile) : base(6198, 2) // Tithe Gold
        {
            _mobile = mobile;
            Enabled = _mobile.Alive;
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (_mobile.CheckAlive())
            {
                _mobile.SendGump(new TithingGump(_mobile));
            }
        }
    }
}

public class TurnInGump : BaseImageTileButtonsGump
{
    public TurnInGump(BaseTurnInMobile collector, List<ImageTileButtonInfo> buttons)
        : base(collector.TitleLocalization, buttons) => Collector = collector;

    public BaseTurnInMobile Collector { get; }

    public override void HandleButtonResponse(NetState sender, int adjustedButton, ImageTileButtonInfo buttonInfo)
    {
        var pm = sender.Mobile as PlayerMobile;
        var item = ((ItemTileButtonInfo)buttonInfo).Item;

        if (pm == null || item?.Deleted != false || !item.IsChildOf(pm.Backpack) || !pm.InRange(Collector.Location, 7))
        {
            return;
        }

        Collector.AwardPoints(pm, item, Collector.TurnInPoints);

        item.Delete();

        var buttons = Collector.FindRedeemableItems(pm);

        if (buttons.Count > 0)
        {
            pm.SendGump(new TurnInGump(Collector, buttons));
        }
    }

    public override void HandleCancel(NetState sender)
    {
        if (Collector.CancelLocalization > 0)
        {
            Collector.LocalOverheadMessage(MessageType.Regular, 0x3B2, Collector.CancelLocalization);
        }
    }
}
