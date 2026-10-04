// LeagueSignpostsVerification.cs
//
// cc-P48 Part A (F-24, the 2026-10-01 signposts). Notes in shard-migration notes/cc-P48-league-batch-1.md.
//
// Facts:
//   1. The League row is first in all three lists: [guild, the Guild Board and the Registrar's guild list each open the
//      Guild Directory with the League row above every guild row. Each list is opened through its own entry point.
//   2. The League row's button sets a quest arrow to the League Registrar; with no Registrar at her post it says so and
//      sets no arrow.
//   3. The first-login welcome page contains the League line, once, beside the Guild Board line.
//   4. The Registrar speaks her ambient lines after the New Haven seeder places her (MoveToWorld), as she does after a
//      spawner places her. Before cc-P48 only a spawner started them (OnAfterSpawn), so a seeded Registrar never spoke.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Server;
using Server.Accounting;
using Server.Commands;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Items;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeagueSignpostsVerification
{
    private readonly ITestOutputHelper _out;

    public LeagueSignpostsVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();

        if (!MLQuestSystem.Enabled)
        {
            MLQuestSystem.Configure();
        }

        // As NewHavenQuestBoardVerification. Without it the board refuses the double-click (CanUse false; cc-P48 build 2,
        // found by a probe).
        NewHavenQuestBoard.Register();
    }

    // ---------------------------------------------------------------- helpers

    private static Account NewAccount() => new($"p48s{Guid.NewGuid():N}"[..16], "p48-test-only");

    private static PlayerMobile Online(Account account, Point3D where, out NetState ns)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Server.Items.Backpack());
        account[0] = pm;
        pm.MoveToWorld(where, Map.Trammel);
        ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        // As NewHavenQuestBoardVerification (cc-P30): a real player is past login, and a connection with no account
        // has a 4 KiB send ring. One directory is about 2 KiB (measured, cc-P48), so a fact that opens several needs it.
        ns.Account = account;
        return pm;
    }

    private static void Cleanup(PlayerMobile pm, Account account)
    {
        pm.QuestArrow = null;
        var ns = pm.NetState;
        pm.NetState = null;

        if (ns != null)
        {
            ns.Mobile = null;
            ns.Dispose();
        }

        pm.Delete();
        Accounts.Remove(account);
    }

    private static void Press(BaseGump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    private static int Mark(NetState ns) => ns.SendBuffer.GetReadSpan().Length;

    private static bool SentText(NetState ns, int from, string text)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        return span.IndexOf(Encoding.BigEndianUnicode.GetBytes(text)) >= 0 ||
               span.IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;
    }

    // The directory's row names in the order a player reads them (top to bottom, page 0 first): every label at x 20
    // below the header text, with its y.
    private static List<(string Name, int Y)> RowNames(GuildProgressGump gump) =>
        gump.Entries.OfType<GumpLabel>().Where(l => l.X == 20 && l.Y >= GuildProgressGump.LeagueRowY)
            .Select(l => (l.Text, l.Y)).ToList();

    private void AssertLeagueRowFirst(GuildProgressGump gump, string list)
    {
        var rows = RowNames(gump);
        var guildNames = ClusterFGuildSystem.AllGuilds.Values.Select(d => d.Name).ToHashSet();
        var league = rows.FindIndex(r => r.Name == GuildProgressGump.LeagueRowName);
        var firstGuild = rows.FindIndex(r => guildNames.Contains(r.Name));
        var minGuildY = rows.Where(r => guildNames.Contains(r.Name)).Min(r => r.Y);

        _out.WriteLine($"{list}: League row entry {league} at y {(league >= 0 ? rows[league].Y : -1)}; first guild row entry {firstGuild}, top guild y {minGuildY}");

        Assert.True(league >= 0, $"{list}: no League row");
        Assert.True(league < firstGuild, $"{list}: the League row is not before the guild rows");
        Assert.True(rows[league].Y < minGuildY, $"{list}: the League row is not above the guild rows");
        Assert.Contains(gump.Entries.OfType<GumpButton>(), b => b.ButtonID == GuildProgressGump.BtnLeagueWay);
        Assert.Equal(1, rows.Count(r => r.Name == GuildProgressGump.LeagueRowName));
    }

    private static LeagueRegistrar PlaceRegistrar()
    {
        var post = ClusterFLeagueSystem.RegistrarPost;
        var npc = new LeagueRegistrar();
        npc.MoveToWorld(post.Point, post.Map);
        return npc;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheLeagueRowIsFirstInAllThreeLists()
    {
        var account = NewAccount();
        var board = new NewHavenQuestBoard();
        board.MoveToWorld(NewHavenQuestBoard.HomeLocation, Map.Trammel);
        var pm = Online(account, new Point3D(NewHavenQuestBoard.HomeLocation.X + 1, NewHavenQuestBoard.HomeLocation.Y, 14), out var ns);

        try
        {
            // [guild: its handler, as the command system calls it.
            var handler = typeof(ClusterFGuildSystem).GetMethod("OnGuildCommand", BindingFlags.NonPublic | BindingFlags.Static)!;
            handler.Invoke(null, [new CommandEventArgs(pm, "guild", "", [])]);
            var fromCommand = Assert.IsType<GuildProgressGump>(pm.FindGump<GuildProgressGump>());
            AssertLeagueRowFirst(fromCommand, "[guild");
            pm.CloseGump<GuildProgressGump>();

            // The Guild Board: double-clicked from beside it.
            Assert.True(board.CanUse(pm), "the board refused the player");
            Assert.True(ns.Running, "the connection was closed by the directory before the board was used");
            board.OnDoubleClick(pm);
            var fromBoard = Assert.IsType<GuildProgressGump>(pm.FindGump<GuildProgressGump>());
            AssertLeagueRowFirst(fromBoard, "Guild Board");
            pm.CloseGump<GuildProgressGump>();

            // The Registrar's guild list: Guild Referrals, then Open the Guild Directory; and Guilds Overview.
            Press(new LeagueRegistrarGump(pm, LeagueRegistrarGump.View.GuildReferrals), ns, 20);
            AssertLeagueRowFirst(Assert.IsType<GuildProgressGump>(pm.FindGump<GuildProgressGump>()), "Registrar, Guild Referrals");
            pm.CloseGump<GuildProgressGump>();

            Press(new LeagueRegistrarGump(pm), ns, 14);
            AssertLeagueRowFirst(Assert.IsType<GuildProgressGump>(pm.FindGump<GuildProgressGump>()), "Registrar, Guilds Overview");
        }
        finally
        {
            board.Delete();
            Cleanup(pm, account);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheLeagueRowPointsAQuestArrowAtTheRegistrar()
    {
        var account = NewAccount();
        var post = ClusterFLeagueSystem.RegistrarPost;
        Assert.NotNull(post);
        var registrar = PlaceRegistrar();
        var pm = Online(account, new Point3D(post.Point.X + 8, post.Point.Y + 8, post.Point.Z), out var ns);

        try
        {
            var mark = Mark(ns);
            Press(new GuildProgressGump(pm, account), ns, GuildProgressGump.BtnLeagueWay);

            var arrow = Assert.IsType<GuildDirectionArrow>(pm.QuestArrow);
            Assert.Same(registrar, arrow.Target);
            Assert.Equal(registrar.Location, arrow.Point);
            Assert.True(SentText(ns, mark, "Follow the arrow to the League Registrar"), "no direction message");

            // No Registrar at her post: reported in the directory's words, and no arrow.
            pm.QuestArrow = null;
            registrar.Delete();
            mark = Mark(ns);
            Press(new GuildProgressGump(pm, account), ns, GuildProgressGump.BtnLeagueWay);
            Assert.Null(pm.QuestArrow);
            Assert.True(SentText(ns, mark, "Please tell a Game Master"), "a missing Registrar is not reported");
        }
        finally
        {
            registrar.Delete();
            Cleanup(pm, account);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheWelcomePageCarriesTheLeagueLine()
    {
        var pm = new PlayerMobile { Player = true };

        try
        {
            var gump = new GuildWelcomeGump(pm);
            var html = string.Concat(gump.Entries.OfType<GumpHtml>().Select(h => h.Text));
            _out.WriteLine(html);

            Assert.Contains("Guild Board", html);
            Assert.Contains(GuildWelcomeGump.LeagueLine, html);
            Assert.Contains("League of Extraordinary Citizens", GuildWelcomeGump.LeagueLine);
            Assert.Equal(1, html.Split("League of Extraordinary Citizens").Length - 1);
            Assert.DoesNotContain("\n", GuildWelcomeGump.LeagueLine);
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheRegistrarSpeaksAfterTheSeederPlacesHer()
    {
        ShardTestClock.Arm(); // before anything starts a timer (ShardTestClock, D16)

        var account = NewAccount();
        var post = ClusterFLeagueSystem.RegistrarPost;

        // Placed as ClusterFNewHavenSeeder.ApplyEntry places her: MoveToWorld, no spawner.
        var registrar = new LeagueRegistrar { Direction = Direction.West, CantWalk = true };
        registrar.MoveToWorld(post.Point, Map.Trammel);

        var pm = Online(account, new Point3D(post.Point.X + 3, post.Point.Y, post.Point.Z), out var ns);

        try
        {
            var mark = Mark(ns);
            ShardTestClock.Advance(TimeSpan.FromSeconds(50)); // the first line is due 25 to 45 s after placing

            var spoke = SentText(ns, mark, "League") || SentText(ns, mark, "New Haven");
            _out.WriteLine($"timer running={registrar.AmbientSpeechRunning}; spoke within 50 s={spoke}");

            Assert.True(registrar.AmbientSpeechRunning, "no ambient timer after MoveToWorld");
            Assert.True(spoke, "the seeded Registrar said nothing in 50 seconds");

            // Arriving again (a GM moving her) does not start a second timer: one line per tick, not two.
            registrar.MoveToWorld(new Point3D(post.Point.X, post.Point.Y + 1, post.Point.Z), Map.Trammel);
            registrar.MoveToWorld(post.Point, Map.Felucca);
            registrar.MoveToWorld(post.Point, Map.Trammel);
            Assert.True(registrar.AmbientSpeechRunning);
        }
        finally
        {
            registrar.Delete();
            Cleanup(pm, account);
        }
    }
}
