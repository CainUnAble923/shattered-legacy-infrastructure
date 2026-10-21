using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

/// <summary>
/// Jacob's Deepdelver Pickaxe - Tier 4 upgrade of Jacob's Prospector Pickaxe.
///
/// Stats:
///   - Name: "Jacob's Deepdelver Pickaxe"
///   - Blessed: yes
///   - Mining skill bonus: +22
///   - UsesRemaining: 800
///   - Hue: Valorite, as the Deepdelver's Ore Satchel (cc-P55 Part E; was 0x0455)
///
/// Special - Deepdelver's Advantage:
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
/// Exhausted state: tracked via Hue. Functional = Valorite (cc-P55), exhausted = 0x0415.
/// Serialization: v0 - no custom fields. Hue serialized by base Item.
/// </summary>
[SerializationGenerator(0, false)]
public partial class JacobsDeepdelverPickaxe : Pickaxe
{
    // cc-P55 Part E (D72): the hue of the ore satchel of the same tier, read from the metal's own entry. A saved pickaxe in
    // the old hue takes it on load; the exhausted state (ExhaustedHue, unchanged) is kept as saved.
    public const CraftResource TierMetal = CraftResource.Valorite; // Satchel tier 4
    private const int LegacyFunctionalHue = 0x0455;
    private static int FunctionalHue => CraftResources.GetHue(TierMetal);
    private const int ExhaustedHue  = 0x0415; // Charcoal - exhausted state

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
            list.Add("(Exhausted - speak with the Miners' Compact Liaison)");
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

// -----------------------------------------------------------------------------
// T4 Upgrade Gump - opened from the Miners' Compact Liaison Member Dashboard
// -----------------------------------------------------------------------------

public class JacobsT4UpgradeGump : Gump
{
    private readonly PlayerMobile _pm;

    private const int UpgradeVoucherCost  = 350;
    private const int UpgradeIronCost     = 2000;
    private const int UpgradeValoriteCost = 500;
    private const int UpgradeGoldCost     = 150000;

    // F-11: materials come from the pack and the bank (GuildResources), gold from both through CompactGoldHelper.
    private static GuildCost[] Materials => new[] { GuildCost.Of<IronIngot>(UpgradeIronCost), GuildCost.Of<ValoriteIngot>(UpgradeValoriteCost) };
    private const int UpgradeStandingReq  = 40000;  // Master Delver rank
    private const double UpgradeSkillReq  = 90.0;

    private const int W    = 440;
    private readonly int H; // cc-P169: from what the page shows
    private const int BgId = 9270;

    public JacobsT4UpgradeGump(PlayerMobile pm) : base(100, 80)
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

        AddLabel(W / 2 - 120, 12, RequirementStyle.HeadingHue, "Jacob's Pickaxe - Tier 4 Upgrade");
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
    private const string Intro = "Deepdelver's Advantage: +1 ore per yield when mining in Felucca. Also retains Prospector's Insight " +
            "(new discoveries = +3 vouchers).";

    private static int IntroHeight =>
        Intro.Length == 0 ? 0 : ClusterFGumpText.Lines(Intro, W - 40) * ClusterFGumpText.LineHeight + 8;

    /// <summary>cc-P169: the upgrade's requirements, one line each; HandleUpgrade checks the same again.</summary>
    internal RequirementList Requirements(CharacterGuildData guild)
    {
        guild.GuildReputation.TryGetValue("mining", out var standing);

        return new RequirementList()
            .Rank("Master Delver", standing, UpgradeStandingReq)
            .Skill("Mining", _pm.Skills[SkillName.Mining].Value, UpgradeSkillReq)
            .Price(_pm, guild, "mining", "Mining Vouchers", UpgradeVoucherCost, UpgradeGoldCost, Materials)
            .Has("Jacob's Prospector Pickaxe in pack, not exhausted", FindT3InPack() != null);
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
        reqs.DrawFooter(this, 18, y + 4, 10, "Upgrade to Jacob's Deepdelver Pickaxe", DevTestingCrystal.IsActive(_pm));
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

        var guild         = ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(_pm.Serial);
        var pack         = _pm.Backpack;
        if (pack == null) return;

        guild.GuildReputation.TryGetValue("mining", out var standing);
        var mining       = _pm.Skills[SkillName.Mining].Value;
        var srcPickaxe   = FindT3InPack();
        var bypass       = DevTestingCrystal.IsActive(_pm);

        if (srcPickaxe == null
            || (!bypass && (standing < UpgradeStandingReq || mining < UpgradeSkillReq)))
        {
            _pm.SendMessage(0x22, "Requirements no longer met. Upgrade cancelled.");
            _pm.SendGump(new JacobsT4UpgradeGump(_pm));
            return;
        }

        // Vouchers, materials (pack then bank) and gold, all or nothing; skipped when the testing token is active.
        if (!bypass && !GuildResources.TryPay(_pm, guild, "mining", UpgradeVoucherCost, "Mining Vouchers", UpgradeGoldCost, Materials))
        {
            _pm.SendGump(new JacobsT4UpgradeGump(_pm));
            return;
        }

        srcPickaxe.Exhausted = true;
        srcPickaxe.Delete();
        ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_prospector_pickaxe");

        var upgraded = new JacobsDeepdelverPickaxe();
        pack.DropItem(upgraded);

        _pm.SendMessage(0x44,
            "Jacob's Deepdelver Pickaxe is yours. The veins of Felucca will yield more for you now. " +
            "Tread carefully - the deep earth does not forgive carelessness.");
        _pm.PlaySound(0x35D);
    }
}
