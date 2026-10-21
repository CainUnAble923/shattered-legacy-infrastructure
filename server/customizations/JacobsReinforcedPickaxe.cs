using System;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

/// <summary>
/// Jacob's Reinforced Pickaxe - Tier 2 upgrade of Jacob's Pickaxe.
///
/// Stats:
///   - Name: "Jacob's Reinforced Pickaxe"
///   - Blessed: yes (cannot be looted)
///   - Mining skill bonus: +10
///   - UsesRemaining: 400 (very durable)
///   - Hue: Gold, as the Reinforced Ore Satchel (cc-P55 Part E; was 0x8A5C, which carries the 0x8000 flag bit)
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
/// Exhausted state: tracked via Hue - functional = Gold (cc-P55), exhausted = 0x0415.
/// Serialization: v0 - no custom fields beyond Pickaxe. Hue is serialized by base Item.
/// </summary>
[SerializationGenerator(0, false)]
public partial class JacobsReinforcedPickaxe : Pickaxe
{
    // cc-P55 Part E (D72): the hue of the ore satchel of the same tier, read from the metal's own entry. A saved pickaxe in
    // the old hue takes it on load; the exhausted state (ExhaustedHue, unchanged) is kept as saved.
    public const CraftResource TierMetal = CraftResource.Gold; // Satchel tier 2
    private const int LegacyFunctionalHue = 0x8A5C;
    private static int FunctionalHue => CraftResources.GetHue(TierMetal);
    private const int ExhaustedHue  = 0x0415; // Charcoal - exhausted state

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

    // cc-P55 Part E: a working pickaxe saved in the old hue takes the new one. An exhausted one keeps ExhaustedHue, so it
    // stays exhausted; any other hue (one staff set) is kept.
    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (Hue == LegacyFunctionalHue)
        {
            Hue = FunctionalHue;
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
            list.Add("(Exhausted - speak with the Miners' Compact Liaison)");
        else
            list.Add("<BASEFONT COLOR=#AAAAAA>Tier 2 Miners' Compact Legacy Tool</BASEFONT>");
    }

    // -- Registry unlock on acquisition ----------------------------------------

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
            // T1 safety-net unlock - ensures a T1 record exists if somehow T2 was obtained
            // without going through the normal T1 acquisition path.
            // Do NOT set T1 HasActiveCopy here - acquiring T2 doesn't mean you have a T1.
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_pickaxe", "item_acquisition");

            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_reinforced_pickaxe", "item_acquisition");

            // Mark T2 active copy whenever a non-exhausted T2 pickaxe enters the player's possession.
            // Exhausted replacement drops are skipped - ClearActiveCopy was already called in OnDelete.
            if (!Exhausted)
            {
                var entry = ClusterFRestorationRegistry.GetEntry(acct, "legacy.jacobs_reinforced_pickaxe");
                if (entry != null) entry.HasActiveCopy = true;
            }
        }
    }

    // -- Exhausted state -------------------------------------------------------

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

    // -- Durability MVP - non-disposable behaviour -----------------------------

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

// -----------------------------------------------------------------------------
// Upgrade gump - opened from the Miners' Compact Liaison Member Dashboard
// -----------------------------------------------------------------------------

public class JacobsUpgradeGump : Gump
{
    private readonly PlayerMobile _pm;

    private const int UpgradeVoucherCost    = 50;
    private const int UpgradeIronCost       = 1000;
    private const int UpgradeDullCopperCost = 250;
    private const int UpgradeGoldCost       = 25000;

    // F-11: materials come from the pack and the bank (GuildResources), gold from both through CompactGoldHelper.
    private static GuildCost[] Materials => new[] { GuildCost.Of<IronIngot>(UpgradeIronCost), GuildCost.Of<DullCopperIngot>(UpgradeDullCopperCost) };
    private const int UpgradeStandingReq    = 1000;  // Apprentice rank
    private const double UpgradeSkillReq    = 75.0;

    private const int W    = 440;
    private readonly int H; // cc-P169: from what the page shows
    private const int BgId = 9270;

    public JacobsUpgradeGump(PlayerMobile pm) : base(100, 80)
    {
        _pm = pm;

        Closable   = true;
        Disposable = true;

        var acct = pm.Account as IAccount;
        var guild = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(pm.Serial) : null;
        var reqs = guild != null ? Requirements(guild) : null;

        // Title 56, the bonus text, the lines, summary and action (60), the footer 46.
        H = 56 + IntroHeight + (reqs?.Height ?? 18) + 60 + 46;

        AddBackground(0, 0, W, H, BgId);
        RequirementStyle.AddSolidPanel(this, W, H); // cc-P169: opaque (was an alpha region)

        AddLabel(W / 2 - 105, 12, RequirementStyle.HeadingHue, "Jacob's Pickaxe - Tier 2 Upgrade");
        AddLabel(W / 2 - 80,  28, 999,  "Miners' Compact Liaison");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        if (reqs == null)
            AddLabel(18, 56, 999, "Account data unavailable.");
        else
            DrawUpgradeInfo(reqs);

        AddImageTiled(10, H - 38, W - 20, 2, 9304);

        AddButton(18, H - 28, 4014, 4015, 1);
        AddLabel(40, H - 26, 999, "Back");

        AddButton(W - 50, H - 28, 4023, 4025, 0);
        AddLabel(W - 28, H - 26, RequirementStyle.HeadingHue, "X");
    }

    // cc-P169: the bonus text above the requirements, gray (#ACACAC, the info hue as the client draws it).
    private const string Intro = "";

    private static int IntroHeight =>
        Intro.Length == 0 ? 0 : ClusterFGumpText.Lines(Intro, W - 40) * ClusterFGumpText.LineHeight + 8;

    /// <summary>cc-P169: the upgrade's requirements, one line each; HandleUpgrade checks the same again.</summary>
    internal RequirementList Requirements(CharacterGuildData guild)
    {
        guild.GuildReputation.TryGetValue("mining", out var standing);

        return new RequirementList()
            .Rank("Apprentice", standing, UpgradeStandingReq)
            .Skill("Mining", _pm.Skills[SkillName.Mining].Value, UpgradeSkillReq)
            .Price(_pm, guild, "mining", "Mining Vouchers", UpgradeVoucherCost, UpgradeGoldCost, Materials)
            .Has("Jacob's Pickaxe in pack, not exhausted", FindPickaxeInPack() != null);
    }

    private void DrawUpgradeInfo(RequirementList reqs)
    {
        var y = 56;

        if (Intro.Length > 0)
        {
            AddHtml(16, y, W - 32, IntroHeight, $"<BASEFONT COLOR=#ACACAC>{Intro}</BASEFONT>", false, false);
            y += IntroHeight;
        }

        y = reqs.Draw(this, 18, y);
        reqs.DrawFooter(this, 18, y + 4, 10, "Upgrade to Jacob's Reinforced Pickaxe", DevTestingCrystal.IsActive(_pm));
    }

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

        if (info.ButtonID == 1) // Back -> Member Dashboard
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

        var guild = ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(_pm.Serial);
        var pack = _pm.Backpack;
        if (pack == null) return;

        guild.GuildReputation.TryGetValue("mining", out var standing);
        var mining      = _pm.Skills[SkillName.Mining].Value;
        var srcPickaxe  = FindPickaxeInPack();

        var bypass = DevTestingCrystal.IsActive(_pm);

        if (srcPickaxe == null // T1 always required
            || (!bypass && (standing < UpgradeStandingReq || mining < UpgradeSkillReq)))
        {
            _pm.SendMessage(0x22, "Requirements no longer met. Upgrade cancelled.");
            _pm.SendGump(new JacobsUpgradeGump(_pm));
            return;
        }

        // Vouchers, materials (pack then bank) and gold, all or nothing; skipped when the testing token is active.
        if (!bypass && !GuildResources.TryPay(_pm, guild, "mining", UpgradeVoucherCost, "Mining Vouchers", UpgradeGoldCost, Materials))
        {
            _pm.SendGump(new JacobsUpgradeGump(_pm));
            return;
        }

        // Consume source pickaxe - mark exhausted first to suppress the durability replacement
        srcPickaxe.Exhausted = true;
        srcPickaxe.Delete();

        // Clear tier-1 active copy
        ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_pickaxe");

        // Deliver the upgraded tool
        var upgraded = new JacobsReinforcedPickaxe();
        pack.DropItem(upgraded);

        _pm.SendMessage(0x44,
            "Jacob's Reinforced Pickaxe is yours. The Compact acknowledges your dedication. " +
            "Keep it well - only the Compact can restore it.");
        _pm.PlaySound(0x35D);
    }
}
