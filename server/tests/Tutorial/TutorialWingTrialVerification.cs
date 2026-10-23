// cc-P290: the tutorial's wing trials, part 1 (customizations/Tutorial/TutorialWingTrials.cs). Notes: shard-migration
// notes/cc-P290-tutorial-wing-trials-combat-magic.md.
//
// Facts, per trial its done check and its loaner cleanup:
//   1. Pick-your-weapon: three hits with the picked weapon's skill finish it; hits with another skill do not; the
//      weapon, buckler and skill loan go back.
//   2. Healing: bandaging yourself and the wounded villager finish it, in either order; someone else's heal does not;
//      the bandages and the Healing loan go back.
//   3. Magery, Necromancy, Mysticism: a successful cast roll then damage on the training echo; damage without a cast,
//      or long after one, does not. Chivalry: a cast then a heal on the villager; the tithing loan goes back.
//   4. Bushido and Ninjitsu: a hit with the school's move readied; a plain hit does not.
//   5. Shared rules: every trial is registered by key; a loaner dropped or banked goes back at once; leaving the yard
//      stops the trial and takes every loan back; a reset starts from nothing; loaners left by a restart are swept.
//   6. From the first test-world run: the trainee may heal their own wounded villager in the yard (Trammel refuses a
//      player's heal on an uncontrolled creature), nobody else may; the training echo is gray to its trainee (a human
//      body read innocent, so spells at it were refused).

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Network;
using Server.Spells;
using Server.Spells.Bushido;
using Server.Spells.Ninjitsu;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialWingTrialVerification : IDisposable
{
    private readonly Func<Point3D, bool> _fits = TutorialWingTrials.Fits;

    public TutorialWingTrialVerification()
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

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p290{Guid.NewGuid():N}"[..16], "p290-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(Race? race = null)
        {
            Pm = new PlayerMobile { Player = true, Race = race ?? Race.Human, Body = 0x190, Name = "Trainee", RawStr = 60, RawDex = 60, RawInt = 20 };
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

        public TutorialRecord Record => ClusterFTutorial.RecordOf(Pm)!;
        public TutorialWingTrials.Trial Trial => TutorialWingTrials.Current(Pm)!;

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

    private static TutorialWingTrials.Trial Begin(Player p, string key)
    {
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find(key)!);
        var trial = p.Trial;
        Assert.Equal(key, trial.Key);
        Assert.True(trial.Armed); // the player stands in the yard
        return trial;
    }

    private static void AssertAllReturned(Player p, TutorialWingTrials.Trial trial, List<Item> loaners, params SkillName[] lent)
    {
        Assert.All(loaners, i => Assert.True(i.Deleted));
        // A human's untrained skills read 20 (Jack of All Trades), so the check is that no loan is left on the player.
        Assert.DoesNotContain(p.Pm.SkillMods ?? new List<SkillMod>(), m => m.Name == TutorialWingTrials.LoanName && Array.IndexOf(lent, m.Skill) >= 0);
        Assert.Null(p.Pm.GetStatMod(TutorialWingTrials.LoanName));
        Assert.True(trial.Target?.Deleted != false);
        Assert.True(trial.Patient?.Deleted != false);
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ThreeHitsWithThePickedWeaponFinishItAndTheLoansGoBack()
    {
        using var p = new Player();
        var trial = Begin(p, "combat.weapons");
        var echo = trial.Target!;
        Assert.False(echo.Deleted);
        Assert.False(TutorialWingTrials.PickWeapon(p.Pm, 4)); // throwing is for gargoyles

        Assert.True(TutorialWingTrials.PickWeapon(p.Pm, 0)); // swords: a longsword and a buckler
        var loaners = trial.Loaners.ToList();
        var sword = loaners.OfType<Longsword>().Single();
        Assert.Single(loaners.OfType<Buckler>());
        Assert.StartsWith(TutorialWingTrials.LoanerPrefix, sword.Name);
        Assert.Equal(30.0, p.Pm.Skills[SkillName.Swords].Value);

        // Fists are Wrestling, not the picked Swords: no count.
        for (var i = 0; i < 3; i++) { TutorialWingTrials.OnTargetDamaged(echo, p.Pm); }
        Assert.Equal(0, trial.Hits);

        // The fake host has no tiledata; give the loaned sword its real layer to wield it.
        sword.Layer = Layer.OneHanded;
        Assert.True(p.Pm.EquipItem(sword));
        for (var i = 0; i < 3; i++) { TutorialWingTrials.OnTargetDamaged(echo, p.Pm); }
        Assert.Contains("combat.weapons", p.Record.StationsDone);
        AssertAllReturned(p, trial, loaners, SkillName.Swords);
    }

    [Fact]
    public void WrestlingNeedsNoWeaponAndAGargoyleCanThrow()
    {
        using var p = new Player();
        var trial = Begin(p, "combat.weapons");
        Assert.True(TutorialWingTrials.PickWeapon(p.Pm, 5)); // wrestling: fists, and a buckler
        for (var i = 0; i < 3; i++) { TutorialWingTrials.OnTargetDamaged(trial.Target!, p.Pm); }
        Assert.Contains("combat.weapons", p.Record.StationsDone);

        using var g = new Player(Race.Gargoyle);
        Begin(g, "combat.weapons");
        Assert.True(TutorialWingTrials.PickWeapon(g.Pm, 4));
        Assert.Single(g.Trial.Loaners.OfType<Boomerang>());
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void BandagingYourselfAndTheVillagerFinishesHealingAndTheLoansGoBack()
    {
        using var p = new Player();
        using var other = new Player();
        var trial = Begin(p, "combat.healing");
        var loaners = trial.Loaners.ToList();
        Assert.Single(loaners.OfType<Bandage>());
        Assert.True(p.Pm.Skills[SkillName.Healing].Value >= 50.0);
        Assert.True(p.Pm.Hits < p.Pm.HitsMax); // the lesson's scrape
        var villager = trial.Patient!;
        Assert.True(villager.Hits < villager.HitsMax);

        villager.Heal(10, other.Pm); // someone else's heal counts for nothing
        Assert.False(trial.PatientHealed);
        villager.Heal(10, p.Pm);
        Assert.True(trial.PatientHealed);
        Assert.DoesNotContain("combat.healing", p.Record.StationsDone);
        TutorialWingTrials.SelfHealed(p.Pm);
        Assert.Contains("combat.healing", p.Record.StationsDone);
        AssertAllReturned(p, trial, loaners, SkillName.Healing);
    }

    // ---------------------------------------------------------------- 3

    [Theory]
    [InlineData("magic.magery", SkillName.Magery, typeof(Spellbook))]
    [InlineData("magic.necromancy", SkillName.Necromancy, typeof(NecromancerSpellbook))]
    [InlineData("magic.mysticism", SkillName.Mysticism, typeof(MysticSpellbook))]
    public void ASpellCastAtTheEchoFinishesTheSchoolAndTheLoansGoBack(string key, SkillName skill, Type book)
    {
        using var p = new Player();
        var trial = Begin(p, key);
        var loaners = trial.Loaners.ToList();
        Assert.Contains(loaners, i => i.GetType() == book);
        Assert.True(loaners.Count >= 2); // a book and its reagents
        Assert.True(p.Pm.Skills[skill].Value >= 40.0);
        Assert.NotNull(p.Pm.GetStatMod(TutorialWingTrials.LoanName));
        var echo = trial.Target!;

        TutorialWingTrials.OnTargetDamaged(echo, p.Pm); // a punch: no cast
        TutorialWingTrials.OnSkillUsed(p.Pm, p.Pm.Skills[skill], true);
        ShardTestClock.Advance(TimeSpan.FromSeconds(11));
        TutorialWingTrials.OnTargetDamaged(echo, p.Pm); // too long after the cast
        Assert.DoesNotContain(key, p.Record.StationsDone);

        TutorialWingTrials.OnSkillUsed(p.Pm, p.Pm.Skills[skill], false); // a fizzle
        TutorialWingTrials.OnTargetDamaged(echo, p.Pm);
        Assert.DoesNotContain(key, p.Record.StationsDone);

        TutorialWingTrials.OnSkillUsed(p.Pm, p.Pm.Skills[skill], true);
        TutorialWingTrials.OnTargetDamaged(echo, p.Pm);
        Assert.Contains(key, p.Record.StationsDone);
        AssertAllReturned(p, trial, loaners, skill);
    }

    [Fact]
    public void CloseWoundsOnTheVillagerFinishesChivalryAndTheTithingLoanGoesBack()
    {
        using var p = new Player();
        p.Pm.TithingPoints = 7;
        var trial = Begin(p, "magic.chivalry");
        var loaners = trial.Loaners.ToList();
        Assert.Single(loaners.OfType<BookOfChivalry>());
        Assert.Equal(107, p.Pm.TithingPoints);

        trial.Patient!.Heal(10, p.Pm); // a heal with no cast
        Assert.DoesNotContain("magic.chivalry", p.Record.StationsDone);
        TutorialWingTrials.OnSkillUsed(p.Pm, p.Pm.Skills[SkillName.Chivalry], true);
        p.Pm.TithingPoints -= 10; // Close Wounds' price
        trial.Patient!.Heal(10, p.Pm);
        Assert.Contains("magic.chivalry", p.Record.StationsDone);
        Assert.Equal(7, p.Pm.TithingPoints); // the 10 spent came out of the loan; the player's own 7 are kept
        AssertAllReturned(p, trial, loaners, SkillName.Chivalry);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void AHitWithTheSchoolsMoveReadiedFinishesBushidoAndNinjitsu()
    {
        using var p = new Player();
        p.Pm.Mana = 100;
        var trial = Begin(p, "magic.bushido");
        var loaners = trial.Loaners.ToList();
        Assert.Single(loaners.OfType<BookOfBushido>());
        TutorialWingTrials.OnTargetDamaged(trial.Target!, p.Pm); // a plain hit
        Assert.DoesNotContain("magic.bushido", p.Record.StationsDone);
        Assert.True(SpecialMove.SetCurrentMove(p.Pm, new HonorableExecution())); // the lent Bushido is enough
        Assert.IsType<HonorableExecution>(SpecialMove.GetCurrentMove(p.Pm));
        TutorialWingTrials.OnTargetDamaged(trial.Target!, p.Pm);
        Assert.Contains("magic.bushido", p.Record.StationsDone);
        AssertAllReturned(p, trial, loaners, SkillName.Bushido);
        SpecialMove.ClearCurrentMove(p.Pm);

        var ninja = Begin(p, "magic.ninjitsu");
        var ninjaLoaners = ninja.Loaners.ToList();
        p.Pm.Mana = p.Pm.ManaMax;
        var katana = ninjaLoaners.OfType<Katana>().Single();
        katana.Layer = Layer.TwoHanded; // the fake host has no tiledata; Focus Attack needs a melee weapon in hand
        Assert.True(p.Pm.EquipItem(katana));
        Assert.True(SpecialMove.SetCurrentMove(p.Pm, new FocusAttack()));
        Assert.IsType<FocusAttack>(SpecialMove.GetCurrentMove(p.Pm));
        TutorialWingTrials.OnTargetDamaged(ninja.Target!, p.Pm);
        Assert.Contains("magic.ninjitsu", p.Record.StationsDone);
        AssertAllReturned(p, ninja, ninjaLoaners, SkillName.Ninjitsu);
        SpecialMove.ClearCurrentMove(p.Pm);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void EveryTrialIsRegisteredByKeyAndAResetStartsFromNothing()
    {
        foreach (var key in TutorialWingTrials.Keys)
        {
            Assert.NotNull(TutorialStations.Find(key));
            Assert.True(TutorialStations.Handlers.TryGetValue(key, out var handler));
            Assert.Equal((Action<PlayerMobile, TutorialStation>)TutorialWingTrials.Start, handler);
        }

        using var p = new Player();
        var first = Begin(p, "magic.magery");
        var old = first.Loaners.ToList();
        TutorialWingTrials.Reset(p.Pm, TutorialStations.Find("magic.magery")!);
        Assert.All(old, i => Assert.True(i.Deleted));
        Assert.NotSame(first, p.Trial);
        Assert.True(p.Trial.Armed);
        Assert.Single(p.Trial.Loaners.OfType<Spellbook>());
        Assert.Equal(p.Pm.Skills[SkillName.Magery].Value, Math.Max(40.0, p.Pm.Skills[SkillName.Magery].Base)); // one loan, not two
    }

    [Fact]
    public void LoanersNeverLeaveTheYard()
    {
        using var p = new Player();
        var trial = Begin(p, "magic.necromancy");
        var book = trial.Loaners.OfType<NecromancerSpellbook>().Single();
        var dust = trial.Loaners.OfType<GraveDust>().Single();

        book.MoveToWorld(p.Pm.Location, Map.Trammel); // dropped on the ground
        p.Pm.BankBox.DropItem(dust);                  // banked
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));
        Assert.True(book.Deleted);
        Assert.True(dust.Deleted);
        Assert.NotNull(TutorialWingTrials.Current(p.Pm));

        var rest = trial.Loaners.ToList();
        p.Pm.MoveToWorld(new Point3D(TutorialWingTrials.Yard.X + 30, TutorialWingTrials.Yard.Y, 0), Map.Trammel);
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));
        Assert.Null(TutorialWingTrials.Current(p.Pm));
        AssertAllReturned(p, trial, rest, SkillName.Necromancy);
        Assert.DoesNotContain("magic.necromancy", p.Record.StationsDone);
    }

    [Fact]
    public void LoanersLeftByARestartAreSwept()
    {
        var left = new Bandage(5) { Name = TutorialWingTrials.LoanerPrefix + "bandage" };
        var own = new Bandage(5);
        left.MoveToWorld(new Point3D(1800, 1800, 0), Map.Trammel);
        own.MoveToWorld(new Point3D(1800, 1801, 0), Map.Trammel);
        try
        {
            Assert.True(TutorialWingTrials.SweepLoaners() >= 1);
            Assert.True(left.Deleted);
            Assert.False(own.Deleted);
        }
        finally
        {
            own.Delete();
        }
    }

    // ---------------------------------------------------------------- 6

    // Pinned's notoriety rules (NotorietyHandlers.Initialize), restored after: the test host runs without them.
    private static void WithNotoriety(Action body)
    {
        var handler = Notoriety.Handler;
        var hues = (int[])Notoriety.Hues.Clone();
        var harmful = Mobile.AllowHarmfulHandler;
        var beneficial = Mobile.AllowBeneficialHandler;
        Server.Misc.NotorietyHandlers.Initialize();
        try
        {
            body();
        }
        finally
        {
            Notoriety.Handler = handler;
            Array.Copy(hues, Notoriety.Hues, hues.Length);
            Mobile.AllowHarmfulHandler = harmful;
            Mobile.AllowBeneficialHandler = beneficial;
        }
    }

    [Fact]
    public void TheTraineeMayHealTheirOwnVillagerInTheYardAndNobodyElse() => WithNotoriety(() =>
    {
        using var p = new Player();
        using var other = new Player();
        var trial = Begin(p, "combat.healing");
        var villager = trial.Patient!;
        var yard = new TutorialYardRegion(null);
        Assert.True(yard.Contains(TutorialWingTrials.PatientSpot));
        Assert.True(yard.Contains(TutorialWingTrials.TargetSpot));

        Assert.False(Mobile.AllowBeneficialHandler(p.Pm, villager)); // Trammel's rule, without the yard
        Assert.True(yard.AllowBeneficial(p.Pm, villager));
        Assert.False(yard.AllowBeneficial(other.Pm, villager)); // not their lesson
        Assert.True(yard.AllowBeneficial(p.Pm, p.Pm));
    });

    [Fact]
    public void TheTrainingEchoIsGrayToItsTraineeAndTheVillagerCannotBeHarmed() => WithNotoriety(() =>
    {
        using var p = new Player();
        var trial = Begin(p, "magic.magery");
        Assert.Equal(Notoriety.CanBeAttacked, Notoriety.Compute(p.Pm, trial.Target!));
        Assert.True(p.Pm.CanBeHarmful(trial.Target!, false));
        Assert.False(p.Pm.IsHarmfulCriminal(trial.Target!)); // a blow at it is no crime

        var healing = Begin(p, "combat.healing");
        Assert.False(p.Pm.CanBeHarmful(healing.Patient!, false));
    });
}
