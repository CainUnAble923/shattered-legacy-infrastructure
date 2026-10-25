// cc-P610 (P533 U3): the tutorial's loans come back whole. Notes: shard-migration notes/cc-P610-fix-drafts-from-p533.md.
//
// Facts:
//   1. A split loaned stack: both halves go back on Stop, Reset, logout and leaving the yard, and a half moved to the
//      bank box before the next tick goes back too (P503 #1, P523 #1).
//   2. Loaned cloth cannot be cut into bandages and loaned logs cannot be axed into boards, so nothing of the loan
//      outlives the lesson that way; the lesson's own crafting still uses them (P523 #1).
//   3. A missed shot with the loaned bow recovers no ordinary arrows after Stop; a player's own bow and arrows still
//      recover their own as before (P503 #2).
//   4. A restart in the middle of the Chivalry or thieving lesson gives back only what was lent, once: the tithing loan
//      and the Hiding base (P503 #3, P291's Hiding loan in P514).
//   5. A backpack too full for the lesson's supplies lends nothing at all (no half loan, no creature, no skill or
//      tithing loan), says to make room, and the lesson starts once there is room (P503 #7).

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Accounting;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Network;
using Server.Random;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialLoanLifecycleVerification : IDisposable
{
    private readonly Func<Point3D, bool> _fits = TutorialWingTrials.Fits;

    public TutorialLoanLifecycleVerification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        ClusterFTutorial.Enabled = true;
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
        TutorialWingTrials.EnsureHooks();
        TutorialWingTrials.Fits = _ => true;
    }

    public void Dispose() => TutorialWingTrials.Fits = _fits;

    private sealed class ConstantRandom : System.Random
    {
        public double Value;

        protected override double Sample() => Value;
        public override double NextDouble() => Value;
        public override int Next() => (int)(Value * int.MaxValue);
        public override int Next(int maxValue) => (int)(Value * maxValue);
        public override int Next(int minValue, int maxValue) => minValue + (int)(Value * (maxValue - minValue));
        public override long NextInt64(long maxValue) => (long)(Value * maxValue);
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p610{Guid.NewGuid():N}"[..16], "p610-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Trainee", RawStr = 60, RawDex = 60, RawInt = 20 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(TutorialWingTrials.Yard.X, TutorialWingTrials.Yard.Y, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
            Assert.True(ClusterFTutorial.TryEnter(Pm));
        }

        public TutorialWingTrials.Trial? Trial => TutorialWingTrials.Current(Pm);

        public void Dispose()
        {
            TutorialWingTrials.Cancel(Pm);
            TutorialCoreTrials.Cancel(Pm);
            ClusterFFanfare.FlushAll();
            Pm.NetState = null;
            Ns.Mobile = null;
            Ns.Dispose();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    // Where each lesson arms: the training yard for combat and magic, the trades yard for crafting and thieving.
    private static Point2D PlaceOf(string key) =>
        key.StartsWith("crafting.", StringComparison.Ordinal) || key == "thieving.trial" ? TutorialWingTrials.TradesYard : TutorialWingTrials.Yard;

    private static void Run(Player p, string key)
    {
        var at = PlaceOf(key);
        p.Pm.MoveToWorld(new Point3D(at.X, at.Y, 0), Map.Trammel);
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find(key)!);
    }

    private static TutorialWingTrials.Trial Begin(Player p, string key)
    {
        Run(p, key);
        var trial = p.Trial!;
        Assert.Equal(key, trial.Key);
        Assert.True(trial.Armed);
        return trial;
    }

    private static T Loaned<T>(TutorialWingTrials.Trial trial) where T : Item => trial.Loaners.OfType<T>().First(i => !i.Deleted);

    private static void Tick() => ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));

    private static bool LoanerNamed(Item i) => i.Name?.StartsWith(TutorialWingTrials.LoanerPrefix, StringComparison.Ordinal) == true;

    // ---------------------------------------------------------------- 1

    [Theory]
    [InlineData("stop")]
    [InlineData("reset")]
    [InlineData("logout")]
    [InlineData("leave")]
    [InlineData("bank")]
    public void BothHalvesOfASplitLoanGoBack(string how)
    {
        using var p = new Player();
        var trial = Begin(p, "combat.healing");
        var bandages = Loaned<Bandage>(trial);
        Assert.Equal(10, bandages.Amount);

        // A lift of three from the stack of ten: the lifted three keep the loan's identity, the seven left behind are
        // a new item (Mobile.LiftItemDupe).
        Mobile.LiftItemDupe(bandages, 3);
        var remainder = p.Pm.Backpack!.Items.OfType<Bandage>().Single(b => b != bandages);
        Assert.Equal(7, remainder.Amount);

        if (how == "bank")
        {
            p.Pm.BankBox.DropItem(remainder); // before the trial's next tick has seen it
        }
        else
        {
            Tick();
        }

        switch (how)
        {
            case "stop":
            case "bank":
                TutorialWingTrials.Cancel(p.Pm);
                break;
            case "reset":
                TutorialWingTrials.Reset(p.Pm, TutorialStations.Find("combat.healing")!);
                break;
            case "logout":
                p.Pm.NetState = null;
                Tick();
                break;
            case "leave":
                p.Pm.MoveToWorld(new Point3D(TutorialWingTrials.Yard.X + 30, TutorialWingTrials.Yard.Y, 0), Map.Trammel);
                Tick();
                break;
        }

        Assert.True(bandages.Deleted, "the lifted half went back");
        Assert.True(remainder.Deleted, $"the half left behind went back ({how})");
        p.Pm.NetState = p.Ns;
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void LoanedClothAndLogsCannotBeCutOrAxedIntoSomethingThatStays()
    {
        using var p = new Player();
        var scissors = new Scissors();
        var axe = new Hatchet();
        p.Pm.Backpack!.DropItem(scissors);
        p.Pm.Backpack.DropItem(axe);

        var tailoring = Begin(p, "crafting.tailoring");
        var cloth = Loaned<Cloth>(tailoring);
        ((IScissorable)cloth).Scissor(p.Pm, scissors);
        TutorialWingTrials.Cancel(p.Pm);
        Assert.DoesNotContain(p.Pm.Backpack.Items.OfType<Bandage>(), b => !b.Deleted);
        Assert.True(cloth.Deleted);

        var fletching = Begin(p, "crafting.fletching");
        var logs = Loaned<Log>(fletching);
        logs.Axe(p.Pm, axe);
        TutorialWingTrials.Cancel(p.Pm);
        Assert.DoesNotContain(p.Pm.Backpack.Items.OfType<Board>(), b => !b.Deleted);
        Assert.True(logs.Deleted);

        // A restart's sweep would not find converted things either way: nothing loaned is left to find.
        Assert.False(scissors.Deleted, "the player's own scissors stay");
        Assert.False(axe.Deleted, "the player's own hatchet stays");
    }

    [Fact]
    public void ThePlayersOwnClothStillCutsIntoBandages()
    {
        using var p = new Player();
        var scissors = new Scissors();
        var own = new Cloth(4);
        p.Pm.Backpack!.DropItem(scissors);
        p.Pm.Backpack.DropItem(own);
        Begin(p, "crafting.tailoring");

        Assert.True(((IScissorable)own).Scissor(p.Pm, scissors));
        Assert.Equal(4, p.Pm.Backpack.Items.OfType<Bandage>().Where(b => !b.Deleted).Sum(b => b.Amount));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AMissWithTheLoanedBowRecoversNoArrowsAfterStopButAnOwnBowStillDoes()
    {
        using var p = new Player();
        var trial = Begin(p, "combat.weapons");
        Assert.True(TutorialWingTrials.PickWeapon(p.Pm, 3)); // Archery
        var bow = trial.Loaners.OfType<BaseRanged>().First();
        var echo = trial.Target!;

        BuiltInRng.Generator = new ConstantRandom { Value = 0.0 }; // every miss is recoverable (BaseRanged.OnMiss: 40%)
        try
        {
            bow.OnMiss(p.Pm, echo);
            bow.OnMiss(p.Pm, echo);
        }
        finally
        {
            BuiltInRng.Reset();
        }

        TutorialWingTrials.Cancel(p.Pm);
        p.Pm.Warmode = false;
        ShardTestClock.Advance(TimeSpan.FromSeconds(11)); // AmmoRecovery gathers once the archer is out of war mode
        Tick();

        Assert.DoesNotContain(p.Pm.Backpack!.Items, i => !i.Deleted && (i is Arrow || LoanerNamed(i)));

        // The player's own bow, after the lesson: its missed arrows come back as before.
        var own = new Bow();
        p.Pm.Backpack.DropItem(own);
        BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };
        try
        {
            own.OnMiss(p.Pm, echo);
        }
        finally
        {
            BuiltInRng.Reset();
        }

        ShardTestClock.Advance(TimeSpan.FromSeconds(11));
        Assert.Equal(1, p.Pm.Backpack.Items.OfType<Arrow>().Where(a => !a.Deleted).Sum(a => a.Amount));
    }

    // ---------------------------------------------------------------- 4

    /// <summary>A restart: trials live in memory, so they are gone; the loan ledger (if there is one) is saved and loaded;
    /// then the world-up step runs. Reflection, so the fact also runs (and fails) against the code before the fix.</summary>
    private static void Restart()
    {
        var active = typeof(TutorialWingTrials).GetField("Active", BindingFlags.NonPublic | BindingFlags.Static)!;
        ((System.Collections.IDictionary)active.GetValue(null)!).Clear();

        var ledger = typeof(TutorialWingTrials).Assembly.GetType("Server.Tutorial.TutorialLoanLedger");
        if (ledger != null)
        {
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            ledger.GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [writer]);
            writer.Flush();
            ledger.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
            ledger.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [new BufferReader(buffer)]);
        }

        TutorialWingTrials.Initialize();
    }

    [Fact]
    public void ARestartMidChivalryGivesBackOnlyTheTithingLoanOnce()
    {
        using var p = new Player();
        p.Pm.TithingPoints = 7;
        Begin(p, "magic.chivalry");
        Assert.Equal(107, p.Pm.TithingPoints);
        p.Pm.TithingPoints -= 10; // a Close Wounds paid from the loan

        Restart();
        Assert.Equal(7, p.Pm.TithingPoints); // the player's own 7; the lesson spent 10 of the loan

        Restart();
        Assert.Equal(7, p.Pm.TithingPoints); // once
    }

    [Fact]
    public void ARestartMidThievingGivesBackOnlyTheHidingLoanOnceAndKeepsTheGain()
    {
        using var p = new Player();
        var hiding = p.Pm.Skills[SkillName.Hiding];
        hiding.Base = 5;
        Begin(p, "thieving.trial");
        Assert.Equal(Server.SkillHandlers.Stealth.HidingRequirement, hiding.Base);
        hiding.Base += 1.2; // a gain during the lesson (bases are kept in tenths)
        var expected = 5 + (hiding.Base - Server.SkillHandlers.Stealth.HidingRequirement);
        Assert.True(expected > 6);

        Restart();
        Assert.Equal(expected, hiding.Base, 1);

        Restart();
        Assert.Equal(expected, hiding.Base, 1);
    }

    // ---------------------------------------------------------------- 5

    private static List<Item> Fill(Container pack, int leave)
    {
        var filler = new List<Item>();
        while (pack.TotalItems < pack.MaxItems - leave)
        {
            var i = new Item(0x1F14) { Stackable = false };
            pack.DropItem(i);
            filler.Add(i);
        }

        return filler;
    }

    [Theory]
    [InlineData("magic.necromancy", 0)] // nothing fits
    [InlineData("magic.necromancy", 1)] // the book fits, its reagents do not
    [InlineData("magic.chivalry", 0)]   // the tithing loan is undone too
    [InlineData("crafting.tailoring", 1)]
    public void AFullBackpackLendsNothingAndTheLessonStartsOnceThereIsRoom(string key, int leave)
    {
        using var p = new Player();
        p.Pm.TithingPoints = 3;
        var own = Fill(p.Pm.Backpack!, leave);
        var before = p.Pm.Backpack.Items.ToList();

        Run(p, key);
        var trial = p.Trial!;
        Assert.False(trial.Armed, "no half-lent lesson");
        Assert.DoesNotContain(trial.Loaners, i => !i.Deleted);
        Assert.DoesNotContain(p.Pm.Backpack.Items, i => !i.Deleted && LoanerNamed(i));
        Assert.Equal(before.Count, p.Pm.Backpack.Items.Count(i => !i.Deleted));
        Assert.All(own, i => Assert.False(i.Deleted, "the player's own things stay"));
        Assert.DoesNotContain(p.Pm.SkillMods ?? new List<SkillMod>(), m => m.Name == TutorialWingTrials.LoanName);
        Assert.Null(p.Pm.GetStatMod(TutorialWingTrials.LoanName));
        Assert.Equal(3, p.Pm.TithingPoints);
        Assert.True(trial.Target?.Deleted != false, "no training creature left standing");
        Assert.True(trial.Patient?.Deleted != false);
        Assert.DoesNotContain(trial.Props, i => !i.Deleted);

        // Room is made; the lesson starts on its own.
        foreach (var i in own.Take(12)) { i.Delete(); }
        ShardTestClock.Advance(TimeSpan.FromSeconds(6));
        Assert.True(trial.Armed, "the lesson starts once the supplies fit");
        Assert.Contains(trial.Loaners, i => !i.Deleted);
    }
}
