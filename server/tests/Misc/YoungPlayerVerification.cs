// YoungPlayerVerification.cs
//
// cc-P18, F-5: Young status by time played, 2 weeks (ClusterFYoungPlayer.cs, and the two patches it names).
// Notes in shard-migration notes/cc-P18-reset-stone-young-craftx.md.
//
// Facts (the brief's numbering):
//   1. A Young account under 336 hours of play stays Young with a skill total over 450.
//   2. At 336 hours CheckYoung removes Young, from every character on the account; at 40 hours (pinned's
//      limit) it does not.
//   3. OSI's phrase and "I want to grow up!" (any case, "!" optional) both open the renounce confirm;
//      anything else does not; the confirm's OKAY ends Young.
// And: a murder count still ends Young (kept), and the login countdown reads in days and hours.

using System;
using System.Reflection;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class YoungPlayerVerification
{
    private readonly ITestOutputHelper _out;

    private static bool _speechHooked;
    private static bool _accountsConfigured;

    public YoungPlayerVerification(ITestOutputHelper output)
    {
        _out = output;

        // As GuildStarterPathVerification: new Account(...) needs Accounts.Configure and a password algorithm.
        if (!_accountsConfigured)
        {
            Accounts.Configure();
            _accountsConfigured = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }

        // The server registers both speech handlers at start-up; the test host registers neither.
        if (!_speechHooked)
        {
            Keywords.Initialize();
            ClusterFYoungPlayer.Configure();
            _speechHooked = true;
        }
    }

    private static readonly FieldInfo GameTime =
        typeof(Account).GetField("_totalGameTime", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static Account NewAccount() => new($"p18y{Guid.NewGuid():N}"[..16], "p18-test-only");

    private static PlayerMobile NewYoungCharacter(Account account, int slot)
    {
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        account[slot] = pm;
        pm.Young = account.Young; // as CharacterCreation does
        pm.MoveToWorld(new Point3D(1220, 1220, 0), Map.Trammel);
        return pm;
    }

    private static void Played(Account account, TimeSpan time) => GameTime.SetValue(account, time);

    private static void Cleanup(Account account)
    {
        for (var i = 0; i < account.Length; i++)
        {
            if (account[i] is PlayerMobile pm)
            {
                if (pm.NetState != null)
                {
                    pm.NetState.Mobile = null;
                    pm.NetState = null;
                }

                pm.Delete();
            }
        }

        Accounts.Remove(account);
    }

    [Fact]
    public void TheDurationIsTwoWeeksOfPlay()
    {
        Assert.Equal(TimeSpan.FromHours(336), ClusterFYoungPlayer.Duration);
        Assert.Equal(ClusterFYoungPlayer.Duration, Account.YoungDuration); // the patch reached pinned
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void AYoungAccountUnder336HoursStaysYoungWithSkillsOver450()
    {
        var account = NewAccount();
        var pm = NewYoungCharacter(account, 0);

        try
        {
            Assert.True(account.Young);
            Assert.True(pm.Young);
            Played(account, TimeSpan.FromHours(335));

            // Seven skills at 70 is 490.0; each change runs PlayerMobile.OnSkillChange, where OSI's rule was.
            foreach (var skill in new[]
                     {
                         SkillName.Swords, SkillName.Tactics, SkillName.Anatomy, SkillName.Healing,
                         SkillName.Parry, SkillName.Mining, SkillName.Blacksmith
                     })
            {
                pm.Skills[skill].Base = 70.0;
            }

            _out.WriteLine($"skill total {pm.SkillsTotal / 10.0:F1}, played {account.TotalGameTime.TotalHours} h");
            Assert.True(pm.SkillsTotal >= 4500);

            account.CheckYoung();
            Assert.True(account.Young);
            Assert.True(pm.Young);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void At336HoursCheckYoungRemovesYoungFromEveryCharacter()
    {
        var account = NewAccount();
        var first = NewYoungCharacter(account, 0);
        var second = NewYoungCharacter(account, 1);

        try
        {
            // Pinned's 40 hours no longer ends it.
            Played(account, TimeSpan.FromHours(40));
            account.CheckYoung();
            Assert.True(account.Young);

            Played(account, TimeSpan.FromHours(336) - TimeSpan.FromMinutes(1));
            account.CheckYoung();
            Assert.True(account.Young);
            Assert.True(first.Young);

            Played(account, TimeSpan.FromHours(336));
            account.CheckYoung();
            Assert.False(account.Young);
            Assert.False(first.Young);
            Assert.False(second.Young);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 3

    private static bool Says(PlayerMobile pm, string text, params int[] keywords)
    {
        pm.CloseGump<RenounceYoungGump>();
        EventSink.InvokeSpeech(new SpeechEventArgs(pm, text, MessageType.Regular, 0x3B2, keywords));
        return pm.HasGump<RenounceYoungGump>();
    }

    [Fact]
    public void BothPhrasesOpenTheRenounceConfirmAndNothingElseDoes()
    {
        var account = NewAccount();
        var pm = NewYoungCharacter(account, 0);

        try
        {
            var ns = PacketTestUtilities.CreateTestNetState();
            pm.NetState = ns;
            ns.Mobile = pm;

            // OSI's: the client sends keyword 0x35 with it (Misc/Keywords.cs:40).
            Assert.True(Says(pm, "I renounce my young player status", 0x35));

            // Ours, loosely.
            foreach (var said in new[]
                     {
                         "I want to grow up!", "I want to grow up", "i want to grow up!", "I WANT TO GROW UP",
                         "  I want  to grow up!!  ", "I want to grow up."
                     })
            {
                Assert.True(Says(pm, said), said);
            }

            // Anything else does not.
            foreach (var said in new[]
                     {
                         "I want to grow up later", "grow up", "I want to grow", "hello", "I want to grow up? no",
                         "want to grow up!", ""
                     })
            {
                Assert.False(Says(pm, said), said);
            }

            // The confirm's OKAY ends Young for the account.
            Assert.True(Says(pm, "I want to grow up!"));
            pm.FindGump<RenounceYoungGump>().OnResponse(ns, new RelayInfo(1, default, default, default, default));
            Assert.False(account.Young);
            Assert.False(pm.Young);

            // Once no longer Young, the phrase opens nothing.
            Assert.False(Says(pm, "I want to grow up!"));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- kept, and the countdown

    [Fact]
    public void AMurderCountStillEndsYoung()
    {
        var account = NewAccount();
        var pm = NewYoungCharacter(account, 0);

        try
        {
            pm.Kills = 1;
            Assert.False(account.Young);
            Assert.False(pm.Young);
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void TheLoginCountdownReadsInDaysAndHours()
    {
        Assert.Equal("14 days", ClusterFYoungPlayer.FormatRemaining(TimeSpan.FromHours(336)));
        Assert.Equal("13 days and 23 hours", ClusterFYoungPlayer.FormatRemaining(TimeSpan.FromHours(335.5)));
        Assert.Equal("1 day and 1 hour", ClusterFYoungPlayer.FormatRemaining(TimeSpan.FromHours(25)));
        Assert.Equal("5 hours", ClusterFYoungPlayer.FormatRemaining(TimeSpan.FromHours(5)));
        Assert.Equal("less than an hour", ClusterFYoungPlayer.FormatRemaining(TimeSpan.FromMinutes(20)));
        Assert.Equal("less than an hour", ClusterFYoungPlayer.FormatRemaining(TimeSpan.FromHours(-3)));

        var line = ClusterFYoungPlayer.LoginMessage(TimeSpan.FromHours(300));
        _out.WriteLine(line);
        Assert.Contains("12 days and 12 hours", line);
        Assert.Contains("I want to grow up!", line);
    }
}
