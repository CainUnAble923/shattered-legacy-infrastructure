using System;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

/// <summary>
/// Jacob's Reinforced Pickaxe — Tier 2 upgrade of Jacob's Pickaxe.
///
/// Stats:
///   - Name: "Jacob's Reinforced Pickaxe"
///   - Blessed: yes (cannot be looted)
///   - Mining skill bonus: +10
///   - UsesRemaining: 400 (very durable)
///   - Hue: 0x8A5C (Dull Copper — matches the upgrade material)
///
/// Upgrade requirements (from Miners' Compact Liaison):
///   - Compact member
///   - Apprentice rank (1,000 Compact Standing)
///   - Mining skill 75.0+
///   - 50 Mining Vouchers
///   - 1,000 Iron Ingots
///   - 250 Dull Copper Ingots
///   - 25,000 gold
///   - Existing Jacob's Pickaxe (non-exhausted) consumed
///
/// Durability MVP (same as Tier 1):
///   - When UsesRemaining hits 0 the item enters an exhausted state (Hue = 0x0415)
///     instead of being deleted. Cannot be used while exhausted.
///   - Registry entry "legacy.jacobs_reinforced_pickaxe" is cleared so the player
///     can request restoration (Phase 3 restoration flow).
///
/// Exhausted state: tracked via Hue — functional = 0x8A5C, exhausted = 0x0415.
/// Serialization: v0 — no custom fields beyond Pickaxe. Hue is serialized by base Item.
/// </summary>
[SerializationGenerator(0, false)]
public partial class JacobsReinforcedPickaxe : Pickaxe
{
    private const int FunctionalHue = 0x8A5C; // Dull Copper
    private const int ExhaustedHue  = 0x0415; // Charcoal — exhausted state

    [Constructible]
    public JacobsReinforcedPickaxe()
    {
        Name          = "Jacob's Reinforced Pickaxe";
        UsesRemaining = 400;
        LootType      = LootType.Blessed;
        SkillBonuses.SetValues(0, SkillName.Mining, 10.0);
        Hue = FunctionalHue;
    }

    /// <summary>Whether this pickaxe is in the exhausted state. Backed by Hue.</summary>
    public bool Exhausted
    {
        get => Hue == ExhaustedHue;
        set
        {
            Hue = value ? ExhaustedHue : FunctionalHue;
            // Suppress the "uses remaining" tooltip while exhausted.
            // IUsesRemaining.ShowUsesRemaining is not virtual so we set it via interface cast.
            ((IUsesRemaining)this).ShowUsesRemaining = !value;
            InvalidateProperties();
        }
    }

    /// <summary>
    /// Prevent equipping an exhausted pickaxe. Standard pattern for all tier tools.
    /// </summary>
    public override bool OnEquip(Mobile from)
    {
        if (Exhausted)
        {
            from.SendMessage(0x22,
                "Jacob's Reinforced Pickaxe is exhausted and cannot be equipped. " +
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
            list.Add("<BASEFONT COLOR=#AAAAAA>Tier 2 Miners' Compact Legacy Tool</BASEFONT>");
    }

    // ── Registry unlock on acquisition ────────────────────────────────────────

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
            // T1 safety-net unlock — ensures a T1 record exists if somehow T2 was obtained
            // without going through the normal T1 acquisition path.
            // Do NOT set T1 HasActiveCopy here — acquiring T2 doesn't mean you have a T1.
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_pickaxe", "item_acquisition");

            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_reinforced_pickaxe", "item_acquisition");

            // Mark T2 active copy whenever a non-exhausted T2 pickaxe enters the player's possession.
            // Exhausted replacement drops are skipped — ClearActiveCopy was already called in OnDelete.
            if (!Exhausted)
            {
                var entry = ClusterFRestorationRegistry.GetEntry(acct, "legacy.jacobs_reinforced_pickaxe");
                if (entry != null) entry.HasActiveCopy = true;
            }
        }
    }

    // ── Exhausted state ───────────────────────────────────────────────────────

    public override void OnDoubleClick(Mobile from)
    {
        if (Exhausted)
        {
            from.SendMessage(0x22,
                "Jacob's Reinforced Pickaxe is exhausted. " +
                "Speak with the Miners' Compact Liaison in New Haven to have it restored.");
            return;
        }

        base.OnDoubleClick(from);
    }

    // ── Durability MVP — non-disposable behaviour ─────────────────────────────

    /// <summary>
    /// When the pickaxe's uses run out, create an exhausted replacement instead of
    /// deleting the item. Handles both in-pack (Parent is Container) and equipped
    /// (Parent is PlayerMobile) states.
    /// </summary>
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
                    ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_reinforced_pickaxe");

                var replacement = new JacobsReinforcedPickaxe();
                replacement.Exhausted = true;
                pack.DropItem(replacement);

                owner.SendMessage(0x22,
                    "Jacob's Reinforced Pickaxe has worn out. " +
                    "Speak with the Miners' Compact Liaison in New Haven to restore it.");
            }
        }

        base.OnDelete();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Upgrade gump — opened from the Miners' Compact Liaison Member Dashboard
// ─────────────────────────────────────────────────────────────────────────────

public class JacobsUpgradeGump : Gump
{
    private readonly PlayerMobile _pm;

    private const int UpgradeVoucherCost    = 50;
    private const int UpgradeIronCost       = 1000;
    private const int UpgradeDullCopperCost = 250;
    private const int UpgradeGoldCost       = 25000;
    private const int UpgradeStandingReq    = 1000;  // Apprentice rank
    private const double UpgradeSkillReq    = 75.0;

    private const int W    = 440;
    private const int H    = 380;
    private const int BgId = 9270;

    public JacobsUpgradeGump(PlayerMobile pm) : base(100, 80)
    {
        _pm = pm;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        AddLabel(W / 2 - 105, 12, 1154, "Jacob's Pickaxe — Tier 2 Upgrade");
        AddLabel(W / 2 - 80,  28, 999,  "Miners' Compact Liaison");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        var acct = pm.Account as IAccount;
        var data = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : null;

        if (data == null)
        {
            AddLabel(18, 56, 999, "Account data unavailable.");
        }
        else
        {
            DrawUpgradeInfo(data, acct!);
        }

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

        var ironCount       = pack?.GetAmount(typeof(IronIngot))       ?? 0;
        var dullCopperCount = pack?.GetAmount(typeof(DullCopperIngot)) ?? 0;
        var goldCount       = CompactGoldHelper.GetTotalGold(_pm);
        var hasPickaxe      = FindPickaxeInPack() != null;

        var bypass = DevTestingCrystal.IsActive(_pm);

        var reqMet_rank    = bypass || standing        >= UpgradeStandingReq;
        var reqMet_skill   = bypass || mining          >= UpgradeSkillReq;
        var reqMet_voucher = bypass || vouchers        >= UpgradeVoucherCost;
        var reqMet_iron    = bypass || ironCount       >= UpgradeIronCost;
        var reqMet_copper  = bypass || dullCopperCount >= UpgradeDullCopperCost;
        var reqMet_gold    = bypass || goldCount       >= UpgradeGoldCost;
        var reqMet_pickaxe = hasPickaxe; // T1 pickaxe always required — consumed in upgrade

        var allMet = reqMet_rank && reqMet_skill && reqMet_voucher
                  && reqMet_iron && reqMet_copper && reqMet_gold && reqMet_pickaxe;

        static string Clr(bool met) => met ? "#FFD700" : "#FF6666";

        var html =
            $"<BASEFONT COLOR={Clr(reqMet_rank)}>Rank: Apprentice required — Standing {standing:N0}/{UpgradeStandingReq:N0}</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqMet_skill)}>Mining skill: {mining:F1}/{UpgradeSkillReq:F0} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqMet_voucher)}>Mining Vouchers: {vouchers}/{UpgradeVoucherCost} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqMet_iron)}>Iron Ingots in pack: {ironCount}/{UpgradeIronCost} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqMet_copper)}>Dull Copper Ingots in pack: {dullCopperCount}/{UpgradeDullCopperCost} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqMet_gold)}>Gold (pack+bank): {goldCount:N0}/{UpgradeGoldCost:N0} required</BASEFONT><BR>" +
            $"<BASEFONT COLOR={Clr(reqMet_pickaxe)}>Jacob's Pickaxe (non-exhausted) in pack: {(hasPickaxe ? "Yes" : "No")}</BASEFONT>";

        AddHtml(16, 56, W - 32, H - 130, html, false, true);

        if (allMet)
        {
            AddButton(18, H - 68, 4011, 4012, 10);
            AddLabel(44, H - 66, 999, "Upgrade to Jacob's Reinforced Pickaxe");
        }
        else
        {
            AddLabel(18, H - 68, 0x22, "Requirements not met. See above.");
        }
    }

    /// <summary>
    /// Locate a non-exhausted Jacob's Pickaxe (T1) to consume for the upgrade.
    /// Checks the equipped two-hand layer first, then the backpack.
    /// Exhausted pickaxes cannot be equipped (OnEquip blocks it), but we check
    /// both locations for safety.
    /// </summary>
    private JacobsPickaxe? FindPickaxeInPack()
    {
        // Check equipped two-hand layer
        if (_pm.FindItemOnLayer(Layer.TwoHanded) is JacobsPickaxe equipped && !equipped.Exhausted)
            return equipped;

        if (_pm.Backpack == null) return null;

        foreach (var item in _pm.Backpack.Items)
        {
            if (item is JacobsPickaxe p && !p.Exhausted)
                return p;
        }

        return null;
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return;

        if (info.ButtonID == 1) // Back → Member Dashboard
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

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        var pack = _pm.Backpack;
        if (pack == null) return;

        data.GuildReputation.TryGetValue("mining", out var standing);
        data.GuildCurrency.TryGetValue("mining", out var vouchers);
        var mining      = _pm.Skills[SkillName.Mining].Value;
        var ironCount   = pack.GetAmount(typeof(IronIngot));
        var copperCount = pack.GetAmount(typeof(DullCopperIngot));
        var srcPickaxe  = FindPickaxeInPack();

        var bypass = DevTestingCrystal.IsActive(_pm);

        if (srcPickaxe == null // T1 always required
            || (!bypass && (standing < UpgradeStandingReq || mining < UpgradeSkillReq
                || vouchers < UpgradeVoucherCost || ironCount < UpgradeIronCost
                || copperCount < UpgradeDullCopperCost
                || CompactGoldHelper.GetTotalGold(_pm) < UpgradeGoldCost)))
        {
            _pm.SendMessage(0x22, "Requirements no longer met. Upgrade cancelled.");
            _pm.SendGump(new JacobsUpgradeGump(_pm));
            return;
        }

        // Consume costs (skipped when testing token is active; T1 pickaxe always consumed)
        if (!bypass)
        {
            data.GuildCurrency["mining"] = vouchers - UpgradeVoucherCost;
            pack.ConsumeTotal(typeof(IronIngot),       UpgradeIronCost);
            pack.ConsumeTotal(typeof(DullCopperIngot), UpgradeDullCopperCost);
            CompactGoldHelper.ConsumeGold(_pm, UpgradeGoldCost);
        }

        // Consume source pickaxe — mark exhausted first to suppress the durability replacement
        srcPickaxe.Exhausted = true;
        srcPickaxe.Delete();

        // Clear tier-1 active copy
        ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_pickaxe");

        // Deliver the upgraded tool
        var upgraded = new JacobsReinforcedPickaxe();
        pack.DropItem(upgraded);

        _pm.SendMessage(0x44,
            "Jacob's Reinforced Pickaxe is yours. The Compact acknowledges your dedication. " +
            "Keep it well — only the Compact can restore it.");
        _pm.PlaySound(0x35D);
    }
}
