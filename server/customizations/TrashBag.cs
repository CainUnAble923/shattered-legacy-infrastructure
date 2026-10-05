using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

// -----------------------------------------------------------------------------
// TrashBag - Custodians portable cleanup bag
//
// Works as a normal bag: double-click opens the container as usual, items
// can be dragged in and out freely.
//
// Right-click the bag -> "Dump for Clean Up points" -> opens TrashBagGump which shows contents, estimated yield,
// current points, and action buttons.
//
// "Dump Now"       - deletes eligible items and awards Clean Up Britannia points (cc-P33, F-3; was Civic Tokens).
// "Return All"     - moves all bag contents back to the player's backpack.
//
// cc-P33 (F-3): the bag is free and one per character at a time, enforced by owner (Chase, 2026-09-29). The bag
// records its owner (version 1); IssueTo deletes every bag the character already owns, wherever it is, then gives
// one. Only the owner can dump it. A bag from before version 1 has no owner and dumps for no one; its owner asks a
// Sanitation Warden for a new one. Craft X routes rejects into the crafter's own bag (ClusterFCraftRejects.cs).
// -----------------------------------------------------------------------------

[SerializationGenerator(1, false)]
public partial class TrashBag : Container
{
    private const int BagItemID = 0xE75; // bag graphic
    private const int BagHue    = 0x48D; // muted green

    // Every bag in the world, so IssueTo finds a character's old bag wherever it is.
    private static readonly HashSet<TrashBag> _all = new();

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _owner;

    [Constructible]
    public TrashBag() : base(BagItemID)
    {
        Name     = "trash bag";
        Hue      = BagHue;
        LootType = LootType.Blessed;
        _all.Add(this);
    }

    // A version 0 bag has no fields and no owner.
    private void MigrateFrom(V0Content content)
    {
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization() => _all.Add(this);

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        _all.Remove(this);
    }

    /// <summary>Every bag this character owns, wherever it is.</summary>
    public static List<TrashBag> OwnedBy(Mobile owner) =>
        _all.Where(b => !b.Deleted && b.Owner == owner).ToList();

    /// <summary>
    /// Gives the character a new trash bag, free, after deleting every bag it already owns (in a house, the bank, on the
    /// ground). Returns the new bag, or null if the character has no backpack.
    /// </summary>
    public static TrashBag IssueTo(PlayerMobile pm)
    {
        if (pm.Backpack == null)
        {
            return null;
        }

        foreach (var old in OwnedBy(pm))
        {
            old.Delete();
        }

        var bag = new TrashBag { Owner = pm };
        pm.Backpack.DropItem(bag);
        return bag;
    }

    // -- Context menu: "Dump for Clean Up points" --------------------------------

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from is PlayerMobile pm && IsChildOf(pm.Backpack))
            list.Add(new DumpEntry(pm, this));
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (_owner != null)
        {
            list.Add($"Owned by {_owner.Name}");
        }
    }

    // -- Context entry ---------------------------------------------------------

    internal sealed class DumpEntry : ContextMenuEntry
    {
        private readonly PlayerMobile _pm;
        private readonly TrashBag     _bag;

        // cc-P56 Part C (D79): 1151316 "Clean Up Britannia", the system the bag dumps into (was 6146, "Talk").
        public const int Cliloc = 1151316;

        public DumpEntry(PlayerMobile pm, TrashBag bag) : base(Cliloc, 3)
        {
            _pm  = pm;
            _bag = bag;
        }

        public override string ToString() => "Dump for Clean Up points";

        public override void OnClick(Mobile from, IEntity target)
        {
            if (_bag.Deleted || _bag.RootParent != _pm) return;
            _pm.SendGump(new TrashBagGump(_pm, _bag));
        }
    }
}

// -- TrashBag gump -------------------------------------------------------------

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

        var points   = ClusterFCustodianSystem.GetPoints(pm);
        var lifetime = ClusterFCustodianSystem.GetLifetimePoints(pm);
        var rank     = ClusterFCustodianSystem.GetCustodianRank((int)Math.Floor(lifetime));
        var owned    = bag.Owner == pm;

        // Count eligible items
        var eligibleCount  = 0;
        var estimatedTotal = 0.0;
        foreach (var item in bag.Items)
        {
            if (ClusterFCustodianSystem.IsEligible(item, requireGround: false))
            {
                eligibleCount++;
                estimatedTotal += ClusterFCustodianSystem.ComputePoints(item);
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
        AddLabel(18, 64, 999,  "Clean Up points:");
        AddLabel(130, 64, 68,  ClusterFCustodianSystem.FormatPoints(points));
        AddImageTiled(10, 86, W - 20, 2, 9304);

        if (!owned)
        {
            AddLabel(18, 96, 37, "This bag is not yours. Ask a Sanitation Warden for your own.");
        }
        else if (bag.Items.Count == 0)
        {
            AddLabel(18, 96, 999, "The bag is empty.");
        }
        else
        {
            AddLabel(18, 96,  999, $"Contents:  {bag.Items.Count} item{(bag.Items.Count == 1 ? "" : "s")}");
            AddLabel(18, 116, 999, $"Eligible:  {eligibleCount} item{(eligibleCount == 1 ? "" : "s")}");
            if (eligibleCount > 0)
                AddLabel(18, 136, 68, $"Estimated: +{ClusterFCustodianSystem.FormatPoints(estimatedTotal)} Clean Up points");
            else
                AddLabel(18, 136, 37, "No eligible items in bag.");
        }

        AddImageTiled(10, 162, W - 20, 2, 9304);

        var btnY = 174;
        if (owned && eligibleCount > 0)
        {
            AddButton(18, btnY, 4005, 4007, BtnDump, GumpButtonType.Reply, 0);
            AddLabel(54, btnY + 2, 1154,
                $"Dump Now  (+{ClusterFCustodianSystem.FormatPoints(estimatedTotal)} Clean Up points)");
            btnY += 32;
        }

        AddButton(18, btnY, 4005, 4007, BtnReturnAll, GumpButtonType.Reply, 0);
        AddLabel(54, btnY + 2, 999,
            bag.Items.Count > 0 ? "Return All Items to Pack" : "Close");

        AddHtml(18, 232, W - 36, 46,
            "<BASEFONT COLOR=#888888>Place items in the bag then right-click -> Dump for Clean Up points. " +
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

    /// <summary>Deletes the bag's eligible items and pays their Clean Up points. Only the owner can dump a bag.</summary>
    public static double DumpBag(PlayerMobile pm, TrashBag bag)
    {
        if (bag.Owner != pm)
        {
            pm.SendMessage(0x22, "This trash bag is not yours. Ask a Sanitation Warden for your own.");
            return 0;
        }

        var snapshot = new List<Item>(bag.Items);
        var count    = 0;
        var valued   = 0;
        var total    = 0.0;

        foreach (var item in snapshot)
        {
            if (item.Deleted) continue;
            if (!ClusterFCustodianSystem.IsEligible(item, requireGround: false)) continue;

            var points = ClusterFCustodianSystem.ComputePoints(item);
            total += points;
            count++;
            if (points > 0) valued++;
            item.Delete();
        }

        if (count > 0)
        {
            ClusterFCustodianSystem.AwardPoints(pm, total);

            pm.SendMessage(0x44,
                $"Dumped {count} item{(count == 1 ? "" : "s")}: " +
                $"+{ClusterFCustodianSystem.FormatPoints(total)} Clean Up points" +
                ClusterFCustodianSystem.GiveBundles(pm, valued) + ".");
            pm.SendSound(0x3D);
        }
        else
        {
            pm.SendMessage(0x22, "No eligible items in the bag to dump.");
        }

        if (pm.NetState != null)
        {
            pm.SendGump(new TrashBagGump(pm, bag));
        }

        return total;
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
