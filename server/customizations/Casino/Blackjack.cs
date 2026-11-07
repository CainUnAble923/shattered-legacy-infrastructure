// Blackjack.cs
//
// cc-P811: the casino's blackjack table, our own build. Gold only. CEO's video blackjack (third-party, reference only; credit
// to CEO of EasyUO / Alexandria) was read for the idea; no code was taken.
//
// One player a hand against the house, an endless shoe (each card is drawn fresh, so no counting). The house's edge is in
// its rules, all in casino.json: a natural pays 6:5 (not 3:2), the dealer hits soft 17, a player may double on the first two
// cards, and there is no splitting or insurance. With those rules and sound play the house keeps a few percent; the facts
// measure it over many simulated hands, through the same rule functions the table uses.
//
// A bet is on the table from the deal to the end of the hand (CasinoLedger.OpenBets); a restart in between hands it back.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Casino;

public enum BlackjackOutcome
{
    None,
    Natural,     // the player's two-card 21
    Win,
    Push,
    Lose,
    Bust
}

public sealed class BlackjackHand
{
    public BlackjackTable Table;
    public int Bet;
    public readonly List<int> Player = [];
    public readonly List<int> Dealer = [];
    public bool Doubled;
    public BlackjackOutcome Outcome;
    public int Paid;

    public bool Live => Outcome == BlackjackOutcome.None;
}

public static class BlackjackGame
{
    // A card's rank from 1 (ace) to 13 (king); the facts replace it.
    public static Func<int> Draw = () => Utility.Random(13) + 1;

    public static readonly Dictionary<Mobile, BlackjackHand> Hands = new();

    private static CasinoSettings S => CasinoSettings.Current;

    // ---------------------------------------------------------------- the rules, shared by the table and the facts' simulation

    public static int CardValue(int rank) => rank >= 10 ? 10 : rank;

    /// <summary>A hand's best total; soft when an ace counts 11.</summary>
    public static int Value(IReadOnlyList<int> cards, out bool soft)
    {
        var total = cards.Sum(CardValue);
        var aces = cards.Any(c => c == 1);
        soft = aces && total + 10 <= 21;
        return soft ? total + 10 : total;
    }

    public static int Value(IReadOnlyList<int> cards) => Value(cards, out _);

    public static bool IsNatural(IReadOnlyList<int> cards) => cards.Count == 2 && Value(cards) == 21;

    /// <summary>The dealer draws to 17, and on a soft 17 too when the house hits soft 17.</summary>
    public static void DealerPlays(List<int> dealer, Func<int> draw, bool hitsSoft17)
    {
        while (true)
        {
            var v = Value(dealer, out var soft);
            if (v < 17 || v == 17 && soft && hitsSoft17)
            {
                dealer.Add(draw());
                continue;
            }

            return;
        }
    }

    /// <summary>The finished hand's outcome, the dealer having played.</summary>
    public static BlackjackOutcome Judge(IReadOnlyList<int> player, IReadOnlyList<int> dealer)
    {
        int p = Value(player), d = Value(dealer);
        if (p > 21)
        {
            return BlackjackOutcome.Bust;
        }

        if (d > 21 || p > d)
        {
            return BlackjackOutcome.Win;
        }

        return p == d ? BlackjackOutcome.Push : BlackjackOutcome.Lose;
    }

    /// <summary>What the table hands back for a stake: the stake and the win, or nothing.</summary>
    public static int Payout(BlackjackOutcome outcome, int stake, int num, int den) => outcome switch
    {
        BlackjackOutcome.Natural => stake + stake * num / den,
        BlackjackOutcome.Win => stake * 2,
        BlackjackOutcome.Push => stake,
        _ => 0
    };

    // ---------------------------------------------------------------- the table

    private static string CheckReach(PlayerMobile pm, BlackjackTable table)
    {
        if (table is not { Deleted: false } || pm.Map != table.Map || !pm.InRange(table.GetWorldLocation(), S.UseRange))
        {
            return "You are too far from the table.";
        }

        return pm.Alive ? null : "The dead cannot play.";
    }

    public static BlackjackHand Deal(PlayerMobile pm, BlackjackTable table, int bet, out string refusal)
    {
        refusal = CheckReach(pm, table);
        if (refusal != null)
        {
            return null;
        }

        if (Hands.TryGetValue(pm, out var open) && open.Live)
        {
            refusal = "Finish the hand you are playing first.";
            return null;
        }

        if (!S.BlackjackBets.Contains(bet))
        {
            refusal = "The table does not take that bet.";
            return null;
        }

        refusal = CasinoLedger.TryTake(pm, bet, CasinoGame.Blackjack);
        if (refusal != null)
        {
            return null;
        }

        var hand = new BlackjackHand { Table = table, Bet = bet };
        hand.Player.Add(Draw());
        hand.Dealer.Add(Draw());
        hand.Player.Add(Draw());
        hand.Dealer.Add(Draw());
        Hands[pm] = hand;
        CasinoLedger.OpenBets[pm] = bet;

        if (IsNatural(hand.Player) || IsNatural(hand.Dealer))
        {
            var outcome = IsNatural(hand.Player)
                ? IsNatural(hand.Dealer) ? BlackjackOutcome.Push : BlackjackOutcome.Natural
                : BlackjackOutcome.Lose;
            Settle(pm, hand, outcome);
        }

        return hand;
    }

    public static string Hit(PlayerMobile pm)
    {
        if (!Hands.TryGetValue(pm, out var hand) || !hand.Live)
        {
            return "You have no hand in play.";
        }

        var reach = CheckReach(pm, hand.Table);
        if (reach != null)
        {
            return reach;
        }

        hand.Player.Add(Draw());
        if (Value(hand.Player) > 21)
        {
            Settle(pm, hand, BlackjackOutcome.Bust);
        }
        else if (Value(hand.Player) == 21)
        {
            Stand(pm);
        }

        return null;
    }

    public static string Stand(PlayerMobile pm)
    {
        if (!Hands.TryGetValue(pm, out var hand) || !hand.Live)
        {
            return "You have no hand in play.";
        }

        DealerPlays(hand.Dealer, Draw, S.DealerHitsSoft17);
        Settle(pm, hand, Judge(hand.Player, hand.Dealer));
        return null;
    }

    public static string Double(PlayerMobile pm)
    {
        if (!Hands.TryGetValue(pm, out var hand) || !hand.Live)
        {
            return "You have no hand in play.";
        }

        if (!S.AllowDouble || hand.Player.Count != 2)
        {
            return "You may only double on your first two cards.";
        }

        var reach = CheckReach(pm, hand.Table);
        if (reach != null)
        {
            return reach;
        }

        var refusal = CasinoLedger.TryTake(pm, hand.Bet, CasinoGame.Blackjack);
        if (refusal != null)
        {
            return refusal;
        }

        hand.Bet *= 2;
        hand.Doubled = true;
        CasinoLedger.OpenBets[pm] = hand.Bet;
        hand.Player.Add(Draw());

        if (Value(hand.Player) > 21)
        {
            Settle(pm, hand, BlackjackOutcome.Bust);
            return null;
        }

        return Stand(pm);
    }

    private static void Settle(PlayerMobile pm, BlackjackHand hand, BlackjackOutcome outcome)
    {
        hand.Outcome = outcome;
        hand.Paid = Payout(outcome, hand.Bet, S.BlackjackPaysNumerator, S.BlackjackPaysDenominator);
        CasinoLedger.OpenBets.Remove(pm);
        CasinoLedger.Pay(pm, hand.Paid, CasinoGame.Blackjack);

        if (hand.Table != null)
        {
            Effects.PlaySound(hand.Table.Location, hand.Table.Map, hand.Paid > hand.Bet ? 0x2E6 : 0x4F);
        }

        if (hand.Paid - hand.Bet >= S.NewsAtGold)
        {
            World.Broadcast(SlotGame.NewsHue, false,
                $"System: {pm.Name} beats the house at blackjack in {hand.Table?.Where ?? "the casino"} and wins {hand.Paid - hand.Bet:N0} gold!");
        }
    }

    public static string CardName(int rank) => rank switch
    {
        1 => "A",
        11 => "J",
        12 => "Q",
        13 => "K",
        _ => rank.ToString()
    };

    /// <summary>For the facts: no hands.</summary>
    public static void ResetForTests() => Hands.Clear();
}

[SerializationGenerator(0)]
public partial class BlackjackTable : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _where = "the Haven inn";

    [SerializableField(1)]
    private Item _cards;

    [Constructible]
    public BlackjackTable() : base(CasinoSettings.Current.TableItemID)
    {
        Movable = false;
    }

    public override string DefaultName => "a blackjack table";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        var s = CasinoSettings.Current;
        list.Add(1060658, $"Blackjack pays\t{s.BlackjackPaysNumerator}:{s.BlackjackPaysDenominator}"); // ~1_val~: ~2_val~
        list.Add(1060659, $"Bets\t{string.Join(", ", s.BlackjackBets.Select(b => b.ToString("N0")))} gold");
    }

    // The cards and chips lying on the table: plain decoration that moves and goes with it.
    public void Dress()
    {
        if (_cards is { Deleted: false } || Map == null || Map == Map.Internal)
        {
            return;
        }

        _cards = new Static(0xFA3) { Name = "playing cards" };
        _cards.MoveToWorld(new Point3D(X, Y, Z + 6), Map);
    }

    public override void OnLocationChange(Point3D oldLocation)
    {
        base.OnLocationChange(oldLocation);
        if (_cards is { Deleted: false })
        {
            _cards.Location = new Point3D(X, Y, Z + 6);
        }
    }

    public override void OnMapChange()
    {
        base.OnMapChange();
        if (_cards is { Deleted: false })
        {
            _cards.Map = Map;
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        _cards?.Delete();
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

        pm.CloseGump<BlackjackGump>();
        pm.SendGump(new BlackjackGump(pm, this, CasinoSettings.Current.BlackjackBets[0]));
    }
}

public class BlackjackGump : Gump
{
    public const int BtnDeal = 1;
    public const int BtnHit = 2;
    public const int BtnStand = 3;
    public const int BtnDouble = 4;
    public const int BtnBetBase = 10;

    private readonly BlackjackTable _table;
    private readonly int _bet;

    public BlackjackGump(PlayerMobile pm, BlackjackTable table, int bet) : base(80, 80)
    {
        _table = table;
        _bet = bet;
        var s = CasinoSettings.Current;
        BlackjackGame.Hands.TryGetValue(pm, out var hand);
        var live = hand is { Live: true };

        AddBackground(0, 0, 400, 280, 9270);
        AddImageTiled(14, 14, 372, 252, 2624);
        AddLabel(30, 22, 0x386, "Blackjack");
        AddLabel(150, 22, 0x3B2, $"Pays {s.BlackjackPaysNumerator}:{s.BlackjackPaysDenominator}; dealer {(s.DealerHitsSoft17 ? "hits" : "stands on")} soft 17");

        if (hand != null)
        {
            var dealer = live
                ? $"{BlackjackGame.CardName(hand.Dealer[0])}  ?"
                : $"{string.Join("  ", hand.Dealer.Select(BlackjackGame.CardName))}   ({BlackjackGame.Value(hand.Dealer)})";
            AddLabel(30, 52, 0x386, "Dealer");
            AddLabel(100, 52, 1153, dealer);
            AddLabel(30, 82, 0x386, "You");
            AddLabel(100, 82, 1153, $"{string.Join("  ", hand.Player.Select(BlackjackGame.CardName))}   ({BlackjackGame.Value(hand.Player)})");
            AddLabel(30, 106, 0x3B2, $"Bet {hand.Bet:N0} gold{(hand.Doubled ? ", doubled" : "")}");

            if (!live)
            {
                var text = hand.Outcome switch
                {
                    BlackjackOutcome.Natural => $"Blackjack! You are paid {hand.Paid:N0} gold.",
                    BlackjackOutcome.Win => $"You win: {hand.Paid:N0} gold.",
                    BlackjackOutcome.Push => "A push: your bet comes back.",
                    BlackjackOutcome.Bust => "Bust. The house takes the bet.",
                    _ => "The house wins."
                };
                AddLabel(30, 130, hand.Paid > hand.Bet ? 0x44 : 0x22, text);
            }
        }
        else
        {
            AddLabel(30, 60, 1153, "Choose a bet and deal.");
        }

        if (live)
        {
            AddButton(30, 170, 4005, 4007, BtnHit);
            AddLabel(64, 172, 1153, "Hit");
            AddButton(130, 170, 4005, 4007, BtnStand);
            AddLabel(164, 172, 1153, "Stand");
            if (s.AllowDouble && hand.Player.Count == 2)
            {
                AddButton(230, 170, 4005, 4007, BtnDouble);
                AddLabel(264, 172, 1153, "Double");
            }
        }
        else
        {
            AddLabel(30, 160, 0x386, "Bet");
            for (var i = 0; i < s.BlackjackBets.Count; i++)
            {
                var b = s.BlackjackBets[i];
                AddButton(30 + i * 86, 182, b == bet ? 2154 : 2151, 2154, BtnBetBase + i);
                AddLabel(64 + i * 86, 186, b == bet ? 0x386 : 1153, b.ToString("N0"));
            }

            AddButton(30, 226, 4005, 4007, BtnDeal);
            AddLabel(64, 228, 1153, $"Deal ({bet:N0} gold)");
        }

        AddLabelCropped(230, 228, 150, 18, 0x3B2, $"Gold: {CompactGoldHelper.GetTotalGold(pm):N0}");
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
        string refusal = null;
        var bet = _bet;

        if (id == 0)
        {
            return;
        }

        if (id >= BtnBetBase && id < BtnBetBase + s.BlackjackBets.Count)
        {
            bet = s.BlackjackBets[id - BtnBetBase];
        }
        else
        {
            switch (id)
            {
                case BtnDeal:
                    BlackjackGame.Deal(pm, _table, _bet, out refusal);
                    break;
                case BtnHit:
                    refusal = BlackjackGame.Hit(pm);
                    break;
                case BtnStand:
                    refusal = BlackjackGame.Stand(pm);
                    break;
                case BtnDouble:
                    refusal = BlackjackGame.Double(pm);
                    break;
            }
        }

        if (refusal != null)
        {
            pm.SendMessage(0x22, refusal);
        }

        pm.SendGump(new BlackjackGump(pm, _table, bet));
    }
}
