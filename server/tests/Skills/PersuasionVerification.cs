// PersuasionVerification.cs
//
// cc-P626, Persuasion stage 1 (Begging reworked): customizations ClusterFPersuasion and ClusterFExchangeLetter.
// Design: shard-migration notes/cc-P606-begging-skill-design.md; notes: notes/cc-P626-build-social-skill.md.
//
//   1. The skill is called Persuasion and its button opens Persuasion's target, after pinned's Begging.Initialize.
//   2. Charity: coins from the giver's own purse (2 to 14, no richer past 40, less at bad karma), else a meal; one
//      result per account, contact and day; 50 gold and 2 meals a day per account, shared by its characters; only
//      spawned civilians give; karma never changes.
//   3. Bad karma: refused coins as untrustworthy, still fed.
//   4. Charity teaches once per distinct contact that gave, and only below 20 Base.
//   5. The courier ladder: every Base from 0 to 199.9 has a job whose gain window holds it.
//   6. A courier letter goes to the named kind of NPC in another town (the first tier in the same town, the top tiers to
//      a far town, never straight back), only its courier can deliver it, it is consumed before its one gain, it goes
//      stale, and it trains 199.9 to 200 but nothing at the cap or when locked.
//   7. Anyone but the Exchange gives directions only, and the persuade check refuses players, pets and the distant.
//   8. The tutorial's thief trial still completes its Begging step on the practice person, with nothing paid or taught.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.Spawners;
using Server.Items;
using Server.LootBoxes;
using Server.Misc;
using Server.Mobiles;
using Server.Random;
using Server.Regions;
using Server.SkillHandlers;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PersuasionVerification : IDisposable
{
    // Three towns of our own on Trammel's empty north-west: Home and Near 400 tiles apart, Far 1500 tiles off.
    private static readonly Point3D Home = new(150, 150, 0);
    private static readonly Point3D Near = new(550, 150, 0);
    private static readonly Point3D Far = new(1650, 150, 0);

    private readonly ITestOutputHelper _out;
    private readonly List<IEntity> _made = new();
    private readonly List<Region> _regions = new();
    private readonly List<Account> _accounts = new();
    private readonly List<(Mobile Who, string Text)> _heard = new();

    private readonly SkillCheckLocationHandler _location = Mobile.SkillCheckLocationHandler;
    private readonly SkillCheckDirectLocationHandler _directLocation = Mobile.SkillCheckDirectLocationHandler;
    private readonly SkillCheckTargetHandler _target = Mobile.SkillCheckTargetHandler;
    private readonly SkillCheckDirectTargetHandler _directTarget = Mobile.SkillCheckDirectTargetHandler;
    private readonly bool _enabled = ClusterFSkillGain.Enabled;
    private readonly int _attempts = ClusterFSkillGain.ChanceAttempts;
    private readonly int _amount = ClusterFSkillGain.AmountMultiplier;

    public PersuasionVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestClock.Arm();

        // The shard's gain hook over pinned's handlers, one attempt and one amount, so one gain event moves 0.1 at most.
        Mobile.SkillCheckLocationHandler = SkillCheck.Mobile_SkillCheckLocation;
        Mobile.SkillCheckDirectLocationHandler = SkillCheck.Mobile_SkillCheckDirectLocation;
        Mobile.SkillCheckTargetHandler = SkillCheck.Mobile_SkillCheckTarget;
        Mobile.SkillCheckDirectTargetHandler = SkillCheck.Mobile_SkillCheckDirectTarget;
        ClusterFSkillGain.Initialize();
        ClusterFSkillGain.Enabled = true;
        ClusterFSkillGain.ChanceAttempts = 1;
        ClusterFSkillGain.AmountMultiplier = 1;

        ClusterFPersuasion.ResetForTests();
        ClusterFPersuasion.Heard = (m, text) => _heard.Add((m, text));

        Town("P626 Home", Home);
        Town("P626 Near", Near);
        Town("P626 Far", Far);
    }

    public void Dispose()
    {
        ClusterFPersuasion.Heard = null;
        ClusterFPersuasion.ResetForTests();
        BuiltInRng.Reset();
        Mobile.SkillCheckLocationHandler = _location;
        Mobile.SkillCheckDirectLocationHandler = _directLocation;
        Mobile.SkillCheckTargetHandler = _target;
        Mobile.SkillCheckDirectTargetHandler = _directTarget;
        ClusterFSkillGain.Enabled = _enabled;
        ClusterFSkillGain.ChanceAttempts = _attempts;
        ClusterFSkillGain.AmountMultiplier = _amount;

        foreach (var e in _made)
        {
            if (e is Mobile m)
            {
                foreach (var item in m.Items.ToList())
                {
                    item.Delete();
                }
            }

            e.Delete();
        }

        foreach (var r in _regions)
        {
            r.Unregister();
        }

        foreach (var a in _accounts)
        {
            Accounts.Remove(a);
        }
    }

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

    // A roll of 0 passes every chance: every gain event that may gain, gains.
    private static void AlwaysRoll() => BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };

    private void Town(string name, Point3D centre)
    {
        var r = new TownRegion(name, Map.Trammel, 50,
            new Rectangle3D(new Point3D(centre.X - 30, centre.Y - 30, -128), new Point3D(centre.X + 30, centre.Y + 30, 127)));
        r.Register();
        _regions.Add(r);
    }

    private Account NewAccount()
    {
        var acct = new Account($"p626{Guid.NewGuid():N}"[..16], "p626-test-only");
        _accounts.Add(acct);
        return acct;
    }

    private PlayerMobile Player(double persuasion, Point3D at, Account acct = null, int slot = 0)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Elf, Name = "Asker" }; // no Human +20 racial floor
        pm.AddItem(new Backpack());
        acct ??= NewAccount();
        acct[slot] = pm;
        pm.MoveToWorld(at, Map.Trammel);
        pm.RawStr = pm.RawDex = pm.RawInt = 0; // Value is exactly Base below 100 (no stat offset)
        pm.Skills.Cap = 58 * 2000;
        pm.Skills.Begging.Cap = 200.0;
        pm.Skills.Begging.Base = persuasion;
        _made.Add(pm);
        return pm;
    }

    // A townsperson a spawner keeps, with gold in their pack.
    private T Civilian<T>(T npc, Point3D at, int gold = 500) where T : BaseCreature
    {
        var spawner = new Spawner();
        spawner.MoveToWorld(new Point3D(at.X, at.Y, at.Z), Map.Trammel);
        _made.Add(spawner);
        npc.Spawner = spawner;
        if (npc.Backpack == null)
        {
            npc.AddItem(new Backpack());
        }

        npc.Backpack.ConsumeTotal(typeof(Gold), npc.Backpack.GetAmount(typeof(Gold))); // a vendor's own random purse
        if (gold > 0)
        {
            npc.Backpack.DropItem(new Gold(gold));
        }

        npc.MoveToWorld(at, Map.Trammel);
        _made.Add(npc);
        return npc;
    }

    private static Point3D Beside(Mobile m) => new(m.X + 1, m.Y, m.Z);

    private List<string> HeardBy(Mobile m) => _heard.Where(h => h.Who == m).Select(h => h.Text).ToList();

    private static int GoldOf(Mobile m) => m.Backpack.GetAmount(typeof(Gold));

    private static int MealsOf(Mobile m) => m.Backpack.GetAmount(typeof(BegFrenchBread));

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheSkillIsPersuasionAndItsButtonOpensPersuasion()
    {
        // The server runs every Initialize in CallPriority order (pinned Server/AssemblyHandler.cs:143, default 50), so ours
        // must sort after pinned's Begging.Initialize, which sets the same callback.
        var priority = typeof(ClusterFPersuasion).GetMethod("Initialize")!
            .GetCustomAttributes(typeof(CallPriorityAttribute), true).OfType<CallPriorityAttribute>().SingleOrDefault();
        Assert.True(priority?.Priority > 50, "ClusterFPersuasion.Initialize could run before pinned's Begging.Initialize");

        Begging.Initialize();
        ClusterFPersuasion.Initialize();

        var info = SkillInfo.Table[(int)SkillName.Begging];
        Assert.Equal("Persuasion", info.Name);
        Assert.Equal(6, (int)SkillName.Begging); // the saved slot is kept

        var pm = Player(30.0, Home);
        Assert.True(pm.UseSkill(SkillName.Begging));
        Assert.Equal("PersuadeTarget", pm.Target?.GetType().Name);
        Assert.Contains("Whom do you wish to persuade?", HeardBy(pm));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void CharityGivesCoinsOrAMealWithinTheDailyLimits()
    {
        var acct = NewAccount();
        var pm = Player(40.0, Home, acct);
        var npcs = Enumerable.Range(0, 7).Select(i => Civilian(new Banker(), new Point3D(Home.X + i, Home.Y + 2, 0))).ToList();

        // 14 a time at 40 (2 + 0.3 x 40), never more than a tenth of the giver's purse.
        Assert.True(ClusterFPersuasion.AskForHelp(pm, npcs[0]));
        Assert.Equal(14, GoldOf(pm));
        Assert.Equal(486, npcs[0].Backpack.GetAmount(typeof(Gold)));

        // The same contact once a day.
        Assert.False(ClusterFPersuasion.AskForHelp(pm, npcs[0]));
        Assert.Equal(14, GoldOf(pm));

        ClusterFPersuasion.AskForHelp(pm, npcs[1]);
        ClusterFPersuasion.AskForHelp(pm, npcs[2]);
        Assert.Equal(42, GoldOf(pm));
        ClusterFPersuasion.AskForHelp(pm, npcs[3]);
        Assert.Equal(50, GoldOf(pm)); // 8: the rest of the 50 a day

        // Coins spent for the day: meals, two of them, then nothing (and that contact's attempt is spent).
        ClusterFPersuasion.AskForHelp(pm, npcs[4]);
        ClusterFPersuasion.AskForHelp(pm, npcs[5]);
        Assert.Equal(2, MealsOf(pm));
        Assert.True(ClusterFPersuasion.AskForHelp(pm, npcs[6]));
        Assert.Equal(50, GoldOf(pm));
        Assert.Equal(2, MealsOf(pm));
        Assert.Contains(HeardBy(pm), t => t.EndsWith("I have nothing to spare for thee today."));

        // Another character on the same account shares the allowance.
        var alt = Player(40.0, Home, acct, 1);
        var fresh = Civilian(new Banker(), new Point3D(Home.X, Home.Y - 2, 0));
        ClusterFPersuasion.AskForHelp(alt, fresh);
        Assert.Equal(0, GoldOf(alt));
        Assert.Equal(0, MealsOf(alt));

        // A new UTC day resets it.
        acct.SetTag(ClusterFPersuasion.DayTag, "20000101");
        Assert.True(ClusterFPersuasion.AskForHelp(pm, npcs[0]));
        Assert.Equal(64, GoldOf(pm));

        // An empty purse gives a meal; a staff-made townsperson (no spawner) gives nothing and spends nothing.
        var poor = Civilian(new Banker(), new Point3D(Home.X + 3, Home.Y - 2, 0), gold: 0);
        ClusterFPersuasion.AskForHelp(pm, poor);
        Assert.Equal(3, MealsOf(pm));

        var unspawned = new Banker();
        unspawned.MoveToWorld(Beside(pm), Map.Trammel);
        _made.Add(unspawned);
        Assert.False(ClusterFPersuasion.AskForHelp(pm, unspawned));
        Assert.Null(ClusterFPersuasion.ContactKey(unspawned));

        Assert.Equal(0, pm.Karma);
    }

    [Theory]
    [InlineData(0.0, 0, 2)]
    [InlineData(10.0, 0, 5)]
    [InlineData(40.0, 0, 14)]
    [InlineData(200.0, 0, 14)]
    [InlineData(40.0, 5000, 14)]
    [InlineData(40.0, -10000, 11)]
    [InlineData(20.0, -5000, 7)]
    public void TheCoinCeilingFollowsSkillAndKarma(double skill, int karma, int coins)
    {
        var pm = Player(skill, Home);
        pm.Karma = karma;
        Assert.Equal(coins, ClusterFPersuasion.CoinsOffered(pm));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void BadKarmaIsRefusedCoinsButStillFed()
    {
        var pm = Player(40.0, Home);
        pm.Karma = -15000; // pinned's chance is then above 1: always refused (Begging.cs:100-106)
        var npc = Civilian(new Banker(), Beside(pm));

        Assert.True(ClusterFPersuasion.AskForHelp(pm, npc));
        Assert.Equal(0, GoldOf(pm));
        Assert.Equal(1, MealsOf(pm));
        Assert.Contains(HeardBy(pm), t => t.Contains("Thou dost not look trustworthy"));
        Assert.Equal(-15000, pm.Karma);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void CharityTeachesOncePerContactAndOnlyBelow20()
    {
        AlwaysRoll();
        var acct = NewAccount();
        var pm = Player(10.0, Home, acct);
        var a = Civilian(new Banker(), new Point3D(Home.X + 1, Home.Y + 3, 0));
        var b = Civilian(new Banker(), new Point3D(Home.X + 2, Home.Y + 3, 0));
        var c = Civilian(new Banker(), new Point3D(Home.X + 3, Home.Y + 3, 0));
        var d = Civilian(new Banker(), new Point3D(Home.X + 4, Home.Y + 3, 0));

        ClusterFPersuasion.AskForHelp(pm, a);
        Assert.Equal(10.1, pm.Skills.Begging.Base, 1);

        // The same contact on a later day gives again but does not teach again.
        acct.SetTag(ClusterFPersuasion.DayTag, "20000101");
        ClusterFPersuasion.AskForHelp(pm, a);
        Assert.Equal(10.1, pm.Skills.Begging.Base, 1);

        ClusterFPersuasion.AskForHelp(pm, b);
        Assert.Equal(10.2, pm.Skills.Begging.Base, 1);

        pm.Skills.Begging.Base = 19.9;
        ClusterFPersuasion.AskForHelp(pm, c);
        Assert.Equal(20.0, pm.Skills.Begging.Base, 1);

        ClusterFPersuasion.AskForHelp(pm, d);
        Assert.Equal(20.0, pm.Skills.Begging.Base, 1); // charity stops teaching at 20
        Assert.True(GoldOf(pm) > 0);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheCourierLadderCoversEveryBandTo200()
    {
        Assert.Equal(10, ClusterFPersuasion.JobDifficultyFor(0.0));
        Assert.Equal(35, ClusterFPersuasion.JobDifficultyFor(10.0));
        Assert.Equal(110, ClusterFPersuasion.JobDifficultyFor(100.0));
        Assert.Equal(195, ClusterFPersuasion.JobDifficultyFor(199.9));

        for (var tenths = 0; tenths < 2000; tenths++)
        {
            var b = tenths / 10.0;
            var d = ClusterFPersuasion.JobDifficultyFor(b);
            Assert.True(d - ClusterFPersuasion.GainHalfWindow <= b && b < d + ClusterFPersuasion.GainHalfWindow,
                $"Base {b:F1} gets difficulty {d}, whose window does not hold it");
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void ACourierLetterGoesToTheRightPersonOnceAndTeaches()
    {
        AlwaysRoll();
        var homeBank = Civilian(new Banker(), new Point3D(Home.X + 2, Home.Y, 0));
        var homeShop = Civilian(new Provisioner(), new Point3D(Home.X - 2, Home.Y, 0));
        var nearBank = Civilian(new Banker(), Near);
        var farBank = Civilian(new Banker(), Far);

        var pm = Player(50.0, Home);
        var letter = ClusterFPersuasion.AskAboutWork(pm, homeBank);
        Assert.NotNull(letter);
        Assert.Equal(60, letter.Difficulty);
        Assert.Same(pm, letter.Courier);
        Assert.Contains(letter.Town, new[] { "P626 Near", "P626 Far" });
        Assert.StartsWith("a banker in P626 ", letter.Instructions);
        Assert.Same(letter, ClusterFPersuasion.CarriedLetter(pm));

        // One letter at a time.
        Assert.Null(ClusterFPersuasion.AskAboutWork(pm, homeBank));

        // Not to the wrong person, not by anyone else.
        Assert.False(ClusterFPersuasion.Deliver(pm, letter, homeShop));
        var other = Player(50.0, Home);
        var dest = letter.Town == "P626 Near" ? nearBank : farBank;
        other.MoveToWorld(Beside(dest), Map.Trammel);
        Assert.False(ClusterFPersuasion.Deliver(other, letter, dest));
        Assert.False(letter.Deleted);

        // Delivered: consumed first, then one gain; a replay gives nothing.
        pm.MoveToWorld(Beside(dest), Map.Trammel);
        Assert.True(ClusterFPersuasion.Deliver(pm, letter, dest));
        Assert.True(letter.Deleted);
        Assert.Equal(50.1, pm.Skills.Begging.Base, 1);
        Assert.False(ClusterFPersuasion.Deliver(pm, letter, dest));
        Assert.Equal(50.1, pm.Skills.Begging.Base, 1);

        // Never straight back to the same destination.
        var next = ClusterFPersuasion.AskAboutWork(pm, homeBank);
        Assert.NotEqual(letter.Town, next.Town);
        next.Delete();

        // The top tiers go far; the first tier stays in town, to another kind of shopkeeper.
        ClusterFPersuasion.ResetForTests();
        var veteran = Player(100.0, Home);
        var farLetter = ClusterFPersuasion.AskAboutWork(veteran, homeBank);
        Assert.Equal(110, farLetter.Difficulty);
        Assert.Equal("P626 Far", farLetter.Town);

        var novice = Player(0.0, Home);
        var local = ClusterFPersuasion.AskAboutWork(novice, homeBank);
        Assert.Equal(10, local.Difficulty);
        Assert.Equal("P626 Home", local.Town);
        Assert.Equal("a provisioner in P626 Home", local.Instructions);
        novice.MoveToWorld(Beside(homeShop), Map.Trammel);
        Assert.True(ClusterFPersuasion.Deliver(novice, local, homeShop));
        Assert.Equal(0.1, novice.Skills.Begging.Base, 1);

        // A stale letter is refused and torn up.
        farLetter.Expires = Core.Now - TimeSpan.FromSeconds(1);
        veteran.MoveToWorld(Beside(farBank), Map.Trammel);
        Assert.False(ClusterFPersuasion.Deliver(veteran, farLetter, farBank));
        Assert.True(farLetter.Deleted);
        Assert.Equal(100.0, veteran.Skills.Begging.Base, 1);
    }

    [Fact]
    public void TheTopLetterTrainsTo200AndNothingPastTheCapOrWhenLocked()
    {
        AlwaysRoll();
        var homeBank = Civilian(new Banker(), new Point3D(Home.X + 2, Home.Y, 0));
        var farBank = Civilian(new Banker(), Far);

        double Run(double start, SkillLock lockState)
        {
            ClusterFPersuasion.ResetForTests();
            var pm = Player(start, Home);
            pm.Skills.Begging.SetLockNoRelay(lockState);
            var letter = ClusterFPersuasion.AskAboutWork(pm, homeBank);
            Assert.Equal(195, letter.Difficulty);
            pm.MoveToWorld(Beside(farBank), Map.Trammel);
            Assert.True(ClusterFPersuasion.Deliver(pm, letter, farBank));
            return pm.Skills.Begging.Base;
        }

        Assert.Equal(200.0, Run(199.9, SkillLock.Up), 1);
        Assert.Equal(200.0, Run(200.0, SkillLock.Up), 1);
        Assert.Equal(180.0, Run(180.0, SkillLock.Locked), 1);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void OnlyTheExchangeHiresAndOnlyPeopleCanBePersuaded()
    {
        var pm = Player(50.0, Home);
        var shop = Civilian(new Provisioner(), Beside(pm));
        Assert.Null(ClusterFPersuasion.AskAboutWork(pm, shop));
        Assert.Contains(HeardBy(pm), t => t.EndsWith("Ask any banker."));
        Assert.Null(ClusterFPersuasion.CarriedLetter(pm));

        var otherPlayer = Player(0.0, new Point3D(Home.X, Home.Y + 1, 0));
        Assert.False(ClusterFPersuasion.Open(pm, otherPlayer));

        var pet = new Horse { Controlled = true, ControlMaster = pm };
        pet.MoveToWorld(Beside(pm), Map.Trammel);
        _made.Add(pet);
        Assert.False(ClusterFPersuasion.Open(pm, pet));

        var distant = Civilian(new Banker(), new Point3D(Home.X + 10, Home.Y, 0));
        Assert.False(ClusterFPersuasion.Open(pm, distant));
        Assert.True(ClusterFPersuasion.Open(pm, shop));
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void TheThiefTrialsPracticePersonStillCountsAndPaysNothing()
    {
        var enabled = ClusterFTutorial.Enabled;
        var fits = TutorialWingTrials.Fits;
        ClusterFTutorial.Enabled = true;
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
        TutorialWingTrials.EnsureHooks();
        TutorialWingTrials.Fits = _ => true;

        var acct = NewAccount();
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Trainee", RawStr = 60, RawDex = 60, RawInt = 20 };
        pm.Hits = pm.HitsMax;
        pm.AddItem(new Backpack());
        acct[0] = pm;
        pm.MoveToWorld(new Point3D(TutorialWingTrials.TradesYard.X, TutorialWingTrials.TradesYard.Y, 0), Map.Trammel);
        _made.Add(pm);
        var ns = PacketTestUtilities.CreateTestNetState();
        ns.Account = acct;
        pm.NetState = ns;
        ns.Mobile = pm;

        try
        {
            Assert.True(ClusterFTutorial.TryEnter(pm));
            ClusterFTutorial.RunStation(pm, TutorialStations.Find("thieving.trial")!);
            var trial = TutorialWingTrials.Current(pm)!;
            Assert.True(trial.Armed);
            var mark = trial.Creatures.Single();
            var before = pm.Skills.Begging.Base;

            ClusterFPersuasion.AskForHelp(pm, mark);

            Assert.Contains(SkillName.Begging.ToString(), trial.Steps);
            Assert.Equal(0, GoldOf(pm));
            Assert.Equal(0, MealsOf(pm));
            Assert.Equal(before, pm.Skills.Begging.Base);
        }
        finally
        {
            TutorialWingTrials.Cancel(pm);
            TutorialCoreTrials.Cancel(pm);
            ClusterFFanfare.FlushAll();
            pm.NetState = null;
            ns.Mobile = null;
            ns.Dispose();
            TutorialWingTrials.Fits = fits;
            ClusterFTutorial.Enabled = enabled;
        }
    }
}
