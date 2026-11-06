// cc-P801: the New Haven jail lesson (customizations/Tutorial/TutorialJail.cs; reputation.wing). Notes: shard-migration
// notes/cc-P801-name-colors-lesson.md.
//
// Facts:
//   1. Each of the five people shows their real name color, and none of them is a real criminal or murderer.
//   2. The lesson completes only when every part is done, and in order: names, crate, ledger, guards.
//   3. No real penalty sticks: karma, fame, kills and the criminal flag read the same before, during and after.
//   4. The gray name is seen only by the trainee and goes when the pouch goes back, when it is lost, or when the lesson stops.
//   5. The thief and the guard are seen only by their trainee and go with the lesson.
//   6. The ledger shows the reader's own numbers; only after the crate does it count.
//   7. The jail seeds once: a second seed places nothing. Its walls close at the corners and the door is in the south wall.
//   8. [lesson brings the window back; a finished lesson still counts for an older character (the key did not change).

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialJailVerification : IDisposable
{
    private readonly NotorietyHandler _notoriety = Notoriety.Handler;
    private readonly AllowHarmfulHandler _harmful = Mobile.AllowHarmfulHandler;

    public TutorialJailVerification()
    {
        ShardTestClock.Arm();
        Notoriety.Handler = Server.Misc.NotorietyHandlers.MobileNotoriety; // the name rules alone; the host runs without them
        Mobile.AllowHarmfulHandler = Server.Misc.NotorietyHandlers.Mobile_AllowHarmful;
        ShardTestHost.EnsureAccounts();
        ClusterFTutorial.Enabled = true;
        TutorialWingTrials.EnsureHooks();
        TutorialCoreTrials.EnsureHooks();
        TutorialJail.Seed(false);
    }

    public void Dispose()
    {
        Notoriety.Handler = _notoriety;
        Mobile.AllowHarmfulHandler = _harmful;
        var gone = new List<IEntity>();
        foreach (var m in Map.Trammel.GetMobilesInRange(TutorialJail.Hall, 14))
        {
            if (m is TutorialJailPerson or TutorialJailActor) { gone.Add(m); }
        }

        for (var x = -1; x <= 12; x++)
        {
            for (var y = -1; y <= 10; y++)
            {
                var at = TutorialJail.At(x, y);
                foreach (var item in Map.Trammel.GetItemsAt(at.X, at.Y)) { gone.Add(item); }
            }
        }

        foreach (var e in gone) { e.Delete(); }
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p801{Guid.NewGuid():N}"[..16], "p801-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(bool enter = true)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Trainee", RawStr = 60, RawDex = 60, RawInt = 20 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(TutorialJail.Hall, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
            if (enter) { Assert.True(ClusterFTutorial.TryEnter(Pm)); }
        }

        public TutorialWingTrials.Trial Trial => TutorialWingTrials.Current(Pm)!;
        public TutorialRecord Record => ClusterFTutorial.RecordOf(Pm)!;

        public void Dispose()
        {
            TutorialWingTrials.Cancel(Pm);
            TutorialCoreTrials.Cancel(Pm);
            Pm.NetState = null;
            Ns.Mobile = null;
            Ns.Dispose();
            Pm.Delete();
            Account.Delete();
        }
    }

    private static TutorialWingTrials.Trial Begin(Player p)
    {
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find(TutorialJail.Key)!);
        Assert.True(p.Trial.Armed);
        return p.Trial;
    }

    private static TutorialJailPerson Person(int role)
    {
        foreach (var m in Map.Trammel.GetMobilesInRange(TutorialJail.Hall, 12))
        {
            if (m is TutorialJailPerson person && person.Role == role) { return person; }
        }

        throw new InvalidOperationException($"no person {role}");
    }

    private static T Item<T>(Point3D at) where T : Item
    {
        foreach (var item in Map.Trammel.GetItemsAt(at.X, at.Y))
        {
            if (item is T found) { return found; }
        }

        throw new InvalidOperationException($"no {typeof(T).Name} at {at}");
    }

    private static TutorialJailCrate Crate => Item<TutorialJailCrate>(TutorialJail.CrateSpot);
    private static TutorialJailLedger Ledger => Item<TutorialJailLedger>(TutorialJail.LedgerSpot);

    private static void ClickAll(Player p)
    {
        for (var i = 0; i < TutorialJail.People.Length; i++) { Person(i).OnAosSingleClick(p.Pm); }
    }

    private static void Say(Player p, string words) =>
        EventSink.InvokeSpeech(new SpeechEventArgs(p.Pm, words, MessageType.Regular, 0, []));

    private static void Wait(double seconds) => ShardTestClock.Advance(TimeSpan.FromSeconds(seconds));

    private static void ToTheThief(Player p)
    {
        ClickAll(p);
        p.Pm.Location = TutorialJail.At(8, 7);
        Crate.OnDoubleClick(p.Pm);
        Crate.OnDoubleClick(p.Pm);
        p.Pm.Location = TutorialJail.At(3, 5);
        Ledger.OnDoubleClick(p.Pm);
    }

    // ---------------------------------------------------------------- 1

    [Theory]
    [InlineData(0, Notoriety.Invulnerable, 0x35)] // yellow
    [InlineData(1, Notoriety.Innocent, 0x59)]     // blue
    [InlineData(2, Notoriety.CanBeAttacked, 0x3B2)] // gray
    [InlineData(3, Notoriety.Enemy, 0x90)]        // orange
    [InlineData(4, Notoriety.Murderer, 0x22)]     // red
    public void EachPersonShowsTheirRealNameColor(int role, int noto, int hue)
    {
        using var p = new Player();
        Assert.Equal(noto, Notoriety.Compute(p.Pm, Person(role)));
        Assert.Equal(hue, Notoriety.GetHue(Notoriety.Compute(p.Pm, Person(role))));
    }

    [Fact]
    public void ThePeopleAreNeitherRealCriminalsNorHarmable()
    {
        using var p = new Player();
        for (var i = 0; i < TutorialJail.People.Length; i++)
        {
            var person = Person(i);
            Assert.False(person.Murderer);
            Assert.False(person.Criminal);
            Assert.False(person.Blessed); // TazUO paints a blessed mobile gold unless its name is yellow
            Assert.False(p.Pm.CanBeHarmful(person, false));
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheLessonCompletesOnlyWhenEveryPartIsDoneInOrder()
    {
        using var p = new Player();
        var trial = Begin(p);

        // A click for someone else's color, and nothing else, counts for one.
        Person(0).OnAosSingleClick(p.Pm);
        Assert.Contains("yellow", trial.Steps);
        Assert.Single(trial.Steps);

        // The crate waits for all five.
        p.Pm.Location = TutorialJail.At(8, 7);
        Crate.OnDoubleClick(p.Pm);
        Assert.False(trial.JailGray);
        Assert.Empty(trial.Loaners);

        for (var i = 1; i < TutorialJail.People.Length; i++) { Person(i).OnDoubleClick(p.Pm); }
        Assert.Equal(5, trial.Steps.Count);

        // The ledger before the crate counts for nothing, and no thief comes.
        p.Pm.Location = TutorialJail.At(3, 5);
        Ledger.OnDoubleClick(p.Pm);
        Assert.DoesNotContain(TutorialJail.LedgerStep, trial.Steps);
        Assert.Null(trial.JailThief);

        // "guards" with no thief does nothing.
        Say(p, "guards");
        Wait(4);
        Assert.DoesNotContain(TutorialJail.ThiefStep, trial.Steps);

        p.Pm.Location = TutorialJail.At(8, 7);
        Crate.OnDoubleClick(p.Pm);
        Assert.True(trial.JailGray);
        Assert.DoesNotContain(TutorialJail.CrateStep, trial.Steps); // taking it is not the step; putting it back is

        // The ledger while holding the pouch does not count either.
        p.Pm.Location = TutorialJail.At(3, 5);
        Ledger.OnDoubleClick(p.Pm);
        Assert.DoesNotContain(TutorialJail.LedgerStep, trial.Steps);

        p.Pm.Location = TutorialJail.At(8, 7);
        Crate.OnDoubleClick(p.Pm);
        Assert.Contains(TutorialJail.CrateStep, trial.Steps);
        Assert.False(trial.JailGray);

        p.Pm.Location = TutorialJail.At(3, 5);
        Ledger.OnDoubleClick(p.Pm);
        Assert.Contains(TutorialJail.LedgerStep, trial.Steps);
        Assert.NotNull(trial.JailThief);
        Assert.False(trial.Finished);
        Assert.DoesNotContain(TutorialJail.Key, p.Record.StationsDone);

        Say(p, "hello there");
        Wait(4);
        Assert.False(trial.Finished);

        Say(p, "Guards!");
        Assert.False(trial.Finished); // the strike takes a moment to see
        Wait(4);
        Assert.True(trial.Finished);
        Assert.Contains(TutorialJail.Key, p.Record.StationsDone);
    }

    [Fact]
    public void TheCratePouchCanBeDroppedBack()
    {
        using var p = new Player();
        var trial = Begin(p);
        ClickAll(p);
        p.Pm.Location = TutorialJail.At(8, 7);
        Crate.OnDoubleClick(p.Pm);
        var pouch = trial.Loaners.Single();
        Assert.True(Crate.OnDragDrop(p.Pm, pouch));
        Assert.True(pouch.Deleted);
        Assert.Contains(TutorialJail.CrateStep, trial.Steps);
        Assert.False(Crate.OnDragDrop(p.Pm, new Gold(1)));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void NoRealPenaltySticks()
    {
        using var p = new Player();
        p.Pm.Karma = 120;
        p.Pm.Fame = 40;
        void Same()
        {
            Assert.Equal(120, p.Pm.Karma);
            Assert.Equal(40, p.Pm.Fame);
            Assert.Equal(0, p.Pm.Kills);
            Assert.False(p.Pm.Criminal);
        }

        var trial = Begin(p);
        ClickAll(p);
        Same();
        p.Pm.Location = TutorialJail.At(8, 7);
        Crate.OnDoubleClick(p.Pm);
        Assert.True(trial.JailGray);
        Same();
        Wait(5);
        Same();
        Crate.OnDoubleClick(p.Pm);
        Same();
        p.Pm.Location = TutorialJail.At(3, 5);
        Ledger.OnDoubleClick(p.Pm);
        Same();
        Say(p, "guards");
        Wait(4);
        Assert.True(trial.Finished);
        Same();
        Assert.Equal(Notoriety.Innocent, Notoriety.Compute(p.Pm, p.Pm));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheGrayIsSeenOnlyByTheTraineeAndGoesWithThePouch()
    {
        using var p = new Player();
        using var other = new Player(false);
        var trial = Begin(p);
        ClickAll(p);
        p.Pm.Location = TutorialJail.At(8, 7);
        Assert.Equal(Notoriety.Innocent, Notoriety.Compute(p.Pm, p.Pm));
        Crate.OnDoubleClick(p.Pm);
        Assert.Equal(Notoriety.Criminal, Notoriety.Compute(p.Pm, p.Pm));
        Assert.Equal(Notoriety.Innocent, Notoriety.Compute(other.Pm, p.Pm));
        Crate.OnDoubleClick(p.Pm);
        Assert.Equal(Notoriety.Innocent, Notoriety.Compute(p.Pm, p.Pm));
        Assert.Empty(trial.Loaners);
    }

    [Fact]
    public void ALostPouchEndsTheGrayAndEarnsNothing()
    {
        using var p = new Player();
        var trial = Begin(p);
        ClickAll(p);
        p.Pm.Location = TutorialJail.At(8, 7);
        Crate.OnDoubleClick(p.Pm);
        trial.Loaners.Single().MoveToWorld(p.Pm.Location, p.Pm.Map); // the loaner rule takes it back at the next tick
        Wait(1);
        Assert.False(trial.JailGray);
        Assert.Equal(Notoriety.Innocent, Notoriety.Compute(p.Pm, p.Pm));
        Assert.DoesNotContain(TutorialJail.CrateStep, trial.Steps);
    }

    [Fact]
    public void LeavingTheJailEndsTheLessonAndTheGray()
    {
        using var p = new Player();
        var trial = Begin(p);
        ClickAll(p);
        p.Pm.Location = TutorialJail.At(8, 7);
        Crate.OnDoubleClick(p.Pm);
        var pouch = trial.Loaners.Single();
        p.Pm.MoveToWorld(TutorialJail.At(40, 40), Map.Trammel);
        Wait(1);
        Assert.Null(TutorialWingTrials.Current(p.Pm));
        Assert.True(pouch.Deleted);
        Assert.False(trial.JailGray); // cleared, not just forgotten: the client was told its name is blue
        Assert.Equal(Notoriety.Innocent, Notoriety.Compute(p.Pm, p.Pm));
        Assert.DoesNotContain(TutorialJail.Key, p.Record.StationsDone);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheThiefAndGuardAreSeenOnlyByTheirTraineeAndGoWithTheLesson()
    {
        using var p = new Player();
        using var other = new Player(false);
        var trial = Begin(p);
        ToTheThief(p);
        var thief = trial.JailThief!;
        Assert.True(p.Pm.CanSee(thief));
        Assert.False(other.Pm.CanSee(thief));
        Assert.Equal(Notoriety.Criminal, Notoriety.Compute(p.Pm, thief));
        Say(p, "guards");
        var guard = trial.Creatures.OfType<TutorialJailActor>().Single(c => c != thief);
        Assert.True(p.Pm.CanSee(guard));
        Assert.False(other.Pm.CanSee(guard));
        Wait(4);
        Assert.True(trial.Finished);
        Assert.True(thief.Deleted);
        Assert.True(guard.Deleted);
    }

    [Fact]
    public void GuardsSaidTwiceStrikesOnce()
    {
        using var p = new Player();
        var trial = Begin(p);
        ToTheThief(p);
        Say(p, "guards");
        Say(p, "guards");
        Assert.Equal(2, trial.Creatures.Count); // the thief and one guard
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheLedgerShowsTheReadersOwnNumbers()
    {
        using var p = new Player();
        p.Pm.Karma = 77;
        p.Pm.Fame = 33;
        p.Pm.Kills = 2;
        var page = TutorialJailLedgerGump.Page(p.Pm);
        Assert.Contains("Karma: 77", page);
        Assert.Contains("Fame: 33", page);
        Assert.Contains("Murder counts: 2", page);
        p.Pm.Kills = 0;
    }

    [Fact]
    public void AnyoneMayReadTheLedgerAndTouchesNothing()
    {
        using var p = new Player(false);
        p.Pm.Location = TutorialJail.At(3, 5);
        Ledger.OnDoubleClick(p.Pm);
        Assert.Null(TutorialWingTrials.Current(p.Pm));
        Person(0).OnAosSingleClick(p.Pm); // a veteran hears the line, nothing more
        Assert.Null(TutorialWingTrials.Current(p.Pm));
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheJailSeedsOnce()
    {
        Assert.Contains("placed 0 pieces", TutorialJail.Seed(false));
    }

    [Fact]
    public void TheWallsCloseAndTheDoorIsInTheSouthWall()
    {
        var pieces = TutorialJailLayout.Pieces;
        Assert.Contains(pieces, q => q is { Id: 0x0036, X: 0, Y: 0, Z: 0 });   // post, north-west
        Assert.Contains(pieces, q => q is { Id: 0x0033, X: 10, Y: 8, Z: 0 });  // corner, south-east
        Assert.DoesNotContain(pieces, q => q.Y == 8 && q.X == 5 && q.Z == 0 && q.Id != 0x0519); // only floor under the door
        Assert.NotNull(Item<BaseDoor>(TutorialJail.Door));
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void LessonBringsTheWindowBack()
    {
        using var p = new Player();
        Begin(p);
        p.Pm.CloseGump<TutorialJailGump>();
        Assert.True(TutorialCoreTrials.Reopen(p.Pm));
        Assert.NotNull(p.Pm.FindGump<TutorialJailGump>());
    }

    [Fact]
    public void TheWindowStaysThroughEachStep()
    {
        using var p = new Player();
        Begin(p);
        Assert.NotNull(p.Pm.FindGump<TutorialJailGump>());
        Person(0).OnAosSingleClick(p.Pm);
        Assert.NotNull(p.Pm.FindGump<TutorialJailGump>());
    }

    [Fact]
    public void TheNowLineNamesWhatIsStillToDo()
    {
        using var p = new Player();
        var trial = Begin(p);
        Person(0).OnAosSingleClick(p.Pm);
        var line = TutorialWingTrials.JailNow(trial);
        Assert.DoesNotContain("yellow", line);
        Assert.Contains("red", line);
        Assert.Contains("double-click Rook", TutorialJail.DoneText);
    }
}
