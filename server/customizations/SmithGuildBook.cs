using System.Collections.Generic;
using System.Text.RegularExpressions;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

// ─────────────────────────────────────────────────────────────────────────────
// SmithGuildBook — Phase 4C-iii
//
// Portable guild interface issued to every Smith member on join.
// Blessed — survives death.
//
// Functions:
//   • Stores SmallSmithBODs and LargeSmithBODs (drag in to file, drag out
//     to remove).
//   • Gump shows all active BODs (book + backpack) with one-click turn-in.
//   • Commission tab: request new commissions, turn in / abandon — no
//     Guildmaster visit required.
//   • Seal balance visible at all times in the header.
// ─────────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class SmithGuildBook : Container
{
    private const int BookItemID = 8793;   // guild book graphic
    private const int BookHue    = 1154;  // Society blue

    [Constructible]
    public SmithGuildBook() : base(BookItemID)
    {
        Name     = "Smithing Guild Book";
        Hue      = BookHue;
        LootType = LootType.Blessed;
        Weight   = 2.0;
    }

    public SmithGuildBook(Serial serial) : base(serial) { }

    // ── Post-load fixup ───────────────────────────────────────────────────────

    /// <summary>Ensure ItemID/Hue are correct on existing instances after a save/load.</summary>
    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (ItemID != BookItemID) ItemID = BookItemID;
        if (Hue    != BookHue)   Hue    = BookHue;
    }

    // ── Container overrides ───────────────────────────────────────────────────

    /// <summary>
    /// When the client drops an item directly ON the book icon,
    /// DropToItem routes here. Redirect to our custom handler.
    /// </summary>
    public override bool OnDragDropInto(Mobile from, Item item, Point3D p) => OnDragDrop(from, item);

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (dropped is not (SmallSmithBOD or LargeSmithBOD))
        {
            from.SendMessage(0x22, "Only Smith bulk orders can be stored in the guild book.");
            return false;
        }
        if (!IsChildOf(from.Backpack))
        {
            from.SendMessage(0x22, "The guild book must be in your backpack.");
            return false;
        }
        AddItem(dropped);
        from.SendMessage(0x59, "Bulk order filed in your guild book.");
        from.PlaySound(0x249);
        return true;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm) return;

        if (!IsChildOf(pm.Backpack))
        {
            pm.SendMessage(0x22, "The guild book must be in your backpack to open.");
            return;
        }

        if (pm.Account is not IAccount acct || !ClusterFGuildSystem.IsJoined(acct, "smithing"))
        {
            pm.SendMessage(0x22, "This book is for members of the Society of Smiths.");
            return;
        }

        pm.SendGump(new SmithGuildBookGump(pm, this));
    }

    // ── Join bonus ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when a player joins the Society of Smiths.
    /// Issues a SmithGuildBook if they don't already have one.
    /// </summary>
    public static void OnSmithingJoined(PlayerMobile pm)
    {
        if (pm.Backpack == null) return;

        foreach (var item in pm.Backpack.Items)
            if (item is SmithGuildBook) return;

        pm.Backpack.DropItem(new SmithGuildBook());
        pm.SendMessage(0x44,
            "You have been issued a Smithing Guild Book. " +
            "File your bulk orders inside and manage commissions from anywhere.");
    }
}

// ── Gump ──────────────────────────────────────────────────────────────────────

public class SmithGuildBookGump : Gump
{
    // Navigation
    private const int BtnNavBODs  = 1;
    private const int BtnNavComms = 2;
    private const int BtnCatalog  = 4;

    // BOD tab
    private const int BtnRequestSmallBOD = 5;
    private const int BtnRequestLargeBOD = 7;

    // Small commission actions
    private const int BtnReqComm        = 3;
    private const int BtnTurnInCommBase  = 20; // 20–22 (small comms)
    private const int BtnAbandonCommBase = 30; // 30–32

    // Large commission actions
    private const int BtnReqLargeComm          = 6;
    private const int BtnTurnInLargePieceBase   = 40; // 40–44 (up to 5 pieces)
    private const int BtnAbandonLargeComm       = 50;

    // BOD turn-in (unchanged)
    private const int BtnTurnInBODBase = 10; // 10–17 (up to 8 BODs)

    private const int W = 500;
    private const int H = 620;

    private readonly PlayerMobile   _pm;
    private readonly SmithGuildBook _book;
    private readonly int            _page; // 0 = BODs, 1 = Commissions

    private readonly List<Serial> _bodSerials = new();
    private readonly List<string> _commIds    = new();

    // Large commission state (set during BuildCommPage, used in OnResponse)
    private string   _largeCommId   = "";
    private string[] _largePieceKeys = new string[0];

    public SmithGuildBookGump(PlayerMobile pm, SmithGuildBook book, int page = 0)
        : base(80, 50)
    {
        _pm   = pm;
        _book = book;
        _page = page;

        if (pm.Account is not IAccount acct) return;

        var data          = ClusterFAccountPersistence.GetOrCreate(acct);
        var seals         = data.GetCurrency("smithing");
        var standing      = data.GetReputation("smithing");
        var rank          = ClusterFGuildSystem.GetRankName("smithing", standing);
        var commissions   = data.SmithCommissions;
        var largeComms    = data.SmithLargeCommissions;

        // Collect all smith BODs: book storage + loose in backpack
        var bods = new List<Item>();
        foreach (var item in book.Items)
            if (item is SmallSmithBOD or LargeSmithBOD) bods.Add(item);
        if (pm.Backpack != null)
            foreach (var item in pm.Backpack.Items)
                if (item is SmallSmithBOD or LargeSmithBOD) bods.Add(item);

        Closable   = true;
        Disposable = true;
        Resizable  = false;

        AddPage(0);
        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(8, 8, W - 16, H - 16);

        // ── Header ────────────────────────────────────────────────────────────
        AddLabel(W / 2 - 80, 14, 1153, "Smithing Guild Book");
        AddImageTiled(10, 34, W - 20, 2, 9304);

        AddLabel(18,  44, 999,  "Rank:");
        AddLabel(60,  44, 1153, rank);
        AddLabel(220, 44, 999,  "Smithing Seals:");
        AddLabel(330, 44, 68,   $"{seals:N0}");
        AddImageTiled(10, 62, W - 20, 2, 9304);

        // ── Tab buttons ───────────────────────────────────────────────────────
        var bodHue  = _page == 0 ? 1154 : 999;
        var commHue = _page == 1 ? 1154 : 999;

        AddButton(18,  70, 4005, 4007, BtnNavBODs,  GumpButtonType.Reply, 0);
        AddLabel(54,  72, bodHue,  $"Bulk Orders ({bods.Count})");

        AddButton(196, 70, 4005, 4007, BtnNavComms, GumpButtonType.Reply, 0);
        AddLabel(232, 72, commHue, $"Commissions ({commissions.Count}s/{largeComms.Count}L)");

        // ClusterF: Seal Catalog shortcut — anchored to right edge
        AddButton(W - 126, 70, 4005, 4007, BtnCatalog, GumpButtonType.Reply, 0);
        AddLabel(W - 90, 72, 999, "Seal Catalog");

        AddImageTiled(10, 92, W - 20, 2, 9304);

        var y = 102;

        if (_page == 0)
            BuildBODPage(bods, ref y);
        else
            BuildCommPage(pm, commissions, largeComms, data, ref y);
    }

    // ── BOD page ──────────────────────────────────────────────────────────────

    private void BuildBODPage(List<Item> bods, ref int y)
    {
        if (bods.Count == 0)
        {
            AddLabel(18, y, 37, "No active bulk orders.");
            y += 22;
            AddLabel(18, y, 999, "Request a new order from the Society Guildmaster.");
            y += 30;
        }
        else
        {
            for (var i = 0; i < bods.Count && i < 8; i++)
            {
                var bod = bods[i];
                _bodSerials.Add(bod.Serial);

                if (bod is SmallSmithBOD small)
                {
                    // ── Small BOD — single line ───────────────────────────────
                    var mat   = BodMatName(small.Material);
                    var exc   = small.RequireExceptional ? "Exceptional " : "";
                    var item  = TypeNameToWords(small.Type?.Name ?? "Item");
                    var label = $"{exc}{mat} {item}  {small.AmountCur}/{small.AmountMax}";

                    var statusHue = small.Complete ? 68 : 999;
                    AddLabel(18,  y, statusHue, small.Complete ? "[Complete]" : "[Active]");
                    AddLabel(100, y, 999, label);

                    if (small.Complete)
                    {
                        AddButton(W - 96, y - 2, 4023, 4025, BtnTurnInBODBase + i, GumpButtonType.Reply, 0);
                        AddLabel(W - 60, y, 1154, "Turn In");
                    }

                    y += 26;
                }
                else if (bod is LargeSmithBOD large)
                {
                    // ── Large BOD — header + per-entry expansion ──────────────
                    var mat        = BodMatName(large.Material);
                    var exc        = large.RequireExceptional ? "Exceptional " : "";
                    var pieceDone  = 0;
                    var pieceTotal = large.Entries?.Length ?? 0;
                    if (large.Entries != null)
                        foreach (var pe in large.Entries)
                            if (pe.Amount >= large.AmountMax)
                                pieceDone++;

                    var headerHue = large.Complete ? 68 : 1154;
                    AddLabel(18,  y, large.Complete ? 68 : 999,
                        large.Complete ? "[Complete]" : "[Active]");
                    AddLabel(100, y, headerHue,
                        $"{exc}{mat} Large Bulk Order  ({pieceDone}/{pieceTotal} pieces)");

                    if (large.Complete)
                    {
                        AddButton(W - 96, y - 2, 4023, 4025, BtnTurnInBODBase + i, GumpButtonType.Reply, 0);
                        AddLabel(W - 60, y, 1154, "Turn In");
                    }

                    y += 22;

                    // Per-entry rows
                    if (large.Entries != null)
                    {
                        foreach (var pe in large.Entries)
                        {
                            var entryDone   = pe.Amount >= large.AmountMax;
                            var entryHue    = entryDone ? 0x44 : (pe.Amount > 0 ? 68 : 999);
                            var marker      = entryDone ? "[✓]" : "[ ]";
                            var entryName   = TypeNameToWords(pe.Details.Type?.Name ?? "Item");
                            var amtLabel    = $"{pe.Amount}/{large.AmountMax}";

                            AddLabel(30,      y, entryHue, marker);
                            AddLabel(62,      y, entryHue, entryName);
                            AddLabel(W - 100, y, entryHue, amtLabel);
                            y += 18;
                        }
                    }

                    y += 6; // extra gap after a large BOD
                }
                else continue;
            }
            y += 4;
        }

        AddImageTiled(10, y, W - 20, 2, 9304);
        y += 10;

        AddHtml(18, y, W - 36, 26,
            "<BASEFONT COLOR=#666666>Drag bulk orders into this book to store them.</BASEFONT>",
            false, false);
        y += 32;

        AddButton(18, y, 4005, 4007, BtnRequestSmallBOD, GumpButtonType.Reply, 0);
        AddLabel(54, y + 2, 999, "Request Small Bulk Order");
        y += 28;

        AddButton(18, y, 4005, 4007, BtnRequestLargeBOD, GumpButtonType.Reply, 0);
        AddLabel(54, y + 2, 999, "Request Large Bulk Order  (requires Journeyman rank)");
    }

    // ── Commission page ───────────────────────────────────────────────────────

    private void BuildCommPage(PlayerMobile pm, List<SmithCommissionEntry> commissions,
        List<SmithLargeCommissionEntry> largeComms, ClusterFAccountData data, ref int y)
    {
        // ── Pre-scan backpack for small commission matches ─────────────────────
        var backpackMatch = new Dictionary<string, bool>();
        if (pm.Backpack != null)
            foreach (var c in commissions)
                foreach (var item in pm.Backpack.Items)
                    if (SmithCommissionSystem.IsMatch(c, item)) { backpackMatch[c.Id] = true; break; }

        // ── Small commissions ──────────────────────────────────────────────────
        AddLabel(18, y, 999, $"Small Commissions ({commissions.Count}/{SmithCommissionSystem.MaxActiveSmallCommissions}):");
        y += 20;

        if (commissions.Count == 0)
        {
            AddLabel(28, y, 37, "None active.");
            y += 22;
        }
        else
        {
            for (var i = 0; i < commissions.Count; i++)
            {
                var c        = commissions[i];
                var hasMatch = backpackMatch.ContainsKey(c.Id);
                _commIds.Add(c.Id);

                AddLabel(18, y, 1153, c.RequesterName);
                AddButton(W - 90, y - 2, 4005, 4007, BtnAbandonCommBase + i, GumpButtonType.Reply, 0);
                AddLabel(W - 54, y, 33, "Abandon");
                y += 20;

                AddHtml(18, y, W - 36, 28,
                    $"<BASEFONT COLOR=#AAAAAA>\"{c.RequesterNote}\"</BASEFONT>", false, false);
                y += 30;

                AddLabel(18, y, 68, $"Wants: {c.FullLabel}");
                y += 20;

                AddLabel(18, y, 999,
                    $"Reward: {c.SealReward} Seal{(c.SealReward == 1 ? "" : "s")}, +{c.StandingReward} standing");
                if (hasMatch)
                {
                    AddButton(W - 96, y - 2, 4023, 4025, BtnTurnInCommBase + i, GumpButtonType.Reply, 0);
                    AddLabel(W - 60, y, 1154, "Turn In");
                }
                else
                    AddLabel(W - 108, y, 37, "Not in pack");

                y += 22;

                if (i < commissions.Count - 1)
                { AddImageTiled(10, y + 2, W - 20, 2, 9304); y += 14; }
            }
            y += 6;
        }

        // ── Large commission ───────────────────────────────────────────────────
        AddImageTiled(10, y, W - 20, 2, 9304);
        y += 10;
        AddLabel(18, y, 999, $"Large Commission ({largeComms.Count}/{SmithCommissionSystem.MaxActiveLargeCommissions}):");
        y += 20;

        if (largeComms.Count == 0)
        {
            AddLabel(28, y, 37, "None active.");
            y += 22;
        }
        else
        {
            var lc     = largeComms[0];
            var setDef = lc.SetDef;

            _largeCommId   = lc.Id;
            _largePieceKeys = setDef?.ItemKeys ?? new string[0];

            // Pre-scan backpack for piece matches
            var pieceMatch = new bool[_largePieceKeys.Length];
            if (pm.Backpack != null)
                for (var pi = 0; pi < _largePieceKeys.Length; pi++)
                {
                    if (lc.FulfilledPieces.Contains(_largePieceKeys[pi])) continue;
                    foreach (var item in pm.Backpack.Items)
                        if (SmithCommissionSystem.IsMatchForPieceKey(lc, _largePieceKeys[pi], item))
                        { pieceMatch[pi] = true; break; }
                }

            // Header
            AddLabel(18, y, 1153, $"{lc.RequesterName}  —  {lc.FullLabel}");
            AddButton(W - 90, y - 2, 4005, 4007, BtnAbandonLargeComm, GumpButtonType.Reply, 0);
            AddLabel(W - 54, y, 33, "Abandon");
            y += 20;

            AddHtml(18, y, W - 36, 26,
                $"<BASEFONT COLOR=#AAAAAA>\"{lc.RequesterNote}\"</BASEFONT>", false, false);
            y += 28;

            // Pieces
            for (var pi = 0; pi < _largePieceKeys.Length; pi++)
            {
                var key       = _largePieceKeys[pi];
                var fulfilled = lc.FulfilledPieces.Contains(key);
                var label     = SmithCommissionPool.GetItemLabel(key);
                var marker    = fulfilled ? "[✓]" : "[ ]";
                var hue       = fulfilled ? 0x3DE : 999;

                AddLabel(24, y, hue, $"{marker} {label}");

                if (!fulfilled && pieceMatch[pi])
                {
                    AddButton(W - 96, y - 2, 4023, 4025, BtnTurnInLargePieceBase + pi, GumpButtonType.Reply, 0);
                    AddLabel(W - 60, y, 1154, "Turn In");
                }
                y += 18;
            }

            y += 4;
            var done  = lc.FulfilledPieces.Count;
            var total = _largePieceKeys.Length;
            AddLabel(18, y, 999,
                $"Progress: {done}/{total}  —  Reward: {lc.SealReward} Seals, +{lc.StandingReward} standing");
            y += 22;
        }

        // ── Request buttons ────────────────────────────────────────────────────
        AddImageTiled(10, y, W - 20, 2, 9304);
        y += 10;

        if (commissions.Count < SmithCommissionSystem.MaxActiveSmallCommissions)
        {
            AddButton(18, y, 4005, 4007, BtnReqComm, GumpButtonType.Reply, 0);
            AddLabel(54, y + 2, 999, "Request Small Commission");
        }
        else
            AddLabel(18, y, 37, "Small slots full — complete or abandon one first.");

        y += 26;

        if (largeComms.Count < SmithCommissionSystem.MaxActiveLargeCommissions)
        {
            AddButton(18, y, 4005, 4007, BtnReqLargeComm, GumpButtonType.Reply, 0);
            AddLabel(54, y + 2, 999, "Request Large Commission  (full armor set)");
        }
        else
            AddLabel(18, y, 37, "Large slot full — complete or abandon to request another.");
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm) return;
        if (pm.Account is not IAccount acct) return;
        var data = ClusterFAccountPersistence.GetOrCreate(acct);

        switch (info.ButtonID)
        {
            case BtnNavBODs:
                pm.SendGump(new SmithGuildBookGump(pm, _book, 0));
                return;

            case BtnNavComms:
                pm.SendGump(new SmithGuildBookGump(pm, _book, 1));
                return;

            case BtnCatalog:
                pm.SendGump(new SmithSealCatalogGump(pm));
                return;

            case BtnRequestSmallBOD:
                HandleRequestBOD(pm);
                return;

            case BtnRequestLargeBOD:
                HandleRequestLargeBOD(pm);
                return;

            case BtnReqComm:
                HandleReqComm(pm, data);
                return;

            case BtnReqLargeComm:
                HandleReqLargeComm(pm, data);
                return;

            case BtnAbandonLargeComm:
                HandleAbandonLargeComm(pm, data);
                return;
        }

        // BOD turn-in
        var bodIdx = info.ButtonID - BtnTurnInBODBase;
        if (bodIdx is >= 0 and <= 7 && bodIdx < _bodSerials.Count)
        {
            HandleTurnInBOD(pm, _bodSerials[bodIdx]);
            return;
        }

        // Small commission turn-in
        var commIdx = info.ButtonID - BtnTurnInCommBase;
        if (commIdx is >= 0 and <= 2 && commIdx < _commIds.Count)
        {
            HandleTurnInComm(pm, data, _commIds[commIdx]);
            return;
        }

        // Small commission abandon
        var abandonIdx = info.ButtonID - BtnAbandonCommBase;
        if (abandonIdx is >= 0 and <= 2 && abandonIdx < _commIds.Count)
        {
            HandleAbandonComm(pm, data, _commIds[abandonIdx]);
            return;
        }

        // Large commission piece turn-in
        var pieceIdx = info.ButtonID - BtnTurnInLargePieceBase;
        if (pieceIdx >= 0 && pieceIdx < _largePieceKeys.Length)
            HandleLargePieceTurnIn(pm, data, pieceIdx);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void HandleReqComm(PlayerMobile pm, ClusterFAccountData data)
    {
        if (data.SmithCommissions.Count >= SmithCommissionSystem.MaxActiveCommissions)
        {
            pm.SendMessage(0x22, "You already have the maximum active commissions.");
        }
        else
        {
            var entry = SmithCommissionSystem.Generate(pm);
            pm.SendMessage(entry != null ? 0x44 : 0x22,
                entry != null
                    ? $"New commission: {entry.FullLabel} for {entry.RequesterName}."
                    : "No commissions available right now.");
        }
        pm.SendGump(new SmithGuildBookGump(pm, _book, 1));
    }

    private void HandleTurnInBOD(PlayerMobile pm, Serial serial)
    {
        var bod = World.FindItem(serial);
        if (bod == null || bod.Deleted)
            pm.SendMessage(0x22, "That order no longer exists.");
        else if (!BlacksmithGuildmaster.TurnInBOD(pm, bod))
            pm.SendMessage(0x22, "That order is not yet complete.");

        pm.SendGump(new SmithGuildBookGump(pm, _book, 0));
    }

    private void HandleTurnInComm(PlayerMobile pm, ClusterFAccountData data, string commId)
    {
        var c = data.SmithCommissions.Find(x => x.Id == commId);
        if (c == null)
        {
            pm.SendMessage(0x22, "That commission no longer exists.");
            pm.SendGump(new SmithGuildBookGump(pm, _book, 1));
            return;
        }

        Item? match = null;
        if (pm.Backpack != null)
            foreach (var item in pm.Backpack.Items)
                if (SmithCommissionSystem.IsMatch(c, item)) { match = item; break; }

        if (match == null)
            pm.SendMessage(0x22, $"You don't have the required {c.FullLabel} in your backpack.");
        else
            SmithCommissionSystem.Complete(pm, c, match);

        pm.SendGump(new SmithGuildBookGump(pm, _book, 1));
    }

    private void HandleAbandonComm(PlayerMobile pm, ClusterFAccountData data, string commId)
    {
        var c = data.SmithCommissions.Find(x => x.Id == commId);
        if (c != null)
        {
            data.SmithCommissions.Remove(c);
            pm.SendMessage(0x59, $"Commission from {c.RequesterName} abandoned. Slot is now open.");
        }
        pm.SendGump(new SmithGuildBookGump(pm, _book, 1));
    }

    private void HandleRequestBOD(PlayerMobile pm)
    {
        var bod = BlacksmithGuildmaster.TryCreateBOD(pm);
        if (bod != null)
        {
            pm.AddToBackpack(bod);
            var desc = bod switch
            {
                SmallSmithBOD s => $"New order: {s.AmountMax}× {(s.RequireExceptional ? "exceptional " : "")}{s.Type?.Name ?? "item"}",
                LargeSmithBOD _ => "New large order received.",
                _               => "New order received.",
            };
            pm.SendMessage(0x44, desc);
        }
        pm.SendGump(new SmithGuildBookGump(pm, _book, 0));
    }

    private void HandleRequestLargeBOD(PlayerMobile pm)
    {
        var bod = BlacksmithGuildmaster.TryCreateLargeBOD(pm);
        if (bod != null)
        {
            pm.AddToBackpack(bod);
            pm.SendMessage(0x44, "New large bulk order received.");
        }
        pm.SendGump(new SmithGuildBookGump(pm, _book, 0));
    }

    private void HandleReqLargeComm(PlayerMobile pm, ClusterFAccountData data)
    {
        var entry = SmithCommissionSystem.GenerateLarge(pm);
        pm.SendMessage(entry != null ? 0x44 : 0x22,
            entry != null
                ? $"Large commission: {entry.FullLabel} for {entry.RequesterName}."
                : "No large commissions available right now.");
        pm.SendGump(new SmithGuildBookGump(pm, _book, 1));
    }

    private void HandleAbandonLargeComm(PlayerMobile pm, ClusterFAccountData data)
    {
        var c = data.SmithLargeCommissions.Count > 0 ? data.SmithLargeCommissions[0] : null;
        if (c != null)
        {
            data.SmithLargeCommissions.Remove(c);
            pm.SendMessage(0x59, $"Large commission from {c.RequesterName} abandoned.");
        }
        pm.SendGump(new SmithGuildBookGump(pm, _book, 1));
    }

    private void HandleLargePieceTurnIn(PlayerMobile pm, ClusterFAccountData data, int pieceIdx)
    {
        if (string.IsNullOrEmpty(_largeCommId) || pieceIdx >= _largePieceKeys.Length)
        {
            pm.SendGump(new SmithGuildBookGump(pm, _book, 1));
            return;
        }

        var c = data.SmithLargeCommissions.Find(x => x.Id == _largeCommId);
        if (c == null)
        {
            pm.SendMessage(0x22, "That commission no longer exists.");
            pm.SendGump(new SmithGuildBookGump(pm, _book, 1));
            return;
        }

        var pieceKey = _largePieceKeys[pieceIdx];

        Item? match = null;
        if (pm.Backpack != null)
            foreach (var item in pm.Backpack.Items)
                if (SmithCommissionSystem.IsMatchForPieceKey(c, pieceKey, item)) { match = item; break; }

        if (match == null)
        {
            pm.SendMessage(0x22, $"You don't have a matching {SmithCommissionPool.GetItemLabel(pieceKey)} in your backpack.");
            pm.SendGump(new SmithGuildBookGump(pm, _book, 1));
            return;
        }

        SmithCommissionSystem.TurnInLargePiece(pm, c, pieceKey, match);

        // If commission just completed, CompleteLarge already awarded rewards; reopen on comm tab.
        pm.SendGump(new SmithGuildBookGump(pm, _book, 1));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a PascalCase type name to spaced words.
    /// "PlateChest" → "Plate Chest", "VikingSword" → "Viking Sword".
    /// </summary>
    private static string TypeNameToWords(string name) =>
        Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");

    private static string BodMatName(BulkMaterialType mat) => mat switch
    {
        BulkMaterialType.DullCopper  => "Dull Copper",
        BulkMaterialType.ShadowIron  => "Shadow Iron",
        BulkMaterialType.Copper      => "Copper",
        BulkMaterialType.Bronze      => "Bronze",
        BulkMaterialType.Gold        => "Gold",
        BulkMaterialType.Agapite     => "Agapite",
        BulkMaterialType.Verite      => "Verite",
        BulkMaterialType.Valorite    => "Valorite",
        // post-Valorite metals
        BulkMaterialType.Platinum    => "Platinum",
        BulkMaterialType.Toxic       => "Toxic",
        BulkMaterialType.Blaze       => "Blaze",
        BulkMaterialType.Frost       => "Frost",
        BulkMaterialType.Obsidian    => "Obsidian",
        BulkMaterialType.Mythril     => "Mythril",
        BulkMaterialType.Adamantium  => "Adamantium",
        BulkMaterialType.Celestial   => "Celestial",
        _                            => "Iron",
    };
}
