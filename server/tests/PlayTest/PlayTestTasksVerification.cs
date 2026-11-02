// cc-P773: in-game play test tasks ([tasks, [report, "tasks" said aloud). Notes: shard-migration
// notes/cc-P773-playtest-tasks.md.
//
// Facts, each seen failing first (on a mutant that undoes its part; notes/cc-P773-tools/):
//   1. [tasks for a tester opens a window with that tester's open tasks only (not a pulled-back one, not another
//      tester's, not one already answered), and the window carries why, where, the steps and what to look for.
//   2. [tasks for someone who is not a tester opens nothing (they get one line).
//   3. A missing or broken tester or task file, or a broken report line, never throws and opens nothing broken.
//   4. Each of the three buttons writes exactly one well-formed report line (account, character, task, test, button,
//      text, facet, location, UTC time, version, and no IP), and the task is then finished; the two problem buttons
//      need words. [report writes a free report.
//   5. "tasks" said aloud opens the window for a tester, alone and in any case; nobody else is answered.
//   6. A tester logging in to tasks they have not been shown is told; once shown, not again until a new one arrives.
//   7. Nothing here writes Pass or Fail: the buttons are worked, wrong, couldnt and report.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.PlayTest;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PlayTestTasksVerification : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "p773-" + Guid.NewGuid().ToString("N"));

    public PlayTestTasksVerification()
    {
        ShardTestHost.EnsureAccounts();
        Directory.CreateDirectory(_folder);
        PlayTestFiles.FolderOverride = _folder;
        PlayTestTasks.Configure();
    }

    public void Dispose()
    {
        PlayTestFiles.FolderOverride = null;
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p773{Guid.NewGuid():N}"[..16], "p773-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(string name = "Paige")
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x191, Female = true, Name = name };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1495, 1629, 10), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public string User => Account.Username;

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Ns.Dispose();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static readonly System.Reflection.MethodInfo GumpRemove = typeof(GumpSystem).GetMethod("Remove",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
        [typeof(NetState), typeof(BaseGump)], null)!;

    /// <summary>The client's answer to a gump: one button, and one text box with these words.</summary>
    private static void Press(BaseGump gump, NetState ns, int button, int entryId, string text)
    {
        GumpRemove.Invoke(null, [ns, gump]);
        var bytes = Encoding.BigEndianUnicode.GetBytes(text);
        ushort[] ids = [(ushort)entryId];
        Range[] ranges = [new Range(0, bytes.Length)];
        gump.OnResponse(ns, new RelayInfo(button, ReadOnlySpan<int>.Empty, ids, ranges, bytes));
    }

    private static void Say(PlayerMobile pm, string text) => pm.DoSpeech(text, [], MessageType.Regular, 0x3B2);

    private static bool Command(Mobile m, string line) => CommandSystem.Handle(m, CommandSystem.Prefix + line);

    private void Testers(params (string Account, bool Paused)[] testers) =>
        File.WriteAllText(Path.Combine(_folder, "testers.json"), JsonSerializer.Serialize(new
        {
            version = 1,
            testers = testers.Select(t => new { account = t.Account, name = "Tester " + t.Account, paused = t.Paused })
        }));

    private static object Task(string id, string account, string status = "open", string at = "2026-11-02T05:00:00Z") => new
    {
        id, account, testId = "T-" + id[2..], title = "Wake on the beach " + id, why = "It is the first minute of the game.",
        where = "The Wakening Isle beach, by Tamsin", coords = "6638, 3993", steps = new[] { "Make a new character.", "Get up." },
        lookFor = "You can stand up with an arrow key.", intro = "The Goblin sniffs the sand.", status, assignedAt = at
    };

    private void Tasks(params object[] tasks) =>
        File.WriteAllText(Path.Combine(_folder, "tasks.json"), JsonSerializer.Serialize(new { version = 1, tasks }));

    private string[] ReportLines() =>
        File.Exists(Path.Combine(_folder, "playtest-reports.jsonl"))
            ? File.ReadAllLines(Path.Combine(_folder, "playtest-reports.jsonl")).Where(l => l.Length > 0).ToArray()
            : [];

    // ---------------------------------------------------------------- 1

    [Fact]
    public void P773_TasksForATesterOpensTheirOpenTasks()
    {
        using var p = new Player();
        using var other = new Player("Dylan");
        Testers((p.User, false), (other.User, false));
        Tasks(Task("G-0001", p.User), Task("G-0002", p.User, "pulled"), Task("G-0003", other.User),
            Task("G-0004", p.User, at: "2026-11-02T05:00:01Z"));

        Assert.True(Command(p.Pm, "tasks"));
        var gump = p.Pm.FindGump<PlayTestTasksGump>();
        Assert.NotNull(gump);
        Assert.Equal(new[] { "G-0001", "G-0004" }, gump!.Tasks.Select(t => t.Id));

        var body = PlayTestTasksGump.Body(gump.Tasks[0]);
        Assert.Contains("Why it matters:", body);
        Assert.Contains("Where to go:</B> The Wakening Isle beach, by Tamsin (6638, 3993)", body);
        Assert.Contains("1. Make a new character.", body);
        Assert.Contains("2. Get up.", body);
        Assert.Contains("What to look for:", body);

        // an answered task is finished
        Assert.True(PlayTestTasks.Answer(p.Pm, "G-0001", "worked", ""));
        Assert.Equal(new[] { "G-0004" }, PlayTestTasks.OpenTasks(p.User)!.Select(t => t.Id));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void P773_TasksForSomeoneWhoIsNotATesterOpensNothing()
    {
        using var p = new Player();
        using var stranger = new Player("Stranger");
        Testers((p.User, false));
        Tasks(Task("G-0001", p.User), Task("G-0002", stranger.User));

        Assert.True(Command(stranger.Pm, "tasks"));
        Assert.Null(stranger.Pm.FindGump<PlayTestTasksGump>());
        Assert.True(Command(stranger.Pm, "report it rained"));
        Assert.Empty(ReportLines());
        Assert.False(PlayTestTasks.Answer(stranger.Pm, "G-0002", "worked", "fine"));
        Assert.Empty(ReportLines());
        Assert.Contains("ask Chase", PlayTestTasks.NotTesterLine);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void P773_AMissingOrBrokenFileNeverThrows()
    {
        using var p = new Player();

        // nothing there at all
        Assert.True(Command(p.Pm, "tasks"));
        Assert.Null(p.Pm.FindGump<PlayTestTasksGump>());
        PlayTestTasks.OnLogin(p.Pm);

        // a broken tester list
        File.WriteAllText(Path.Combine(_folder, "testers.json"), "{ \"testers\": [ {\"account\": ");
        Assert.True(Command(p.Pm, "tasks"));
        Say(p.Pm, "tasks");
        PlayTestTasks.OnLogin(p.Pm);
        Assert.False(PlayTestTasks.Answer(p.Pm, null!, "report", "hello"));
        Assert.Null(p.Pm.FindGump<PlayTestTasksGump>());
        Assert.Null(PlayTestTasks.TesterOf(p.Pm, out var error));
        Assert.NotNull(error);

        // a good list, a broken task file
        Testers((p.User, false));
        File.WriteAllText(Path.Combine(_folder, "tasks.json"), "[1, 2");
        Assert.True(Command(p.Pm, "tasks"));
        Say(p.Pm, "tasks");
        PlayTestTasks.OnLogin(p.Pm);
        Assert.Null(p.Pm.FindGump<PlayTestTasksGump>());
        Assert.Null(PlayTestTasks.OpenTasks(p.User));

        // the wrong shape, and a broken report line among good ones
        File.WriteAllText(Path.Combine(_folder, "tasks.json"), "{\"tasks\": 7}");
        Assert.Null(PlayTestTasks.OpenTasks(p.User));
        Tasks(Task("G-0001", p.User), Task("G-0002", p.User));
        File.WriteAllText(Path.Combine(_folder, "playtest-reports.jsonl"),
            "not json at all\n" + PlayTestFiles.ReportLine(DateTime.UtcNow, p.User, "Paige", "G-0001", "T-0001", "worked", "",
                "Trammel", new Point3D(1, 2, 3), "") + "\n{\"account\": 5\n");
        Assert.Equal(new[] { "G-0002" }, PlayTestTasks.OpenTasks(p.User)!.Select(t => t.Id));

        // a report that cannot be written (a folder stands where the file goes) says so and does not throw
        File.Delete(Path.Combine(_folder, "playtest-reports.jsonl"));
        Directory.CreateDirectory(Path.Combine(_folder, "playtest-reports.jsonl"));
        Assert.True(PlayTestTasks.TesterOf(p.Pm, out _) != null);
        Assert.False(PlayTestTasks.Answer(p.Pm, null!, "report", "hello"));
        Assert.False(PlayTestTasks.Answer(p.Pm, "G-0002", "worked", "fine"));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void P773_EachButtonWritesOneWellFormedLine()
    {
        using var p = new Player();
        Testers((p.User, false));
        Tasks(Task("G-0001", p.User), Task("G-0002", p.User, at: "2026-11-02T05:00:01Z"),
            Task("G-0003", p.User, at: "2026-11-02T05:00:02Z"));
        PlayTestTasks.ShowTasks(p.Pm, true);

        var expected = new[] { ("G-0001", "worked", "Stood right up."), ("G-0002", "wrong", "The arrow pointed into the sea."),
            ("G-0003", "couldnt", "No beach for me.") };
        var count = 0;
        foreach (var (id, button, text) in expected)
        {
            var gump = p.Pm.FindGump<PlayTestTasksGump>()!;
            var index = gump.Tasks.ToList().FindIndex(t => t.Id == id);

            if (button != "worked")
            {
                // the problem buttons need words: nothing written without them
                Press(gump, p.Ns, PlayTestTasksGump.ButtonFor(index, button), index, "   ");
                Assert.Equal(count, ReportLines().Length);
                gump = p.Pm.FindGump<PlayTestTasksGump>()!;
                index = gump.Tasks.ToList().FindIndex(t => t.Id == id);
            }

            Press(gump, p.Ns, PlayTestTasksGump.ButtonFor(index, button), index, text);
            var lines = ReportLines();
            Assert.Equal(++count, lines.Length);

            using var doc = JsonDocument.Parse(lines[^1]);
            var r = doc.RootElement;
            Assert.Equal(new[] { "at", "account", "character", "task", "test", "button", "text", "facet", "x", "y", "z", "version" },
                r.EnumerateObject().Select(o => o.Name));
            Assert.Equal(p.User, r.GetProperty("account").GetString());
            Assert.Equal("Paige", r.GetProperty("character").GetString());
            Assert.Equal(id, r.GetProperty("task").GetString());
            Assert.Equal("T-" + id[2..], r.GetProperty("test").GetString());
            Assert.Equal(button, r.GetProperty("button").GetString());
            Assert.Equal(text, r.GetProperty("text").GetString());
            Assert.Equal("Trammel", r.GetProperty("facet").GetString());
            Assert.Equal(1495, r.GetProperty("x").GetInt32());
            Assert.Equal(1629, r.GetProperty("y").GetInt32());
            Assert.EndsWith("Z", r.GetProperty("at").GetString());
            Assert.True(DateTime.TryParse(r.GetProperty("at").GetString(), out _));
            Assert.Equal(ShardVersion.Current, r.GetProperty("version").GetString());
            Assert.DoesNotContain("ip", lines[^1], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(PlayTestTasks.OpenTasks(p.User)!, t => t.Id == id);
        }

        Assert.Null(p.Pm.FindGump<PlayTestTasksGump>()); // all three answered: nothing left to show

        // [report with words writes one free report line; without words it opens the box
        Assert.True(Command(p.Pm, "report The door in the lodge hums."));
        var free = JsonDocument.Parse(ReportLines()[^1]).RootElement;
        Assert.Equal("report", free.GetProperty("button").GetString());
        Assert.Equal("", free.GetProperty("task").GetString());
        Assert.Equal("The door in the lodge hums.", free.GetProperty("text").GetString());
        Assert.True(Command(p.Pm, "report"));
        var box = p.Pm.FindGump<PlayTestReportGump>();
        Assert.NotNull(box);
        Press(box!, p.Ns, PlayTestReportGump.SendButton, 0, "Line\nbreak");
        var sent = JsonDocument.Parse(ReportLines()[^1]).RootElement.GetProperty("text").GetString()!;
        Assert.StartsWith("Line", sent);
        Assert.DoesNotContain('\n', sent);   // one line per report, whatever the client sends
        Assert.Equal(5, ReportLines().Length);
        Assert.Equal("Line break", PlayTestFiles.Clean("Line\nbreak"));
        Assert.Equal(PlayTestFiles.TextMax, PlayTestFiles.Clean(new string('x', 5000)).Length);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void P773_SayingTasksAloudOpensTheWindow()
    {
        using var p = new Player();
        using var stranger = new Player("Stranger");
        Testers((p.User, false));
        Tasks(Task("G-0001", p.User), Task("G-0002", stranger.User));

        Say(p.Pm, "tasks");
        Assert.NotNull(p.Pm.FindGump<PlayTestTasksGump>());
        p.Pm.CloseGump<PlayTestTasksGump>();
        Say(p.Pm, "Tasks!");
        Assert.NotNull(p.Pm.FindGump<PlayTestTasksGump>());
        p.Pm.CloseGump<PlayTestTasksGump>();
        Say(p.Pm, "my tasks are done"); // only the word alone
        Assert.Null(p.Pm.FindGump<PlayTestTasksGump>());

        Say(stranger.Pm, "tasks");
        Assert.Null(stranger.Pm.FindGump<PlayTestTasksGump>());
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void P773_ALoginLineWhenNewTasksArrive()
    {
        using var p = new Player();
        Testers((p.User, false));
        Assert.False(PlayTestTasks.HasNewTasks(p.Pm));

        Tasks(Task("G-0001", p.User));
        Assert.True(PlayTestTasks.HasNewTasks(p.Pm));
        PlayTestTasks.ShowTasks(p.Pm, true);
        Assert.False(PlayTestTasks.HasNewTasks(p.Pm));

        Tasks(Task("G-0001", p.User), Task("G-0002", p.User, at: "2026-11-03T05:00:00Z"));
        Assert.True(PlayTestTasks.HasNewTasks(p.Pm));
        Assert.Contains("[tasks", PlayTestTasks.NewTasksLine);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void P773_NoPassOrFailIsEverWritten()
    {
        Assert.Equal(new[] { "worked", "wrong", "couldnt", "report" }, PlayTestFiles.Buttons);
        Assert.DoesNotContain(PlayTestFiles.Buttons, b => b.Contains("pass") || b.Contains("fail"));
    }
}
