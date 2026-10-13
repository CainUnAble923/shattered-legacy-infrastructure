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
        AddAlphaRegion(6, 6, W - 12, H - 12);

        AddLabel(W / 2 - 95, 12, 1154, "The Civic Mule Exchange");
        AddLabel(W / 2 - 80, 28, 999, "Mule tier upgrades");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        var guild = pm.Account is IAccount acct
            ? ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(pm.Serial)
            : new CharacterGuildData();

        DrawMules(guild);

        AddImageTiled(10, H - 38, W - 20, 2, 9304);
        if (_back != null)
        {
            AddButton(18, H - 28, 4014, 4016, BtnBack);
            AddLabel(52, H - 26, 999, "Back");
        }
        AddButton(W - 50, H - 28, 4023, 4025, 0);
        AddLabel(W - 28, H - 26, 1154, "X");
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

        var y = 58;

        foreach (var (mule, i) in mules.Take(3).Select((m, i) => (m, i)))
        {
            var step = MuleUpgrades.StepFrom(mule.Tier);
            AddLabel(16, y, 1154, $"{mule.Name}: tier {mule.Tier}{(mule.IsLegendaryLine ? ", Legendary line" : "")}");

            if (step == null)
            {
                AddLabel(30, y + 20, 999, "At the top tier (Celestial).");
                y += 50;
                continue;
            }

            var next = MuleData.Tier(step.FromTier + 1);
            var shortfalls = bypass ? new System.Collections.Generic.List<string>() : MuleUpgrades.Shortfalls(_pm, guild, step);

            var cost = string.Join(", ", step.Scrip.Select(s => $"{s.Amount} {MuleUpgrades.CurrencyName(s.Guild)}")) +
                       $", {step.Gold:N0} gold" +
                       (step.CompactRank > 0 ? $"; Miners' Compact rank {step.CompactRank}+" : "");

            AddHtml(30, y + 20, W - 50, 54,
                $"<BASEFONT COLOR=#AAAAAA>To {next.Name} (tier {next.Tier}): {cost}</BASEFONT>" +
                (shortfalls.Count > 0 ? $"<BR><BASEFONT COLOR=#FF6666>Short: {shortfalls[0]}</BASEFONT>" : ""),
                false, false);

            AddButton(30, y + 78, 4011, 4012, BtnUpgradeBase + i);
            AddLabel(64, y + 80, shortfalls.Count == 0 ? 0x44 : 0x22, $"Upgrade to {next.Name}");
            y += 118;
        }

        if (bypass)
        {
            AddLabel(16, H - 58, 0x480, "[Testing Token active: costs bypassed]");
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
