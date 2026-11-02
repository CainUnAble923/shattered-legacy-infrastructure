// cc-P773: in-game play test tasks for the shard's play testers ([tasks, [report). Notes in shard-migration
// notes/cc-P773-playtest-tasks.md.
//
// WHAT. Three files in the world folder's playtest\ subfolder (beside Saves, so a -Fresh start keeps them; on the game PC
// D:\ShatteredLegacy\server\lib\uo\modernuo-test\playtest). The shard only reads the first two and only appends to the
// third:
//   testers.json              who the testers are: {"version": 1, "testers": [{"account", "name", "paused"}]}. Written by
//                             the web console's Testers page (web-console/playtesters.py), by hand.
//   tasks.json                every task ever handed out: {"version": 1, "tasks": [{"id": "G-0001", "account", "testId",
//                             "title", "why", "where", "coords", "steps": [...], "lookFor", "intro", "status": "open" or
//                             "pulled", "assignedAt"}]}. Written by the Play Test Goblin (scripts/playtest/goblin.py) and
//                             by the Testers page's Pull back. Never deleted from, so the Goblin never repeats a test.
//   playtest-reports.jsonl    one line per answer: at (UTC), account, character, task, test, button (worked, wrong,
//                             couldnt or report), text, facet, x, y, z, version. No IP address, ever.
// A task is open for its tester while its status is "open" and no report line names it from that account.
//
// IN GAME. [tasks (or "tasks" said aloud, as P769 did for lesson) opens the tester's open tasks, one page each: title,
// why it matters, where to go, steps, what to look for, a text box and three buttons, Worked, Something was wrong,
// Couldn't do it. [report opens a box for anything else ([report <text> sends it at once). A tester who logs in to new
// tasks is told so once. Anyone who is not a tester gets one line. A missing or broken file never throws: it is logged
// once and the player gets a plain line.
//
// Testers only produce evidence. Nothing here marks a test Pass or Fail; Chase alone does that on the play-test page.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Commands;
using Server.Gumps;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Tutorial;

namespace Server.PlayTest;

public sealed class PlayTester
{
    public string Account { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Paused { get; init; }
}

public sealed class PlayTestTask
{
    public string Id { get; init; } = "";
    public string Account { get; init; } = "";
    public string TestId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Why { get; init; } = "";
    public string Where { get; init; } = "";
    public string Coords { get; init; } = "";
    public List<string> Steps { get; init; } = new();
    public string LookFor { get; init; } = "";
    public string Intro { get; init; } = "";
    public string Status { get; init; } = "open";
    public string AssignedAt { get; init; } = "";
}

public static class PlayTestFiles
{
    public const string FolderName = "playtest";
    public const string TestersFileName = "testers.json";
    public const string TasksFileName = "tasks.json";
    public const string ReportsFileName = "playtest-reports.jsonl";
    public const int TextMax = 2000;

    public static readonly string[] Buttons = { "worked", "wrong", "couldnt", "report" };

    private static readonly ILogger logger = LogFactory.GetLogger(typeof(PlayTestFiles));
    private static readonly HashSet<string> _logged = new();

    /// <summary>Tests point this at a scratch folder; null means the world folder's playtest\.</summary>
    public static string FolderOverride { get; set; }

    public static string Folder => FolderOverride ??
                                   Path.Combine(Path.GetDirectoryName(World.SavePath.TrimEnd('/', '\\')) ?? ".", FolderName);

    public static string TestersPath => Path.Combine(Folder, TestersFileName);
    public static string TasksPath => Path.Combine(Folder, TasksFileName);
    public static string ReportsPath => Path.Combine(Folder, ReportsFileName);

    /// <summary>One log line per distinct problem, so a broken file does not fill the log at every login.</summary>
    private static void LogOnce(string what)
    {
        lock (_logged)
        {
            if (!_logged.Add(what))
            {
                return;
            }
        }

        logger.Warning("[PlayTest] {What}", what);
    }

    /// <summary>The testers, or null and why. A missing file is an empty list, not an error.</summary>
    public static List<PlayTester> ReadTesters(out string error)
    {
        error = null;
        string json;
        try
        {
            if (!File.Exists(TestersPath))
            {
                return new List<PlayTester>();
            }

            json = File.ReadAllText(TestersPath);
        }
        catch (Exception e)
        {
            error = $"{TestersFileName} could not be read: {e.Message}";
            LogOnce(error);
            return null;
        }

        var list = ParseTesters(json, out error);
        if (list == null)
        {
            LogOnce(error);
        }

        return list;
    }

    /// <summary>Pure, for the tests: the testers in a testers.json text, or null and why.</summary>
    public static List<PlayTester> ParseTesters(string json, out string error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("testers", out var list) ||
                list.ValueKind != JsonValueKind.Array)
            {
                error = $"{TestersFileName} has no testers list";
                return null;
            }

            var result = new List<PlayTester>();
            foreach (var e in list.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var account = Str(e, "account");
                if (account.Length == 0)
                {
                    continue;
                }

                result.Add(new PlayTester
                {
                    Account = account, Name = Str(e, "name"),
                    Paused = e.TryGetProperty("paused", out var p) && p.ValueKind == JsonValueKind.True
                });
            }

            return result;
        }
        catch (JsonException e)
        {
            error = $"{TestersFileName} is not valid JSON: {e.Message}";
            return null;
        }
    }

    /// <summary>Every task, or null and why. A missing file is an empty list.</summary>
    public static List<PlayTestTask> ReadTasks(out string error)
    {
        error = null;
        string json;
        try
        {
            if (!File.Exists(TasksPath))
            {
                return new List<PlayTestTask>();
            }

            json = File.ReadAllText(TasksPath);
        }
        catch (Exception e)
        {
            error = $"{TasksFileName} could not be read: {e.Message}";
            LogOnce(error);
            return null;
        }

        var list = ParseTasks(json, out error);
        if (list == null)
        {
            LogOnce(error);
        }

        return list;
    }

    public static List<PlayTestTask> ParseTasks(string json, out string error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tasks", out var list) ||
                list.ValueKind != JsonValueKind.Array)
            {
                error = $"{TasksFileName} has no tasks list";
                return null;
            }

            var result = new List<PlayTestTask>();
            foreach (var e in list.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = Str(e, "id");
                var account = Str(e, "account");
                if (id.Length == 0 || account.Length == 0)
                {
                    continue;
                }

                var steps = new List<string>();
                if (e.TryGetProperty("steps", out var s) && s.ValueKind == JsonValueKind.Array)
                {
                    foreach (var step in s.EnumerateArray())
                    {
                        if (step.ValueKind == JsonValueKind.String && step.GetString() is { Length: > 0 } text)
                        {
                            steps.Add(text);
                        }
                    }
                }

                var status = Str(e, "status");
                result.Add(new PlayTestTask
                {
                    Id = id, Account = account, TestId = Str(e, "testId"), Title = Str(e, "title"), Why = Str(e, "why"),
                    Where = Str(e, "where"), Coords = Str(e, "coords"), Steps = steps, LookFor = Str(e, "lookFor"),
                    Intro = Str(e, "intro"), Status = status.Length == 0 ? "open" : status, AssignedAt = Str(e, "assignedAt")
                });
            }

            return result;
        }
        catch (JsonException e)
        {
            error = $"{TasksFileName} is not valid JSON: {e.Message}";
            return null;
        }
    }

    /// <summary>The task ids this account has answered (any of the three buttons), from the reports file. A missing or
    /// unreadable file is none; a broken line is skipped.</summary>
    public static HashSet<string> AnsweredTaskIds(string account)
    {
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] lines;
        try
        {
            if (!File.Exists(ReportsPath))
            {
                return done;
            }

            lines = File.ReadAllLines(ReportsPath);
        }
        catch (Exception e)
        {
            LogOnce($"{ReportsFileName} could not be read: {e.Message}");
            return done;
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                var e = doc.RootElement;
                if (e.ValueKind == JsonValueKind.Object &&
                    string.Equals(Str(e, "account"), account, StringComparison.OrdinalIgnoreCase) &&
                    Str(e, "task") is { Length: > 0 } task && Str(e, "button") != "report")
                {
                    done.Add(task);
                }
            }
            catch (JsonException)
            {
                // one broken line never hides the rest
            }
        }

        return done;
    }

    /// <summary>Appends one report line. False (and logged) when it could not be written.</summary>
    public static bool AppendReport(string line)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(ReportsPath, line + "\n", new UTF8Encoding(false));
            return true;
        }
        catch (Exception e)
        {
            LogOnce($"{ReportsFileName} could not be written: {e.Message}");
            return false;
        }
    }

    /// <summary>One report as a JSON line. Pure, for the tests. Never an IP address: the fields are exactly these.</summary>
    public static string ReportLine(
        DateTime utc, string account, string character, string task, string test, string button, string text,
        string facet, Point3D loc, string version
    )
    {
        var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("at", utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            w.WriteString("account", account ?? "");
            w.WriteString("character", character ?? "");
            w.WriteString("task", task ?? "");
            w.WriteString("test", test ?? "");
            w.WriteString("button", button);
            w.WriteString("text", Clean(text));
            w.WriteString("facet", facet ?? "");
            w.WriteNumber("x", loc.X);
            w.WriteNumber("y", loc.Y);
            w.WriteNumber("z", loc.Z);
            w.WriteString("version", version ?? "");
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>The player's words, one line, control characters out, at most TextMax characters.</summary>
    public static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var sb = new StringBuilder(Math.Min(text.Length, TextMax));
        foreach (var c in text)
        {
            if (sb.Length >= TextMax)
            {
                break;
            }

            sb.Append(char.IsControl(c) ? ' ' : c);
        }

        return sb.ToString().Trim();
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";
}

public static class PlayTestTasks
{
    public const string NotTesterLine =
        "Play test tasks are for the shard's play testers. If you would like to help test, ask Chase.";
    public const string BrokenListLine =
        "The play tester list cannot be read right now. Please tell Chase; nothing you did caused it.";
    public const string BrokenTasksLine =
        "Your play test tasks cannot be read right now. Please tell Chase. You can still send a note with [report.";
    public const string NoTasksLine =
        "You have no open play test tasks right now. New ones arrive overnight. For anything odd, type [report.";
    public const string NewTasksLine =
        "You have new play test tasks. Type [tasks (or just say tasks) to see them.";
    public const string ThanksLine = "Thank you! Your report was saved for Chase.";
    public const string NotSavedLine = "Your report could not be saved. Please tell Chase what you wanted to say.";
    public const string NeedWordsLine = "Please write a few words about what happened in the box, then press the button again.";
    public const string SpokenWord = "tasks";
    public const string SeenTag = "playtest.seenTasks";

    private static bool _hooked;

    /// <summary>The two commands and the spoken word. Safe to call twice (the tests call it).</summary>
    public static void Configure()
    {
        if (_hooked)
        {
            return;
        }

        _hooked = true;
        CommandSystem.Register("Tasks", AccessLevel.Player, OnTasksCommand);
        CommandSystem.Register("Report", AccessLevel.Player, OnReportCommand);
        EventSink.Speech += OnSpeech;
    }

    /// <summary>The tester for this player, or null; error is set when the list cannot be read.</summary>
    public static PlayTester TesterOf(Mobile m, out string error)
    {
        error = null;
        var account = (m?.Account as Account)?.Username;
        if (string.IsNullOrEmpty(account))
        {
            return null;
        }

        var testers = PlayTestFiles.ReadTesters(out error);
        return testers?.FirstOrDefault(t => string.Equals(t.Account, account, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>This account's open tasks, oldest first, or null when the tasks file cannot be read.</summary>
    public static List<PlayTestTask> OpenTasks(string account)
    {
        var all = PlayTestFiles.ReadTasks(out _);
        if (all == null)
        {
            return null;
        }

        var answered = PlayTestFiles.AnsweredTaskIds(account);
        return all.Where(
                t => string.Equals(t.Account, account, StringComparison.OrdinalIgnoreCase) && t.Status == "open" &&
                     !answered.Contains(t.Id)
            )
            .OrderBy(t => t.AssignedAt, StringComparer.Ordinal)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList();
    }

    [Usage("Tasks")]
    [Description("Play testers: your open play test tasks.")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnTasksCommand(CommandEventArgs e) => ShowTasks(e.Mobile, true);

    [Usage("Report [text]")]
    [Description("Play testers: tell Chase about anything odd. With text, it is sent at once.")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnReportCommand(CommandEventArgs e)
    {
        var m = e.Mobile;
        if (!IsTester(m))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(e.ArgString))
        {
            Answer(m, null, "report", e.ArgString);
            return;
        }

        m.CloseGump<PlayTestReportGump>();
        m.SendGump(new PlayTestReportGump());
    }

    /// <summary>A tester (answered with the right line when not). Never throws.</summary>
    private static bool IsTester(Mobile m)
    {
        var tester = TesterOf(m, out var error);
        if (tester != null)
        {
            return true;
        }

        m.SendMessage(error != null ? BrokenListLine : NotTesterLine);
        return false;
    }

    /// <summary>The tasks window, or one line. answerOthers false: someone who is not a tester gets nothing (speech).</summary>
    public static void ShowTasks(Mobile m, bool answerOthers)
    {
        if (m == null)
        {
            return;
        }

        var tester = TesterOf(m, out var error);
        if (tester == null)
        {
            if (answerOthers)
            {
                m.SendMessage(error != null ? BrokenListLine : NotTesterLine);
            }

            return;
        }

        var open = OpenTasks(tester.Account);
        if (open == null)
        {
            m.SendMessage(BrokenTasksLine);
            return;
        }

        m.CloseGump<PlayTestTasksGump>();
        if (open.Count == 0)
        {
            m.SendMessage(NoTasksLine);
            return;
        }

        MarkSeen(m, open);
        m.SendGump(new PlayTestTasksGump(open));
    }

    /// <summary>
    /// One answer: a task's button (worked, wrong, couldnt) or a free report (task null, button report). Writes one line
    /// and thanks the player, or says why not. The task must be one of the player's open tasks.
    /// </summary>
    public static bool Answer(Mobile m, string taskId, string button, string text)
    {
        if (m == null || Array.IndexOf(PlayTestFiles.Buttons, button) < 0)
        {
            return false;
        }

        var tester = TesterOf(m, out var error);
        if (tester == null)
        {
            m.SendMessage(error != null ? BrokenListLine : NotTesterLine);
            return false;
        }

        text = PlayTestFiles.Clean(text);
        PlayTestTask task = null;
        if (button != "report")
        {
            task = OpenTasks(tester.Account)?.FirstOrDefault(t => t.Id == taskId);
            if (task == null)
            {
                m.SendMessage("That task is no longer open. Type [tasks to see your list.");
                return false;
            }

            if (button != "worked" && text.Length == 0)
            {
                m.SendMessage(NeedWordsLine);
                return false;
            }
        }
        else if (text.Length == 0)
        {
            m.SendMessage(NeedWordsLine);
            return false;
        }

        var line = PlayTestFiles.ReportLine(
            DateTime.UtcNow, tester.Account, m.Name, task?.Id ?? "", task?.TestId ?? "", button, text,
            m.Map?.Name ?? "", m.Location, ShardVersion.Current
        );
        if (!PlayTestFiles.AppendReport(line))
        {
            m.SendMessage(NotSavedLine);
            return false;
        }

        m.SendMessage(ThanksLine);
        return true;
    }

    /// <summary>Remember the newest task the account has been shown, for the login line.</summary>
    private static void MarkSeen(Mobile m, List<PlayTestTask> open)
    {
        if (m.Account is Account acct && open.Count > 0)
        {
            var newest = open.Max(t => t.AssignedAt + "|" + t.Id);
            if (string.CompareOrdinal(newest, acct.GetTag(SeenTag) ?? "") > 0)
            {
                acct.SetTag(SeenTag, newest);
            }
        }
    }

    /// <summary>True when the tester has an open task newer than the last one shown to the account. Pure on the files.</summary>
    public static bool HasNewTasks(Mobile m)
    {
        var tester = TesterOf(m, out _);
        if (tester == null || m.Account is not Account acct)
        {
            return false;
        }

        var open = OpenTasks(tester.Account);
        if (open == null || open.Count == 0)
        {
            return false;
        }

        var seen = acct.GetTag(SeenTag) ?? "";
        return open.Any(t => string.CompareOrdinal(t.AssignedAt + "|" + t.Id, seen) > 0);
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        try
        {
            if (HasNewTasks(pm))
            {
                pm.SendMessage(0x35, NewTasksLine);
            }
        }
        catch (Exception e)
        {
            // a login is never stopped by the play test files
            Console.WriteLine($"[PlayTest] login line skipped: {e.Message}");
        }
    }

    private static void OnSpeech(SpeechEventArgs e)
    {
        if (e.Blocked || e.Mobile is not PlayerMobile pm)
        {
            return;
        }

        if (string.Equals(e.Speech.Trim().TrimEnd('.', '!', '?'), SpokenWord, StringComparison.OrdinalIgnoreCase))
        {
            ShowTasks(pm, false); // only a tester is answered: anyone may say the word
        }
    }
}

/// <summary>The tester's open tasks, one page each.</summary>
public sealed class PlayTestTasksGump : Gump
{
    public const int ReportButton = 2;
    public const int Width = 540, Height = 500;
    private readonly List<PlayTestTask> _tasks;

    public IReadOnlyList<PlayTestTask> Tasks => _tasks;

    public static int ButtonFor(int index, string button) =>
        100 + index * 10 + button switch { "worked" => 1, "wrong" => 2, _ => 3 };

    public PlayTestTasksGump(List<PlayTestTask> tasks) : base(60, 60)
    {
        _tasks = tasks;
        AddPage(0);
        TutorialStyle.Panel(this, Width, Height);
        AddButton(24, Height - 40, 4011, 4013, ReportButton);
        AddLabel(60, Height - 38, TutorialStyle.ActionHue, "Report something else");

        for (var i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            AddPage(i + 1);
            AddLabel(24, 16, TutorialStyle.HeadingHue, $"Play test tasks: {i + 1} of {tasks.Count}");
            if (t.Intro.Length > 0)
            {
                AddHtml(24, 40, Width - 48, 22, TutorialStyle.Html("<I>" + Escape(t.Intro) + "</I>"), false, false);
            }

            var ids = t.TestId.Length > 0 ? $" (test {t.TestId}, task {t.Id})" : $" (task {t.Id})";
            AddHtml(24, 64, Width - 48, 22, TutorialStyle.HeadingHtml(Escape(t.Title) + ids), false, false);
            AddHtml(24, 90, Width - 48, 236, TutorialStyle.Html(Body(t)), true, true);

            AddLabel(24, 334, TutorialStyle.TextHue, "What happened? (needed for the last two buttons)");
            AddBackground(24, 354, Width - 48, 26, 9350);
            AddTextEntry(30, 357, Width - 60, 20, 0, i, "");

            AddButton(24, 392, 4005, 4007, ButtonFor(i, "worked"));
            AddLabel(58, 394, TutorialStyle.DoneHue, "Worked");
            AddButton(150, 392, 4005, 4007, ButtonFor(i, "wrong"));
            AddLabel(184, 394, TutorialStyle.WarnHue, "Something was wrong");
            AddButton(346, 392, 4005, 4007, ButtonFor(i, "couldnt"));
            AddLabel(380, 394, TutorialStyle.WarnHue, "Couldn't do it");

            if (i > 0)
            {
                AddButton(300, Height - 40, 4014, 4016, 0, GumpButtonType.Page, i);
                AddLabel(334, Height - 38, TutorialStyle.TextHue, "Back");
            }

            if (i < tasks.Count - 1)
            {
                AddButton(410, Height - 40, 4005, 4007, 0, GumpButtonType.Page, i + 2);
                AddLabel(444, Height - 38, TutorialStyle.TextHue, "Next task");
            }
        }
    }

    /// <summary>The task's words in the window: why, where, steps, what to look for. Plain player language.</summary>
    public static string Body(PlayTestTask t)
    {
        var sb = new StringBuilder();
        if (t.Why.Length > 0)
        {
            sb.Append("<B>Why it matters:</B> ").Append(Escape(t.Why)).Append("<BR><BR>");
        }

        if (t.Where.Length > 0 || t.Coords.Length > 0)
        {
            sb.Append("<B>Where to go:</B> ").Append(Escape(t.Where));
            if (t.Coords.Length > 0)
            {
                sb.Append(t.Where.Length > 0 ? " " : "").Append('(').Append(Escape(t.Coords)).Append(')');
            }

            sb.Append("<BR><BR>");
        }

        if (t.Steps.Count > 0)
        {
            sb.Append("<B>Steps:</B><BR>");
            for (var i = 0; i < t.Steps.Count; i++)
            {
                sb.Append(i + 1).Append(". ").Append(Escape(t.Steps[i])).Append("<BR>");
            }

            sb.Append("<BR>");
        }

        if (t.LookFor.Length > 0)
        {
            sb.Append("<B>What to look for:</B> ").Append(Escape(t.LookFor));
        }

        return sb.ToString();
    }

    public static string Escape(string s) => (s ?? "").Replace("<", "(").Replace(">", ")");

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var m = sender.Mobile;
        if (m == null)
        {
            return;
        }

        if (info.ButtonID == ReportButton)
        {
            m.SendGump(new PlayTestReportGump());
            return;
        }

        var index = (info.ButtonID - 100) / 10;
        var which = (info.ButtonID - 100) % 10;
        if (info.ButtonID < 100 || index >= _tasks.Count || which is < 1 or > 3)
        {
            return;
        }

        var button = which == 1 ? "worked" : which == 2 ? "wrong" : "couldnt";
        PlayTestTasks.Answer(m, _tasks[index].Id, button, info.GetTextEntry(index));
        PlayTestTasks.ShowTasks(m, true); // the list again: the rest of the tasks, or "no open tasks"
    }
}

/// <summary>[report: a box for anything odd, not tied to a task.</summary>
public sealed class PlayTestReportGump : Gump
{
    public const int SendButton = 1;

    public PlayTestReportGump() : base(80, 80)
    {
        TutorialStyle.Panel(this, 500, 210);
        AddLabel(24, 16, TutorialStyle.HeadingHue, "Report something to Chase");
        AddHtml(
            24, 42, 452, 56,
            TutorialStyle.Html(
                "Anything odd, broken, confusing or fun: say what happened and what you expected. Where you stand and the time are saved with it."
            ),
            false, false
        );
        AddBackground(24, 104, 452, 26, 9350);
        AddTextEntry(30, 107, 440, 20, 0, 0, "");
        AddButton(24, 150, 4005, 4007, SendButton);
        AddLabel(58, 152, TutorialStyle.ActionHue, "Send");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is { } m && info.ButtonID == SendButton)
        {
            PlayTestTasks.Answer(m, null, "report", info.GetTextEntry(0));
        }
    }
}
