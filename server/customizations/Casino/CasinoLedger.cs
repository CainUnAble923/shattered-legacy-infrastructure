// CasinoLedger.cs
//
// cc-P811: the casino's books, so the sink can be seen. Every play is counted: gold taken in and paid out, per game, all time
// and today. A daily wager limit per account. Blackjack bets on the table at a save are kept, and handed back to the bank at
// the next start, so a restart in the middle of a hand never eats a bet. At every world save one line goes to
// Logs/casino.log: today's and all-time in, out and kept. [Casino status shows the same.
//
// Saved through pinned's GenericPersistence ("Casino"), versioned; an unknown version fails loudly (D40).

using System;
using System.Collections.Generic;
using System.IO;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;

namespace Server.Casino;

public enum CasinoGame
{
    Slots,
    Blackjack
}

public class CasinoLedger : GenericPersistence
{
    public const int CurrentVersion = 0;

    private static CasinoLedger _instance;

    // [game] = gold; all time, then today.
    public static readonly long[] In = new long[2], Out = new long[2], DayIn = new long[2], DayOut = new long[2];
    public static DateTime Day { get; private set; } = DateTime.MinValue;

    // Gold each account has played today (both games).
    public static readonly Dictionary<string, long> Wagered = new(StringComparer.OrdinalIgnoreCase);

    // Blackjack bets on the table, by the player who made them.
    public static readonly Dictionary<Mobile, int> OpenBets = new();

    // Read at load, handed back at world load.
    private static readonly List<(Mobile Player, int Gold)> _refunds = [];

    public static Func<DateTime> Clock = () => Core.Now;

    public CasinoLedger() : base("Casino", 10)
    {
    }

    public static void Configure()
    {
        _instance ??= new CasinoLedger();
        EventSink.WorldLoad += () => RefundAfterRestart();
    }

    public static long Kept(CasinoGame g) => In[(int)g] - Out[(int)g];
    public static long KeptToday(CasinoGame g) => DayIn[(int)g] - DayOut[(int)g];

    private static void RollDay()
    {
        var today = Clock().Date;
        if (today == Day)
        {
            return;
        }

        Day = today;
        Array.Clear(DayIn);
        Array.Clear(DayOut);
        Wagered.Clear();
    }

    private static string AccountOf(Mobile m) => m.Account?.Username ?? $"serial {m.Serial}";

    /// <summary>What this player may still play today.</summary>
    public static long Allowance(Mobile m)
    {
        RollDay();
        return Math.Max(0, CasinoSettings.Current.DailyWagerLimit - Wagered.GetValueOrDefault(AccountOf(m)));
    }

    /// <summary>
    /// Takes a stake: refuses Young players (unless allowed), the daily limit and a short purse; else takes the gold
    /// (backpack first, then bank) and books it in. Returns null on success, or the reason.
    /// </summary>
    public static string TryTake(PlayerMobile pm, int gold, CasinoGame game)
    {
        if (!CasinoSettings.Current.Enabled)
        {
            return "The house is closed.";
        }

        if (pm.Young && !CasinoSettings.Current.AllowYoung)
        {
            return "The house does not take the coin of young adventurers.";
        }

        if (gold > Allowance(pm))
        {
            return $"That would pass today's limit: you may play {Allowance(pm):N0} more gold today.";
        }

        if (!CompactGoldHelper.ConsumeGold(pm, gold))
        {
            return "You don't have that much gold, in your pack or the bank.";
        }

        Wagered[AccountOf(pm)] = Wagered.GetValueOrDefault(AccountOf(pm)) + gold;
        In[(int)game] += gold;
        DayIn[(int)game] += gold;
        return null;
    }

    /// <summary>Pays a win: gold in the backpack when it fits, else the bank. Books it out.</summary>
    public static void Pay(PlayerMobile pm, int gold, CasinoGame game)
    {
        if (gold <= 0)
        {
            return;
        }

        RollDay();
        Out[(int)game] += gold;
        DayOut[(int)game] += gold;

        if (gold > 60000 || pm.Backpack?.TryDropItem(pm, new Gold(gold), false) != true)
        {
            Banker.Deposit(pm, gold);
            pm.SendMessage(0x44, $"{gold:N0} gold has been placed in your bank.");
        }
    }

    public static List<string> Status()
    {
        RollDay();
        var s = CasinoSettings.Current;
        var lines = new List<string>
        {
            $"Casino {(s.Enabled ? "open" : "closed")}. Slot table pays back {s.SlotPayback():0.00}% (house edge {100 - s.SlotPayback():0.00}%); blackjack pays {s.BlackjackPaysNumerator}:{s.BlackjackPaysDenominator}, dealer {(s.DealerHitsSoft17 ? "hits" : "stands on")} soft 17.",
        };

        foreach (var g in new[] { CasinoGame.Slots, CasinoGame.Blackjack })
        {
            lines.Add($"{g}: today in {DayIn[(int)g]:N0}, out {DayOut[(int)g]:N0}, kept {KeptToday(g):N0}; all time in {In[(int)g]:N0}, out {Out[(int)g]:N0}, kept {Kept(g):N0}.");
        }

        lines.Add($"Gold taken out of the world in all: {Kept(CasinoGame.Slots) + Kept(CasinoGame.Blackjack):N0}. Bets on the table now: {OpenBets.Count}.");
        return lines;
    }

    private static void WriteLog()
    {
        try
        {
            var dir = Path.Combine(Core.BaseDirectory, "Logs");
            Directory.CreateDirectory(dir);
            var s = CasinoGame.Slots;
            var b = CasinoGame.Blackjack;
            File.AppendAllText(Path.Combine(dir, "casino.log"),
                $"{Clock():yyyy-MM-dd HH:mm} today slots in {DayIn[0]} out {DayOut[0]} | blackjack in {DayIn[1]} out {DayOut[1]} | " +
                $"all time kept slots {Kept(s)} blackjack {Kept(b)} total {Kept(s) + Kept(b)}\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Casino: could not write Logs/casino.log ({ex.Message}).");
        }
    }

    public override void Serialize(IGenericWriter writer)
    {
        RollDay();
        writer.WriteEncodedInt(CurrentVersion);

        for (var g = 0; g < 2; g++)
        {
            writer.Write(In[g]);
            writer.Write(Out[g]);
            writer.Write(DayIn[g]);
            writer.Write(DayOut[g]);
        }

        writer.Write(Day);
        writer.WriteEncodedInt(Wagered.Count);
        foreach (var (account, gold) in Wagered)
        {
            writer.Write(account);
            writer.Write(gold);
        }

        writer.WriteEncodedInt(OpenBets.Count);
        foreach (var (m, gold) in OpenBets)
        {
            writer.Write(m);
            writer.WriteEncodedInt(gold);
        }

        WriteLog();
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"CasinoLedger version {version} is not one this build can read.");
        }

        for (var g = 0; g < 2; g++)
        {
            In[g] = reader.ReadLong();
            Out[g] = reader.ReadLong();
            DayIn[g] = reader.ReadLong();
            DayOut[g] = reader.ReadLong();
        }

        Day = reader.ReadDateTime();
        Wagered.Clear();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            Wagered[reader.ReadString()] = reader.ReadLong();
        }

        _refunds.Clear();
        OpenBets.Clear();
        count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var m = reader.ReadEntity<Mobile>();
            var gold = reader.ReadEncodedInt();
            if (m != null)
            {
                _refunds.Add((m, gold));
            }
        }
    }

    /// <summary>World load: a hand cut off by the restart gives its bet back to the bank, and is booked as never played.</summary>
    public static int RefundAfterRestart()
    {
        var n = 0;
        foreach (var (m, gold) in _refunds)
        {
            if (m.Deleted)
            {
                continue;
            }

            Banker.Deposit(m, gold);
            In[(int)CasinoGame.Blackjack] -= gold;
            n++;
        }

        _refunds.Clear();
        if (n > 0)
        {
            Console.WriteLine($"Casino: handed back {n} blackjack bet(s) cut off by the restart.");
        }

        return n;
    }

    /// <summary>For the facts: an empty book.</summary>
    public static void ResetForTests()
    {
        Array.Clear(In);
        Array.Clear(Out);
        Array.Clear(DayIn);
        Array.Clear(DayOut);
        Day = DateTime.MinValue;
        Wagered.Clear();
        OpenBets.Clear();
        _refunds.Clear();
    }
}
