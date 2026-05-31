using System;
using System.Collections.Generic;
using System.Linq;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// Guild Contract Ledger — opened from a guild liaison's Member Dashboard.
///
/// Three tabs:
///   Available — orders the player is eligible for and can accept
///   Active    — accepted orders with live pack-count progress and Turn In
///   History   — recently completed orders (last 20)
///
/// Scroll arrows (▲/▼) appear on the right edge whenever there are more items
/// than fit in the visible window.  Button IDs 20/21 carry the scroll action;
/// accept/turn-in IDs encode the absolute list index so they survive scrolling.
///
/// Instantiate with a guildKey (e.g. "mining") to scope the display to one guild.
/// The same gump is reused by all future guilds — only the guildKey changes.
/// </summary>
public class GuildContractLedgerGump : Gump
{
    public enum Tab { Available, Active, History }

    private readonly PlayerMobile _pm;
    private readonly string       _guildKey;
    private readonly Tab          _tab;
    private readonly int          _offset;

    private const int W        = 540;
    private const int H        = 490;
    private const int BgId     = 9270;
    private const int ContentY = 86;    // top of the order card area
    private const int FooterY  = H - 42;
    private const int CardH    = 88;    // height per order card
    private const int MaxCards = 4;     // visible cards at one time
    private const int ScrollX  = W - 22; // right-edge x for scroll buttons

    public GuildContractLedgerGump(PlayerMobile pm, string guildKey, Tab tab = Tab.Available, int offset = 0)
        : base(80, 60)
    {
        _pm       = pm;
        _guildKey = guildKey;
        _tab      = tab;
        _offset   = offset;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        // ── Header ────────────────────────────────────────────────────────
        AddLabel(W / 2 - 80, 12, 1154, "Guild Contract Ledger");
        AddLabel(W / 2 - 60, 28, 999,  GuildDisplayName(guildKey));
        AddImageTiled(10, 48, W - 20, 2, 9304);

        // ── Tab bar ───────────────────────────────────────────────────────
        DrawTabButton(18,  56, 10, Tab.Available, "Available");
        DrawTabButton(165, 56, 11, Tab.Active,    "Active");
        DrawTabButton(290, 56, 12, Tab.History,   "History");
        AddImageTiled(10, 78, W - 20, 2, 9304);

        // ── Content ───────────────────────────────────────────────────────
        var acct = pm.Account as IAccount;
        var data = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : new ClusterFAccountData();

        switch (tab)
        {
            case Tab.Available: DrawAvailable(data); break;
            case Tab.Active:    DrawActive(data);    break;
            case Tab.History:   DrawHistory(data);   break;
        }

        // ── Footer ────────────────────────────────────────────────────────
        AddImageTiled(10, FooterY, W - 20, 2, 9304);
        AddButton(18,      H - 32, 4014, 4016, 1);   // Back
        AddLabel(44,       H - 30, 999,   "Back");
        AddButton(W - 50,  H - 32, 4023, 4025, 0);   // Close (X)
        AddLabel(W - 28,   H - 30, 1154,  "X");
    }

    private void DrawTabButton(int x, int y, int btnId, Tab forTab, string label)
    {
        var color = _tab == forTab ? 1154 : 999;
        AddButton(x, y, 4011, 4012, btnId);
        AddLabel(x + 24, y + 2, color, label);
    }

    // ── Scroll helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Draws ▲/▼ arrow buttons on the right edge of the content area.
    /// Only the buttons that are actually usable are shown.
    /// Returns the clamped, valid offset for the given total.
    /// </summary>
    private int AddScrollButtons(int offset, int total)
    {
        // Clamp offset so it never goes past the last page
        offset = Math.Max(0, Math.Min(offset, Math.Max(0, total - MaxCards)));

        if (total <= MaxCards) return offset; // nothing to scroll

        if (offset > 0)
            AddButton(ScrollX, ContentY + 4, 0x15E3, 0x15E7, 20); // ▲ up

        if (offset + MaxCards < total)
            AddButton(ScrollX, FooterY - 22, 0x15E5, 0x15E9, 21); // ▼ down

        // Small position label, e.g. "2/5"
        var labelY = ContentY + (FooterY - ContentY) / 2 - 8;
        AddLabel(ScrollX - 2, labelY, 999, $"{Math.Min(offset + MaxCards, total)}");
        AddLabel(ScrollX - 2, labelY + 14, 999, $"{total}");

        return offset;
    }

    // ── Available tab ─────────────────────────────────────────────────────────

    private void DrawAvailable(ClusterFAccountData data)
    {
        var allForGuild = ClusterFWorkOrderSystem.GetForGuild(_guildKey);

        var orders = allForGuild
            .Where(d => d.IsEligible(_pm, data))
            .ToList();

        // Discovery-pending: standing + skill met, ore found but not yet reported to Velara
        var pendingDiscovery = allForGuild
            .Where(d => !d.IsEligible(_pm, data) && IsDiscoveryPending(d, data))
            .ToList();

        if (orders.Count == 0 && pendingDiscovery.Count == 0)
        {
            AddLabel(18, ContentY + 12, 999, "No work orders available at your current rank.");
            AddLabel(18, ContentY + 30, 999, "Increase your Compact Standing to unlock additional contracts.");
            return;
        }

        var guildActiveCount = data.ActiveWorkOrders.Count(e => e.GuildKey.Equals(_guildKey, StringComparison.OrdinalIgnoreCase));
        var atMax  = guildActiveCount >= ClusterFWorkOrderSystem.MaxActiveOrdersPerGuild;
        var offset = AddScrollButtons(_offset, orders.Count + pendingDiscovery.Count);

        // Unified scrollable list: eligible orders first, then discovery-pending
        var allItems = orders
            .Select(d => (Def: d, IsPending: false))
            .Concat(pendingDiscovery.Select(d => (Def: d, IsPending: true)))
            .ToList();

        var visible = allItems.Skip(offset).Take(MaxCards).ToList();
        var y       = ContentY;

        for (var i = 0; i < visible.Count; i++, y += CardH)
        {
            var (def, isPending) = visible[i];
            var absIdx = offset + i; // absolute index into allItems / orders

            if (i > 0) AddImageTiled(10, y, W - 36, 1, 9304);
            var cy = i > 0 ? y + 6 : y;

            if (isPending)
            {
                // ── Discovery-pending entry (locked) ──────────────────────
                AddLabel(18,      cy,     999, def.Title + "  [Locked]");
                AddLabel(W - 175, cy,     999, def.TypeLabel);

                var reqLine = string.Join("  +  ", def.Requirements.Select(r => $"{r.Amount} {r.Label}"));
                AddLabel(18, cy + 16, 999, reqLine);

                var parts = new List<string>();
                if (def.StandingReward > 0) parts.Add($"+{def.StandingReward} Standing");
                if (def.VoucherReward  > 0) parts.Add($"+{def.VoucherReward} Vouchers");
                if (def.GoldReward     > 0) parts.Add($"+{def.GoldReward:N0}gp");
                AddLabel(18, cy + 32, 0x44, "Reward: " + string.Join(", ", parts));

                AddLabel(18, cy + 48, 0x22,
                    $"Report your {def.RequiredDiscovery} discovery to Velara Thorne at the south mine to unlock.");
            }
            else
            {
                // ── Eligible entry ────────────────────────────────────────
                var hasThis = ClusterFWorkOrderSystem.HasActive(data, def.Key);

                AddLabel(18,      cy,     1154, def.Title);
                AddLabel(W - 175, cy,     999,  def.TypeLabel);

                var reqLine = string.Join("  +  ", def.Requirements.Select(r => $"{r.Amount} {r.Label}"));
                AddLabel(18, cy + 16, 999, reqLine);

                var parts = new List<string>();
                if (def.StandingReward > 0) parts.Add($"+{def.StandingReward} Standing");
                if (def.VoucherReward  > 0) parts.Add($"+{def.VoucherReward} Vouchers");
                if (def.GoldReward     > 0) parts.Add($"+{def.GoldReward:N0}gp");
                AddLabel(18, cy + 32, 0x44, "Reward: " + string.Join(", ", parts));

                if (def.MinStanding > 0 || def.SkillRequired.HasValue)
                {
                    var notes = new List<string>();
                    if (def.MinStanding    > 0)    notes.Add($"{def.MinStanding:N0} Standing");
                    if (def.SkillRequired.HasValue) notes.Add($"{def.MinSkill:F0} {def.SkillRequired}");
                    AddLabel(18, cy + 48, 999, "Requires: " + string.Join(", ", notes));
                }

                if (hasThis)
                    AddLabel(18, cy + 66, 0x22, "Already active.");
                else if (atMax)
                    AddLabel(18, cy + 66, 0x22, $"Guild limit reached ({ClusterFWorkOrderSystem.MaxActiveOrdersPerGuild} active per guild).");
                else
                {
                    // Button ID encodes the absolute orders-list index (not allItems index)
                    AddButton(18, cy + 64, 4011, 4012, 100 + absIdx);
                    AddLabel(44, cy + 66, 999, "Accept");
                }
            }
        }
    }

    /// <summary>
    /// Returns true if the order is locked only by discovery state (ore has been found but not
    /// yet reported to Velara Thorne).  Standing and skill requirements must already be met.
    /// </summary>
    private bool IsDiscoveryPending(WorkOrderDef def, ClusterFAccountData data)
    {
        if (def.RequiredDiscovery == null) return false;
        if (data.GetReputation(def.GuildKey) < def.MinStanding) return false;
        if (def.SkillRequired.HasValue && _pm.Skills[def.SkillRequired.Value].Value < def.MinSkill) return false;

        // Ore must be discovered (entry exists) but not yet reported
        return data.OreDiscoveries.TryGetValue(def.RequiredDiscovery, out var entry)
            && entry.State == DiscoveryState.Discovered;
    }

    // ── Active tab ────────────────────────────────────────────────────────────

    private void DrawActive(ClusterFAccountData data)
    {
        var entries = data.ActiveWorkOrders
            .Where(e => e.GuildKey.Equals(_guildKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (entries.Count == 0)
        {
            AddLabel(18, ContentY + 12, 999, "No active work orders for this guild.");
            AddLabel(18, ContentY + 30, 999, "Visit the Available tab to accept a contract.");
            return;
        }

        var bypass = Items.DevTestingCrystal.IsActive(_pm);
        var offset = AddScrollButtons(_offset, entries.Count);
        var visible = entries.Skip(offset).Take(MaxCards).ToList();
        var y       = ContentY;

        for (var i = 0; i < visible.Count; i++, y += CardH)
        {
            var entry  = visible[i];
            var absIdx = offset + i;
            var def    = ClusterFWorkOrderSystem.Get(entry.DefKey);

            if (i > 0) AddImageTiled(10, y, W - 36, 1, 9304);
            var cy = i > 0 ? y + 6 : y;

            if (def == null)
            {
                AddLabel(18, cy, 0x22, $"Unknown order: {entry.DefKey}");
                continue;
            }

            // Title
            AddLabel(18, cy, 1154, def.Title);

            // Requirements — taming contracts show delivery progress, others show pack count
            var reqY   = cy + 16;
            var allMet = bypass;

            if (!bypass)
            {
                allMet = def.RequirementsMet(_pm, entry);
            }

            foreach (var req in def.Requirements)
            {
                int have, total;
                string label;

                if (req is WorkOrderTamingRequirement)
                {
                    entry.TamingProgress.TryGetValue(req.ItemType.Name, out have);
                    total  = req.Amount;
                    label  = $"• Deliver {req.Label}: {(bypass ? total : have)}/{total}";
                }
                else
                {
                    have  = _pm.Backpack?.GetAmount(req.ItemType) ?? 0;
                    total = req.Amount;
                    label = $"• {req.Label}: {(bypass ? total : have)}/{total}";
                }

                var met = bypass || have >= total;
                AddLabel(18, reqY, met ? 0x44 : 0x22, label);
                reqY += 16;
            }

            // Turn in / deliver more — button ID encodes absolute active-list index
            if (allMet)
            {
                AddButton(18, reqY + 2, 4011, 4012, 200 + absIdx);
                AddLabel(44, reqY + 4, 999, "Turn In");
            }
            else if (def.Type == WorkOrderType.TamingContract)
            {
                AddLabel(18, reqY + 4, 0x22, "Deliver remaining animals using your Outrider's Crook.");
            }
            else
            {
                AddLabel(18, reqY + 4, 0x22, "Gather the remaining materials to turn in.");
            }
        }
    }

    // ── History tab ───────────────────────────────────────────────────────────

    private void DrawHistory(ClusterFAccountData data)
    {
        var entries = data.CompletedWorkOrders
            .Where(e => e.GuildKey.Equals(_guildKey, StringComparison.OrdinalIgnoreCase))
            .Reverse()          // most recent first
            .ToList();

        if (entries.Count == 0)
        {
            AddLabel(18, ContentY + 12, 999, "No completed work orders yet.");
            return;
        }

        // History rows are compact (22px each) — fit more per page
        const int historyRowH  = 22;
        const int historyMax   = 14; // rows visible in the content area
        var offset = Math.Max(0, Math.Min(_offset, Math.Max(0, entries.Count - historyMax)));

        if (entries.Count > historyMax)
        {
            // Draw scroll arrows using the same button IDs
            if (offset > 0)
                AddButton(ScrollX, ContentY + 4, 0x15E3, 0x15E7, 20);
            if (offset + historyMax < entries.Count)
                AddButton(ScrollX, FooterY - 22, 0x15E5, 0x15E9, 21);

            var labelY = ContentY + (FooterY - ContentY) / 2 - 8;
            AddLabel(ScrollX - 2, labelY,      999, $"{Math.Min(offset + historyMax, entries.Count)}");
            AddLabel(ScrollX - 2, labelY + 14, 999, $"{entries.Count}");
        }

        AddLabel(18, ContentY, 999, "Recent completions (most recent first):");
        AddImageTiled(10, ContentY + 16, W - 36, 1, 9304);

        var y       = ContentY + 24;
        var visible = entries.Skip(offset).Take(historyMax);
        foreach (var entry in visible)
        {
            var def       = ClusterFWorkOrderSystem.Get(entry.DefKey);
            var title     = def?.Title ?? entry.DefKey;
            var completed = entry.CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? entry.AcceptedAt.ToString("yyyy-MM-dd");

            AddLabel(18,      y, 999,  title);
            AddLabel(W - 185, y, 0x44, completed);
            y += historyRowH;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SendBackGump()
    {
        switch (_guildKey.ToLowerInvariant())
        {
            case "mining":
                _pm.SendGump(new MinersCompactLiaisonGump(_pm, MinersCompactLiaisonGump.View.MemberDashboard));
                break;
            // Future guilds: add cases here (e.g. "smithing" → SmithsBrotherhoodLiaisonGump)
        }
    }

    private static string GuildDisplayName(string guildKey) => guildKey.ToLowerInvariant() switch
    {
        "mining"   => "Miners' Compact",
        "smithing" => "Smiths' Brotherhood",
        "rangers"  => "Rangers' League",
        "healers"  => "Healers' Circle",
        _          => guildKey
    };

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return;                          // close
        if (info.ButtonID == 1) { SendBackGump(); return; }     // back

        // Tab switching (resets scroll offset)
        if (info.ButtonID == 10) { _pm.SendGump(new GuildContractLedgerGump(_pm, _guildKey, Tab.Available)); return; }
        if (info.ButtonID == 11) { _pm.SendGump(new GuildContractLedgerGump(_pm, _guildKey, Tab.Active));    return; }
        if (info.ButtonID == 12) { _pm.SendGump(new GuildContractLedgerGump(_pm, _guildKey, Tab.History));   return; }

        // Scroll up / down (preserves current tab)
        if (info.ButtonID == 20) { _pm.SendGump(new GuildContractLedgerGump(_pm, _guildKey, _tab, Math.Max(0, _offset - 1))); return; }
        if (info.ButtonID == 21) { _pm.SendGump(new GuildContractLedgerGump(_pm, _guildKey, _tab, _offset + 1));              return; }

        var acct = _pm.Account as IAccount;
        var data = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : null;
        if (data == null) return;

        // Accept: 100 + absolute index into the available+pending list
        if (info.ButtonID is >= 100 and < 200)
        {
            HandleAccept(data, info.ButtonID - 100);
            _pm.SendGump(new GuildContractLedgerGump(_pm, _guildKey, Tab.Available));
            return;
        }

        // Turn in: 200 + absolute index into the active-for-this-guild list
        if (info.ButtonID is >= 200 and < 300)
        {
            HandleTurnIn(data, info.ButtonID - 200);
            _pm.SendGump(new GuildContractLedgerGump(_pm, _guildKey, Tab.Active));
        }
    }

    private void HandleAccept(ClusterFAccountData data, int idx)
    {
        if (!data.JoinedGuilds.Contains(_guildKey))
        {
            _pm.SendMessage(0x22, "You must be a member of the guild to accept work orders.");
            return;
        }

        var allForGuild = ClusterFWorkOrderSystem.GetForGuild(_guildKey);
        var orders = allForGuild
            .Where(d => d.IsEligible(_pm, data))
            .ToList();
        var pendingDiscovery = allForGuild
            .Where(d => !d.IsEligible(_pm, data) && IsDiscoveryPending(d, data))
            .ToList();

        // idx is absolute into the unified eligible+pending list; eligible orders come first
        var allItems = orders.Concat(pendingDiscovery).ToList();

        if (idx < 0 || idx >= allItems.Count)
        {
            _pm.SendMessage(0x22, "That order is no longer available.");
            return;
        }

        var def = allItems[idx];

        // Guard: pending-discovery orders are not actually eligible
        if (!def.IsEligible(_pm, data))
        {
            _pm.SendMessage(0x22, "You must report your ore discovery to Velara Thorne before accepting this order.");
            return;
        }

        if (ClusterFWorkOrderSystem.HasActive(data, def.Key))
        {
            _pm.SendMessage(0x22, "You already have this order active.");
            return;
        }

        var guildCount = data.ActiveWorkOrders.Count(e => e.GuildKey.Equals(def.GuildKey, StringComparison.OrdinalIgnoreCase));
        if (guildCount >= ClusterFWorkOrderSystem.MaxActiveOrdersPerGuild)
        {
            _pm.SendMessage(0x22, $"You can only hold {ClusterFWorkOrderSystem.MaxActiveOrdersPerGuild} active work orders per guild at a time. Complete one first.");
            return;
        }

        data.ActiveWorkOrders.Add(new WorkOrderEntry(def.Key, def.GuildKey));
        _pm.SendMessage(0x44, $"Accepted: {def.Title}. Gather the required materials and return to turn it in.");
    }

    private void HandleTurnIn(ClusterFAccountData data, int idx)
    {
        var entries = data.ActiveWorkOrders
            .Where(e => e.GuildKey.Equals(_guildKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (idx < 0 || idx >= entries.Count)
        {
            _pm.SendMessage(0x22, "That order is no longer active.");
            return;
        }

        var entry = entries[idx];
        var def   = ClusterFWorkOrderSystem.Get(entry.DefKey);

        if (def == null)
        {
            _pm.SendMessage(0x22, "Order definition not found — please report this to an admin.");
            return;
        }

        if (!def.RequirementsMet(_pm, entry))
        {
            _pm.SendMessage(0x22,
                def.Type == WorkOrderType.TamingContract
                    ? "You have not yet delivered all required animals."
                    : "You don't have all required materials in your backpack.");
            return;
        }

        // Consume materials and apply rewards
        def.ConsumeRequirements(_pm);
        data.AddReputation(_guildKey, def.StandingReward);
        data.AddCurrency(_guildKey,   def.VoucherReward);

        if (def.GoldReward > 0)
            _pm.Backpack?.DropItem(new Gold(def.GoldReward));

        // Move to history
        entry.CompletedAt = DateTime.UtcNow;
        data.ActiveWorkOrders.Remove(entry);
        data.CompletedWorkOrders.Add(entry);
        while (data.CompletedWorkOrders.Count > ClusterFWorkOrderSystem.MaxHistoryEntries)
            data.CompletedWorkOrders.RemoveAt(0);

        // Confirmation message
        var reward       = new List<string>();
        var currencyName = _guildKey switch
        {
            "smithing"  => "Smithing Seals",
            "rangers"   => "Trail Marks",
            "foresters" => "Timber Tokens",
            _           => "Vouchers"
        };
        if (def.StandingReward > 0) reward.Add($"+{def.StandingReward} Standing");
        if (def.VoucherReward  > 0) reward.Add($"+{def.VoucherReward} {currencyName}");
        if (def.GoldReward     > 0) reward.Add($"+{def.GoldReward:N0}gp");

        _pm.SendMessage(0x44, $"Work order complete: {def.Title}. {string.Join(", ", reward)}.");
        _pm.PlaySound(0x57);
    }
}
