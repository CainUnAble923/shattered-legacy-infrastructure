// cc-P122: the vendor half of the 2016 bulk-order rules, ported from ServUO Scripts/Mobiles/NPCs/BaseVendor.cs: the
// "Bulk Order Info" offer under NewSystemEnabled (:132-231), the bribe (:233-263, :1380-1456), "Claim Rewards"
// (:265-308) and the turn-in branch of OnDragDrop (:1174-1291). Pinned's BaseVendor calls in here from three places
// (BaseVendor-bulk-orders-2016.patch); each call returns "not mine" for any vendor this file does not name, so every other
// vendor runs pinned's code exactly as before.
//
// Which vendors (Chase's answers to P118, 2026-10-15): the town vendors, on OSI's 2016 rules for all eight crafts (option
// A), and each craft's owning guild master for its members. Smith and tailor deeds exist now; the six other crafts are P123.
//   Smith:  Blacksmith, Weaponsmith (and their subclasses: GeorgeHephaestus, our GargoyleWeaponsmith)
//   Tailor: Tailor, Weaver, and the Tailors' Circle guild master (TailorGuildmaster, below) for its members
// The Society of Smiths is not here: its guild master takes smith deeds entirely by its own rules, and at a regular smith
// a member's deed is paid the Society's way before this file is reached (ClusterFSmithBODPayout.cs). Its choice gump at
// the regular smiths takes its offers from the 2016 rule (ClusterFRegularSmithBODs.CreateChosen).
//
// Changes from ServUO, argued in the notes: the reward item the old engine would have built is not built (ServUO builds
// it and drops it, BaseVendor.cs:1218-1226, unused under the new system); City Loyalty's turn-in hook is not ported (no
// City Loyalty on this shard).

using System;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.BulkOrders;
using Server.Gumps;
using Server.Misc;

namespace Server.Mobiles;

public static class BulkOrderVendors
{
    public static BODType? GetVendorBODType(BaseVendor vendor) =>
        vendor switch
        {
            Blacksmith or Weaponsmith            => BODType.Smith,
            Tailor or Weaver or TailorGuildmaster => BODType.Tailor,
            _                                    => null
        };

    public static bool IsNewSystemVendor(BaseVendor vendor) =>
        BulkOrderSystem.NewSystemEnabled && GetVendorBODType(vendor) != null;

    // -- Bulk Order Info (ServUO :151-182) ---------------------------------------------------------------------------

    /// <summary>The stock entry's click at a 2016 vendor: an offer if one is waiting, else when the next comes.</summary>
    public static bool TryOffer(BaseVendor vendor, Mobile from)
    {
        if (!IsNewSystemVendor(vendor) || from is not PlayerMobile pm || !from.InRange(vendor.Location, 10))
        {
            return false;
        }

        var type = GetVendorBODType(vendor)!.Value;

        if (BulkOrderSystem.CanGetBulkOrder(pm, type) || pm.AccessLevel > AccessLevel.Player)
        {
            ShowOffer(pm, BulkOrderSystem.CreateBulkOrder(pm, type, true));
        }
        else
        {
            SayNextOffer(vendor, pm, type);
        }

        return true;
    }

    public static void ShowOffer(PlayerMobile pm, Item bulkOrder)
    {
        if (bulkOrder is LargeBOD large)
        {
            pm.CloseGump<LargeBODAcceptGump>();
            pm.SendGump(new LargeBODAcceptGump(large));
        }
        else if (bulkOrder is SmallBOD small)
        {
            pm.CloseGump<SmallBODAcceptGump>();
            pm.SendGump(new SmallBODAcceptGump(small));
        }
    }

    public static void SayNextOffer(BaseVendor vendor, PlayerMobile pm, BODType type)
    {
        var ts = BulkOrderSystem.GetNextBulkOrder(type, pm);
        var totalMinutes = Math.Max(0, ((int)ts.TotalSeconds + 59) / 60);

        vendor.SayTo(pm, 1072058, 0x3B2, totalMinutes.ToString()); // An offer may be available in about ~1_minutes~ minutes.
    }

    // -- The context menu (ServUO :2438-2448) ------------------------------------------------------------------------

    public static void AddContextEntries(BaseVendor vendor, Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        if (!IsNewSystemVendor(vendor))
        {
            return;
        }

        list.Add(new BribeEntry(vendor.CheckVendorAccess(from)));
        list.Add(new ClaimRewardsEntry(vendor.CheckVendorAccess(from)));
    }

    public class BribeEntry : ContextMenuEntry
    {
        public const int Cliloc = 1152294;

        public BribeEntry(bool enabled) : base(Cliloc, 2) => Enabled = enabled;

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is BaseVendor vendor && from is PlayerMobile && from.InRange(vendor.Location, 2) &&
                vendor.SupportsBulkOrders(from))
            {
                TryBribe(vendor, from);
            }
        }
    }

    public class ClaimRewardsEntry : ContextMenuEntry
    {
        public const int Cliloc = 1155593;

        public ClaimRewardsEntry(bool enabled) : base(Cliloc, 3) => Enabled = enabled;

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is BaseVendor vendor && from is PlayerMobile pm && from.InRange(vendor.Location, 3))
            {
                ClaimRewards(vendor, pm);
            }
        }
    }

    /// <summary>ServUO ClaimRewardsEntry.OnClick (:279-307).</summary>
    public static void ClaimRewards(BaseVendor vendor, PlayerMobile pm)
    {
        var type = GetVendorBODType(vendor);

        if (type == null)
        {
            return;
        }

        var context = BulkOrderSystem.GetContext(pm);
        var pending = context.GetPendingRewardFor(type.Value);

        if (pending > 0)
        {
            if (context.PointsMode == PointsMode.Enabled)
            {
                pm.SendGump(new BODConfirmBankPointsGump(pm, vendor, type.Value, pending, pending * 0.02));
            }
            else
            {
                pm.SendGump(new BODRewardsGump(vendor, pm, type.Value, pending));
            }
        }
        else if (!BulkOrderSystem.CanClaimRewards(pm))
        {
            // You must claim your last turn-in reward in order for us to continue doing business.
            vendor.SayTo(pm, 1157083, 0x3B2);
        }
        else
        {
            pm.SendGump(new BODRewardsGump(vendor, pm, type.Value));
        }
    }

    // -- Bribes (ServUO :1380-1456) ----------------------------------------------------------------------------------

    public static void TryBribe(BaseVendor vendor, Mobile m)
    {
        var state = BulkOrderSystem.GetVendorState(vendor);

        if (state.UnderWatch)
        {
            if (state.WatchEnds < Core.Now)
            {
                state.WatchEnds = DateTime.MinValue;
                state.RecentBribes = 0;
            }
            else
            {
                // My business is being watched by the Guild, so I can't be messing with bulk orders right now. Come back when there's less heat on me!
                vendor.SayTo(m, 1152293, 0x3B2);
                return;
            }
        }

        vendor.SayTo(m, 1152295, 0x3B2); // So you want to do a little business under the table?
        m.SendLocalizedMessage(1152296); // Target a bulk order deed to show to the shopkeeper.

        m.BeginTarget(-1, false, Targeting.TargetFlags.None, (from, targeted) => OfferBribe(vendor, from, targeted));
    }

    /// <summary>The vendor names a price for upgrading the targeted deed; the deed is handed over to pay it.</summary>
    public static int OfferBribe(BaseVendor vendor, Mobile from, object targeted)
    {
        if (targeted is not BaseBOD bod)
        {
            vendor.SayTo(from, 1152297, 0x3B2); // That is not a bulk order deed.
            return -1;
        }

        if (!bod.IsChildOf(from.Backpack) || !BulkOrderSystem.CanExchangeBOD(from, vendor, bod, -1))
        {
            return -1;
        }

        var state = BulkOrderSystem.GetVendorState(vendor);
        var amount = BulkOrderSystem.GetBribe(bod) * state.BribeMultiplier;

        // Per EA, new bribe replaced old pending bribe
        state.Bribes[from] = new PendingBribe(bod, amount);

        // If you help me out, I'll help you out. I can replace that bulk order with a better one, but it's gonna cost you ~1_amt~ gold coin. Payment is due immediately. Just hand me the order and I'll pull the old switcheroo.
        vendor.SayTo(from, 1152292, 0x3B2, amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
        return amount;
    }

    public static void DoBribe(BaseVendor vendor, Mobile m, BaseBOD bod)
    {
        var state = BulkOrderSystem.GetVendorState(vendor);

        BulkOrderSystem.MutateBOD(bod);

        state.RecentBribes++;

        if (state.RecentBribes >= 3 && Utility.Random(6) < state.RecentBribes)
        {
            state.WatchEnds = Core.Now + TimeSpan.FromMinutes(Utility.RandomMinMax(120, 180));
        }

        // You'll find this one much more to your liking. It's been a pleasure, and I look forward to you greasing my palm again very soon.
        vendor.SayTo(m, 1152303, 0x3B2);

        state.Bribes.Remove(m);
        state.BribeMultiplier++;
        state.ResetDecay();
    }

    // -- The turn-in (ServUO :1180-1291) -----------------------------------------------------------------------------

    /// <summary>
    /// A deed dropped on a 2016 vendor. False (not mine) for any other vendor or item, so pinned's own turn-in runs.
    /// </summary>
    public static bool TryTurnIn(BaseVendor vendor, Mobile from, Item dropped, out bool accepted)
    {
        accepted = false;

        if (!IsNewSystemVendor(vendor) || dropped is not BaseBOD bod || from is not PlayerMobile pm)
        {
            return false;
        }

        var state = BulkOrderSystem.GetVendorState(vendor);

        if (state.Bribes.TryGetValue(from, out var bribe) && bribe.BOD == bod)
        {
            if (BulkOrderSystem.CanExchangeBOD(from, vendor, bod, bribe.Amount))
            {
                DoBribe(vendor, from, bod);
            }

            return true; // the deed goes back to the pack, upgraded or not
        }

        if (Core.ML && pm.NextBODTurnInTime > Core.Now)
        {
            vendor.SayTo(from, 1079976, 0x3B2); // You'll have to wait a few seconds while I inspect the last order.
            return true;
        }

        if (!vendor.IsValidBulkOrder(dropped) || !vendor.SupportsBulkOrders(from))
        {
            vendor.SayTo(from, 1045130, 0x3B2); // That order is for some other shopkeeper.
            return true;
        }

        if (!BulkOrderSystem.CanClaimRewards(from))
        {
            // You must claim your last turn-in reward in order for us to continue doing business.
            vendor.SayTo(from, 1157083, 0x3B2);
            return true;
        }

        if (!bod.Complete)
        {
            vendor.SayTo(from, 1045131, 0x3B2); // You have not completed the order yet.
            return true;
        }

        var type = GetVendorBODType(vendor)!.Value;
        var gold = BulkOrderSystem.ComputeGold(bod);
        var fame = bod.ComputeFame();

        from.SendSound(0x3D);
        vendor.SayTo(from, 1157204, 0x3B2, from.Name); // Ho! Ho! Thank ye ~1_PLAYER~ for giving me a Bulk Order Deed!

        var context = BulkOrderSystem.GetContext(from);
        BulkOrderSystem.ComputePoints(bod, out var points, out var banked);

        switch (context.PointsMode)
        {
            case PointsMode.Enabled:
                context.AddPending(type, points);
                pm.SendGump(new BODConfirmBankPointsGump(pm, vendor, type, points, banked));
                break;
            case PointsMode.Disabled:
                context.AddPending(type, points);
                pm.SendGump(new BODRewardsGump(vendor, pm, type, points));
                break;
            case PointsMode.Automatic:
                BulkOrderSystem.SetPoints(from, type, banked);
                pm.SendGump(new BODRewardsGump(vendor, pm, type));
                break;
        }

        // On EA, you have to choose the reward before you get the gold/fame reward.  IF you right click the gump, you lose
        // the gold/fame for that bod. (ServUO's note; ServUO, and this, pay at once.)
        Banker.Deposit(from, gold);

        Titles.AwardFame(from, fame, true);

        vendor.OnSuccessfulBulkOrderReceive(from);

        if (Core.ML)
        {
            pm.NextBODTurnInTime = Core.Now + TimeSpan.FromSeconds(2.0);
        }

        dropped.Delete();
        accepted = true;
        return true;
    }

    // -- Housekeeping ------------------------------------------------------------------------------------------------

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(Mobile m)
    {
        if (m is PlayerMobile pm)
        {
            BulkOrderSystem.RemovePlayer(pm);
        }
    }

    [OnEvent(nameof(CreatureEvents.CreatureDeletedEvent))]
    public static void OnCreatureDeleted(BaseCreature bc)
    {
        if (bc is BaseVendor vendor)
        {
            BulkOrderSystem.RemoveVendor(vendor);
        }
    }
}

/// <summary>
/// The Tailors' Circle guild master gives and takes tailor deeds for its members, on the stock (2016) rules (Chase's
/// answer 2 to P118). Pinned's TailorGuildmaster declares none of these, so a partial declaration adds them.
/// </summary>
public partial class TailorGuildmaster
{
    public const string GuildKey = "tailoring";

    public override bool SupportsBulkOrders(Mobile from) =>
        from is PlayerMobile { Account: IAccount } pm && ClusterFGuildSystem.IsJoined(pm, GuildKey) &&
        pm.Skills.Tailoring.Base > 0;

    public override bool IsValidBulkOrder(Item item) => item is SmallTailorBOD or LargeTailorBOD;

    // Offers come through Bulk Order Info (BulkOrderVendors.TryOffer); a sale to the guild master never brings one.
    public override Item CreateBulkOrder(Mobile from, bool fromContextMenu) => null;
}
