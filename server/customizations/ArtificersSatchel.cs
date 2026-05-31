using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

/// <summary>
/// Shattered Legacy — Artificers' Essence Satchel.
///
/// A blessed container issued to Artificers' Order members on joining.
/// Accepts only PropertyEssence items.
///
/// Double-clicking opens the Mastery Dashboard instead of the standard
/// container gump — showing all imbue properties, how many essences of each
/// type are stored here, and discovery progress toward mastery.
///
/// Members can still drag essences in/out normally; the container also
/// works as a regular bag via [Props / admin if needed.
///
/// Not replaceable (issued once on join). Blessed — not dropped on death.
/// </summary>
[SerializationGenerator(0, false)]
public partial class ArtificersSatchel : Container
{
    private const int SatchelGraphic = 0xA272;
    private const int SatchelHue     = 0x497;  // arcane blue-purple

    public override int    DefaultGumpID    => 0x3C;
    public override double DefaultWeight    => 2.0;
    public override int    DefaultMaxWeight => 500;

    [Constructible]
    public ArtificersSatchel() : base(SatchelGraphic)
    {
        Hue      = SatchelHue;
        Name     = "Artificers' Essence Satchel";
        LootType = LootType.Blessed;
    }

    // ── Accept only PropertyEssence ──────────────────────────────────────────

    public override bool CheckHold(Mobile m, Item item, bool message, bool checkItems, int plusItems, int plusWeight)
    {
        if (item is not PropertyEssence)
        {
            if (message)
                m.SendMessage("The Artificers' Essence Satchel only holds PropertyEssences.");
            return false;
        }
        return base.CheckHold(m, item, message, checkItems, plusItems, plusWeight);
    }

    // ── Double-click opens Mastery Dashboard instead of container gump ───────

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm) return;
        if (!IsChildOf(pm.Backpack) && Parent != pm)
        {
            pm.SendMessage("That must be in your backpack.");
            return;
        }
        pm.SendGump(new ArtificersMasteryGump(pm, this));
    }

    // ── Properties tooltip ────────────────────────────────────────────────────

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        var count = 0;
        foreach (var item in Items)
            if (item is PropertyEssence) count++;
        list.Add(1049644, $"{count} essence(s) stored");
    }

    private void Deserialize(IGenericReader reader, int version) { }
}

// ── Mastery Dashboard Gump ────────────────────────────────────────────────────

/// <summary>
/// Shows all imbue property groups with:
///  - How many PropertyEssences of each type are in the satchel
///  - Discovery progress toward mastery (X / threshold)
///  - Color coding: mastered=green, have essences=yellow, locked=grey
/// </summary>
public sealed class ArtificersMasteryGump : Gump
{
    private static readonly string[] Groups = { "General", "Stats", "Weapon", "Defense", "Slayer" };

    private readonly PlayerMobile      _pm;
    private readonly ArtificersSatchel _satchel;
    private readonly string            _group;
    private readonly int               _page;

    private const int W    = 560;
    private const int H    = 540;
    private const int BgId = 9270;

    public ArtificersMasteryGump(PlayerMobile pm, ArtificersSatchel satchel, string group = "General", int page = 0)
        : base(60, 40)
    {
        _pm      = pm;
        _satchel = satchel;
        _group   = group;
        _page    = page;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        AddLabel(W / 2 - 100, 12, 1153, "Artificers' Essence Satchel — Mastery");
        AddImageTiled(10, 30, W - 20, 2, 9304);

        // Group tabs
        var tabX = 14;
        for (var i = 0; i < Groups.Length; i++)
        {
            var g      = Groups[i];
            var active = g == _group;
            var hue    = active ? 0x44 : 999;
            AddButton(tabX, 36, active ? 4023 : 4005, active ? 4025 : 4006, 200 + i);
            AddLabel(tabX + 26, 38, hue, g);
            tabX += 100;
        }

        AddImageTiled(10, 56, W - 20, 2, 9304);

        // Column headers
        AddLabel(18,  62, 1153, "Property");
        AddLabel(310, 62, 1153, "Essences");
        AddLabel(390, 62, 1153, "Discovery");
        AddLabel(480, 62, 1153, "Status");
        AddImageTiled(10, 78, W - 20, 1, 9304);

        var acct     = _pm.Account as IAccount;
        var data     = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : new ClusterFAccountData();

        // Count essences in satchel by property key
        var essenceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _satchel.Items)
            if (item is PropertyEssence ess)
                essenceCounts[ess.PropertyKey] = essenceCounts.GetValueOrDefault(ess.PropertyKey) + 1;

        // Filter properties for selected group
        var props = new List<ImbuePropertyDef>();
        foreach (var def in ImbueCatalogue.All)
            if (def.Group.Equals(_group, StringComparison.OrdinalIgnoreCase))
                props.Add(def);

        var rowH     = 20;
        var rowsMax  = (H - 100) / rowH;
        var startIdx = _page * rowsMax;
        var y        = 82;
        var rowIdx   = 0;

        for (var i = startIdx; i < props.Count && rowIdx < rowsMax; i++, rowIdx++)
        {
            var def       = props[i];
            var mastered  = data.IsMastered(def.Name, def.DiscoveryThreshold);
            var count     = essenceCounts.GetValueOrDefault(def.Name);
            var discCount = data.GetDiscoveryCount(def.Name);
            var threshold = def.DiscoveryThreshold;

            var hue       = mastered ? 0x44 :
                            count > 0 ? 1154 : 0x3B2;

            var statusStr = mastered ? "MASTERED" :
                            count > 0 ? "Ready" : "Need essence";

            var statusHue = mastered ? 0x44 :
                            count > 0 ? 1154 : 0x3B2;

            var discStr   = mastered ? $"{threshold}/{threshold}" : $"{discCount}/{threshold}";

            AddLabel(18,  y, hue, def.Name);
            AddLabel(310, y, count > 0 ? 999 : 0x3B2, count > 0 ? count.ToString() : "-");
            AddLabel(390, y, mastered ? 0x44 : 999, discStr);
            AddLabel(480, y, statusHue, statusStr);

            y += rowH;
        }

        if (rowIdx == 0)
            AddLabel(18, y, 0x3B2, "(no properties in this group)");

        // Footer: prev/next + summary
        AddImageTiled(10, H - 52, W - 20, 2, 9304);

        var masteredCount = 0;
        var totalCount    = 0;
        foreach (var def in ImbueCatalogue.All)
        {
            totalCount++;
            if (data.IsMastered(def.Name, def.DiscoveryThreshold)) masteredCount++;
        }
        AddLabel(18, H - 46, 999, $"Overall mastery: {masteredCount}/{totalCount} properties");

        var footY = H - 26;
        if (_page > 0)
        {
            AddButton(18, footY, 4014, 4015, 1);
            AddLabel(40, footY + 2, 999, "Prev");
        }
        if (startIdx + rowsMax < props.Count)
        {
            AddButton(120, footY, 4005, 4006, 2);
            AddLabel(142, footY + 2, 999, "Next");
        }

        AddButton(W - 50, footY, 4023, 4025, 0);
        AddLabel(W - 28, footY + 2, 1153, "X");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_satchel.Deleted) return;

        switch (info.ButtonID)
        {
            case 0: return;
            case 1: _pm.SendGump(new ArtificersMasteryGump(_pm, _satchel, _group, Math.Max(0, _page - 1))); return;
            case 2: _pm.SendGump(new ArtificersMasteryGump(_pm, _satchel, _group, _page + 1)); return;
        }

        // Group tab buttons: 200+i
        if (info.ButtonID >= 200 && info.ButtonID < 200 + Groups.Length)
        {
            var g = Groups[info.ButtonID - 200];
            _pm.SendGump(new ArtificersMasteryGump(_pm, _satchel, g, 0));
        }
    }
}
