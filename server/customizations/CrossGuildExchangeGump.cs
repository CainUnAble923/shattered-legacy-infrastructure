using System;
using System.Linq;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server;

// -----------------------------------------------------------------------------------------------------------------------
// The Civic Mule Exchange (opened from the trade guilds' Member Dashboards).
//
// cc-P69 (F-33): it no longer sells a mule. The base mule is earned through the League's Compact Mule errand
// (MuleErrand, at the League Registrar); this window tiers the player's mule up, one step at a time, paying the step's
// guild currencies, gold and Miners' Compact rank (MuleUpgrades.Steps). Tier 1 -> 2 is the old Exchange price.
// The player's mules here are the one they ride and any following within 12 tiles.
// -----------------------------------------------------------------------------------------------------------------------

public class CrossGuildExchangeGump : Gump
{
    private readonly PlayerMobile          _pm;
    private readonly Action<PlayerMobile>? _back; // cc-P52 Part E (D65): reopens the guild menu that opened this

    public const int BtnBack = 1;
    private const int BtnUpgradeBase = 100; // + index of the mule in MuleUpgrades.MulesOf

    private const int W    = 460;
    private const int H    = 460;
    private const int BgId = 9270;

    public CrossGuildExchangeGump(PlayerMobile pm, Action<PlayerMobile>? back = null) : base(100, 80)
    {
        _pm   = pm;
        _back = back;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, BgId);
        RequirementStyle.AddSolidPanel(this, W, H); // cc-P169: opaque (was an alpha region)

        AddLabel(W / 2 - 95, 12, RequirementStyle.HeadingHue, "The Civic Mule Exchange");
        AddLabel(W / 2 - 80, 28, 999, "Mule tier upgrades");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        var guild = pm.Account is IAccount acct
            ? ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(pm.Serial)
            : new CharacterGuildData();

        AddImageTiled(10, H - 38, W - 20, 2, 9304);
        if (_back != null)
        {
            AddButton(18, H - 28, 4014, 4016, BtnBack);
            AddLabel(52, H - 26, 999, "Back");
        }
        AddButton(W - 50, H - 28, 4023, 4025, 0);
        AddLabel(W - 28, H - 26, RequirementStyle.HeadingHue, "X");

        DrawMules(guild); // last: its pages follow the frame (page 0, on every page)
    }

    private void DrawMules(CharacterGuildData guild)
    {
        var mules = MuleUpgrades.MulesOf(_pm);
        var bypass = Items.DevTestingCrystal.IsActive(_pm);

        if (mules.Count == 0)
        {
            AddHtml(16, 60, W - 32, 120,
                "<BASEFONT COLOR=#AAAAAA>Bring your pack mule here (riding it, or following you) to raise its tier. " +
                "A first mule comes from the League: ask the Registrar about the Compact's errand.</BASEFONT>", false, false);
            return;
        }

        // cc-P169: one mule a page (each step's requirements are a line each now), met / not met each, with the upgrade
        // button only when all are met. MuleUpgrades.TryUpgrade checks them all again on the press.
        var shown = mules.Take(3).ToList();

        for (var i = 0; i < shown.Count; i++)
        {
            var mule = shown[i];
            AddPage(i + 1);

            var step = MuleUpgrades.StepFrom(mule.Tier);
            AddLabel(16, 58, RequirementStyle.HeadingHue, $"{mule.Name}: tier {mule.Tier}{(mule.IsLegendaryLine ? ", Legendary line" : "")}");

            if (step == null)
            {
                AddLabel(30, 82, 999, "At the top tier (Celestial).");
            }
            else
            {
                var next = MuleData.Tier(step.FromTier + 1);
                AddLabel(16, 80, 999, $"To {next.Name} (tier {next.Tier}):");

                var reqs = MuleUpgrades.Requirements(_pm, guild, step);
                var y = reqs.Draw(this, 18, 104);
                reqs.DrawFooter(this, 18, y + 4, BtnUpgradeBase + i, $"Upgrade to {next.Name}", bypass);
            }

            if (i + 1 < shown.Count)
            {
                AddButton(W - 60, H - 64, 4005, 4007, 0, GumpButtonType.Page, i + 2);
                AddLabel(W - 120, H - 62, 999, "Next mule");
            }

            if (i > 0)
            {
                AddButton(18, H - 64, 4014, 4016, 0, GumpButtonType.Page, i);
                AddLabel(52, H - 62, 999, "Previous mule");
            }

            if (shown.Count > 1)
            {
                AddLabel(W / 2 - 40, H - 62, 999, $"Mule {i + 1} of {shown.Count}");
            }
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == BtnBack)
        {
            _back?.Invoke(_pm);
            return;
        }

        if (info.ButtonID < BtnUpgradeBase || _pm.Account is not IAccount acct)
        {
            return;
        }

        var mules = MuleUpgrades.MulesOf(_pm);
        var index = info.ButtonID - BtnUpgradeBase;

        if (index >= 0 && index < mules.Count)
        {
            var guild = ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(_pm.Serial);
            MuleUpgrades.TryUpgrade(_pm, guild, mules[index]); // re-checks everything on the press
        }

        _pm.SendGump(new CrossGuildExchangeGump(_pm, _back));
    }
}
