using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

/// <summary>
/// Jacob's Worldbreaker Pickaxe — Tier 5 (final) upgrade of Jacob's Deepdelver Pickaxe.
///
/// Stats:
///   - Name: "Jacob's Worldbreaker Pickaxe"
///   - Blessed: yes
///   - Mining skill bonus: +25
///   - UsesRemaining: 1,200
///   - Hue: 0x0B2A (warm amber/gold — legendary relic identity)
///
/// Special — Worldbreaker's Edge (combines T3 + T4 + new bonus):
///   - Prospector's Insight: new ore discoveries grant +3 Mining Vouchers (T3 carry-over)
///   - Deepdelver's Advantage: +1 ore per yield in Felucca (T4 carry-over)
///   - Worldbreaker's Edge: +1 ore per yield everywhere (stacks with Felucca bonus = +2 in Felucca)
///
/// Upgrade requirements (from Miners' Compact Liaison):
///   - Deepwarden rank (80,000 Compact Standing)
///   - Mining skill 100.0 (GM)
///   - 500 Mining Vouchers
///   - 3,000 Iron Ingots
///   - 1,000 Valorite Ingots
///   - 200 Adamantium Ingots
///   - 300,000 gold
///   - Existing T4 Jacob's Deepdelver Pickaxe (consumed)
///
/// Exhausted state: tracked via Hue. Functional = 0x0B2A, exhausted = 0x0415.
/// Serialization: v0 — no custom fields. Hue serialized by base Item.
/// </summary>
[SerializationGenerator(0, false)]
public partial class JacobsWorldbreakerPickaxe : Pickaxe
{
    private const int FunctionalHue = 0x0B2A; // Warm amber/gold — legendary
    private const int ExhaustedHue  = 0x0415; // Charcoal — exhausted state

    [Constructible]
    public JacobsWorldbreakerPickaxe()
    {
        Name          = "Jacob's Worldbreaker Pickaxe";
        UsesRemaining = 1200;
        LootType      = LootType.Blessed;
        SkillBonuses.SetValues(0, SkillName.Mining, 25.0);
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
                "Jacob's Worldbreaker Pickaxe is exhausted and cannot be equipped. " +
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
            list.Add("<BASEFONT COLOR=#AAAAAA>Tier 5 Miners' Compact Legacy Tool</BASEFONT>");
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
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_pickaxe",              "item_acquisition");
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_reinforced_pickaxe",   "item_acquisition");
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_prospector_pickaxe",   "item_acquisition");
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_deepdelver_pickaxe",   "item_acquisition");
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_worldbreaker_pickaxe", "item_acquisition");

            if (!Exhausted)
            {
                var entry = ClusterFRestorationRegistry.GetEntry(acct, "legacy.jacobs_worldbreaker_pickaxe");
                if (entry != null) entry.HasActiveCopy = true;
            }
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (Exhausted)
        {
            from.SendMessage(0x22,
                "Jacob's Worldbreaker Pickaxe is exhausted. " +
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
                    ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_worldbreaker_pickaxe");

                var replacement = new JacobsWorldbreakerPickaxe();
                replacement.Exhausted = true;
                pack.DropItem(replacement);

                owner.SendMessage(0x22,
                    "Jacob's Worldbreaker Pickaxe has worn out. " +
                    "Speak with the Miners' Compact Liaison in New Haven to restore it.");
            }
        }

        base.OnDelete();
    }

    private void Deserialize(IGenericReader reader, int version) { }
}

// ─────────────────────────────────────────────────────────────────────────────
// T5 Upgrade Gump — opened from the Miners' Compact Liaison Member Dashboard
// ─────────────────────────────────────────────────────────────────────────────

public class JacobsT5UpgradeGump : Gump
{
    private readonly PlayerMobile _pm;

    private const int UpgradeVoucherCost    = 500;
    private const int UpgradeIronCost       = 3000;
    private const int UpgradeValoriteCost   = 1000;
    private const int UpgradeAdamantiumCost = 200;
    private const int UpgradeGoldCost       = 300000;
    private const int UpgradeStandingReq    = 80000;  // Deepwarden rank
    private const double UpgradeSkillReq    = 100.0;  // GM Mining

    private const int W    = 440;
    private const int H    = 420;
    private const int BgId = 9270;

    public JacobsT5UpgradeGump(PlayerMobile pm) : base(100, 80)
    {
        _pm = pm;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        AddLabel(W / 2 - 120, 12, 1154, "Jacob's Pickaxe — Tier 5 Upgrade");
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
        var mining          = _pm.Skills[SkillName.Mining].Value;
        var pack            = _pm.Backpack;
        var bypass          = DevTestingCrystal.IsActive(_pm);

        var ironCount       = pack?.GetAmount(typeof(IronIngot))       ?? 0;
        var valoriteCount   = pack?.GetAmount(typeof(ValoriteIngot))   ?? 0;
        var adamantiumCount = pack?.GetAmount(typeof(AdamantiumIngot)) ?? 0;
        var goldCount       = CompactGoldHelper.GetTotalGold(_pm);
        var hasT4           = FindT4InPack() != null;

        var reqRank         = bypass || standing        >= UpgradeStandingReq;
        var reqSkill        = bypass || mining           >= UpgradeSkillReq;
        var reqVoucher      = bypass || vouchers         >= UpgradeVoucherCost;
        var reqIron         = bypass || ironCount        >= UpgradeIronCost;
        var reqValorite     = bypass || valoriteCount    >= UpgradeValoriteCost;
        var reqAdamantium   = bypass || adamantiumCount  >= UpgradeAdamantiumCost;
        var reqGold         = bypass || goldCount        >= UpgradeGoldCost;
        var reqT4           = hasT4;

        var allMet = reqRank && reqSkill && reqVoucher && reqIron && reqValorite
                  && reqAdamantium && reqGold && reqT4;

        static string Clr(bool met) => met ? "#FFD700" : "#FF6666";

        var html =
            "<BASEFONT COLOR=#AAAAAA>Worldbreaker's Edge: +1 ore per yield everywhere. " +
            "Stacks with Deepdelver's Advantage for +2 in Felucca. " +
            "Retains all prior bonuses.</BASEFONT><BR><BR>" +
            $"<BASEFONT COLOR={Clr(reqRank)}>Rank: Deepwarden required — Standing {standing:N0}/{UpgradeStandingReq:N0}</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqSkill)}>Mining skill: {mining:F1}/{UpgradeSkillReq:F0} required (GM)</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqVoucher)}>Mining Vouchers: {vouchers}/{UpgradeVoucherCost} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqIron)}>Iron Ingots in pack: {ironCount:N0}/{UpgradeIronCost:N0} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqValorite)}>Valorite Ingots in pack: {valoriteCount}/{UpgradeValoriteCost} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqAdamantium)}>Adamantium Ingots in pack: {adamantiumCount}/{UpgradeAdamantiumCost} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqGold)}>Gold (pack+bank): {goldCount:N0}/{UpgradeGoldCost:N0} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqT4)}>Jacob's Deepdelver Pickaxe (non-exhausted) in pack: {(hasT4 ? "Yes" : "No")}</BASEFONT>";

        AddHtml(16, 56, W - 32, H - 130, html, false, true);

        if (allMet)
        {
            AddButton(18, H - 68, 4011, 4012, 10);
            AddLabel(44, H - 66, 999, "Forge the Jacob's Worldbreaker Pickaxe");
        }
        else
        {
            AddLabel(18, H - 68, 0x22, "Requirements not met. See above.");
        }
    }

    private JacobsDeepdelverPickaxe? FindT4InPack()
    {
        if (_pm.FindItemOnLayer(Layer.TwoHanded) is JacobsDeepdelverPickaxe eq && !eq.Exhausted)
            return eq;

        if (_pm.Backpack == null) return null;

        foreach (var item in _pm.Backpack.Items)
            if (item is JacobsDeepdelverPickaxe p && !p.Exhausted)
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

        var data            = ClusterFAccountPersistence.GetOrCreate(acct);
        var pack            = _pm.Backpack;
        if (pack == null) return;

        data.GuildReputation.TryGetValue("mining", out var standing);
        data.GuildCurrency.TryGetValue("mining", out var vouchers);
        var mining          = _pm.Skills[SkillName.Mining].Value;
        var ironCount       = pack.GetAmount(typeof(IronIngot));
        var valoriteCount   = pack.GetAmount(typeof(ValoriteIngot));
        var adamantiumCount = pack.GetAmount(typeof(AdamantiumIngot));
        var srcPickaxe      = FindT4InPack();
        var bypass          = DevTestingCrystal.IsActive(_pm);

        if (srcPickaxe == null
            || (!bypass && (standing < UpgradeStandingReq || mining < UpgradeSkillReq
                || vouchers < UpgradeVoucherCost || ironCount < UpgradeIronCost
                || valoriteCount < UpgradeValoriteCost || adamantiumCount < UpgradeAdamantiumCost
                || CompactGoldHelper.GetTotalGold(_pm) < UpgradeGoldCost)))
        {
            _pm.SendMessage(0x22, "Requirements no longer met. Upgrade cancelled.");
            _pm.SendGump(new JacobsT5UpgradeGump(_pm));
            return;
        }

        if (!bypass)
        {
            data.GuildCurrency["mining"] = vouchers - UpgradeVoucherCost;
            pack.ConsumeTotal(typeof(IronIngot),       UpgradeIronCost);
            pack.ConsumeTotal(typeof(ValoriteIngot),   UpgradeValoriteCost);
            pack.ConsumeTotal(typeof(AdamantiumIngot), UpgradeAdamantiumCost);
            CompactGoldHelper.ConsumeGold(_pm, UpgradeGoldCost);
        }

        srcPickaxe.Exhausted = true;
        srcPickaxe.Delete();
        ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_deepdelver_pickaxe");

        var upgraded = new JacobsWorldbreakerPickaxe();
        pack.DropItem(upgraded);

        _pm.SendMessage(0x44,
            "Jacob's Worldbreaker Pickaxe is forged. " +
            "The Compact has no higher honour to bestow. " +
            "May the veins of Britannia yield endlessly to your hand.");
        _pm.PlaySound(0x35D);
    }
}
