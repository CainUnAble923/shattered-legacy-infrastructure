using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

/// <summary>
/// Jacob's Prospector Pickaxe — Tier 3 upgrade of Jacob's Reinforced Pickaxe.
///
/// Stats:
///   - Name: "Jacob's Prospector Pickaxe"
///   - Blessed: yes
///   - Mining skill bonus: +18
///   - UsesRemaining: 600
///   - Hue: 0x026C (teal — cartographic/survey identity)
///
/// Special — Prospector's Insight:
///   When a new ore type is discovered for the first time, +3 Mining Vouchers
///   are credited instantly. Tracked in CompactOreSatchelRoutingHook.TryLogDiscovery.
///
/// Upgrade requirements (from Miners' Compact Liaison):
///   - Surveyor rank (15,000 Compact Standing)
///   - Mining skill 80.0+
///   - 200 Mining Vouchers
///   - 1,500 Iron Ingots
///   - 500 Agapite Ingots
///   - 50,000 gold
///   - Existing T2 Jacob's Reinforced Pickaxe (consumed)
///
/// Exhausted state: tracked via Hue. Functional = 0x026C, exhausted = 0x0415.
/// Serialization: v0 — no custom fields. Hue serialized by base Item.
/// </summary>
[SerializationGenerator(0, false)]
public partial class JacobsProspectorPickaxe : Pickaxe
{
    private const int FunctionalHue = 0x026C; // Teal
    private const int ExhaustedHue  = 0x0415; // Charcoal — exhausted state

    [Constructible]
    public JacobsProspectorPickaxe()
    {
        Name          = "Jacob's Prospector Pickaxe";
        UsesRemaining = 600;
        LootType      = LootType.Blessed;
        SkillBonuses.SetValues(0, SkillName.Mining, 18.0);
        Hue = FunctionalHue;
    }

    public bool Exhausted
    {
        get => Hue == ExhaustedHue;
        set
        {
            Hue = value ? ExhaustedHue : FunctionalHue;
            ((IUsesRemaining)this).ShowUsesRemaining = !value;
            InvalidateProperties();
        }
    }

    public override bool OnEquip(Mobile from)
    {
        if (Exhausted)
        {
            from.SendMessage(0x22,
                "Jacob's Prospector Pickaxe is exhausted and cannot be equipped. " +
                "Speak with the Miners' Compact Liaison in New Haven to restore it.");
            return false;
        }

        return base.OnEquip(from);
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (Exhausted)
            list.Add("(Exhausted — speak with the Miners' Compact Liaison)");
        else
            list.Add("<BASEFONT COLOR=#AAAAAA>Tier 3 Miners' Compact Legacy Tool</BASEFONT>");
    }

    public override void OnAdded(IEntity parent)
    {
        base.OnAdded(parent);

        PlayerMobile? pm = null;

        if (parent is PlayerMobile directPm)
            pm = directPm;
        else if (parent is Container c && c.RootParent is PlayerMobile containerPm)
            pm = containerPm;

        if (pm?.Account is IAccount acct)
        {
            // Ensure T1 and T2 registry entries exist (safety-net for unusual acquisition paths)
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_pickaxe",             "item_acquisition");
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_reinforced_pickaxe",  "item_acquisition");
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_prospector_pickaxe",  "item_acquisition");

            if (!Exhausted)
            {
                var entry = ClusterFRestorationRegistry.GetEntry(acct, "legacy.jacobs_prospector_pickaxe");
                if (entry != null) entry.HasActiveCopy = true;
            }
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (Exhausted)
        {
            from.SendMessage(0x22,
                "Jacob's Prospector Pickaxe is exhausted. " +
                "Speak with the Miners' Compact Liaison in New Haven to have it restored.");
            return;
        }

        base.OnDoubleClick(from);
    }

    public override void OnDelete()
    {
        if (!Exhausted)
        {
            PlayerMobile? owner = null;
            Container?    pack  = null;

            if (Parent is Container directPack)
            {
                pack  = directPack;
                owner = directPack.RootParent as PlayerMobile;
            }
            else if (Parent is PlayerMobile layerOwner)
            {
                owner = layerOwner;
                pack  = layerOwner.Backpack;
            }

            if (owner != null && pack != null)
            {
                if (owner.Account is IAccount acct)
                    ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_prospector_pickaxe");

                var replacement = new JacobsProspectorPickaxe();
                replacement.Exhausted = true;
                pack.DropItem(replacement);

                owner.SendMessage(0x22,
                    "Jacob's Prospector Pickaxe has worn out. " +
                    "Speak with the Miners' Compact Liaison in New Haven to restore it.");
            }
        }

        base.OnDelete();
    }

    private void Deserialize(IGenericReader reader, int version) { }
}

// ─────────────────────────────────────────────────────────────────────────────
// T3 Upgrade Gump — opened from the Miners' Compact Liaison Member Dashboard
// ─────────────────────────────────────────────────────────────────────────────

public class JacobsT3UpgradeGump : Gump
{
    private readonly PlayerMobile _pm;

    private const int UpgradeVoucherCost  = 200;
    private const int UpgradeIronCost     = 1500;
    private const int UpgradeAgapiteCost  = 500;
    private const int UpgradeGoldCost     = 50000;
    private const int UpgradeStandingReq  = 15000;  // Surveyor rank
    private const double UpgradeSkillReq  = 80.0;

    private const int W    = 440;
    private const int H    = 400;
    private const int BgId = 9270;

    public JacobsT3UpgradeGump(PlayerMobile pm) : base(100, 80)
    {
        _pm = pm;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        AddLabel(W / 2 - 120, 12, 1154, "Jacob's Pickaxe — Tier 3 Upgrade");
        AddLabel(W / 2 - 80,  28, 999,  "Miners' Compact Liaison");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        var acct = pm.Account as IAccount;
        var data = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : null;

        if (data == null)
            AddLabel(18, 56, 999, "Account data unavailable.");
        else
            DrawUpgradeInfo(data, acct!);

        AddImageTiled(10, H - 38, W - 20, 2, 9304);
        AddButton(18, H - 28, 4014, 4015, 1);
        AddLabel(40, H - 26, 999, "Back");
        AddButton(W - 50, H - 28, 4023, 4025, 0);
        AddLabel(W - 28, H - 26, 1154, "X");
    }

    private void DrawUpgradeInfo(ClusterFAccountData data, IAccount acct)
    {
        data.GuildReputation.TryGetValue("mining", out var standing);
        data.GuildCurrency.TryGetValue("mining", out var vouchers);
        var mining      = _pm.Skills[SkillName.Mining].Value;
        var pack        = _pm.Backpack;
        var bypass      = DevTestingCrystal.IsActive(_pm);

        var ironCount   = pack?.GetAmount(typeof(IronIngot))    ?? 0;
        var agapiteCount = pack?.GetAmount(typeof(AgapiteIngot)) ?? 0;
        var goldCount   = CompactGoldHelper.GetTotalGold(_pm);
        var hasT2       = FindT2InPack() != null;

        var reqRank     = bypass || standing     >= UpgradeStandingReq;
        var reqSkill    = bypass || mining        >= UpgradeSkillReq;
        var reqVoucher  = bypass || vouchers      >= UpgradeVoucherCost;
        var reqIron     = bypass || ironCount     >= UpgradeIronCost;
        var reqAgapite  = bypass || agapiteCount  >= UpgradeAgapiteCost;
        var reqGold     = bypass || goldCount     >= UpgradeGoldCost;
        var reqT2       = hasT2; // T2 always required — consumed in upgrade

        var allMet = reqRank && reqSkill && reqVoucher && reqIron && reqAgapite && reqGold && reqT2;

        static string Clr(bool met) => met ? "#FFD700" : "#FF6666";

        var html =
            "<BASEFONT COLOR=#AAAAAA>Prospector's Insight: Each new ore discovery immediately " +
            "credits +3 Mining Vouchers.</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR={Clr(reqRank)}>Rank: Surveyor required — Standing {standing:N0}/{UpgradeStandingReq:N0}</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqSkill)}>Mining skill: {mining:F1}/{UpgradeSkillReq:F0} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqVoucher)}>Mining Vouchers: {vouchers}/{UpgradeVoucherCost} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqIron)}>Iron Ingots in pack: {ironCount:N0}/{UpgradeIronCost:N0} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqAgapite)}>Agapite Ingots in pack: {agapiteCount}/{UpgradeAgapiteCost} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqGold)}>Gold (pack+bank): {goldCount:N0}/{UpgradeGoldCost:N0} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqT2)}>Jacob's Reinforced Pickaxe (non-exhausted) in pack: {(hasT2 ? "Yes" : "No")}</BASEFONT>";

        AddHtml(16, 56, W - 32, H - 130, html, false, true);

        if (allMet)
        {
            AddButton(18, H - 68, 4011, 4012, 10);
            AddLabel(44, H - 66, 999, "Upgrade to Jacob's Prospector Pickaxe");
        }
        else
        {
            AddLabel(18, H - 68, 0x22, "Requirements not met. See above.");
        }
    }

    private JacobsReinforcedPickaxe? FindT2InPack()
    {
        if (_pm.FindItemOnLayer(Layer.TwoHanded) is JacobsReinforcedPickaxe eq && !eq.Exhausted)
            return eq;

        if (_pm.Backpack == null) return null;

        foreach (var item in _pm.Backpack.Items)
            if (item is JacobsReinforcedPickaxe p && !p.Exhausted)
                return p;

        return null;
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return;

        if (info.ButtonID == 1)
        {
            _pm.SendGump(new MinersCompactLiaisonGump(_pm, MinersCompactLiaisonGump.View.MemberDashboard));
            return;
        }

        if (info.ButtonID == 10) HandleUpgrade();
    }

    private void HandleUpgrade()
    {
        var acct = _pm.Account as IAccount;
        if (acct == null) return;

        var data    = ClusterFAccountPersistence.GetOrCreate(acct);
        var pack    = _pm.Backpack;
        if (pack == null) return;

        data.GuildReputation.TryGetValue("mining", out var standing);
        data.GuildCurrency.TryGetValue("mining", out var vouchers);
        var mining      = _pm.Skills[SkillName.Mining].Value;
        var ironCount   = pack.GetAmount(typeof(IronIngot));
        var agapiteCount = pack.GetAmount(typeof(AgapiteIngot));
        var srcPickaxe  = FindT2InPack();
        var bypass      = DevTestingCrystal.IsActive(_pm);

        if (srcPickaxe == null
            || (!bypass && (standing < UpgradeStandingReq || mining < UpgradeSkillReq
                || vouchers < UpgradeVoucherCost || ironCount < UpgradeIronCost
                || agapiteCount < UpgradeAgapiteCost
                || CompactGoldHelper.GetTotalGold(_pm) < UpgradeGoldCost)))
        {
            _pm.SendMessage(0x22, "Requirements no longer met. Upgrade cancelled.");
            _pm.SendGump(new JacobsT3UpgradeGump(_pm));
            return;
        }

        if (!bypass)
        {
            data.GuildCurrency["mining"] = vouchers - UpgradeVoucherCost;
            pack.ConsumeTotal(typeof(IronIngot),    UpgradeIronCost);
            pack.ConsumeTotal(typeof(AgapiteIngot), UpgradeAgapiteCost);
            CompactGoldHelper.ConsumeGold(_pm, UpgradeGoldCost);
        }

        // Consume T2 — mark exhausted first to suppress the durability replacement drop
        srcPickaxe.Exhausted = true;
        srcPickaxe.Delete();
        ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_reinforced_pickaxe");

        var upgraded = new JacobsProspectorPickaxe();
        pack.DropItem(upgraded);

        _pm.SendMessage(0x44,
            "Jacob's Prospector Pickaxe is yours. The Compact Survey Archivist will take note. " +
            "New ore discoveries will now earn you bonus vouchers.");
        _pm.PlaySound(0x35D);
    }
}
