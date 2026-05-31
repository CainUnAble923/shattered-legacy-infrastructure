using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

// ─────────────────────────────────────────────────────────────────────────────
// TrashBag — Custodians portable cleanup bag
//
// Works as a normal bag: double-click opens the container as usual, items
// can be dragged in and out freely.
//
// Right-click the bag → "Dump for Tokens" → opens TrashBagGump which shows
// contents, estimated yield, current balance, and action buttons.
//
// "Dump Now"       — deletes eligible items and awards Civic Tokens.
// "Return All"     — moves all bag contents back to the player's backpack.
// ─────────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class TrashBag : Container
{
    private const int BagItemID = 0xE75; // bag graphic
    private const int BagHue    = 0x48D; // muted green

    [Constructible]
    public TrashBag() : base(BagItemID)
    {
        Name     = "trash bag";
        Hue      = BagHue;
        LootType = LootType.Blessed;
    }

    public TrashBag(Serial serial) : base(serial) { }

    // ── Context menu: "Dump for Tokens" ──────────────────────────────────────

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from is PlayerMobile pm && IsChildOf(pm.Backpack))
            list.Add(new DumpEntry(pm, this));
    }

    // ── Context entry ─────────────────────────────────────────────────────────

    private sealed class DumpEntry : ContextMenuEntry
    {
        private readonly PlayerMobile _pm;
        private readonly TrashBag     _bag;

        public DumpEntry(PlayerMobile pm, TrashBag bag) : base(6146, 3) // 6146 = "Talk" cliloc
        {
            _pm  = pm;
            _bag = bag;
        }

        public override string ToString() => "Dump for Tokens";

        public override void OnClick(Mobile from, IEntity target)
        {
            if (_bag.Deleted || _bag.RootParent != _pm) return;
            _pm.SendGump(new TrashBagGump(_pm, _bag));
        }
    }
}

// ── TrashBag gump ─────────────────────────────────────────────────────────────

public class TrashBagGump : Gump
{
    private const int BtnDump      = 1;
    private const int BtnReturnAll = 2;

    private readonly PlayerMobile _pm;
    private readonly Serial       _bagSerial;

    public TrashBagGump(PlayerMobile pm, TrashBag bag) : base(120, 100)
    {
        _pm        = pm;
        _bagSerial = bag.Serial;

        Closable   = true;
        Disposable = true;

        // Account state
        var balance  = 0;
        var standing = 0;
        if (pm.Account is IAccount acct)
        {
            var data = ClusterFAccountPersistence.GetOrCreate(acct);
            balance  = data.GetCurrency("custodians");
            standing = data.GetReputation("custodians");
        }
        var rank = ClusterFCustodianSystem.GetCustodianRank(standing);

        // Count eligible items
        var eligibleCount  = 0;
        var estimatedTotal = 0;
        foreach (var item in bag.Items)
        {
            if (ClusterFCustodianSystem.IsEligible(item, requireGround: false))
            {
                eligibleCount++;
                estimatedTotal += ClusterFCustodianSystem.ComputeTokens(item);
            }
        }

        const int W = 380;
        const int H = 290;

        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(8, 8, W - 16, H - 16);

        AddLabel(W / 2 - 35, 14, 1153, "Trash Bag");
        AddImageTiled(10, 34, W - 20, 2, 9304);

        AddLabel(18, 44, 999,  "Rank:");
        AddLabel(70, 44, 1153, rank);
        AddLabel(18, 64, 999,  "Civic Tokens:");
        AddLabel(110, 64, 68,  $"{balance:N0}");
        AddImageTiled(10, 86, W - 20, 2, 9304);

        if (bag.Items.Count == 0)
        {
            AddLabel(18, 96, 999, "The bag is empty.");
        }
        else
        {
            AddLabel(18, 96,  999, $"Contents:  {bag.Items.Count} item{(bag.Items.Count == 1 ? "" : "s")}");
            AddLabel(18, 116, 999, $"Eligible:  {eligibleCount} item{(eligibleCount == 1 ? "" : "s")}");
            if (estimatedTotal > 0)
                AddLabel(18, 136, 68, $"Estimated: +{estimatedTotal} Civic Token{(estimatedTotal == 1 ? "" : "s")}");
            else
                AddLabel(18, 136, 37, "No eligible items in bag.");
        }

        AddImageTiled(10, 162, W - 20, 2, 9304);

        var btnY = 174;
        if (eligibleCount > 0)
        {
            AddButton(18, btnY, 4005, 4007, BtnDump, GumpButtonType.Reply, 0);
            AddLabel(54, btnY + 2, 1154,
                $"Dump Now  (+{estimatedTotal} Civic Token{(estimatedTotal == 1 ? "" : "s")})");
            btnY += 32;
        }

        AddButton(18, btnY, 4005, 4007, BtnReturnAll, GumpButtonType.Reply, 0);
        AddLabel(54, btnY + 2, 999,
            bag.Items.Count > 0 ? "Return All Items to Pack" : "Close");

        AddHtml(18, 232, W - 36, 46,
            "<BASEFONT COLOR=#888888>Place items in the bag then right-click → Dump for Tokens. " +
            "Blessed, quest, named, and exceptional items are ineligible.</BASEFONT>",
            false, false);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm) return;

        var bag = World.FindItem(_bagSerial) as TrashBag;
        if (bag == null || bag.Deleted || bag.RootParent != pm)
        {
            pm.SendMessage(0x22, "Your trash bag is no longer accessible.");
            return;
        }

        switch (info.ButtonID)
        {
            case BtnDump:
                DumpBag(pm, bag);
                break;
            case BtnReturnAll:
                ReturnAll(pm, bag);
                break;
        }
    }

    private static void DumpBag(PlayerMobile pm, TrashBag bag)
    {
        var snapshot = new List<Item>(bag.Items);
        var count    = 0;
        var total    = 0;

        foreach (var item in snapshot)
        {
            if (item.Deleted) continue;
            if (!ClusterFCustodianSystem.IsEligible(item, requireGround: false)) continue;

            total += ClusterFCustodianSystem.ComputeTokens(item);
            count++;
            item.Delete();
        }

        if (count > 0)
        {
            ClusterFCustodianSystem.AwardTokens(pm, total);

            // Award CleanedDebris bundles: 1 per 5 items cleaned in the dump
            var bundles = count / 5;
            if (bundles > 0 && pm.Backpack != null)
            {
                var debris = new CleanedDebris { Amount = bundles };
                pm.Backpack.DropItem(debris);
                pm.SendMessage(0x44,
                    $"Dumped {count} item{(count == 1 ? "" : "s")}: " +
                    $"+{total} Civic Token{(total == 1 ? "" : "s")}, " +
                    $"{bundles} civic waste bundle{(bundles == 1 ? "" : "s")}.");
            }
            else
            {
                pm.SendMessage(0x44,
                    $"Dumped {count} item{(count == 1 ? "" : "s")}: " +
                    $"+{total} Civic Token{(total == 1 ? "" : "s")}.");
            }
            pm.SendSound(0x3D);
        }
        else
        {
            pm.SendMessage(0x22, "No eligible items in the bag to dump.");
        }

        pm.SendGump(new TrashBagGump(pm, bag));
    }

    private static void ReturnAll(PlayerMobile pm, TrashBag bag)
    {
        if (pm.Backpack == null) { pm.SendMessage(0x22, "You have no backpack."); return; }

        var snapshot = new List<Item>(bag.Items);
        var moved    = 0;

        foreach (var item in snapshot)
        {
            if (item.Deleted) continue;
            pm.Backpack.DropItem(item);
            moved++;
        }

        if (moved > 0)
            pm.SendMessage(0x59, $"{moved} item{(moved == 1 ? "" : "s")} returned to your backpack.");
        else
            pm.SendMessage(0x59, "The bag was already empty.");
    }
}
