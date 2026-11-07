// CasinoVerification.cs
//
// cc-P811: the small casino. Notes in shard-migration notes/cc-P811-casino.md.
//
// Facts: the slot table pays back a little under what it takes, exactly and by simulation, and a table at or over the cap is
// refused; a spin takes the bet and pays the pay table; the house refuses the far, the dead, the young, a strange bet, an
// empty purse and the daily limit; blackjack's rules (soft hands, the dealer on soft 17, the outcomes, 6:5) and its edge over
// a million simulated hands; the table deals, hits, stands, doubles and pays; a bet cut off by a restart comes back; the
// books save and load and an unknown version is refused; the casino places itself once; a bad settings file is refused whole.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Casino;
using Server.Items;
using Server.Mobiles;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CasinoVerification : IDisposable
{
    private static readonly Point3D Spot = new(5440, 3840, 0);
    private readonly ITestOutputHelper _out;
    private readonly List<Account> _accounts = [];
    private readonly List<Item> _items = [];
    private readonly Func<int, int> _roll;
    private readonly Func<int> _draw;

    public CasinoVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        CasinoSettings.Current = new CasinoSettings();
        CasinoLedger.ResetForTests();
        BlackjackGame.ResetForTests();
        _roll = SlotGame.Roll;
        _draw = BlackjackGame.Draw;
    }

    public void Dispose()
    {
        SlotGame.Roll = _roll;
        BlackjackGame.Draw = _draw;
        CasinoSettings.Current = new CasinoSettings();
        CasinoLedger.ResetForTests();
        BlackjackGame.ResetForTests();

        foreach (var item in _items)
        {
            item.Delete();
        }

        foreach (var account in _accounts)
        {
            for (var i = 0; i < account.Length; i++)
            {
                account[i]?.Delete();
            }

            Accounts.Remove(account);
        }
    }

    private T Track<T>(T item) where T : Item
    {
        _items.Add(item);
        return item;
    }

    private PlayerMobile Gambler(int gold = 100000, Point3D? at = null)
    {
        var account = new Account($"p811{Guid.NewGuid():N}"[..16], "p811-test-only") { Young = false };
        _accounts.Add(account);
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = $"P811 {Guid.NewGuid():N}"[..12] };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(at ?? Spot, Map.Trammel);
        if (gold > 0)
        {
            Banker.Deposit(pm, gold);
        }

        return pm;
    }

    private SlotMachine Machine()
    {
        var m = Track(new SlotMachine());
        m.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        return m;
    }

    private BlackjackTable Table()
    {
        var t = Track(new BlackjackTable());
        t.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, Spot.Z), Map.Trammel);
        return t;
    }

    private static int Gold(PlayerMobile pm) => CompactGoldHelper.GetTotalGold(pm);

    private static void Rig(params int[] cards)
    {
        var queue = new Queue<int>(cards);
        BlackjackGame.Draw = () => queue.Dequeue();
    }

    // ---------------------------------------------------------------- the slot machine

    [Fact]
    public void TheSlotTablePaysBackALittleUnderWhatItTakes()
    {
        var s = CasinoSettings.Current;
        var exact = s.SlotPayback();
        _out.WriteLine($"exact payback {exact:0.000}%");
        Assert.InRange(exact, 92.0, 98.0);

        // A million spins through the machine's own reel and pay functions agree with the sum.
        var rng = new Random(811);
        var total = s.Symbols.Sum(x => x.Weight);
        long paid = 0;
        const int spins = 1_000_000;
        for (var i = 0; i < spins; i++)
        {
            SlotSymbol[] reels = [SlotGame.Stop(rng.Next(total)), SlotGame.Stop(rng.Next(total)), SlotGame.Stop(rng.Next(total))];
            paid += SlotGame.Multiplier(reels);
        }

        var simulated = paid * 100.0 / spins;
        _out.WriteLine($"simulated payback {simulated:0.000}%");
        Assert.InRange(simulated, exact - 1.5, exact + 1.5);
        Assert.True(simulated < 100);
    }

    [Fact]
    public void ASlotTablePayingBackTooMuchIsRefused()
    {
        var generous = new CasinoSettings();
        generous.Symbols.Single(x => x.Name == "Gem").PaysThree = 5000;
        Assert.True(generous.SlotPayback() >= 99);

        var errors = new List<string>();
        Assert.Null(CasinoSettings.Parse(CasinoSettings.ToJson(generous), errors));
        Assert.Contains(errors, e => e.Contains("pays back"));
    }

    [Fact]
    public void ASpinTakesTheBetAndPaysThePayTable()
    {
        var pm = Gambler();
        var machine = Machine();
        var before = Gold(pm);

        SlotGame.Roll = _ => 24; // Seven, Seven, Seven: 100 times the bet
        var win = SlotGame.Play(pm, machine, 10, out var refusal);
        Assert.Null(refusal);
        Assert.Equal(100, win.Multiplier);
        Assert.Equal(1000, win.Won);
        Assert.Equal(before - 10 + 1000, Gold(pm));

        var rolls = new Queue<int>([0, 8, 15]); // Cherry, Lemon, Bell: nothing
        SlotGame.Roll = _ => rolls.Dequeue();
        var loss = SlotGame.Play(pm, machine, 100, out _);
        Assert.Equal(0, loss.Won);
        Assert.Equal(before - 10 + 1000 - 100, Gold(pm));

        rolls = new Queue<int>([0, 1, 20]); // two cherries: the bet back
        var back = SlotGame.Play(pm, machine, 100, out _);
        Assert.Equal(100, back.Won);
        Assert.Equal(before - 10 + 1000 - 100, Gold(pm));

        Assert.Equal(210, CasinoLedger.In[(int)CasinoGame.Slots]);
        Assert.Equal(1100, CasinoLedger.Out[(int)CasinoGame.Slots]);
        Assert.Equal(-890, CasinoLedger.Kept(CasinoGame.Slots));
        Assert.Contains(CasinoLedger.Status(), l => l.StartsWith("Slots: today in 210, out 1,100"));

        // The machine's window plays the same way.
        SlotGame.Roll = _ => 26; // three Gems
        var gump = new SlotGump(pm, machine, 10, null);
        gump.Respond(pm, SlotGump.BtnSpin);
        Assert.Equal(before - 10 + 1000 - 100 - 10 + 10000, Gold(pm));
    }

    [Fact]
    public void TheHouseRefusesTheFarTheDeadTheYoungAStrangeBetAnEmptyPurseAndTheDailyLimit()
    {
        var machine = Machine();
        SlotGame.Roll = _ => 0;

        var far = Gambler(at: new Point3D(Spot.X + 10, Spot.Y, Spot.Z));
        Assert.Null(SlotGame.Play(far, machine, 10, out var why));
        Assert.Contains("too far", why);

        var ghost = Gambler();
        ghost.Kill();
        Assert.Null(SlotGame.Play(ghost, machine, 10, out _));

        var young = Gambler();
        young.Young = true;
        Assert.Null(SlotGame.Play(young, machine, 10, out why));
        Assert.Contains("young", why);

        var picky = Gambler();
        Assert.Null(SlotGame.Play(picky, machine, 7, out why));  // not a bet the machine takes
        Assert.Contains("bet", why);

        var pm = Gambler(5);
        Assert.Null(SlotGame.Play(pm, machine, 10, out why));    // only 5 gold
        Assert.Equal(5, Gold(pm));

        CasinoSettings.Current.DailyWagerLimit = 150;
        var rich = Gambler();
        Assert.NotNull(SlotGame.Play(rich, machine, 100, out _));
        Assert.Null(SlotGame.Play(rich, machine, 100, out why));
        Assert.Contains("limit", why);

        Assert.Equal(100, CasinoLedger.In[(int)CasinoGame.Slots]);
    }

    // ---------------------------------------------------------------- blackjack

    [Fact]
    public void BlackjackRulesSoftHandsTheDealerAndTheOutcomes()
    {
        Assert.Equal(21, BlackjackGame.Value([1, 13], out var soft));
        Assert.True(soft);
        Assert.True(BlackjackGame.IsNatural([1, 12]));
        Assert.Equal(12, BlackjackGame.Value([1, 1]));
        Assert.Equal(13, BlackjackGame.Value([1, 6, 6], out soft));
        Assert.False(soft);

        // Soft 17: the house hits it, or stands on it when told to.
        var hits = new List<int> { 1, 6 };
        BlackjackGame.DealerPlays(hits, () => 2, true);
        Assert.Equal(19, BlackjackGame.Value(hits));
        var stands = new List<int> { 1, 6 };
        BlackjackGame.DealerPlays(stands, () => 2, false);
        Assert.Equal(17, BlackjackGame.Value(stands));

        Assert.Equal(BlackjackOutcome.Win, BlackjackGame.Judge([10, 9], [10, 6, 10]));
        Assert.Equal(BlackjackOutcome.Push, BlackjackGame.Judge([10, 8], [10, 8]));
        Assert.Equal(BlackjackOutcome.Lose, BlackjackGame.Judge([10, 7], [10, 8]));
        Assert.Equal(BlackjackOutcome.Bust, BlackjackGame.Judge([10, 7, 9], [10, 8]));

        Assert.Equal(220, BlackjackGame.Payout(BlackjackOutcome.Natural, 100, 6, 5));
        Assert.Equal(250, BlackjackGame.Payout(BlackjackOutcome.Natural, 100, 3, 2));
        Assert.Equal(200, BlackjackGame.Payout(BlackjackOutcome.Win, 100, 6, 5));
        Assert.Equal(100, BlackjackGame.Payout(BlackjackOutcome.Push, 100, 6, 5));
        Assert.Equal(0, BlackjackGame.Payout(BlackjackOutcome.Lose, 100, 6, 5));
    }

    // Sound, simple play: double 10 and 11 against 2 to 9, stand on 17 and up (hard), 13-16 against 2 to 6, 12 against
    // 4 to 6, hit soft 17 and under. Through the table's own rule functions.
    private static double PlayerReturn(int hands, int seed, int num, int den, bool hitsSoft17)
    {
        var rng = new Random(seed);
        int Draw() => rng.Next(13) + 1;
        long staked = 0, back = 0;

        for (var h = 0; h < hands; h++)
        {
            var bet = 100;
            List<int> p = [Draw()], d = [Draw()];
            p.Add(Draw());
            d.Add(Draw());
            staked += bet;

            if (BlackjackGame.IsNatural(p) || BlackjackGame.IsNatural(d))
            {
                var o = BlackjackGame.IsNatural(p) ? BlackjackGame.IsNatural(d) ? BlackjackOutcome.Push : BlackjackOutcome.Natural : BlackjackOutcome.Lose;
                back += BlackjackGame.Payout(o, bet, num, den);
                continue;
            }

            var up = BlackjackGame.CardValue(d[0]) == 1 ? 11 : BlackjackGame.CardValue(d[0]);
            var v = BlackjackGame.Value(p, out var soft);
            if (!soft && v is 10 or 11 && up <= 9)
            {
                staked += bet;
                bet *= 2;
                p.Add(Draw());
            }
            else
            {
                while (true)
                {
                    v = BlackjackGame.Value(p, out soft);
                    var hit = soft ? v <= 17 : v <= 11 || v == 12 && up is < 4 or > 6 || v is >= 13 and <= 16 && up >= 7;
                    if (!hit || v >= 21)
                    {
                        break;
                    }

                    p.Add(Draw());
                }
            }

            if (BlackjackGame.Value(p) <= 21)
            {
                BlackjackGame.DealerPlays(d, Draw, hitsSoft17);
            }

            back += BlackjackGame.Payout(BlackjackGame.Judge(p, d), bet, num, den);
        }

        return back * 100.0 / staked;
    }

    [Fact]
    public void TheBlackjackRulesKeepAFewPercentForTheHouse()
    {
        var house = PlayerReturn(1_000_000, 811, 6, 5, true);
        var classic = PlayerReturn(1_000_000, 811, 3, 2, false);
        _out.WriteLine($"6:5, hits soft 17: the player gets back {house:0.000}%; 3:2, stands on soft 17: {classic:0.000}%");

        Assert.InRange(house, 93.0, 99.5); // the house keeps a few percent
        Assert.True(house < classic);       // and the house rules are what keep it
    }

    [Fact]
    public void TheTableDealsHitsStandsDoublesAndPays()
    {
        var pm = Gambler();
        var table = Table();
        var start = Gold(pm);

        // 10 + 8 against 9 + 7: stand, the dealer draws a 10 and busts.
        Rig(10, 9, 8, 7, 10);
        var hand = BlackjackGame.Deal(pm, table, 100, out var why);
        Assert.Null(why);
        Assert.True(hand.Live);
        Assert.Equal(100, CasinoLedger.OpenBets[pm]);
        Assert.Null(BlackjackGame.Deal(pm, table, 100, out why)); // one hand at a time
        Assert.Null(BlackjackGame.Stand(pm));
        Assert.Equal(BlackjackOutcome.Win, hand.Outcome);
        Assert.Equal(start + 100, Gold(pm));
        Assert.False(CasinoLedger.OpenBets.ContainsKey(pm));

        // A natural pays 6:5.
        Rig(1, 9, 13, 7);
        hand = BlackjackGame.Deal(pm, table, 100, out _);
        Assert.Equal(BlackjackOutcome.Natural, hand.Outcome);
        Assert.Equal(start + 100 + 120, Gold(pm));

        // Hit to a bust.
        Rig(10, 9, 6, 7, 10);
        hand = BlackjackGame.Deal(pm, table, 100, out _);
        Assert.Null(BlackjackGame.Hit(pm));
        Assert.Equal(BlackjackOutcome.Bust, hand.Outcome);
        Assert.Equal(start + 120, Gold(pm));

        // Double on 11: a ten makes 21; the dealer's 16 draws a 2.
        Rig(5, 9, 6, 7, 10, 2);
        var gump = new BlackjackGump(pm, table, 100);
        gump.Respond(pm, BlackjackGump.BtnDeal);
        hand = BlackjackGame.Hands[pm];
        new BlackjackGump(pm, table, 100).Respond(pm, BlackjackGump.BtnDouble);
        Assert.True(hand.Doubled);
        Assert.Equal(200, hand.Bet);
        Assert.Equal(BlackjackOutcome.Win, hand.Outcome);
        Assert.Equal(start + 120 + 200, Gold(pm));

        Assert.Equal(100 + 100 + 100 + 200, CasinoLedger.In[(int)CasinoGame.Blackjack]);
        Assert.Equal(200 + 220 + 400, CasinoLedger.Out[(int)CasinoGame.Blackjack]);
    }

    [Fact]
    public void ABetCutOffByARestartComesBackAndTheBooksSave()
    {
        var pm = Gambler();
        var table = Table();
        var start = Gold(pm);
        SlotGame.Roll = _ => 0;
        SlotGame.Play(pm, Machine(), 1000, out _); // Cherry x3: 8000 back

        Rig(10, 9, 8, 7);
        BlackjackGame.Deal(pm, table, 500, out _);
        Assert.Equal(start - 1000 + 8000 - 500, Gold(pm));

        var ledger = new CasinoLedger();
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        ledger.Serialize(writer);
        writer.Flush();

        CasinoLedger.ResetForTests();
        BlackjackGame.ResetForTests();
        ledger.Deserialize(new BufferReader(buffer));

        Assert.Equal(1000, CasinoLedger.In[(int)CasinoGame.Slots]);
        Assert.Equal(8000, CasinoLedger.Out[(int)CasinoGame.Slots]);
        Assert.Equal(500, CasinoLedger.In[(int)CasinoGame.Blackjack]);
        Assert.Equal(1500, CasinoLedger.Wagered.Values.Single());

        Assert.Equal(1, CasinoLedger.RefundAfterRestart());
        Assert.Equal(0, CasinoLedger.RefundAfterRestart()); // once
        Assert.Equal(start - 1000 + 8000, Gold(pm));
        Assert.Equal(0, CasinoLedger.In[(int)CasinoGame.Blackjack]);

        var bad = new byte[16];
        var w2 = new BufferWriter(bad, true);
        w2.WriteEncodedInt(CasinoLedger.CurrentVersion + 1);
        w2.Flush();
        Assert.Throws<InvalidDataException>(() => ledger.Deserialize(new BufferReader(bad)));
    }

    [Fact]
    public void TheSavedMachineAndTableLoad()
    {
        var m = Track(new SlotMachine { Where = "a test hall" });
        var buffer = new byte[4096];
        var writer = new BufferWriter(buffer, true);
        m.Serialize(writer);
        writer.Flush();
        var copy = (SlotMachine)Activator.CreateInstance(typeof(SlotMachine), m.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        Assert.Equal("a test hall", copy.Where);

        var t = Table();
        t.Dress();
        Item cards = null;
        foreach (var i in t.GetItemsAt())
        {
            if (i is Static { ItemID: 0xFA3 })
            {
                cards = i;
            }
        }

        Assert.NotNull(cards);
        t.Location = new Point3D(t.X + 1, t.Y, t.Z);
        Assert.Equal(new Point3D(t.X, t.Y, t.Z + 6), cards.Location);
        t.Delete();
        Assert.True(cards.Deleted);
    }

    [Fact]
    public void TheCasinoPlacesItselfOnceAndNotWhenClosed()
    {
        CasinoSettings.Current.Placements =
        [
            new() { Kind = "Slot", X = Spot.X + 3, Y = Spot.Y + 3, Z = 0 },
            new() { Kind = "Blackjack", X = Spot.X + 5, Y = Spot.Y + 3, Z = 0 },
        ];

        CasinoSettings.Current.Enabled = false;
        Assert.Equal(0, CasinoSeeder.Seed());

        CasinoSettings.Current.Enabled = true;
        Assert.Equal(2, CasinoSeeder.Seed());
        Assert.Equal(0, CasinoSeeder.Seed());

        foreach (var p in CasinoSettings.Current.Placements)
        {
            var found = new List<Item>();
            foreach (var item in Map.Trammel.GetItemsAt(p.X, p.Y))
            {
                if (item is SlotMachine or BlackjackTable)
                {
                    found.Add(item);
                }
            }

            _items.AddRange(found);
        }

        Assert.Equal(2, _items.Count(i => i is SlotMachine or BlackjackTable));

        // The defaults stand in the Haven inn's east room, on Trammel.
        var defaults = new CasinoSettings().Placements;
        Assert.Equal(2, defaults.Count(p => p.Kind == "Slot"));
        Assert.Single(defaults, p => p.Kind == "Blackjack");
    }

    [Fact]
    public void ABadSettingsFileIsRefusedWhole()
    {
        var errors = new List<string>();
        Assert.Null(CasinoSettings.Parse("{\"BlackjackPaysNumerator\": 2, \"BlackjackPaysDenominator\": 1}", errors)); // 2:1
        Assert.NotEmpty(errors);

        errors.Clear();
        Assert.Null(CasinoSettings.Parse("{\"SlotBets\": []}", errors));

        errors.Clear();
        Assert.Null(CasinoSettings.Parse("{\"Placements\": [{\"Kind\": \"Roulette\"}]}", errors));

        errors.Clear();
        var ok = CasinoSettings.Parse(CasinoSettings.ToJson(new CasinoSettings { DailyWagerLimit = 1000 }), errors);
        Assert.Empty(errors);
        Assert.Equal(1000, ok.DailyWagerLimit);
    }
}
