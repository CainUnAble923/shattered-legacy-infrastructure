using System;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

// -----------------------------------------------------------------------------
// Cross-Guild Exchange Gump
//
// Accessible from the Member Dashboard of any of the four trade guilds:
//   Miners' Compact, Society of Smiths, Rangers' League, Foresters' Union.
//
// Allows guild members who have accumulated scrip across all four guilds to
// exchange it for a PackMuleDeed - a prestige pack animal reward.
//
// Cost:
//   25 Mining Vouchers  (Miners' Compact)
//   25 Smithing Seals   (Society of Smiths)
//   25 Trail Marks      (Rangers' League)
//   25 Timber Tokens    (Foresters' Union)
//   25,000 gold         (backpack or bank)
// -----------------------------------------------------------------------------

public class CrossGuildExchangeGump : Gump
{
    private readonly PlayerMobile          _pm;
    private readonly Action<PlayerMobile>? _back; // cc-P52 Part E (D65): reopens the guild menu that opened this

    // Exchange costs - adjust here if the economy needs rebalancing.
    private const int MiningCost   = 25;   // Mining Vouchers
    private const int SmithCost    = 25;   // Smithing Seals
    private const int RangerCost   = 25;   // Trail Marks
    private const int ForesterCost = 25;   // Timber Tokens
    private const int GoldCost     = 25_000;

    private const int BtnPurchase = 100;
    public const int BtnBack = 1;

    private const int W    = 440;
    private const int H    = 440;
    private const int BgId = 9270;

    public CrossGuildExchangeGump(PlayerMobile pm, Action<PlayerMobile>? back = null) : base(100, 80)
    {
        _pm   = pm;
        _back = back;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        // -- Header --------------------------------------------------------
        AddLabel(W / 2 - 95, 12, 1154, "The Civic Mule Exchange");
        AddLabel(W / 2 - 110, 28, 999, "A Cross-Guild Prestige Reward");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        // -- Content -------------------------------------------------------
        var acct = pm.Account as IAccount;
        var guild = acct != null
            ? ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(pm.Serial)
            : new CharacterGuildData();

        DrawMain(guild);

        // -- Footer --------------------------------------------------------
        AddImageTiled(10, H - 38, W - 20, 2, 9304);
        if (_back != null)
        {
            AddButton(18, H - 28, 4014, 4016, BtnBack);
            AddLabel(52, H - 26, 999, "Back");
        }
        AddButton(W - 50, H - 28, 4023, 4025, 0);
        AddLabel(W - 28, H - 26, 1154, "X");
    }

    private void DrawMain(CharacterGuildData guild)
    {
        guild.GuildCurrency.TryGetValue("mining",    out var mining);
        guild.GuildCurrency.TryGetValue("smithing",  out var smithing);
        guild.GuildCurrency.TryGetValue("rangers",   out var rangers);
        guild.GuildCurrency.TryGetValue("foresters", out var foresters);

        var goldAvail = CompactGoldHelper.GetTotalGold(_pm);
        var bypass    = Items.DevTestingCrystal.IsActive(_pm);

        var canAfford = bypass
            || (mining    >= MiningCost
             && smithing  >= SmithCost
             && rangers   >= RangerCost
             && foresters >= ForesterCost
             && goldAvail >= GoldCost);

        static string CostColor(bool met) => met ? "#44AA44" : "#FF6666";

        var html =
            "<BASEFONT COLOR=#AAAAAA>The Pack Mule is awarded only to those who have proved their " +
            "worth across all four of New Haven's trade guilds. This hardy beast carries a " +
            "StrongBackpack and is tougher and more resilient than a common pack horse.</BASEFONT>" +
            "<BR><BR>" +
            "<BASEFONT COLOR=#AAAAAA>The mule arrives as a deed. Double-click it to summon your " +
            "mule, pre-bonded and ready for service. It costs 1 control slot.</BASEFONT>" +
            "<BR><BR>" +
            "<BASEFONT COLOR=#FFD700>Cost (all four guild currencies + gold):</BASEFONT><BR>" +
            $"<BASEFONT COLOR={CostColor(mining    >= MiningCost  || bypass)}>  " +
            $"Mining Vouchers:  {mining} / {MiningCost}</BASEFONT><BR>" +
            $"<BASEFONT COLOR={CostColor(smithing  >= SmithCost   || bypass)}>  " +
            $"Smithing Seals:   {smithing} / {SmithCost}</BASEFONT><BR>" +
            $"<BASEFONT COLOR={CostColor(rangers   >= RangerCost  || bypass)}>  " +
            $"Trail Marks:      {rangers} / {RangerCost}</BASEFONT><BR>" +
            $"<BASEFONT COLOR={CostColor(foresters >= ForesterCost || bypass)}>  " +
            $"Timber Tokens:    {foresters} / {ForesterCost}</BASEFONT><BR>" +
            $"<BASEFONT COLOR={CostColor(goldAvail >= GoldCost    || bypass)}>  " +
            $"Gold:             {goldAvail:N0} / {GoldCost:N0}</BASEFONT>" +
            (bypass ? "<BR><BASEFONT COLOR=#FF44FF>[Testing Token active - costs bypassed]</BASEFONT>" : "");

        AddHtml(16, 56, W - 32, H - 120, html, false, true);

        // Purchase button - grayed out if player cannot afford
        AddButton(18, H - 68, 4011, 4012, BtnPurchase);
        AddLabel(44, H - 66, canAfford ? 999 : 0x22,
            "Exchange - Receive a Pack Mule Deed");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == BtnBack)
            _back?.Invoke(_pm);
        else if (info.ButtonID == BtnPurchase)
            HandlePurchase();
    }

    // -- Purchase logic --------------------------------------------------------

    private void HandlePurchase()
    {
        var acct = _pm.Account as IAccount;
        if (acct == null) return;

        var pack = _pm.Backpack;
        if (pack == null)
        {
            _pm.SendMessage(0x22, "You don't have a backpack.");
            return;
        }

        var guild   = ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(_pm.Serial);
        var bypass = Items.DevTestingCrystal.IsActive(_pm);

        guild.GuildCurrency.TryGetValue("mining",    out var mining);
        guild.GuildCurrency.TryGetValue("smithing",  out var smithing);
        guild.GuildCurrency.TryGetValue("rangers",   out var rangers);
        guild.GuildCurrency.TryGetValue("foresters", out var foresters);
        var goldAvail = CompactGoldHelper.GetTotalGold(_pm);

        if (!bypass)
        {
            if (mining < MiningCost)
            {
                _pm.SendMessage(0x22,
                    $"You need {MiningCost} Mining Vouchers (you have {mining}).");
                _pm.SendGump(new CrossGuildExchangeGump(_pm, _back));
                return;
            }
            if (smithing < SmithCost)
            {
                _pm.SendMessage(0x22,
                    $"You need {SmithCost} Smithing Seals (you have {smithing}).");
                _pm.SendGump(new CrossGuildExchangeGump(_pm, _back));
                return;
            }
            if (rangers < RangerCost)
            {
                _pm.SendMessage(0x22,
                    $"You need {RangerCost} Trail Marks (you have {rangers}).");
                _pm.SendGump(new CrossGuildExchangeGump(_pm, _back));
                return;
            }
            if (foresters < ForesterCost)
            {
                _pm.SendMessage(0x22,
                    $"You need {ForesterCost} Timber Tokens (you have {foresters}).");
                _pm.SendGump(new CrossGuildExchangeGump(_pm, _back));
                return;
            }
            if (goldAvail < GoldCost)
            {
                _pm.SendMessage(0x22,
                    $"You need {GoldCost:N0} gold in your backpack or bank " +
                    $"(you have {goldAvail:N0}).");
                _pm.SendGump(new CrossGuildExchangeGump(_pm, _back));
                return;
            }

            // Consume all four guild currencies and gold.
            guild.GuildCurrency["mining"]    = mining    - MiningCost;
            guild.GuildCurrency["smithing"]  = smithing  - SmithCost;
            guild.GuildCurrency["rangers"]   = rangers   - RangerCost;
            guild.GuildCurrency["foresters"] = foresters - ForesterCost;
            CompactGoldHelper.ConsumeGold(_pm, GoldCost);
        }

        // Deliver deed.
        var deed = new Items.PackMuleDeed();
        pack.DropItem(deed);

        _pm.SendMessage(0x44,
            "The guilds of New Haven acknowledge your service across all trades. " +
            "A Pack Mule Deed has been placed in your backpack.");
        _pm.PlaySound(0x35D);
    }
}
