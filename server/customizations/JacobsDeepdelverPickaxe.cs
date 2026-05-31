using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

/// <summary>
/// Jacob's Deepdelver Pickaxe — Tier 4 upgrade of Jacob's Prospector Pickaxe.
///
/// Stats:
///   - Name: "Jacob's Deepdelver Pickaxe"
///   - Blessed: yes
///   - Mining skill bonus: +22
///   - UsesRemaining: 800
///   - Hue: 0x0455 (deep slate blue — dangerous/deep-earth identity)
///
/// Special — Deepdelver's Advantage:
///   +1 ore per yield when mining in Felucca. Applied in CompactOreSatchelRoutingHook.Give
///   before the ore is routed to the satchel or backpack.
///   Also retains T3 Prospector's Insight (new ore discoveries still grant +3 vouchers).
///
/// Upgrade requirements (from Miners' Compact Liaison):
///   - Master Delver rank (40,000 Compact Standing)
///   - Mining skill 90.0+
///   - 350 Mining Vouchers
///   - 2,000 Iron Ingots
///   - 500 Valorite Ingots
///   - 150,000 gold
///   - Existing T3 Jacob's Prospector Pickaxe (consumed)
///
/// Exhausted state: tracked via Hue. Functional = 0x0455, exhausted = 0x0415.
/// Serialization: v0 — no custom fields. Hue serialized by base Item.
/// </summary>
[SerializationGenerator(0, false)]
public partial class JacobsDeepdelverPickaxe : Pickaxe
{
    private const int FunctionalHue = 0x0455; // Deep slate blue
    private const int ExhaustedHue  = 0x0415; // Charcoal — exhausted state

    [Constructible]
    public JacobsDeepdelverPickaxe()
    {
        Name          = "Jacob's Deepdelver Pickaxe";
        UsesRemaining = 800;
        LootType      = LootType.Blessed;
        SkillBonuses.SetValues(0, SkillName.Mining, 22.0);
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
                "Jacob's Deepdelver Pickaxe is exhausted and cannot be equipped. " +
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
            list.Add("<BASEFONT COLOR=#AAAAAA>Tier 4 Miners' Compact Legacy Tool</BASEFONT>");
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
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_pickaxe",            "item_acquisition");
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_reinforced_pickaxe", "item_acquisition");
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_prospector_pickaxe", "item_acquisition");
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_deepdelver_pickaxe", "item_acquisition");

            if (!Exhausted)
            {
                var entry = ClusterFRestorationRegistry.GetEntry(acct, "legacy.jacobs_deepdelver_pickaxe");
                if (entry != null) entry.HasActiveCopy = true;
            }
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (Exhausted)
        {
            from.SendMessage(0x22,
                "Jacob's Deepdelver Pickaxe is exhausted. " +
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
                    ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_deepdelver_pickaxe");

                var replacement = new JacobsDeepdelverPickaxe();
                replacement.Exhausted = true;
                pack.DropItem(replacement);

                owner.SendMessage(0x22,
                    "Jacob's Deepdelver Pickaxe has worn out. " +
                    "Speak with the Miners' Compact Liaison in New Haven to restore it.");
            }
        }

        base.OnDelete();
    }

    private void Deserialize(IGenericReader reader, int version) { }
}

// ─────────────────────────────────────────────────────────────────────────────
// T4 Upgrade Gump — opened from the Miners' Compact Liaison Member Dashboard
// ─────────────────────────────────────────────────────────────────────────────

public class JacobsT4UpgradeGump : Gump
{
    private readonly PlayerMobile _pm;

    private const int UpgradeVoucherCost  = 350;
    private const int UpgradeIronCost     = 2000;
    private const int UpgradeValoriteCost = 500;
    private const int UpgradeGoldCost     = 150000;
    private const int UpgradeStandingReq  = 40000;  // Master Delver rank
    private const double UpgradeSkillReq  = 90.0;

    private const int W    = 440;
    private const int H    = 400;
    private const int BgId = 9270;

    public JacobsT4UpgradeGump(PlayerMobile pm) : base(100, 80)
    {
        _pm = pm;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        AddLabel(W / 2 - 120, 12, 1154, "Jacob's Pickaxe — Tier 4 Upgrade");
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
        var mining       = _pm.Skills[SkillName.Mining].Value;
        var pack         = _pm.Backpack;
        var bypass       = DevTestingCrystal.IsActive(_pm);

        var ironCount    = pack?.GetAmount(typeof(IronIngot))    ?? 0;
        var valoriteCount = pack?.GetAmount(typeof(ValoriteIngot)) ?? 0;
        var goldCount    = CompactGoldHelper.GetTotalGold(_pm);
        var hasT3        = FindT3InPack() != null;

        var reqRank      = bypass || standing      >= UpgradeStandingReq;
        var reqSkill     = bypass || mining         >= UpgradeSkillReq;
        var reqVoucher   = bypass || vouchers       >= UpgradeVoucherCost;
        var reqIron      = bypass || ironCount      >= UpgradeIronCost;
        var reqValorite  = bypass || valoriteCount  >= UpgradeValoriteCost;
        var reqGold      = bypass || goldCount      >= UpgradeGoldCost;
        var reqT3        = hasT3;

        var allMet = reqRank && reqSkill && reqVoucher && reqIron && reqValorite && reqGold && reqT3;

        static string Clr(bool met) => met ? "#FFD700" : "#FF6666";

        var html =
            "<BASEFONT COLOR=#AAAAAA>Deepdelver's Advantage: +1 ore per yield when mining " +
            "in Felucca. Also retains Prospector's Insight (new discoveries = +3 vouchers)." +
            "</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR={Clr(reqRank)}>Rank: Master Delver required — Standing {standing:N0}/{UpgradeStandingReq:N0}</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqSkill)}>Mining skill: {mining:F1}/{UpgradeSkillReq:F0} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqVoucher)}>Mining Vouchers: {vouchers}/{UpgradeVoucherCost} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqIron)}>Iron Ingots in pack: {ironCount:N0}/{UpgradeIronCost:N0} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqValorite)}>Valorite Ingots in pack: {valoriteCount}/{UpgradeValoriteCost} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqGold)}>Gold (pack+bank): {goldCount:N0}/{UpgradeGoldCost:N0} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqT3)}>Jacob's Prospector Pickaxe (non-exhausted) in pack: {(hasT3 ? "Yes" : "No")}</BASEFONT>";

        AddHtml(16, 56, W - 32, H - 130, html, false, true);

        if (allMet)
        {
            AddButton(18, H - 68, 4011, 4012, 10);
            AddLabel(44, H - 66, 999, "Upgrade to Jacob's Deepdelver Pickaxe");
        }
        else
        {
            AddLabel(18, H - 68, 0x22, "Requirements not met. See above.");
        }
    }

    private JacobsProspectorPickaxe? FindT3InPack()
    {
        if (_pm.FindItemOnLayer(Layer.TwoHanded) is JacobsProspectorPickaxe eq && !eq.Exhausted)
            return eq;

        if (_pm.Backpack == null) return null;

        foreach (var item in _pm.Backpack.Items)
            if (item is JacobsProspectorPickaxe p && !p.Exhausted)
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

        var data         = ClusterFAccountPersistence.GetOrCreate(acct);
        var pack         = _pm.Backpack;
        if (pack == null) return;

        data.GuildReputation.TryGetValue("mining", out var standing);
        data.GuildCurrency.TryGetValue("mining", out var vouchers);
        var mining       = _pm.Skills[SkillName.Mining].Value;
        var ironCount    = pack.GetAmount(typeof(IronIngot));
        var valoriteCount = pack.GetAmount(typeof(ValoriteIngot));
        var srcPickaxe   = FindT3InPack();
        var bypass       = DevTestingCrystal.IsActive(_pm);

        if (srcPickaxe == null
            || (!bypass && (standing < UpgradeStandingReq || mining < UpgradeSkillReq
                || vouchers < UpgradeVoucherCost || ironCount < UpgradeIronCost
                || valoriteCount < UpgradeValoriteCost
                || CompactGoldHelper.GetTotalGold(_pm) < UpgradeGoldCost)))
        {
            _pm.SendMessage(0x22, "Requirements no longer met. Upgrade cancelled.");
            _pm.SendGump(new JacobsT4UpgradeGump(_pm));
            return;
        }

        if (!bypass)
        {
            data.GuildCurrency["mining"] = vouchers - UpgradeVoucherCost;
            pack.ConsumeTotal(typeof(IronIngot),    UpgradeIronCost);
            pack.ConsumeTotal(typeof(ValoriteIngot), UpgradeValoriteCost);
            CompactGoldHelper.ConsumeGold(_pm, UpgradeGoldCost);
        }

        srcPickaxe.Exhausted = true;
        srcPickaxe.Delete();
        ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_prospector_pickaxe");

        var upgraded = new JacobsDeepdelverPickaxe();
        pack.DropItem(upgraded);

        _pm.SendMessage(0x44,
            "Jacob's Deepdelver Pickaxe is yours. The veins of Felucca will yield more for you now. " +
            "Tread carefully — the deep earth does not forgive carelessness.");
        _pm.PlaySound(0x35D);
    }
}
