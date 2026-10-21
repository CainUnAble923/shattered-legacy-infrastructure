using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

/// <summary>
/// Jacob's Prospector Pickaxe - Tier 3 upgrade of Jacob's Reinforced Pickaxe.
///
/// Stats:
///   - Name: "Jacob's Prospector Pickaxe"
///   - Blessed: yes
///   - Mining skill bonus: +18
///   - UsesRemaining: 600
///   - Hue: Verite, as the Surveyor's Ore Satchel (cc-P55 Part E; was 0x026C)
///
/// Special - Prospector's Insight:
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
/// Exhausted state: tracked via Hue. Functional = Verite (cc-P55), exhausted = 0x0415.
/// Serialization: v0 - no custom fields. Hue serialized by base Item.
/// </summary>
[SerializationGenerator(0, false)]
public partial class JacobsProspectorPickaxe : Pickaxe
{
    // cc-P55 Part E (D72): the hue of the ore satchel of the same tier, read from the metal's own entry. A saved pickaxe in
    // the old hue takes it on load; the exhausted state (ExhaustedHue, unchanged) is kept as saved.
    public const CraftResource TierMetal = CraftResource.Verite; // Satchel tier 3
    private const int LegacyFunctionalHue = 0x026C;
    private static int FunctionalHue => CraftResources.GetHue(TierMetal);
    private const int ExhaustedHue  = 0x0415; // Charcoal - exhausted state

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
            list.Add("(Exhausted - speak with the Miners' Compact Liaison)");
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

// -----------------------------------------------------------------------------
// T3 Upgrade Gump - opened from the Miners' Compact Liaison Member Dashboard
// -----------------------------------------------------------------------------

public class JacobsT3UpgradeGump : Gump
{
    private readonly PlayerMobile _pm;

    private const int UpgradeVoucherCost  = 200;
    private const int UpgradeIronCost     = 1500;
    private const int UpgradeAgapiteCost  = 500;
    private const int UpgradeGoldCost     = 50000;

    // F-11: materials come from the pack and the bank (GuildResources), gold from both through CompactGoldHelper.
    private static GuildCost[] Materials => new[] { GuildCost.Of<IronIngot>(UpgradeIronCost), GuildCost.Of<AgapiteIngot>(UpgradeAgapiteCost) };
    private const int UpgradeStandingReq  = 15000;  // Surveyor rank
    private const double UpgradeSkillReq  = 80.0;

    private const int W    = 440;
    private readonly int H; // cc-P169: from what the page shows
    private const int BgId = 9270;

    public JacobsT3UpgradeGump(PlayerMobile pm) : base(100, 80)
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

        AddLabel(W / 2 - 120, 12, RequirementStyle.HeadingHue, "Jacob's Pickaxe - Tier 3 Upgrade");
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
    private const string Intro = "Prospector's Insight: Each new ore discovery immediately credits +3 Mining Vouchers.";

    private static int IntroHeight =>
        Intro.Length == 0 ? 0 : ClusterFGumpText.Lines(Intro, W - 40) * ClusterFGumpText.LineHeight + 8;

    /// <summary>cc-P169: the upgrade's requirements, one line each; HandleUpgrade checks the same again.</summary>
    internal RequirementList Requirements(CharacterGuildData guild)
    {
        guild.GuildReputation.TryGetValue("mining", out var standing);

        return new RequirementList()
            .Rank("Surveyor", standing, UpgradeStandingReq)
            .Skill("Mining", _pm.Skills[SkillName.Mining].Value, UpgradeSkillReq)
            .Price(_pm, guild, "mining", "Mining Vouchers", UpgradeVoucherCost, UpgradeGoldCost, Materials)
            .Has("Jacob's Reinforced Pickaxe in pack, not exhausted", FindT2InPack() != null);
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
        reqs.DrawFooter(this, 18, y + 4, 10, "Upgrade to Jacob's Prospector Pickaxe", DevTestingCrystal.IsActive(_pm));
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

        var guild    = ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(_pm.Serial);
        var pack    = _pm.Backpack;
        if (pack == null) return;

        guild.GuildReputation.TryGetValue("mining", out var standing);
        var mining      = _pm.Skills[SkillName.Mining].Value;
        var srcPickaxe  = FindT2InPack();
        var bypass      = DevTestingCrystal.IsActive(_pm);

        if (srcPickaxe == null
            || (!bypass && (standing < UpgradeStandingReq || mining < UpgradeSkillReq)))
        {
            _pm.SendMessage(0x22, "Requirements no longer met. Upgrade cancelled.");
            _pm.SendGump(new JacobsT3UpgradeGump(_pm));
            return;
        }

        // Vouchers, materials (pack then bank) and gold, all or nothing; skipped when the testing token is active.
        if (!bypass && !GuildResources.TryPay(_pm, guild, "mining", UpgradeVoucherCost, "Mining Vouchers", UpgradeGoldCost, Materials))
        {
            _pm.SendGump(new JacobsT3UpgradeGump(_pm));
            return;
        }

        // Consume T2 - mark exhausted first to suppress the durability replacement drop
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
