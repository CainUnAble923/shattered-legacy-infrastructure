// ArtificerRankCommandVerification.cs
//
// cc-P145: [artificerrank, a player command that says the Artificers' Order rank and the imbue properties the account
// knows, in lines the combat script's loot rater reads from the journal. Notes in shard-migration
// notes/cc-P145-combat-script.md.
//
// Mobile.SendMessage is not virtual in pinned, so the lines are read from ArtificerRankCommand.Lines, the method the
// handler sends; the handler itself is run once by reflection to prove it does not throw for a plain player.
//
// Facts:
//   1. A fresh character (no guild record): "rank 0 of 5", "not a member", every free property at Basic (Luck among
//      them), no Refined or Full line, "[Artificer] end" last, no line over 180 characters.
//   2. A member at standing 5,200 with Hit Chance Increase granted at Full: "rank 2 of 5 (Artificer)",
//      "standing 5,200", Hit Chance Increase on the Full line and on no other, the count up by one.
//   3. Chunk keeps every line at or under 180 characters, repeats the tier header, and loses or repeats no name.
//   4. The handler runs for a player without throwing.

using System;
using System.Collections.Generic;
using System.Linq;
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
public class ArtificerRankCommandVerification
{
    private readonly ITestOutputHelper _out;

    public ArtificerRankCommandVerification(ITestOutputHelper output)
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
        public readonly Account Account = new($"p145{Guid.NewGuid():N}"[..16], "p145-test-only");
        public readonly PlayerMobile Pm;

        public Fixture()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Rater" };
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1230, 1230, 0), Map.Trammel);
        }

        public ClusterFAccountData Data => ClusterFAccountPersistence.GetOrCreate(Account);
        public CharacterGuildData Guild => Data.GetOrCreateGuildData(Pm.Serial);

        public void Dispose()
        {
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static readonly MethodInfo Handler = typeof(ArtificerRankCommand).GetMethod(
        "OnCommand", BindingFlags.NonPublic | BindingFlags.Static);

    private static IEnumerable<string> TierNames(IEnumerable<string> lines, string tier) =>
        lines.Where(l => l.StartsWith($"{ArtificerRankCommand.Prefix} {tier}:", StringComparison.Ordinal))
             .SelectMany(l => l[(ArtificerRankCommand.Prefix.Length + tier.Length + 2)..].Split(','))
             .Select(n => n.Trim())
             .Where(n => n.Length > 0);

    private void Print(IEnumerable<string> lines)
    {
        foreach (var l in lines)
        {
            _out.WriteLine(l);
        }
    }

    [Fact]
    public void AFreshCharacterIsRankZeroNotAMemberAndKnowsTheFreePropertiesAtBasic()
    {
        using var f = new Fixture();
        var lines = ArtificerRankCommand.Lines(f.Pm);
        Print(lines);

        Assert.True(lines.Count >= 3, "expected a rank line, at least one Basic line and the end line");
        Assert.StartsWith($"{ArtificerRankCommand.Prefix} rank 0 of 5 (", lines[0]);
        Assert.Contains("not a member", lines[0]);
        Assert.Equal($"{ArtificerRankCommand.Prefix} end", lines[^1]);
        Assert.All(lines, l => Assert.True(l.Length <= ArtificerRankCommand.MaxLine, $"{l.Length} chars: {l}"));

        var basic = TierNames(lines, "Basic").ToList();
        var free = ImbuingTable.Current.Ladder.Where(p => string.Equals(p.Value, "free", StringComparison.OrdinalIgnoreCase))
                                               .Select(p => p.Key).ToList();
        Assert.Contains("Luck", basic);
        foreach (var key in free)
        {
            Assert.Contains(key, basic);
        }

        Assert.Empty(TierNames(lines, "Refined"));
        Assert.Empty(TierNames(lines, "Full"));
        Assert.Contains($"{basic.Count} properties known", lines[0]);
    }

    [Fact]
    public void AMemberAt5200WithHitChanceAtFullReadsRankTwoAndListsItOnTheFullLineOnly()
    {
        using var f = new Fixture();
        f.Guild.JoinedGuilds.Add(ArtificerRankCommand.GuildKey);
        f.Guild.GuildReputation[ArtificerRankCommand.GuildKey] = 5200;
        var before = TierNames(ArtificerRankCommand.Lines(f.Pm), "Basic").Count();
        Assert.True(ClusterFImbueKnowledge.Grant(f.Data, "Hit Chance Increase", ImbueTier.Full));

        var lines = ArtificerRankCommand.Lines(f.Pm);
        Print(lines);

        Assert.StartsWith($"{ArtificerRankCommand.Prefix} rank 2 of 5 (Artificer), standing 5,200, {before + 1} properties known", lines[0]);
        Assert.DoesNotContain("not a member", lines[0]);
        Assert.Contains("Hit Chance Increase", TierNames(lines, "Full"));
        Assert.DoesNotContain("Hit Chance Increase", TierNames(lines, "Basic"));
        Assert.DoesNotContain("Hit Chance Increase", TierNames(lines, "Refined"));
        Assert.Equal($"{ArtificerRankCommand.Prefix} end", lines[^1]);
    }

    [Fact]
    public void ChunkKeepsEveryLineUnderTheLimitAndEveryNameOnce()
    {
        var names = Enumerable.Range(1, 60).Select(i => $"Property Number {i} With A Long Name").ToList();
        var lines = ArtificerRankCommand.Chunk("Basic", names);
        Print(lines);

        Assert.True(lines.Count >= 2, "sixty long names must need more than one line");
        Assert.All(lines, l => Assert.StartsWith($"{ArtificerRankCommand.Prefix} Basic: ", l));
        Assert.All(lines, l => Assert.True(l.Length <= ArtificerRankCommand.MaxLine, $"{l.Length} chars: {l}"));
        Assert.Equal(names, TierNames(lines, "Basic").ToList());
        Assert.Empty(ArtificerRankCommand.Chunk("Full", Array.Empty<string>()));
    }

    [Fact]
    public void TheHandlerRunsForAPlayerWithoutThrowing()
    {
        using var f = new Fixture();
        Assert.NotNull(Handler);
        var ex = Record.Exception(() =>
            Handler.Invoke(null, new object[] { new CommandEventArgs(f.Pm, "artificerrank", "", Array.Empty<string>()) }));
        Assert.Null(ex);
    }
}
