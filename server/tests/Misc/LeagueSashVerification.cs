// LeagueSashVerification.cs
//
// cc-P800: the League sash that grows with its wearer. Notes in shard-migration notes/cc-P800-league-sash.md.
//
// Facts:
//   1. Every step: closed below its rank, then bought at its rank for its fee, with its name, hue and bonus; rank and
//      LifetimeRenown never move.
//   2. One Renown short is refused with nothing taken; the last step has no next.
//   3. Elara's page: a stale page charges once; the first sash is free and only one; an unregistered character gets none.
//   4. A lost sash is replaced free at the character's step; no second one while they hold it.
//   5. Old sashes convert in place (worn, pack, bank) at step 0; a second is a keepsake; a second pass changes nothing; other
//      sashes and sashes held by no character are left.
//   5b. An old sash the load could not place (its holder had no Account yet) converts at login.
//   6. Bound to the character, blessed, no dye, no scissors, and a second character of the same account cannot take it.
//   7. A loop keeps the sash, its step and its bonus, and the sash says so.
//   8. The bonus: only worn, only your own, only achievements and League jobs; a fraction of a Renown carries.
//   9. The file: defaults round-trip, a bad file is refused whole, a missing file is written, a bonus of 0 is off.
//  10. Boxes: the sash leaves the pool once the character has one; an opened box gives the opener theirs; a second is a keepsake.
//  11. A sash survives a save with its owner and step.
//  12. A real achievement pays the bonus.

using System;
using System.IO;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.LeagueRewards;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeagueSashVerification
{
    private static bool _achievements;

    private readonly ITestOutputHelper _out;

    public LeagueSashVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        LeagueSashTable.Current = LeagueSashTable.Defaults;
    }

    // ---------------------------------------------------------------- helpers

    private static Account NewAccount() => new($"p800{Guid.NewGuid():N}"[..16], "p800-test-only");

    private static PlayerMobile NewCharacter(Account account, int slot = 0, bool register = true)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
        pm.AddItem(new Backpack());
        account[slot] = pm;
        pm.MoveToWorld(new Point3D(3460, 2604, 18), Map.Trammel);

        if (register)
        {
            Data(account).SetFlag(ClusterFLeagueSystem.FlagJoined);
        }

        return pm;
    }

    private static ClusterFAccountData Data(Account account) => ClusterFAccountPersistence.GetOrCreate(account);

    private static NetState Online(PlayerMobile pm)
    {
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        ns.Account = pm.Account;
        return ns;
    }

    private static void Cleanup(Account account)
    {
        for (var i = 0; i < account.Length; i++)
        {
            if (account[i] is PlayerMobile pm)
            {
                if (pm.NetState is { } ns)
                {
                    pm.NetState = null;
                    ns.Mobile = null;
                    ns.Dispose();
                }

                pm.Delete();
            }
        }

        Accounts.Remove(account);
    }

    private static void Press(BaseGump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    private static bool HasButton(Gump gump, int id) => gump.Entries.OfType<GumpButton>().Any(b => b.ButtonID == id);

    private static System.Collections.Generic.List<T> InPack<T>(PlayerMobile pm) where T : Item
    {
        var list = new System.Collections.Generic.List<T>();

        foreach (var item in pm.Backpack.FindItemsByType<T>())
        {
            list.Add(item);
        }

        return list;
    }

    private static LeagueSash Issue(PlayerMobile pm)
    {
        var sash = LeagueSashSystem.Issue(pm, out var message);
        Assert.True(sash != null, message);
        return sash;
    }

    private static LeagueSash IssueAtStep(PlayerMobile pm, int step)
    {
        var sash = Issue(pm);
        ClusterFLeagueRanks.SetRank(pm, LeagueSashTable.Current.RankNeeded(LeagueSashTable.Current.MaxStep));
        Data((Account)pm.Account).Renown = int.MaxValue / 2;

        for (var s = 0; s < step; s++)
        {
            Assert.True(LeagueSashSystem.Upgrade(pm, s, out var message), message);
        }

        return sash;
    }

    // ---------------------------------------------------------------- 1

    [Theory]
    [InlineData(1, "a Dull Copper League sash", "DullCopper", 50, 1)]
    [InlineData(2, "a Bronze League sash", "Bronze", 250, 2)]
    [InlineData(3, "a Verite League sash", "Verite", 1000, 3)]
    [InlineData(4, "a Blaze League sash", "Blaze", 3000, 4)]
    [InlineData(5, "a Mythril League sash", "Mythril", 6000, 5)]
    [InlineData(6, "a Celestial League sash", "Celestial", 10000, 6)]
    public void EveryStepOpensAtItsRankAndIsBoughtForItsFee(int step, string name, string metal, int fee, int bonus)
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            var sash = IssueAtStep(pm, step - 1);
            var rank = ClusterFLeagueRanks.Ladder.First(r => r.MetalKey == metal);
            var data = Data(account);
            data.Renown = fee + 7;
            data.LifetimeRenown = 12345;

            // A rank below the step's: refused, nothing taken.
            ClusterFLeagueRanks.SetRank(pm, rank.Index - 1);
            Assert.False(LeagueSashSystem.Upgrade(pm, step - 1, out var why));
            _out.WriteLine(why);
            Assert.Equal(fee + 7, data.Renown);
            Assert.Equal(step - 1, sash.Step);

            ClusterFLeagueRanks.SetRank(pm, rank.Index);
            Assert.True(LeagueSashSystem.Upgrade(pm, step - 1, out var message), message);

            Assert.Equal(step, sash.Step);
            Assert.Equal(step, LeagueSashSystem.StepOf(pm));
            Assert.Equal(name, sash.DefaultName);
            Assert.Equal(rank.Hue, sash.Hue);
            Assert.NotEqual(LeagueShop.LeagueHue, sash.Hue);
            Assert.Equal(7, data.Renown);
            Assert.Equal(12345, data.LifetimeRenown);
            Assert.Equal(rank.Index, ClusterFLeagueRanks.GetRank(pm));
            Assert.Contains($"+{bonus}% Renown earned while worn", sash.Lines());
            Assert.Contains($"step {step} of 6", sash.Lines()[0]);
            Assert.Contains($"{rank.MetalName} Citizen", sash.Lines()[0]);
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void TheFirstSashIsLeagueBlueWithNoBonus()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            var sash = Issue(pm);
            Assert.Equal("a League sash", sash.DefaultName);
            Assert.Equal(LeagueShop.LeagueHue, sash.Hue);
            Assert.Equal(0, sash.Step);
            Assert.DoesNotContain(sash.Lines(), l => l.Contains("Renown"));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void OneRenownShortTakesNothingAndTheLastStepHasNoNext()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            var sash = Issue(pm);
            ClusterFLeagueRanks.SetRank(pm, 2);
            Data(account).Renown = 49;

            Assert.False(LeagueSashSystem.Upgrade(pm, 0, out _));
            Assert.Equal(49, Data(account).Renown);
            Assert.Equal(0, sash.Step);

            Cleanup(account);
            account = NewAccount();
            pm = NewCharacter(account);
            sash = IssueAtStep(pm, 6);
            var left = Data(account).Renown;

            Assert.False(LeagueSashSystem.Upgrade(pm, 6, out var message));
            _out.WriteLine(message);
            Assert.Equal(left, Data(account).Renown);
            Assert.Equal(6, sash.Step);
            Assert.False(HasButton(new LeagueRegistrarGump(pm, LeagueRegistrarGump.View.Sash), LeagueRegistrarGump.BtnSashUpgrade));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ElarasPageGivesOneFreeSashAndAStalePageChargesOnce()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        var ns = Online(pm);

        try
        {
            var main = new LeagueRegistrarGump(pm);
            Assert.True(HasButton(main, LeagueRegistrarGump.BtnSash));

            var offer = new LeagueRegistrarGump(pm, LeagueRegistrarGump.View.Sash);
            Assert.True(HasButton(offer, LeagueRegistrarGump.BtnSashTake));
            Assert.False(HasButton(offer, LeagueRegistrarGump.BtnSashUpgrade));

            Press(offer, ns, LeagueRegistrarGump.BtnSashTake);
            Press(offer, ns, LeagueRegistrarGump.BtnSashTake); // the same page again

            Assert.Single(InPack<LeagueSash>(pm));
            var sash = LeagueSashSystem.Find(pm);
            Assert.Equal(0, Data(account).Renown);

            // Below the rank, or short of Renown, the page has no upgrade button.
            Data(account).Renown = 500;
            Assert.False(HasButton(new LeagueRegistrarGump(pm, LeagueRegistrarGump.View.Sash), LeagueRegistrarGump.BtnSashUpgrade));

            ClusterFLeagueRanks.SetRank(pm, ClusterFLeagueRanks.TopRank);
            var page = new LeagueRegistrarGump(pm, LeagueRegistrarGump.View.Sash);
            Assert.True(HasButton(page, LeagueRegistrarGump.BtnSashUpgrade));

            Press(page, ns, LeagueRegistrarGump.BtnSashUpgrade);
            Press(page, ns, LeagueRegistrarGump.BtnSashUpgrade); // stale: it was drawn at step 0

            Assert.Equal(1, sash.Step);
            Assert.Equal(450, Data(account).Renown);
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void AnUnregisteredCharacterGetsNoSash()
    {
        var account = NewAccount();
        var pm = NewCharacter(account, register: false);

        try
        {
            Assert.Null(LeagueSashSystem.Issue(pm, out var message));
            _out.WriteLine(message);
            Assert.Null(LeagueSashSystem.Find(pm));
            Assert.False(HasButton(new LeagueRegistrarGump(pm, LeagueRegistrarGump.View.Sash), LeagueRegistrarGump.BtnSashTake));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ALostSashComesBackFreeAtItsStepAndNeverASecond()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            var sash = IssueAtStep(pm, 3);
            var renown = Data(account).Renown;

            Assert.Null(LeagueSashSystem.Issue(pm, out _));
            sash.Delete();
            Assert.True(LeagueSashSystem.Has(pm));

            var back = Issue(pm);
            Assert.Equal(3, back.Step);
            Assert.Equal("a Verite League sash", back.DefaultName);
            Assert.Equal(renown, Data(account).Renown);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 5

    private static BoundCosmeticSash Old(Account account, string label = "League", int hue = 1234) => new(account.Username, label, hue);

    [Fact]
    public void OldSashesConvertInPlaceOnceAndTheRestAreKeepsakes()
    {
        var a1 = NewAccount();
        var a2 = NewAccount();
        var a3 = NewAccount();
        var worn = NewCharacter(a1);
        var inPack = NewCharacter(a2);
        var inBank = NewCharacter(a3);
        var alsoOnA1 = NewCharacter(a1, 1);

        try
        {
            var w = Old(a1);
            worn.AddItem(w);

            var keep = Old(a2);
            var second = Old(a2);
            var other = Old(a2, "Box");
            inPack.Backpack.DropItem(keep);
            keep.Location = new Point3D(55, 66, 0);
            inPack.Backpack.DropItem(second);
            inPack.Backpack.DropItem(other);

            var b = Old(a3);
            inBank.BankBox.DropItem(b);

            var onA1Second = Old(a1);
            alsoOnA1.Backpack.DropItem(onA1Second);

            // In the world, no character holds it: left alone.
            var loose = Old(a1);
            loose.MoveToWorld(new Point3D(3461, 2604, 18), Map.Trammel);

            Assert.True(LeagueSashSystem.ConvertOldSashes() >= 4);

            var wornNew = Assert.IsType<LeagueSash>(worn.FindItemOnLayer(Layer.MiddleTorso));
            Assert.True(wornNew.IsOwner(worn));
            Assert.Equal(0, wornNew.Step);
            Assert.Equal(LeagueShop.LeagueHue, wornNew.Hue);
            Assert.True(w.Deleted);
            Assert.Equal(LootType.Blessed, wornNew.LootType);

            var packNew = LeagueSashSystem.Find(inPack);
            Assert.NotNull(packNew);
            Assert.Same(inPack.Backpack, packNew.Parent);
            Assert.Equal(new Point3D(55, 66, 0), packNew.Location);
            Assert.True(keep.Deleted);
            Assert.False(second.Deleted);
            Assert.Equal(LeagueSashSystem.KeepsakeLabel, second.Label);
            Assert.Equal("Box", other.Label);

            var bankNew = LeagueSashSystem.Find(inBank);
            Assert.NotNull(bankNew);
            Assert.Same(inBank.BankBox, bankNew.Parent);

            // Each character of an account gets their own.
            Assert.NotNull(LeagueSashSystem.Find(alsoOnA1));

            Assert.False(loose.Deleted);
            Assert.Equal("League", loose.Label);

            // Idempotent: the pass finds nothing it has not done, and a held step is not reset.
            Data(a3).Renown = 100;
            ClusterFLeagueRanks.SetRank(inBank, 2);
            Assert.True(LeagueSashSystem.Upgrade(inBank, 0, out _));
            loose.Delete();

            Assert.Equal(0, LeagueSashSystem.ConvertOldSashes());
            Assert.Equal(1, bankNew.Step);
            Assert.Same(packNew, LeagueSashSystem.Find(inPack));
            Assert.False(second.Deleted);
        }
        finally
        {
            Cleanup(a1);
            Cleanup(a2);
            Cleanup(a3);
        }
    }

    [Fact]
    public void AnOldSashTheLoadCouldNotPlaceConvertsAtLogin()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            var held = Old(account);
            var second = Old(account);
            pm.Backpack.DropItem(held);
            pm.BankBox.DropItem(second);

            // As on a real load: the holder's Account is not set yet, so the pass must leave them, and touch nothing.
            pm.Account = null;
            LeagueSashSystem.ConvertOldSashes();
            Assert.False(held.Deleted);
            Assert.Equal("League", second.Label);

            pm.Account = account;
            LeagueSashSystem.OnLogin(pm);

            Assert.True(held.Deleted);
            var sash = LeagueSashSystem.Find(pm);
            Assert.NotNull(sash);
            Assert.Same(pm.Backpack, sash.Parent);
            Assert.Equal(LeagueSashSystem.KeepsakeLabel, second.Label);

            // A second login changes nothing, and other cosmetics never convert.
            var box = Old(account, "Box");
            pm.Backpack.DropItem(box);
            LeagueSashSystem.OnLogin(pm);
            Assert.Same(sash, LeagueSashSystem.Find(pm));
            Assert.Equal("Box", box.Label);
            Assert.False(box.Deleted);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheSashIsBlessedAndBoundToTheCharacter()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        var twin = NewCharacter(account, 1);
        var stranger = NewAccount();
        var other = NewCharacter(stranger);

        try
        {
            var sash = Issue(pm);
            Assert.Equal(LootType.Blessed, sash.LootType);

            var reject = LRReason.Inspecific;
            Assert.False(sash.CheckLift(twin, sash, ref reject)); // same account, another character
            Assert.False(sash.CheckLift(other, sash, ref reject));
            Assert.True(sash.CheckLift(pm, sash, ref reject));

            Assert.False(sash.AllowSecureTrade(pm, other, other, false));
            Assert.False(sash.OnDroppedToWorld(pm, pm.Location));
            Assert.False(sash.OnDroppedToMobile(pm, twin));
            Assert.False(sash.OnDroppedInto(pm, twin.Backpack, Point3D.Zero));
            Assert.True(sash.OnDroppedInto(pm, pm.Backpack, Point3D.Zero));
            Assert.False(sash.OnDroppedInto(pm, new Bag(), Point3D.Zero)); // a chest: no holder

            Assert.False(sash.Dye(pm, new DyeTub()));
            Assert.False(sash.Scissor(pm, new Scissors()));
            Assert.False(sash.Deleted);

            // Staff can still move it; a worn stranger's sash pays nobody else (fact 8).
            twin.AccessLevel = AccessLevel.GameMaster;
            Assert.True(sash.CheckLift(twin, sash, ref reject));
        }
        finally
        {
            Cleanup(account);
            Cleanup(stranger);
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void ALoopKeepsTheSashAndItsStep()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            var sash = IssueAtStep(pm, 4);
            pm.AddItem(sash);
            var rank = ClusterFLeagueRanks.GetRank(pm);

            Assert.DoesNotContain(sash.Lines(), l => l.Contains("Worn through"));

            ClusterFLoops.CompleteLoop(pm, 1);
            Assert.Contains("Worn through 1 loop", sash.Lines());
            ClusterFLoops.CompleteLoop(pm, 2);
            Assert.Contains("Worn through 2 loops", sash.Lines());

            Assert.False(sash.Deleted);
            Assert.Same(sash, LeagueSashSystem.Find(pm));
            Assert.Equal(pm, sash.Parent);
            Assert.Equal(4, sash.Step);
            Assert.Equal(4, LeagueSashSystem.StepOf(pm));
            Assert.Equal(4, LeagueSashSystem.WornBonus(pm));
            Assert.Equal(rank, ClusterFLeagueRanks.GetRank(pm));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void TheBonusIsPaidOnlyWhileWornAndOnlyOnDeeds()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        var twin = NewCharacter(account, 1);

        try
        {
            var sash = IssueAtStep(pm, 3);
            var data = Data(account);
            data.Renown = 0;
            data.LifetimeRenown = 0;

            // In the pack: no bonus.
            LeagueSashSystem.Earn(data, pm, 1000, RenownSource.Achievement);
            Assert.Equal(1000, data.Renown);

            pm.AddItem(sash);
            data.Renown = 0;
            data.LifetimeRenown = 0;

            LeagueSashSystem.Earn(data, pm, 1000, RenownSource.Achievement);
            Assert.Equal(1030, data.Renown);
            Assert.Equal(1030, data.LifetimeRenown);

            LeagueSashSystem.Earn(data, pm, 1000, RenownSource.LeagueJob);
            Assert.Equal(2060, data.Renown);

            // Renown from another player or staff: none, and a Commission still never counts toward rank.
            LeagueSashSystem.Earn(data, pm, 1000, RenownSource.Commission);
            Assert.Equal(3060, data.Renown);
            Assert.Equal(2060, data.LifetimeRenown);
            LeagueSashSystem.Earn(data, pm, 1000, RenownSource.Staff);
            Assert.Equal(4060, data.Renown);

            // Offline, or another character of the account: none.
            LeagueSashSystem.Earn(data, null, 1000, RenownSource.Achievement);
            Assert.Equal(5060, data.Renown);
            LeagueSashSystem.Earn(data, twin, 1000, RenownSource.Achievement);
            Assert.Equal(6060, data.Renown);

            // A sash that is not theirs pays nobody.
            sash.Delete();
            var stolen = new LeagueSash(pm, 6);
            twin.AddItem(stolen);
            Assert.Equal(0, LeagueSashSystem.WornBonus(twin));
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void AFractionOfARenownCarriesToTheNextGrant()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            var sash = IssueAtStep(pm, 3); // +3%
            pm.AddItem(sash);
            var data = Data(account);
            data.Renown = 0;

            // 10 at 3% is 0.3: six such grants pay 1.8, so one whole Renown and 0.8 still carried.
            for (var i = 0; i < 6; i++)
            {
                LeagueSashSystem.Earn(data, pm, 10, RenownSource.Achievement);
            }

            Assert.Equal(61, data.Renown);

            LeagueSashSystem.Earn(data, pm, 10, RenownSource.Achievement); // 1.1
            Assert.Equal(72, data.Renown);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void TheFileRoundTripsAndABadOneIsRefusedWhole()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"p800{Guid.NewGuid():N}");
        var path = Path.Combine(dir, LeagueSashTable.FileName);

        try
        {
            // Missing: the defaults are written, and read back the same.
            var lines = LeagueSashTable.LoadOrWriteDefaults(path);
            _out.WriteLine(string.Join("\n", lines));
            Assert.True(File.Exists(path));
            Assert.Contains(lines, l => l.Contains("wrote the built-in defaults"));

            var errors = new System.Collections.Generic.List<string>();
            var read = LeagueSashTable.Parse(File.ReadAllText(path), path, errors);
            Assert.NotNull(read);
            Assert.Empty(errors);
            Assert.Equal(LeagueSashTable.ToJson(LeagueSashTable.Defaults), LeagueSashTable.ToJson(read));
            Assert.Equal(6, read.MaxStep);
            Assert.Equal(LeagueShop.LeagueHue, read.FirstHue);

            // Edited: fees, and a bonus of 0 turns it off.
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"fee\": 50,", "\"fee\": 75,").Replace("\"bonusPercent\": 1 ", "\"bonusPercent\": 0 "));
            LeagueSashTable.Reload(path);
            Assert.Equal(75, LeagueSashTable.Current.FeeOf(1));
            Assert.Equal(0, LeagueSashTable.Current.BonusOf(1));
            Assert.Equal(path, LeagueSashTable.Current.Source);

            var inForce = LeagueSashTable.Current;

            foreach (var bad in new[]
                     {
                         "not json",
                         "{}",
                         "{ \"steps\": [] }",
                         GoodWith("\"metal\": \"Moonstone\", \"fee\": 1, \"bonusPercent\": 1"),
                         GoodWith("\"metal\": \"Iron\", \"fee\": 1, \"bonusPercent\": 1"),
                         GoodWith("\"metal\": \"DullCopper\", \"fee\": -1, \"bonusPercent\": 1"),
                         GoodWith("\"metal\": \"DullCopper\", \"fee\": 1, \"bonusPercent\": 101"),
                         GoodWith("\"metal\": \"DullCopper\", \"fee\": 1"),
                     })
            {
                File.WriteAllText(path, bad);
                var report = LeagueSashTable.Reload(path);
                _out.WriteLine(string.Join(" | ", report));
                Assert.Contains("REFUSED", report[0]);
                Assert.Same(inForce, LeagueSashTable.Current);
            }

            // Rising rank order is required.
            var order = new System.Collections.Generic.List<string>();
            Assert.Null(LeagueSashTable.Parse(
                "{ \"firstHue\": 1, \"upgradeSound\": 1, \"upgradeEffect\": 1, \"steps\": [ " +
                "{ \"metal\": \"Bronze\", \"fee\": 1, \"bonusPercent\": 1 }, { \"metal\": \"DullCopper\", \"fee\": 1, \"bonusPercent\": 1 } ] }",
                "test", order));
            Assert.NotEmpty(order);
        }
        finally
        {
            LeagueSashTable.Current = LeagueSashTable.Defaults;

            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    private static string GoodWith(string row) =>
        "{ \"firstHue\": 1, \"upgradeSound\": 1, \"upgradeEffect\": 1, \"steps\": [ { " + row + " } ] }";

    // ---------------------------------------------------------------- 10

    [Fact]
    public void BoxesGiveTheSashOnlyToACharacterWithoutOne()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            static bool HoldsSash(Func<PlayerMobile, Item> f, PlayerMobile who)
            {
                var item = f(who);
                var sash = item is BoundCosmeticSash;
                item.Delete();
                return sash;
            }

            Assert.Contains(LeagueShop.BoxPool(LeagueShop.BoxTier.Bronze, pm), f => HoldsSash(f, pm));
            Assert.True(LeagueSashSystem.BoxMayGive(pm));

            // A box rolled before the character has theirs, opened: it becomes their sash, and it is the only one.
            var box = new LootBox(BoxTheme.League, 1, account.Username, "test");
            box.Fill("a test box", 0, 0, 0, [(Old(account), false)]);
            pm.Backpack.DropItem(box);
            box.Open(pm);

            var sash = LeagueSashSystem.Find(pm);
            Assert.NotNull(sash);
            Assert.Equal(0, sash.Step);
            Assert.Empty(InPack<BoundCosmeticSash>(pm));

            // Now the pool has none, and the League theme's slot rolls something else.
            Assert.False(LeagueSashSystem.BoxMayGive(pm));
            Assert.DoesNotContain(LeagueShop.BoxPool(LeagueShop.BoxTier.Bronze, pm), f => HoldsSash(f, pm));
            Assert.Contains(LeagueShop.BoxPool(LeagueShop.BoxTier.Bronze), f => HoldsSash(f, pm));

            for (var i = 0; i < 40; i++)
            {
                var made = LeagueShop.MakeBox(pm, LeagueShop.BoxTier.Bronze);
                Assert.DoesNotContain(made.Contents, c => c is BoundCosmeticSash or LeagueSash);
                made.Delete();
            }

            // A second rolled before the first was opened: a keepsake, never a second sash.
            var late = new LootBox(BoxTheme.League, 1, account.Username, "test");
            late.Fill("a late box", 0, 0, 0, [(Old(account), false)]);
            pm.Backpack.DropItem(late);
            late.Open(pm);

            var keepsake = Assert.Single(InPack<BoundCosmeticSash>(pm));
            Assert.Equal(LeagueSashSystem.KeepsakeLabel, keepsake.Label);
            Assert.Single(InPack<LeagueSash>(pm));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 11

    [Fact]
    public void ASashSurvivesASave()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            var sash = IssueAtStep(pm, 2);

            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            sash.Serialize(writer);
            writer.Flush();

            var copy = (LeagueSash)Activator.CreateInstance(typeof(LeagueSash), sash.Serial)!;
            copy.Deserialize(new BufferReader(buffer));

            Assert.Equal(2, copy.Step);
            Assert.True(copy.IsOwner(pm));
            Assert.Equal("a Bronze League sash", copy.DefaultName);
            Assert.Equal(LootType.Blessed, copy.LootType);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 12

    [Fact]
    public void ARealAchievementPaysTheWornBonus()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        var ns = Online(pm);

        try
        {
            if (!_achievements)
            {
                ClusterFAchievementSystem.Configure();
                _achievements = true;
            }

            var sash = IssueAtStep(pm, 6); // +6%
            pm.AddItem(sash);

            var data = Data(account);
            var def = ClusterFAchievementSystem.Definitions.Values.First(d => d.Renown >= 50 && d.PrerequisiteKey == null && !d.Retired);
            data.Renown = 0;

            Assert.True(ClusterFAchievementSystem.TryGrant(account, def.Key));
            Assert.Equal(def.Renown + def.Renown * 6 / 100, data.Renown);
        }
        finally
        {
            _ = ns;
            Cleanup(account);
        }
    }
}
