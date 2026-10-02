// GuildStandingCommandVerification.cs
//
// cc-P29 Part E: [GuildStanding <guildKey> [amount], a test-shard command to inspect or set any guild's standing.
// Notes in shard-migration notes/cc-P29-defect-batch-2.md, Part E.
//
// Every fact runs the command's own handler (by reflection, as CommandSystem would) and answers its target cursor.
//
// Facts:
//   1. [GuildStanding artificers 15000 on a player sets GuildReputation["artificers"] to 15,000, and the
//      Imbuing Table's own rank reads Master Artificer.
//   2. [GuildStanding Artificers with no amount opens the cursor and changes nothing; the line it sends reads
//      standing and the Artificers' rank name. The key is matched without regard to case, and mining reports the
//      Compact's ladder as [CompactStanding does.
//   3. An unknown key is refused before any target cursor, the refusal lists every valid key, and nothing is set.
//
// Mobile.SendMessage is not virtual in pinned, so the lines are read from the methods that build them.

using System;
using System.Reflection;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Commands;
using Server.Misc;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuildStandingCommandVerification
{
    private readonly ITestOutputHelper _out;

    public GuildStandingCommandVerification(ITestOutputHelper output)
    {
        _out = output;
        EnsureStartupHooks();
        ClusterFGuildSystem.EnsureRegistered();
    }

    private static bool _startupHooksRun;

    private static void EnsureStartupHooks()
    {
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly Account Account = new($"p29{Guid.NewGuid():N}"[..16], "p29-test-only");
        public readonly PlayerMobile Pm;
        public readonly PlayerMobile Gm;

        public Fixture()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1230, 1230, 0), Map.Trammel);

            Gm = new PlayerMobile { Name = "GM", AccessLevel = AccessLevel.GameMaster };
            Gm.MoveToWorld(new Point3D(1231, 1230, 0), Map.Trammel);
        }

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial);

        public void Dispose()
        {
            Pm.Delete();
            Gm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static readonly MethodInfo Handler = typeof(ClusterFGuildStandingCommand).GetMethod(
        "GuildStanding_OnCommand", BindingFlags.NonPublic | BindingFlags.Static);

    // Runs "[GuildStanding <args>" as the staff member and, if it opens a target cursor, targets the player.
    private static bool Run(Fixture f, params string[] args)
    {
        Assert.NotNull(Handler);
        f.Gm.Target = null;
        Handler.Invoke(null, new object[] { new CommandEventArgs(f.Gm, "GuildStanding", string.Join(" ", args), args) });

        if (f.Gm.Target == null)
        {
            return false;
        }

        f.Gm.Target.Invoke(f.Gm, f.Pm);
        return true;
    }

    [Fact]
    public void SettingArtificersTo15000MakesAMasterArtificer()
    {
        using var f = new Fixture();
        Assert.Equal(0, f.Guild.GetReputation("artificers"));

        Assert.True(Run(f, "artificers", "15000"), "no target cursor");

        Assert.Equal(15000, f.Guild.GetReputation("artificers"));
        Assert.Equal("Master Artificer", ArtificersGuildmasterGump.GetRankName(f.Guild.GetReputation("artificers")));

        // The line the staff member is sent for a set.
        var line = ClusterFGuildStandingCommand.Apply(f.Pm, "artificers", 15000);
        _out.WriteLine(line);
        Assert.Contains("15,000", line);
        Assert.Contains("Master Artificer", line);
    }

    [Fact]
    public void WithNoAmountItReadsStandingAndTheGuildsOwnRankBack()
    {
        using var f = new Fixture();
        f.Guild.GuildReputation["artificers"] = 5200;
        f.Guild.GuildReputation["mining"] = 16000;

        // The handler with no amount opens a cursor and changes nothing.
        Assert.True(Run(f, "Artificers"));
        Assert.Equal(5200, f.Guild.GetReputation("artificers"));

        Assert.True(ClusterFGuildStandingCommand.TryResolveKey("Artificers", out var key));
        Assert.Equal("artificers", key);
        var line = ClusterFGuildStandingCommand.Apply(f.Pm, key, null);
        _out.WriteLine(line);
        Assert.Contains("5,200", line);
        Assert.Contains("Rank=Artificer", line);

        var mining = ClusterFGuildStandingCommand.Apply(f.Pm, "mining", null);
        _out.WriteLine(mining);
        Assert.Contains("16,000", mining);
        Assert.Contains($"Rank={MinersCompactLiaisonGump.GetRankName(16000)}", mining); // Surveyor
        Assert.Equal(16000, f.Guild.GetReputation("mining"));
    }

    [Fact]
    public void AnUnknownGuildKeyIsRefusedWithTheValidKeys()
    {
        using var f = new Fixture();

        Assert.False(Run(f, "artificer", "15000"), "an unknown key opened a target cursor");
        Assert.False(ClusterFGuildStandingCommand.TryResolveKey("artificer", out _));

        var line = ClusterFGuildStandingCommand.UnknownKeyMessage("artificer");
        _out.WriteLine(line);
        Assert.StartsWith("Unknown guild key 'artificer'", line);
        foreach (var key in ClusterFGuildSystem.AllGuilds.Keys)
        {
            Assert.Contains(key, line);
        }

        Assert.False(f.Guild.GuildReputation.ContainsKey("artificer"));
        Assert.Equal(0, f.Guild.GetReputation("artificers"));
    }
}
