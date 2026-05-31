using System.Collections.Generic;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

// ─────────────────────────────────────────────────────────────────────────────
// SmithCommissionGump — Phase 4C-ii
//
// Shows the player's active Smith Commissions (up to 3).
// Each card displays requester name + note, item requested, and reward.
// "Turn In" button appears when a matching item is found in the backpack.
// "Request New Commission" generates a new commission (while slots < 3).
// ─────────────────────────────────────────────────────────────────────────────

public class SmithCommissionGump : Gump
{
    private const int BtnRequestNew = 1;

    // Turn-in buttons: one per commission slot (0-indexed)
    // BtnTurnIn0 = 11, BtnTurnIn1 = 12, BtnTurnIn2 = 13
    private const int BtnTurnInBase = 11;

    // Abandon buttons: one per commission slot (0-indexed)
    // BtnAbandon0 = 21, BtnAbandon1 = 22, BtnAbandon2 = 23
    private const int BtnAbandonBase = 21;

    private const int W = 420;

    private readonly PlayerMobile           _pm;
    private readonly BlacksmithGuildmaster? _npc;
    private readonly List<string>           _commissionIds = new();

    public SmithCommissionGump(PlayerMobile pm, BlacksmithGuildmaster? npc = null)
        : base(90, 60)
    {
        _pm  = pm;
        _npc = npc;

        if (pm.Account is not IAccount acct) return;

        var data        = ClusterFAccountPersistence.GetOrCreate(acct);
        var commissions = data.SmithCommissions;

        // Pre-scan backpack for matching items per commission
        var backpackMatch = new Dictionary<string, bool>(); // commId → has match
        if (pm.Backpack != null)
        {
            foreach (var c in commissions)
            {
                foreach (var item in pm.Backpack.Items)
                {
                    if (SmithCommissionSystem.IsMatch(c, item))
                    {
                        backpackMatch[c.Id] = true;
                        break;
                    }
                }
            }
        }

        // Height: header (70) + per-card (110) + footer/button area (80)
        const int CardH    = 110;
        const int HeaderH  = 70;
        const int FooterH  = 80;
        var       H        = HeaderH + (commissions.Count > 0 ? commissions.Count * CardH : 30) + FooterH;
        if (H < 240) H = 240;

        Closable   = true;
        Disposable = true;
        Resizable  = false;

        AddPage(0);
        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(8, 8, W - 16, H - 16);

        // ── Header ────────────────────────────────────────────────────────────
        AddLabel(W / 2 - 70, 14, 1153, "Smith Commissions");
        AddImageTiled(10, 34, W - 20, 2, 9304);

        var countColor = commissions.Count >= SmithCommissionSystem.MaxActiveCommissions ? 33 : 999;
        AddLabel(18, 44, countColor,
            $"Active: {commissions.Count} / {SmithCommissionSystem.MaxActiveCommissions}");
        AddImageTiled(10, 62, W - 20, 2, 9304);

        var y = 72;

        // ── Commission cards ──────────────────────────────────────────────────

        if (commissions.Count == 0)
        {
            AddLabel(18, y, 37, "No active commissions.");
            y += 30;
        }
        else
        {
            for (var i = 0; i < commissions.Count; i++)
            {
                var c = commissions[i];
                _commissionIds.Add(c.Id);

                var hasMatch = backpackMatch.ContainsKey(c.Id);

                // Requester name + Abandon button (top-right of card)
                AddLabel(18, y, 1153, c.RequesterName);
                AddButton(W - 90, y - 2, 4005, 4007, BtnAbandonBase + i, GumpButtonType.Reply, 0);
                AddLabel(W - 54, y, 33, "Abandon");
                y += 20;

                // Requester note — HTML for natural wrapping
                AddHtml(18, y, W - 36, 34,
                    $"<BASEFONT COLOR=#AAAAAA>\"{c.RequesterNote}\"</BASEFONT>",
                    false, false);
                y += 36;

                // Item requested
                AddLabel(18, y, 68, $"Wants: {c.FullLabel}");
                y += 20;

                // Reward
                AddLabel(18, y, 999,
                    $"Reward: {c.SealReward} Seal{(c.SealReward == 1 ? "" : "s")}, " +
                    $"+{c.StandingReward} standing");

                // Turn In button aligned right on reward row
                if (hasMatch)
                {
                    var btnId = BtnTurnInBase + i;
                    AddButton(W - 114, y - 2, 4023, 4025, btnId, GumpButtonType.Reply, 0);
                    AddLabel(W - 78, y, 1154, "Turn In");
                }
                else
                {
                    AddLabel(W - 110, y, 37, "Not in pack");
                }

                y += 22;

                // Divider between cards (not after the last one)
                if (i < commissions.Count - 1)
                {
                    AddImageTiled(10, y + 2, W - 20, 2, 9304);
                    y += 12;
                }
                else
                {
                    y += 4;
                }
            }
        }

        // ── Footer area ───────────────────────────────────────────────────────
        AddImageTiled(10, y, W - 20, 2, 9304);
        y += 10;

        if (commissions.Count < SmithCommissionSystem.MaxActiveCommissions)
        {
            AddButton(18, y, 4005, 4007, BtnRequestNew, GumpButtonType.Reply, 0);
            AddLabel(54, y + 2, 999, "Request New Commission");
        }
        else
        {
            AddLabel(18, y, 37, "Commission slots full — complete an order to open a slot.");
        }

        y += 28;
        AddHtml(18, y, W - 36, 38,
            "<BASEFONT COLOR=#666666>Craft the requested item and drag it to the Guildmaster, " +
            "or use Turn In when it is in your backpack.</BASEFONT>",
            false, false);
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm) return;
        if (pm.Account is not IAccount acct) return;

        var data = ClusterFAccountPersistence.GetOrCreate(acct);

        if (info.ButtonID == BtnRequestNew)
        {
            HandleRequestNew(pm, data);
            return;
        }

        // Turn-in buttons
        var turnIdx = info.ButtonID - BtnTurnInBase;
        if (turnIdx >= 0 && turnIdx <= 2 && turnIdx < _commissionIds.Count)
        {
            HandleTurnIn(pm, data, _commissionIds[turnIdx]);
            return;
        }

        // Abandon buttons
        var abandonIdx = info.ButtonID - BtnAbandonBase;
        if (abandonIdx >= 0 && abandonIdx <= 2 && abandonIdx < _commissionIds.Count)
        {
            HandleAbandon(pm, data, _commissionIds[abandonIdx]);
        }
    }

    private void HandleRequestNew(PlayerMobile pm, ClusterFAccountData data)
    {
        if (data.SmithCommissions.Count >= SmithCommissionSystem.MaxActiveCommissions)
        {
            pm.SendMessage(0x22,
                "You already have the maximum number of active commissions.");
        }
        else
        {
            var entry = SmithCommissionSystem.Generate(pm);
            if (entry != null)
                pm.SendMessage(0x44,
                    $"New commission: {entry.FullLabel} for {entry.RequesterName}.");
            else
                pm.SendMessage(0x22, "No commissions are available right now.");
        }

        pm.SendGump(new SmithCommissionGump(pm, _npc));
    }

    private void HandleTurnIn(PlayerMobile pm, ClusterFAccountData data, string commissionId)
    {
        var commission = data.SmithCommissions.Find(c => c.Id == commissionId);

        if (commission == null)
        {
            pm.SendMessage(0x22, "That commission no longer exists.");
            pm.SendGump(new SmithCommissionGump(pm, _npc));
            return;
        }

        if (pm.Backpack == null)
        {
            pm.SendMessage(0x22, "You have no backpack.");
            pm.SendGump(new SmithCommissionGump(pm, _npc));
            return;
        }

        Item? match = null;
        foreach (var item in pm.Backpack.Items)
        {
            if (SmithCommissionSystem.IsMatch(commission, item))
            {
                match = item;
                break;
            }
        }

        if (match == null)
        {
            pm.SendMessage(0x22,
                $"You don't have a matching {commission.FullLabel} in your backpack.");
            pm.SendGump(new SmithCommissionGump(pm, _npc));
            return;
        }

        SmithCommissionSystem.Complete(pm, commission, match);
        // Complete sends its own message + sound — reopen the gump.
        pm.SendGump(new SmithCommissionGump(pm, _npc));
    }

    private void HandleAbandon(PlayerMobile pm, ClusterFAccountData data, string commissionId)
    {
        var commission = data.SmithCommissions.Find(c => c.Id == commissionId);

        if (commission == null)
        {
            pm.SendMessage(0x22, "That commission no longer exists.");
        }
        else
        {
            data.SmithCommissions.Remove(commission);
            pm.SendMessage(0x59,
                $"Commission from {commission.RequesterName} abandoned. The slot is now open.");
        }

        pm.SendGump(new SmithCommissionGump(pm, _npc));
    }
}
