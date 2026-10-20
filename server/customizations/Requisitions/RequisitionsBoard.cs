// cc-P150 (F-47): the requisitions board: the item (a League board when GuildKey is empty, one guild's view when it
// names a guild), the gump, and the turn-in target. The same gump opens from the League Registrar's menu, from a guild's
// hall page (GuildTaskDetailGump: that guild's view at its New Haven chapter) and from the Appraiser. A later real hall
// (F-49) shows the same view with RequisitionsBoard.OpenFor(pm, guildKey) or by placing a RequisitionsBoard with its
// GuildKey set; nothing else is needed.
//
// Turning in needs the character at a counter (CanTurnInHere): a board within 3 tiles whose view covers the guild, the
// League Registrar within 4, or that guild's guildmaster within ClusterFGuildSystem.GuildmasterRange. Reading needs
// nothing: the board was opened from one of those.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class RequisitionsBoard : Item
{
    /// <summary>Empty: the League board, every guild. A guild key: that guild's view.</summary>
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _guildKey;

    public const int ReadRange = 3;

    [Constructible]
    public RequisitionsBoard() : this(null) { }

    [Constructible]
    public RequisitionsBoard(string guildKey) : base(0x1E5E) // bulletin board, east face
    {
        _guildKey = guildKey ?? "";
        Movable = false;
        Name = "requisitions board";
    }

    public bool IsLeagueBoard => string.IsNullOrEmpty(_guildKey);

    public bool Covers(string guildKey) =>
        IsLeagueBoard || guildKey != null && _guildKey.Equals(guildKey, StringComparison.OrdinalIgnoreCase);

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        var def = IsLeagueBoard ? null : ClusterFGuildSystem.GetDef(_guildKey);
        list.Add(def != null ? $"{def.Name}: requisitions" : "League of Extraordinary Citizens: requisitions");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
            return;

        if (!pm.InRange(GetWorldLocation(), ReadRange) || pm.Map != Map)
        {
            pm.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        OpenFor(pm, IsLeagueBoard ? null : _guildKey);
    }

    /// <summary>Opens the board: every guild's requests when <paramref name="guildKey"/> is null, else one guild's.</summary>
    public static void OpenFor(PlayerMobile pm, string guildKey)
    {
        ClusterFGuildSystem.EnsureRegistered();
        pm.CloseGump<RequisitionsBoardGump>();
        pm.SendGump(new RequisitionsBoardGump(pm, guildKey, StartPage(pm, guildKey)));
    }

    /// <summary>The guilds a League board pages through: those with theme rows, in F-9's order.</summary>
    public static List<GuildDef> Guilds()
    {
        ClusterFGuildSystem.EnsureRegistered();
        var table = RequisitionTable.Current;
        return ClusterFGuildSystem.AllGuilds.Values.Where(d => table.ThemesFor(d.Key).Count > 0).ToList();
    }

    private static int StartPage(PlayerMobile pm, string guildKey)
    {
        var guilds = Guilds();
        if (guildKey != null)
            return Math.Max(0, guilds.FindIndex(d => d.Key.Equals(guildKey, StringComparison.OrdinalIgnoreCase)));

        var data = ClusterFAccountPersistence.GetGuild(pm);
        if (data != null)
            for (var i = 0; i < guilds.Count; i++)
                if (data.JoinedGuilds.Contains(guilds[i].Key))
                    return i;

        return 0;
    }

    /// <summary>Where a turn-in may happen for this guild's request.</summary>
    public static bool CanTurnInHere(PlayerMobile pm, string guildKey, out string where)
    {
        where = null;
        if (pm.Map == null || pm.Map == Map.Internal)
            return false;

        foreach (var item in pm.Map.GetItemsInRange(pm.Location, ReadRange))
        {
            if (item is RequisitionsBoard board && !board.Deleted && board.Covers(guildKey))
            {
                where = "the board";
                return true;
            }
        }

        foreach (var m in pm.Map.GetMobilesInRange(pm.Location, ClusterFGuildSystem.GuildmasterRange))
        {
            if (m.Deleted)
                continue;

            if (m is LeagueRegistrar && pm.InRange(m, 4))
            {
                where = "the League Registrar";
                return true;
            }

            if (m is Appraiser && pm.InRange(m, 4))
            {
                where = "the Appraiser";
                return true;
            }

            if (guildKey != null && ClusterFGuildSystem.GetDefForGuildmaster(m.GetType()) is { } def &&
                def.Key.Equals(guildKey, StringComparison.OrdinalIgnoreCase))
            {
                where = $"the {def.Name} guildmaster";
                return true;
            }
        }

        return false;
    }
}

public class RequisitionsBoardGump : Gump
{
    private readonly PlayerMobile _pm;
    private readonly string _guildKey; // null: the League board
    private readonly int _page;
    private readonly List<RequisitionEntry> _rows = new();

    private const int W = 600;
    private const int H = 440;

    public const int BtnClose = 0;
    public const int BtnPrev = 1;
    public const int BtnNext = 2;
    public const int BtnRefresh = 3;
    public const int BtnTurnInBase = 100;

    public RequisitionsBoardGump(PlayerMobile pm, string guildKey, int page) : base(60, 60)
    {
        _pm = pm;
        _guildKey = guildKey;

        var guilds = RequisitionsBoard.Guilds();

        // A guild's own view shows that guild whatever page it was opened on; the League view pages through them.
        if (_guildKey != null)
        {
            page = Math.Max(0, guilds.FindIndex(d => d.Key.Equals(_guildKey, StringComparison.OrdinalIgnoreCase)));
        }

        _page = guilds.Count == 0 ? 0 : Math.Clamp(page, 0, guilds.Count - 1);
        var def = guilds.Count > 0 ? guilds[_page] : null;

        Closable = true;
        Disposable = true;

        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(10, 10, W - 20, H - 20);

        var title = _guildKey == null ? "League requisitions board" : $"{def?.Name ?? _guildKey}: requisitions";
        AddLabel(20, 14, 1154, title);
        AddImageTiled(10, 36, W - 20, 2, 9304);

        var leagueRank = ClusterFLeagueRanks.GetRank(pm);
        var table = RequisitionTable.Current;
        var next = ClusterFRequisitions.NextRefresh();
        AddHtml(20, 42, W - 40, 20,
            $"<BASEFONT COLOR=#AAAAAA>League rank: {ClusterFLeagueRanks.RankName(leagueRank)} (requests up to the {ClusterFRequisitions.TierName(table.GateFor(leagueRank))} tier). " +
            $"The board refreshes daily at {table.RefreshHourUtc:00}:00 UTC.</BASEFONT>", false, false);

        if (def == null)
        {
            AddLabel(20, 80, 37, "No guild posts requisitions yet.");
        }
        else
        {
            var guildData = ClusterFAccountPersistence.GetGuild(pm);
            var member = guildData?.JoinedGuilds.Contains(def.Key) == true;
            var tier = ClusterFRequisitions.TierFor(pm, def.Key, guildData);
            AddLabel(20, 66, 68, $"{def.Name}: {(member ? ClusterFGuildSystem.GetRankName(def.Key, guildData) : "not a member")}, rolling the {ClusterFRequisitions.TierName(tier)} tier.");

            _rows.AddRange(ClusterFRequisitions.Open(pm, def.Key));
            var now = ClusterFRequisitions.Clock();
            var y = 92;

            if (_rows.Count == 0)
            {
                AddLabel(20, y, 999, "Every request of this guild is filled for today. New ones at the refresh.");
            }

            for (var i = 0; i < _rows.Count && i < 3; i++)
            {
                var e = _rows[i];
                AddImageTiled(10, y, W - 20, 1, 9304);
                y += 6;

                AddLabel(20, y, 1154, $"{ClusterFRequisitions.TierName(e.Tier)} tier:");
                AddHtml(130, y + 1, W - 280, 40, $"<BASEFONT COLOR=#FFFFFF>{Cap(ClusterFRequisitions.Describe(e))}.</BASEFONT>", false, false);

                AddButton(W - 120, y, 4023, 4025, BtnTurnInBase + i);
                AddLabel(W - 85, y + 2, 1154, "Turn in");
                y += 40;

                AddHtml(20, y, W - 40, 20, $"<BASEFONT COLOR=#AAAAAA>Pays {ClusterFRequisitions.PayText(e)}.</BASEFONT>", false, false);
                y += 20;

                var lead = RequisitionLeads.Text(e, now);
                AddHtml(20, y, W - 40, 20, $"<BASEFONT COLOR=#{(lead != null ? "FFD700" : "777777")}>{lead ?? "No lead."}</BASEFONT>", false, false);
                y += 26;
            }
        }

        AddImageTiled(10, H - 50, W - 20, 2, 9304);

        if (_guildKey == null && guilds.Count > 1)
        {
            AddButton(20, H - 38, 4014, 4015, BtnPrev);
            AddLabel(45, H - 36, 999, "Previous guild");
            AddButton(160, H - 38, 4005, 4007, BtnNext);
            AddLabel(185, H - 36, 999, "Next guild");
            AddLabel(300, H - 36, 999, $"{_page + 1} of {guilds.Count}");
        }

        AddButton(W - 190, H - 38, 4011, 4012, BtnRefresh);
        AddLabel(W - 165, H - 36, 999, "Refresh");
        AddButton(W - 90, H - 38, 4017, 4019, BtnClose);
        AddLabel(W - 55, H - 36, 999, "Close");
    }

    private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != _pm)
            return;

        var guilds = RequisitionsBoard.Guilds();
        switch (info.ButtonID)
        {
            case BtnClose:
                return;

            case BtnPrev:
                _pm.SendGump(new RequisitionsBoardGump(_pm, _guildKey, (_page - 1 + guilds.Count) % Math.Max(1, guilds.Count)));
                return;

            case BtnNext:
                _pm.SendGump(new RequisitionsBoardGump(_pm, _guildKey, (_page + 1) % Math.Max(1, guilds.Count)));
                return;

            case BtnRefresh:
                _pm.SendGump(new RequisitionsBoardGump(_pm, _guildKey, _page));
                return;
        }

        var index = info.ButtonID - BtnTurnInBase;
        if (index < 0 || index >= _rows.Count)
            return;

        var e = _rows[index];
        if (!RequisitionsBoard.CanTurnInHere(_pm, e.GuildKey, out _))
        {
            _pm.SendMessage(0x22, "Turn in at a requisitions board, the League Registrar, the Appraiser, or that guild's guildmaster.");
            _pm.SendGump(new RequisitionsBoardGump(_pm, _guildKey, _page));
            return;
        }

        _pm.SendMessage(0x3B2, $"Which item fills it? {Cap(ClusterFRequisitions.Describe(e))}.");
        _pm.Target = new RequisitionTurnInTarget(_pm, e, _guildKey, _page);
    }
}

public class RequisitionTurnInTarget : Target
{
    private readonly PlayerMobile _pm;
    private readonly RequisitionEntry _entry;
    private readonly string _guildKey;
    private readonly int _page;

    public RequisitionTurnInTarget(PlayerMobile pm, RequisitionEntry entry, string guildKey, int page) : base(2, false, TargetFlags.None)
    {
        _pm = pm;
        _entry = entry;
        _guildKey = guildKey;
        _page = page;
    }

    protected override void OnTarget(Mobile from, object targeted)
    {
        if (from != _pm)
            return;

        if (targeted is not Item item)
        {
            _pm.SendMessage(0x22, "That is not an item.");
        }
        else if (!RequisitionsBoard.CanTurnInHere(_pm, _entry.GuildKey, out _))
        {
            _pm.SendMessage(0x22, "You have walked away from the counter.");
        }
        else
        {
            var ok = ClusterFRequisitions.TurnIn(_pm, _entry, item, out var message);
            _pm.SendMessage(ok ? 0x3B2 : 0x22, message);
        }

        _pm.SendGump(new RequisitionsBoardGump(_pm, _guildKey, _page));
    }

    protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
    {
        if (from == _pm)
            _pm.SendGump(new RequisitionsBoardGump(_pm, _guildKey, _page));
    }
}
