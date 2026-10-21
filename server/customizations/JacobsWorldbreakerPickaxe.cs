using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

/// <summary>
/// Jacob's Worldbreaker Pickaxe - Tier 5 (final) upgrade of Jacob's Deepdelver Pickaxe.
///
/// Stats:
///   - Name: "Jacob's Worldbreaker Pickaxe"
///   - Blessed: yes
///   - Mining skill bonus: +25
///   - UsesRemaining: 1,200
///   - Hue: Platinum, as the Master Expedition Satchel (cc-P55 Part E; was 0x0B2A)
///
/// Special - Worldbreaker's Edge (combines T3 + T4 + new bonus):
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
/// Exhausted state: tracked via Hue. Functional = Platinum (cc-P55), exhausted = 0x0415.
/// Serialization: v0 - no custom fields. Hue serialized by base Item.
/// </summary>
[SerializationGenerator(0, false)]
public partial class JacobsWorldbreakerPickaxe : Pickaxe
{
    // cc-P55 Part E (D72): the hue of the ore satchel of the same tier, read from the metal's own entry. A saved pickaxe in
    // the old hue takes it on load; the exhausted state (ExhaustedHue, unchanged) is kept as saved.
    public const CraftResource TierMetal = CraftResource.Platinum; // Satchel tier 5
    private const int LegacyFunctionalHue = 0x0B2A;
    private static int FunctionalHue => CraftResources.GetHue(TierMetal);
    private const int ExhaustedHue  = 0x0415; // Charcoal - exhausted state

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
            list.Add("(Exhausted - speak with the Miners' Compact Liaison)");
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

// -----------------------------------------------------------------------------
// T5 Upgrade Gump - opened from the Miners' Compact Liaison Member Dashboard
// -----------------------------------------------------------------------------

public class JacobsT5UpgradeGump : Gump
{
    private readonly PlayerMobile _pm;

    private const int UpgradeVoucherCost    = 500;
    private const int UpgradeIronCost       = 3000;
    private const int UpgradeValoriteCost   = 1000;
    private const int UpgradeAdamantiumCost = 200;
    private const int UpgradeGoldCost       = 300000;

    // F-11: materials come from the pack and the bank (GuildResources), gold from both through CompactGoldHelper.
    private static GuildCost[] Materials => new[] { GuildCost.Of<IronIngot>(UpgradeIronCost), GuildCost.Of<ValoriteIngot>(UpgradeValoriteCost), GuildCost.Of<AdamantiumIngot>(UpgradeAdamantiumCost) };
    private const int UpgradeStandingReq    = 80000;  // Deepwarden rank
    private const double UpgradeSkillReq    = 100.0;  // GM Mining

    private const int W    = 440;
    private readonly int H; // cc-P169: from what the page shows
    private const int BgId = 9270;

    public JacobsT5UpgradeGump(PlayerMobile pm) : base(100, 80)
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

        AddLabel(W / 2 - 120, 12, RequirementStyle.HeadingHue, "Jacob's Pickaxe - Tier 5 Upgrade");
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
    private const string Intro = "Worldbreaker's Edge: +1 ore per yield everywhere. Stacks with Deepdelver's Advantage for +2 in Felucca. " +
            "Retains all prior bonuses.";

    private static int IntroHeight =>
        Intro.Length == 0 ? 0 : ClusterFGumpText.Lines(Intro, W - 40) * ClusterFGumpText.LineHeight + 8;

    /// <summary>cc-P169: the upgrade's requirements, one line each; HandleUpgrade checks the same again.</summary>
    internal RequirementList Requirements(CharacterGuildData guild)
    {
        guild.GuildReputation.TryGetValue("mining", out var standing);

        return new RequirementList()
            .Rank("Deepwarden", standing, UpgradeStandingReq)
            .Skill("Mining", _pm.Skills[SkillName.Mining].Value, UpgradeSkillReq)
            .Price(_pm, guild, "mining", "Mining Vouchers", UpgradeVoucherCost, UpgradeGoldCost, Materials)
            .Has("Jacob's Deepdelver Pickaxe in pack, not exhausted", FindT4InPack() != null);
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
        reqs.DrawFooter(this, 18, y + 4, 10, "Forge the Jacob's Worldbreaker Pickaxe", DevTestingCrystal.IsActive(_pm));
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

        var guild            = ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(_pm.Serial);
        var pack            = _pm.Backpack;
        if (pack == null) return;

        guild.GuildReputation.TryGetValue("mining", out var standing);
        var mining          = _pm.Skills[SkillName.Mining].Value;
        var srcPickaxe      = FindT4InPack();
        var bypass          = DevTestingCrystal.IsActive(_pm);

        if (srcPickaxe == null
            || (!bypass && (standing < UpgradeStandingReq || mining < UpgradeSkillReq)))
        {
            _pm.SendMessage(0x22, "Requirements no longer met. Upgrade cancelled.");
            _pm.SendGump(new JacobsT5UpgradeGump(_pm));
            return;
        }

        // Vouchers, materials (pack then bank) and gold, all or nothing; skipped when the testing token is active.
        if (!bypass && !GuildResources.TryPay(_pm, guild, "mining", UpgradeVoucherCost, "Mining Vouchers", UpgradeGoldCost, Materials))
        {
            _pm.SendGump(new JacobsT5UpgradeGump(_pm));
            return;
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
