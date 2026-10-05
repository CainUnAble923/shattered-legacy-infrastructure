// cc-P9: the test shard behaves like an OSI Test Center, and none of it reaches a shard with the Test Center off.
// See shard-migration/notes/cc-P9-test-center.md.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh.
//
// TestCenter.Enabled has a private setter that only TestCenter.Configure assigns (TestCenter.cs:15-20), and Configure
// reads the config file. TestCenterSwitch flips the property for one fact and restores it, which is exactly the
// state a shard with testCenter.enable true or false is in once Configure has run.
//
// Characters are made through the real creation path, CharacterCreation.CharacterCreatedEvent with a real Account and
// a test NetState, as GargoyleStartingClothesVerification does, and deleted through the real
// AccountHandler.DeleteRequest that the 0x83 packet calls (IncomingAccountPackets.cs:190).

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.CharacterCreation;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TestCenterVerification
{
    private readonly ITestOutputHelper _out;

    public TestCenterVerification(ITestOutputHelper output) => _out = output;

    private sealed class TestCenterSwitch : IDisposable
    {
        private static readonly MethodInfo Setter =
            typeof(TestCenter).GetProperty(nameof(TestCenter.Enabled))!.GetSetMethod(true)!;

        private readonly bool _was;

        public TestCenterSwitch(bool on)
        {
            _was = TestCenter.Enabled;
            Setter.Invoke(null, [on]);
        }

        public void Dispose() => Setter.Invoke(null, [_was]);
    }

    // The startup hooks the creation path needs and the fixture never runs; see GargoyleStartingClothesVerification.
    // Accounts.Configure replaces the account persistence object, so it runs only if nothing has run it yet.
    private static void EnsureStartupHooks()
    {
        var persistence = typeof(Accounts).GetField("_accountsPersistence", BindingFlags.NonPublic | BindingFlags.Static);
        if (persistence!.GetValue(null) == null)
        {
            Accounts.Configure();
        }

        WelcomeTimer.Initialize();

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }
    }

    private static Mobile Create(Account account, NetState ns, string name)
    {
        var stats = ns.NewCharacterCreation ? new byte[] { 30, 30, 30 } : new byte[] { 30, 25, 25 };
        var city = new CityInfo("New Haven", "The Bountiful Harvest Inn", 1150168, 3503, 2574, 14, Map.Trammel);

        var args = new CharacterCreatedEventArgs(
            ns, account, name, false, 0, stats, city, [],
            0, 0, 0, 0, 0, 0,
            0, // no profession
            Race.Human
        );

        CharacterCreation.CharacterCreatedEvent(args);
        Assert.NotNull(args.Mobile);
        return args.Mobile;
    }

    private static Account NewAccount(string prefix) =>
        new($"{prefix}{Guid.NewGuid():N}"[..16], "p9-test-only");

    private static IEnumerable<Item> Deep(Container c)
    {
        foreach (var item in c.Items)
        {
            yield return item;

            if (item is Container inner)
            {
                foreach (var nested in Deep(inner))
                {
                    yield return nested;
                }
            }
        }
    }

    private static List<Bag> KitBags(Mobile m) =>
        new List<Item>(m.BankBox.Items).ConvertAll(i => i as Bag).FindAll(b => b?.Name == TestCenterKit.BagName);

    // Every container FillBankAOS puts in the bank, by the name it gives it (TestCenter.cs:236-545).
    // Core.Expansion is EJ in the fixture, so the AOS path and both SE-only bags run.
    private static readonly string[] PinnedBankBags =
    [
        "Various Potion Kegs", "Tool Bag", "Bag Of Archery Ammo", "Bag Of Treasure Maps", "Raw Materials Bag",
        "Spell Casting Stuff", "Bag Of Ethy's!", "Bag of Artifacts", "Bag of Minor Artifacts",
        "Tokuno Minor Artifacts", "Bag of Bows"
    ];

    private static void AssertPinnedFill(Mobile m)
    {
        var names = new List<Item>(m.BankBox.Items).ConvertAll(i => i.Name);

        foreach (var bag in PinnedBankBags)
        {
            Assert.True(names.Contains(bag), $"bank has no '{bag}'; it holds: {string.Join(", ", names)}");
        }

        Assert.Contains(Deep(m.BankBox), i => i is BankCheck { Worth: 500000 });
    }

    // ---- Part C: testCenter.skillGainMultiplier ----------------------------------------------------------------

    private static double[] GainFactors()
    {
        var table = SkillInfo.Table;
        Assert.True(table.Length >= 58, $"SkillInfo.Table has {table.Length} rows; SkillsInfo.Configure should load 58");
        return Array.ConvertAll(table, info => info.GainFactor);
    }

    [Fact]
    public void Test1_MultiplierLeavesEveryGainFactorAloneWhenTheTestCenterIsOff()
    {
        var before = GainFactors();

        try
        {
            Assert.False(TestCenterSkillGain.Apply(false, 5.0));
            Assert.Equal(before, GainFactors());
        }
        finally
        {
            TestCenterSkillGain.Restore();
        }
    }

    [Fact]
    public void Test2_MultiplierIsExactlyFiveTimesAndASecondApplyDoesNotCompound()
    {
        var before = GainFactors();

        try
        {
            Assert.True(TestCenterSkillGain.Apply(true, 5.0));
            Assert.True(TestCenterSkillGain.Apply(true, 5.0));

            var after = GainFactors();
            for (var i = 0; i < before.Length; i++)
            {
                Assert.True(
                    after[i] == before[i] * 5.0,
                    $"{SkillInfo.Table[i].Name}: {after[i]} is not 5 x {before[i]}"
                );
            }
        }
        finally
        {
            TestCenterSkillGain.Restore();
        }

        Assert.Equal(before, GainFactors());
    }

    // ---- Part B: the live-port guard ---------------------------------------------------------------------------

    [Fact]
    public void TheLivePortGuardTripsOnlyForTheTestCenterOnPort2593()
    {
        var live = new[] { new System.Net.IPEndPoint(System.Net.IPAddress.Any, 2593) };
        var test = new[] { new System.Net.IPEndPoint(System.Net.IPAddress.Any, 2594) };

        Assert.True(TestCenterLiveGuard.IsTestCenterOnLivePort(true, live));
        Assert.False(TestCenterLiveGuard.IsTestCenterOnLivePort(false, live));
        Assert.False(TestCenterLiveGuard.IsTestCenterOnLivePort(true, test));
    }

    // ---- Part D: [TCFill ---------------------------------------------------------------------------------------

    [Fact]
    public void Test3_TCFillIsNotRegisteredWhenTheTestCenterIsOff()
    {
        using var tc = new TestCenterSwitch(false);
        CommandSystem.Entries.Remove(TestCenterFillCommand.Command);

        TestCenterFillCommand.Initialize();

        Assert.False(CommandSystem.Entries.ContainsKey(TestCenterFillCommand.Command));
    }

    [Fact]
    public void TCFillRestocksAnExistingCharacterKeepsOurCapsAndCoolsDown()
    {
        using var tc = new TestCenterSwitch(true);
        var entry = TestCenterKit.Register(() => new Feather(), 4321);
        var pm = new PlayerMobile { Player = true };

        try
        {
            TestCenterFillCommand.Initialize();
            Assert.True(CommandSystem.Entries.TryGetValue(TestCenterFillCommand.Command, out var command));
            Assert.Equal(AccessLevel.Player, command.AccessLevel);

            // What ClusterFSkillCaps and ClusterFStatCaps hold every player at on this shard (skills 200 since cc-P53).
            for (var i = 0; i < pm.Skills.Length; i++)
            {
                pm.Skills[i].CapFixedPoint = 2000;
            }

            pm.StatCap = 1500;

            command.Handler(new CommandEventArgs(pm, TestCenterFillCommand.Command, "", []));

            AssertPinnedFill(pm);
            Assert.Single(KitBags(pm));

            for (var i = 0; i < pm.Skills.Length; i++)
            {
                Assert.True(
                    pm.Skills[i].CapFixedPoint == 2000,
                    $"{pm.Skills[i].Name} cap is {pm.Skills[i].Cap} after [TCFill; ours is 200"
                );
            }

            Assert.Equal(1500, pm.StatCap);

            var count = pm.BankBox.Items.Count;
            command.Handler(new CommandEventArgs(pm, TestCenterFillCommand.Command, "", []));
            Assert.Equal(count, pm.BankBox.Items.Count);
        }
        finally
        {
            CommandSystem.Entries.Remove(TestCenterFillCommand.Command);
            TestCenterKit.Unregister(entry);
            pm.Delete();
        }
    }

    // ---- Test 4 and Part F: the bank at creation ---------------------------------------------------------------

    [Fact]
    public void Test4And6_ANewTestCenterCharacterHasAFilledBankAndTheTestKit()
    {
        EnsureStartupHooks();
        using var tc = new TestCenterSwitch(true);

        var feathers = TestCenterKit.Register(() => new Feather(), 4321);
        var bags = TestCenterKit.Register(() => new Bag(), TestCenterKit.MaxNonStackable + 5);
        var account = NewAccount("p9fill");
        using var ns = PacketTestUtilities.CreateTestNetState();
        Mobile m = null;

        try
        {
            m = Create(account, ns, "Pnine");

            AssertPinnedFill(m);

            var kits = KitBags(m);
            Assert.Single(kits);

            var kit = new List<Item>(kits[0].Items);
            var feather = Assert.Single(kit.FindAll(i => i is Feather));
            Assert.Equal(4321, feather.Amount);

            // A non-stackable entry asking for more than the cap gets the cap.
            Assert.Equal(TestCenterKit.MaxNonStackable, kit.FindAll(i => i.GetType() == typeof(Bag)).Count);
        }
        finally
        {
            TestCenterKit.Unregister(feathers);
            TestCenterKit.Unregister(bags);
            m?.Delete();
            Accounts.Remove(account);
        }
    }

    [Fact]
    public void Test6_WithTheTestCenterOffNoFillAndNoKitIsEverPlaced()
    {
        EnsureStartupHooks();
        using var tc = new TestCenterSwitch(false);

        var entry = TestCenterKit.Register(() => new Feather(), 4321);
        var account = NewAccount("p9off");
        using var ns = PacketTestUtilities.CreateTestNetState();
        Mobile m = null;

        try
        {
            m = Create(account, ns, "Pnineoff");

            var names = new List<Item>(m.BankBox.Items).ConvertAll(i => i.Name);
            Assert.DoesNotContain("Tool Bag", names);
            Assert.Empty(KitBags(m));

            // The placer is the gate, not only the creation hook: called directly it places nothing either.
            Assert.Null(TestCenterKit.PlaceKit(m));
            Assert.Empty(KitBags(m));
        }
        finally
        {
            TestCenterKit.Unregister(entry);
            m?.Delete();
            Accounts.Remove(account);
        }
    }

    [Fact]
    public void EveryShatteredLegacyKitEntryMakesAStackOfItsAmount()
    {
        var before = new List<TestCenterKit.Entry>(TestCenterKit.Entries);
        TestCenterKitEntries.Configure();
        var added = new List<TestCenterKit.Entry>(TestCenterKit.Entries).FindAll(e => !before.Contains(e));

        try
        {
            // 8 ores and 8 ingots (ClusterFExtendedOres.cs), 8 logs and 8 boards (ClusterFExtendedLumber.cs).
            Assert.Equal(32, added.Count);

            var types = new HashSet<Type>();
            foreach (var entry in added)
            {
                var item = entry.Factory();
                try
                {
                    Assert.True(item.Stackable, $"{item.GetType().Name} is not stackable");
                    Assert.Equal("Server.Items", item.GetType().Namespace);
                    Assert.True(types.Add(item.GetType()), $"{item.GetType().Name} registered twice");
                }
                finally
                {
                    item.Delete();
                }
            }
        }
        finally
        {
            foreach (var entry in added)
            {
                TestCenterKit.Unregister(entry);
            }
        }
    }

    // ---- Part E: deletion with no wait on a Test Center shard, the 7-day wait kept elsewhere ------------------

    // Deletes character m from its account through the real handler and returns the 0x85 result it sent, or null when
    // the handler deleted the character (success sends no 0x85, only the character list, AccountHandler.cs:227-234).
    private static DeleteResultType? RequestDelete(NetState ns, Account account, Mobile m)
    {
        var index = -1;
        for (var i = 0; i < account.Length; i++)
        {
            if (account[i] == m)
            {
                index = i;
            }
        }

        Assert.True(index >= 0, "the character is not on its account");

        ns.Account = account;
        var start = ns.SendBuffer.GetReadSpan().Length;

        AccountHandler.DeleteRequest(ns, index);

        var sent = ns.SendBuffer.GetReadSpan()[start..];
        return sent.Length >= 2 && sent[0] == 0x85 ? (DeleteResultType)sent[1] : null;
    }

    private void DeleteAOneSecondOldCharacter(bool testCenter, DeleteResultType? expected)
    {
        EnsureStartupHooks();

        // What production does when AccountHandler's statics are touched before TestCenter.Configure has run: the
        // class initializes with the Test Center still off. Forced here so the fact does not depend on which other
        // tests ran first in this process.
        RuntimeHelpers.RunClassConstructor(typeof(AccountHandler).TypeHandle);

        var account = NewAccount("p9del");
        using var ns = PacketTestUtilities.CreateTestNetState();
        Mobile m = null;

        try
        {
            using (new TestCenterSwitch(false))
            {
                m = Create(account, ns, "Pninedel");
            }

            m.Created = Core.Now - TimeSpan.FromSeconds(1);
            Assert.Null(m.NetState);
            Assert.Equal(AccessLevel.Player, account.AccessLevel);

            using (new TestCenterSwitch(testCenter))
            {
                var result = RequestDelete(ns, account, m);
                _out.WriteLine($"test center {testCenter}: result {result?.ToString() ?? "deleted"}");

                Assert.Equal(expected, result);
                Assert.Equal(expected == null, m.Deleted);
            }
        }
        finally
        {
            m?.Delete();
            Accounts.Remove(account);
        }
    }

    [Fact]
    public void Test5a_OnATestCenterAPlayerDeletesACharacterMadeOneSecondAgo() =>
        DeleteAOneSecondOldCharacter(true, null);

    [Fact]
    public void Test5b_WithTheTestCenterOffTheSameDeletionIsCharTooYoung() =>
        DeleteAOneSecondOldCharacter(false, DeleteResultType.CharTooYoung);
}
