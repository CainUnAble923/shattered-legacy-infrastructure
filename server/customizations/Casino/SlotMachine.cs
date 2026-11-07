// SlotMachine.cs
//
// cc-P811: the casino's slot machine, our own build. Gold only. CEO's slot machines (third-party, reference only; credit to
// CEO of EasyUO / Alexandria) gave the idea of a weighted reel and a pay table; no code was taken, and their power scroll,
// stat scroll and item themes are gone.
//
// Three reels share one weighted strip (CasinoSettings.Symbols). Three of a kind pay their multiple of the bet; exactly two
// cherries give the bet back. The exact payback is CasinoSettings.SlotPayback (94.8% with the defaults, a 5.2% edge), and a
// table at or over MaxPaybackPercent is refused. A win of NewsAtMultiplier times the bet, or of NewsAtGold, is told to the
// shard in the System's voice.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Casino;

public sealed record SlotResult(SlotSymbol[] Reels, int Multiplier, int Bet, int Won);

public static class SlotGame
{
    public const int NewsHue = 0x35;

    // A reel stop from 0 to total weight - 1; the facts replace it.
    public static Func<int, int> Roll = Utility.Random;

    private static CasinoSettings S => CasinoSettings.Current;

    public static SlotSymbol Stop(int roll)
    {
        foreach (var s in S.Symbols)
        {
            if (roll < s.Weight)
            {
                return s;
            }

            roll -= s.Weight;
        }

        return S.Symbols[^1];
    }

    /// <summary>What three reels pay, as a multiple of the bet.</summary>
    public static int Multiplier(SlotSymbol[] reels)
    {
        if (reels[0] == reels[1] && reels[1] == reels[2])
        {
            return reels[0].PaysThree;
        }

        var cherries = reels.Count(r => r.Name.Equals("Cherry", StringComparison.OrdinalIgnoreCase));
        return cherries == 2 ? S.PaysTwoCherries : 0;
    }

    public static SlotSymbol[] Spin()
    {
        var total = S.Symbols.Sum(s => s.Weight);
        return [Stop(Roll(total)), Stop(Roll(total)), Stop(Roll(total))];
    }

    /// <summary>One play: takes the bet, spins, pays. Returns the result, or null and the reason.</summary>
    public static SlotResult Play(PlayerMobile pm, SlotMachine machine, int bet, out string refusal)
    {
        refusal = null;

        if (machine is not { Deleted: false } || pm.Map != machine.Map || !pm.InRange(machine.GetWorldLocation(), S.UseRange))
        {
            refusal = "You are too far from the machine.";
            return null;
        }

        if (!pm.Alive)
        {
            refusal = "The dead cannot play.";
            return null;
        }

        if (!S.SlotBets.Contains(bet))
        {
            refusal = "The machine does not take that bet.";
            return null;
        }

        refusal = CasinoLedger.TryTake(pm, bet, CasinoGame.Slots);
        if (refusal != null)
        {
            return null;
        }

        var reels = Spin();
        var mult = Multiplier(reels);
        var won = (int)Math.Min(int.MaxValue, (long)bet * mult);
        CasinoLedger.Pay(pm, won, CasinoGame.Slots);

        Effects.PlaySound(machine.Location, machine.Map, won > 0 ? 0x2E6 : 0x4F);

        if (won > 0)
        {
            pm.SendMessage(0x44, $"{string.Join(" ", reels.Select(r => r.Name))}: you win {won:N0} gold!");
        }

        if (won > 0 && (mult >= S.NewsAtMultiplier || won >= S.NewsAtGold))
        {
            World.Broadcast(NewsHue, false,
                $"System: {pm.Name} lines up three {reels[0].Name}s on the slot machine in {machine.Where} and wins {won:N0} gold!");
        }

        return new SlotResult(reels, mult, bet, won);
    }
}

[SerializationGenerator(0)]
public partial class SlotMachine : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _where = "the Haven inn";

    [Constructible]
    public SlotMachine() : base(CasinoSettings.Current.SlotItemID)
    {
        Movable = false;
        Hue = CasinoSettings.Current.SlotHue;
    }

    public override string DefaultName => "a slot machine";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060658, $"Bets\t{string.Join(", ", CasinoSettings.Current.SlotBets.Select(b => b.ToString("N0")))} gold"); // ~1_val~: ~2_val~
        list.Add(1060659, $"Pays back\t{CasinoSettings.Current.SlotPayback():0.0}% of what is played");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        if (!from.InRange(GetWorldLocation(), CasinoSettings.Current.UseRange))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        pm.CloseGump<SlotGump>();
        pm.SendGump(new SlotGump(pm, this, CasinoSettings.Current.SlotBets[0], null));
    }
}

public class SlotGump : Gump
{
    public const int BtnSpin = 1;
    public const int BtnBetBase = 10;

    private readonly SlotMachine _machine;
    private readonly int _bet;

    public SlotGump(PlayerMobile pm, SlotMachine machine, int bet, SlotResult last) : base(80, 80)
    {
        _machine = machine;
        _bet = bet;
        var s = CasinoSettings.Current;

        AddBackground(0, 0, 360, 250, 9270);
        AddImageTiled(14, 14, 332, 222, 2624);
        AddLabel(30, 22, 0x386, "Slot machine");
        AddLabel(200, 22, 0x3B2, $"Pays back {s.SlotPayback():0.0}%");

        // The reels.
        for (var i = 0; i < 3; i++)
        {
            AddBackground(30 + i * 104, 52, 92, 44, 9350);
            var sym = last?.Reels[i];
            AddLabel(44 + i * 104, 64, sym?.Hue ?? 0x3B2, sym?.Name ?? "-");
        }

        AddLabel(30, 106, last is { Won: > 0 } ? 0x44 : 1153,
            last == null ? "Choose a bet and pull." : last.Won > 0 ? $"You win {last.Won:N0} gold!" : "No win this time.");

        AddLabel(30, 132, 0x386, "Bet");
        for (var i = 0; i < s.SlotBets.Count; i++)
        {
            var b = s.SlotBets[i];
            AddButton(30 + i * 78, 154, b == bet ? 2154 : 2151, 2154, BtnBetBase + i);
            AddLabel(64 + i * 78, 158, b == bet ? 0x386 : 1153, b.ToString("N0"));
        }

        AddButton(30, 196, 4005, 4007, BtnSpin);
        AddLabel(64, 198, 1153, $"Pull ({bet:N0} gold)");
        AddLabelCropped(190, 198, 150, 18, 0x3B2, $"Gold: {CompactGoldHelper.GetTotalGold(pm):N0}");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is PlayerMobile pm)
        {
            Respond(pm, info.ButtonID);
        }
    }

    /// <summary>A button press without a client packet; public for the facts.</summary>
    public void Respond(PlayerMobile pm, int id)
    {
        var s = CasinoSettings.Current;
        if (id >= BtnBetBase && id < BtnBetBase + s.SlotBets.Count)
        {
            pm.SendGump(new SlotGump(pm, _machine, s.SlotBets[id - BtnBetBase], null));
        }
        else if (id == BtnSpin)
        {
            var result = SlotGame.Play(pm, _machine, _bet, out var refusal);
            if (refusal != null)
            {
                pm.SendMessage(0x22, refusal);
            }

            pm.SendGump(new SlotGump(pm, _machine, _bet, result));
        }
    }
}
