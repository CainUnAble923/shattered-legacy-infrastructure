// cc-P280: the League Register's window. P177's template is not approved yet (P178 is held for it), so this uses the
// League Shop's frame and cc-P169's requirement colors, and every page is one method, ready to move onto the template.
//
// Pages: Guilds (the bound guilds: status, contracts, unbind; bind the guilds you belong to), Ledger (C1: every guild you
// are in, rank, standing, currency and the next rank), League (the rank-gated League actions), Bank (F-34's ledger as a
// page: what your bank holds, by kind), Goods (the backpack upgrades, the Field Satchel, the Combined Logbook).
// Every button re-checks on the server: a page drawn before a rank or membership changed acts on the state now.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.LeagueRegister;

/// <summary>F-34's ledger as the Register's Bank page: what the bank box holds, counted by kind (withdraw-only reach is F-34's own build).</summary>
public static class LeagueBank
{
    public static readonly (string Label, Func<Item, bool> Is)[] Kinds =
    [
        ("Gold", i => i is Gold),
        ("Ingots", i => i is BaseIngot),
        ("Ore", i => i is BaseOre),
        ("Logs", i => i is Log),
        ("Boards", i => i is Board),
        ("Leather", i => i is BaseLeather),
        ("Hides", i => i is BaseHides),
        ("Cloth", i => i is Cloth or UncutCloth or BoltOfCloth),
        ("Reagents", i => i is BaseReagent)
    ];

    /// <summary>Each kind's total in the bank (stacks counted by amount, bank checks as gold), the kinds there are only.</summary>
    public static List<(string Label, int Amount)> Summary(Mobile m)
    {
        var totals = new int[Kinds.Length];
        if (m.FindItemOnLayer(Layer.Bank) is Container bank)
        {
            foreach (var item in bank.FindItemsByType<Item>())
            {
                if (item is BankCheck check)
                {
                    totals[0] += check.Worth;
                    continue;
                }

                for (var k = 0; k < Kinds.Length; k++)
                {
                    if (Kinds[k].Is(item))
                    {
                        totals[k] += item.Amount;
                        break;
                    }
                }
            }
        }

        var result = new List<(string, int)>();
        for (var k = 0; k < Kinds.Length; k++)
        {
            if (totals[k] > 0)
            {
                result.Add((Kinds[k].Label, totals[k]));
            }
        }

        return result;
    }
}

public class LeagueRegisterGump : Gump
{
    public enum Page { Guilds, Ledger, League, Bank, Goods }

    private const int W = 520;
    private const int H = 460;

    private const int BtnPageBase = 1;       // 1..5: the pages
    private const int BtnBindBase = 100;     // + guild index
    private const int BtnUnbindBase = 200;
    private const int BtnContractsBase = 300;
    public const int BtnTurnIn = 10;
    public const int BtnRepair = 11;
    public const int BtnBackpack = 12;
    public const int BtnSatchel = 13;
    public const int BtnLogbook = 14;

    private readonly PlayerMobile _pm;
    private readonly Page _page;
    private readonly List<string> _keys = new();

    public static void Open(PlayerMobile pm, Page page)
    {
        pm.CloseGump<LeagueRegisterGump>();
        pm.SendGump(new LeagueRegisterGump(pm, page));
    }

    public LeagueRegisterGump(PlayerMobile pm, Page page) : base(80, 60)
    {
        _pm = pm;
        _page = page;

        // Answer 9: guilds left since the last open are shown greyed this once, and dropped now.
        var left = LeagueRegisterSystem.DropLeft(pm);
        var rank = LeagueRegisterSystem.Rank(pm);
        var data = pm.Account is IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct) : new ClusterFAccountData();

        AddBackground(0, 0, W, H, 9270);
        RequirementStyle.AddSolidPanel(this, W, H);
        AddLabel(20, 14, RequirementStyle.HeadingHue, "League Register");
        AddLabel(200, 14, ClusterFLeagueRanks.LabelHue(rank), ClusterFLeagueRanks.RankName(rank));
        AddLabel(20, 32, 999,
            $"Renown {data.Renown:N0}    Guilds bound {LeagueRegisterSystem.UsedSlots(pm)} / {LeagueRegisterSystem.Capacity(pm)}");
        AddImageTiled(10, 54, W - 20, 1, 9304);

        var tabs = new[] { "Guilds", "Ledger", "League", "Bank", "Goods" };
        for (var i = 0; i < tabs.Length; i++)
        {
            AddButton(20, 66 + i * 26, 4005, 4007, BtnPageBase + i);
            AddLabel(54, 68 + i * 26, (int)page == i ? RequirementStyle.HeadingHue : 999, tabs[i]);
        }

        var x = 150;
        switch (page)
        {
            case Page.Guilds: DrawGuilds(x, left); break;
            case Page.Ledger: DrawLedger(x); break;
            case Page.League: DrawLeague(x, rank); break;
            case Page.Bank:   DrawBank(x); break;
            default:          DrawGoods(x, rank); break;
        }

        AddButton(W - 50, H - 34, 4017, 4019, 0);
        AddLabel(W - 90, H - 32, 999, "Close");
    }

    private int KeyIndex(string key)
    {
        var i = _keys.IndexOf(key);
        if (i < 0)
        {
            _keys.Add(key);
            i = _keys.Count - 1;
        }

        return i;
    }

    private void DrawGuilds(int x, List<string> left)
    {
        var y = 66;
        var bound = LeagueRegisterSystem.Bound(_pm).ToList();
        var canContracts = LeagueRegisterSystem.RefuseAction(_pm, LeagueAction.Contracts) == null;

        AddLabel(x, y, RequirementStyle.HeadingHue, "Bound guilds"); y += 22;

        foreach (var key in left)
        {
            AddLabel(x, y, 0x3B2, $"{Name(key)} - you are no longer a member (dropped)"); y += 20;
        }

        foreach (var key in bound)
        {
            var i = KeyIndex(key);
            AddLabel(x, y, 999, $"{Name(key)} - {Standing(key)}");
            if (canContracts)
            {
                AddButton(x + 240, y, 4011, 4012, BtnContractsBase + i);
                AddLabel(x + 272, y + 2, 999, "Contracts");
            }

            AddButton(x + 340, y, 4017, 4019, BtnUnbindBase + i);
            AddLabel(x + 372, y + 2, 999, "Unbind");
            y += 22;
        }

        if (bound.Count == 0 && left.Count == 0)
        {
            AddLabel(x, y, 0x3B2, "Nothing bound yet."); y += 20;
        }

        y += 8;
        AddLabel(x, y, RequirementStyle.HeadingHue, "Bind a guild you belong to"); y += 22;

        foreach (var def in Joined().Where(d => !bound.Contains(d.Key)))
        {
            var i = KeyIndex(def.Key);
            var refusal = LeagueRegisterSystem.RefuseBind(_pm, def.Key);
            var book = LeagueRegisterSystem.BookTypeFor(def.Key) != null ? " (takes its book)" : "";
            if (refusal == null)
            {
                AddButton(x, y, 4005, 4007, BtnBindBase + i);
            }

            AddLabel(x + 34, y + 2, refusal == null ? 999 : 0x3B2, $"{def.Name}{book}");
            y += 22;
            if (y > H - 60)
            {
                break;
            }
        }
    }

    private void DrawLedger(int x)
    {
        var y = 66;
        AddLabel(x, y, RequirementStyle.HeadingHue, "Guild Ledger"); y += 22;
        foreach (var def in Joined())
        {
            AddLabel(x, y, 999, $"{def.Name}: {Standing(def.Key)}"); y += 20;
            if (y > H - 60)
            {
                break;
            }
        }
    }

    private void DrawLeague(int x, int rank)
    {
        var y = 66;
        AddLabel(x, y, RequirementStyle.HeadingHue, "League actions"); y += 22;
        foreach (var action in Enum.GetValues<LeagueAction>())
        {
            var need = LeagueRegisterSystem.ActionRank(action);
            var open = rank >= need;
            var button = action switch { LeagueAction.TurnIn => BtnTurnIn, LeagueAction.Repair => BtnRepair, _ => 0 };
            if (open && button != 0)
            {
                AddButton(x, y, 4005, 4007, button);
            }

            AddLabel(x + 34, y + 2, open ? 999 : RequirementStyle.NotMetHue,
                $"{LeagueRegisterSystem.ActionName(action)} ({ClusterFLeagueRanks.RankName(need)})");
            y += 24;
        }

        AddLabel(x, y + 6, 0x3B2, "Mythril and above: reserved.");
    }

    private void DrawBank(int x)
    {
        var y = 66;
        AddLabel(x, y, RequirementStyle.HeadingHue, "Bank"); y += 22;
        var summary = LeagueBank.Summary(_pm);
        if (summary.Count == 0)
        {
            AddLabel(x, y, 0x3B2, "Your bank holds no gold or crafting materials."); y += 20;
        }

        foreach (var (label, amount) in summary)
        {
            AddLabel(x, y, 999, $"{label}: {amount:N0}"); y += 20;
        }

        AddLabel(x, y + 10, 0x3B2, "Crafting from the bank comes with each guild's upgrade.");
    }

    private void DrawGoods(int x, int rank)
    {
        var y = 66;
        AddLabel(x, y, RequirementStyle.HeadingHue, "League goods"); y += 22;

        if (LeagueBackpack.Next(_pm) is { } tier)
        {
            Row(ref y, x, BtnBackpack, LeagueBackpack.Refuse(_pm, out _),
                $"Backpack to {tier.Items} items: {tier.Renown:N0} Renown, {tier.Gold:N0} gold ({ClusterFLeagueRanks.RankName(tier.Rank)})");
        }
        else
        {
            AddLabel(x + 34, y + 2, 0x3B2, "Backpack: fully upgraded."); y += 24;
        }

        Row(ref y, x, BtnSatchel, LeagueConsolidation.RefuseSatchel(_pm),
            $"League Field Satchel: {LeagueRegisterSettings.SatchelRenown:N0} Renown, {LeagueRegisterSettings.SatchelGold:N0} gold " +
            $"({ClusterFLeagueRanks.RankName(LeagueRegisterSettings.SatchelRank)})");
        Row(ref y, x, BtnLogbook, LeagueConsolidation.RefuseLogbook(_pm),
            $"Combined Logbook: {LeagueRegisterSettings.LogbookRenown:N0} Renown, {LeagueRegisterSettings.LogbookGold:N0} gold " +
            $"({ClusterFLeagueRanks.RankName(LeagueRegisterSettings.LogbookRank)})");
    }

    private void Row(ref int y, int x, int button, string refusal, string text)
    {
        if (refusal == null)
        {
            AddButton(x, y, 4005, 4007, button);
        }

        AddLabel(x + 34, y + 2, refusal == null ? 999 : RequirementStyle.NotMetHue, text);
        y += 24;
    }

    private IEnumerable<GuildDef> Joined()
    {
        ClusterFGuildSystem.EnsureRegistered();
        foreach (var def in ClusterFGuildSystem.AllGuilds.Values)
        {
            if (ClusterFGuildSystem.IsJoined(_pm, def.Key))
            {
                yield return def;
            }
        }
    }

    private static string Name(string key) => ClusterFGuildSystem.GetDef(key)?.Name ?? key;

    private string Standing(string key)
    {
        var guild = ClusterFAccountPersistence.GetGuild(_pm);
        var standing = guild?.GetReputation(key) ?? 0;
        var currency = guild?.GetCurrency(key) ?? 0;
        var next = ClusterFGuildSystem.RankThresholds(key).FirstOrDefault(t => t > standing);
        var nextText = next > 0 ? $", next rank at {next:N0}" : "";
        return $"{ClusterFGuildSystem.GetRankName(key, standing)}, {standing:N0} standing, {currency:N0} currency{nextText}";
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var id = info.ButtonID;
        if (id == 0)
        {
            return;
        }

        string message = null;
        var ok = false;
        var page = _page;

        if (id >= BtnPageBase && id < BtnPageBase + 5)
        {
            page = (Page)(id - BtnPageBase);
        }
        else if (id >= BtnContractsBase && id - BtnContractsBase < _keys.Count)
        {
            if (LeagueRegisterSystem.OpenContracts(_pm, _keys[id - BtnContractsBase], out message))
            {
                return;
            }
        }
        else if (id >= BtnUnbindBase && id - BtnUnbindBase < _keys.Count)
        {
            ok = LeagueRegisterSystem.Unbind(_pm, _keys[id - BtnUnbindBase], out message);
        }
        else if (id >= BtnBindBase && id - BtnBindBase < _keys.Count)
        {
            ok = LeagueRegisterSystem.Bind(_pm, _keys[id - BtnBindBase], out message);
        }
        else
        {
            switch (id)
            {
                case BtnTurnIn: ok = LeagueRegisterSystem.TurnInAll(_pm, out message) > 0; break;
                case BtnRepair: ok = LeagueRegisterSystem.RepairAll(_pm, out message) > 0; break;
                case BtnBackpack: ok = LeagueBackpack.Buy(_pm, out message); break;
                case BtnSatchel: ok = LeagueConsolidation.BuySatchel(_pm, out message); break;
                case BtnLogbook: ok = LeagueConsolidation.BuyLogbook(_pm, out message); break;
            }
        }

        if (message != null)
        {
            _pm.SendMessage(ok ? 0x44 : 0x22, message);
        }

        Open(_pm, page);
    }
}
