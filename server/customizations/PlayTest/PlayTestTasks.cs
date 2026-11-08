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
//
// cc-P789 (notes/cc-P789-tasks-window-and-staff-testers.md):
//   - the window is readable: a solid dark body, near-white text in TazUO's plain Unicode font, a taller text area, the
//     plain task title, and the task id only in a small gray footer. No prompt or test id is ever shown (PlayTestText).
//   - a task carries "needsPlayer" (true: the card needs a normal player account: a new character, the tutorial, Young
//     status...). A staff account holding one sees a line saying to try it on a player character.
//   - tester-accounts.json, beside testers.json, is written by the shard only: each tester's account, whether it exists
//     and its access level, so the Play Test Goblin never gives a player-only card to a staff account (it reads this
//     instead of a hand-set flag that could drift). Written at start and every ExportMinutes.

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

    /// <summary>cc-P789: true when the card needs a normal player account; null when the task does not say.</summary>
    public bool? NeedsPlayer { get; init; }
}

public static class PlayTestFiles
{
    public const string FolderName = "playtest";
    public const string TestersFileName = "testers.json";
    public const string TasksFileName = "tasks.json";
    public const string ReportsFileName = "playtest-reports.jsonl";
    public const string AccountsFileName = "tester-accounts.json";
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
    public static string AccountsPath => Path.Combine(Folder, AccountsFileName);

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
                bool? needsPlayer = e.TryGetProperty("needsPlayer", out var np)
                    ? np.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null }
                    : null;
                result.Add(new PlayTestTask
                {
                    Id = id, Account = account, TestId = Str(e, "testId"), Title = Str(e, "title"), Why = Str(e, "why"),
                    Where = Str(e, "where"), Coords = Str(e, "coords"), Steps = steps, LookFor = Str(e, "lookFor"),
                    Intro = Str(e, "intro"), Status = status.Length == 0 ? "open" : status, AssignedAt = Str(e, "assignedAt"),
                    NeedsPlayer = needsPlayer
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
        EventSink.ServerStarted += PlayTestAccounts.Start;
    }

    /// <summary>cc-P789: a staff account (Counselor and up), on the account or the character.</summary>
    public static bool IsStaff(Mobile m) =>
        m != null && (m.AccessLevel >= AccessLevel.Counselor || (m.Account as Account)?.AccessLevel >= AccessLevel.Counselor);

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
        m.SendGump(new PlayTestTasksGump(open, IsStaff(m)));
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
                TutorialWelcome.Note(pm, NewTasksLine, 0x35); // cc-P802 item 7: in the welcome window while waking
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

/// <summary>
/// cc-P789: tester-accounts.json, beside testers.json: for each tester's account, whether it exists on this shard and its
/// access level. The shard writes it (at start and every ExportMinutes, whole and atomic); the Play Test Goblin and the
/// web console's Play testers page only read it. Only the testers' accounts are listed, nothing else about them.
///   {"version": 1, "writtenAt": "...Z", "accounts": [{"account": "admin-chase", "exists": true,
///    "accessLevel": "Owner", "staff": true}]}
/// </summary>
public static class PlayTestAccounts
{
    public const int ExportMinutes = 5;
    private static Timer _timer;

    public static void Start()
    {
        Export();
        _timer?.Stop();
        _timer = Timer.DelayCall(TimeSpan.FromMinutes(ExportMinutes), TimeSpan.FromMinutes(ExportMinutes), () => Export());
    }

    /// <summary>The export's text for these testers. Pure but for the account lookup, for the tests.</summary>
    public static string ExportJson(IEnumerable<PlayTester> testers, DateTime utc)
    {
        var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("version", 1);
            w.WriteString("writtenAt", utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            w.WriteStartArray("accounts");
            foreach (var t in testers)
            {
                var acct = Accounts.GetAccount(t.Account) as Account;
                w.WriteStartObject();
                w.WriteString("account", t.Account);
                w.WriteBoolean("exists", acct != null);
                w.WriteString("accessLevel", (acct?.AccessLevel ?? AccessLevel.Player).ToString());
                w.WriteBoolean("staff", acct != null && acct.AccessLevel >= AccessLevel.Counselor);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    /// <summary>Writes the export. False (and logged) when the tester list cannot be read or the file written.</summary>
    public static bool Export()
    {
        try
        {
            var testers = PlayTestFiles.ReadTesters(out var error);
            if (testers == null)
            {
                return false; // a broken list is logged by ReadTesters; the old export stays
            }

            Directory.CreateDirectory(PlayTestFiles.Folder);
            var tmp = PlayTestFiles.AccountsPath + ".tmp";
            File.WriteAllText(tmp, ExportJson(testers, DateTime.UtcNow), new UTF8Encoding(false));
            File.Move(tmp, PlayTestFiles.AccountsPath, true);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[PlayTest] {PlayTestFiles.AccountsFileName} not written: {e.Message}");
            return false;
        }
    }
}

/// <summary>
/// cc-P789: the look of the play test windows. Chase (11-03) found P773's window hard to read: pale text on the HTML
/// box's parchment, every line in the bold pixel Unicode font, an italic intro, a small scrolling steps box, and prompt
/// and test ids in the title. Now: a solid dark body (no parchment, no see-through), near-white text, color only for
/// the headings and the three answer buttons, the body and the intro in Unicode font 2 (the plain, thin one), and only
/// the title and the headings in font 1.
///
/// The font: TazUO draws a server gump's HTML with its own copy of UO's Unicode fonts, never with its TTF fonts (its
/// HtmlControl is a RenderedText on unifont; GumpHelpers.cs builds every htmlgump that way, TazUO 3212623f). The only
/// font choice the server has is BASEFONT SIZE (FontsLoader.cs:3092-3125): 0 and 4 pick font 1 (the default), 1 to 3
/// pick font 2, and 5 or more pick font 0, which is blackletter (all three rendered from the client's unifont files in
/// shard-migration notes/cc-P789-tools/unifont-sample.png). So the words say SIZE=2 and the headings SIZE=4.
/// </summary>
public static class PlayTestStyle
{
    public const int Frame = 9270;          // the tutorial's dark stone frame
    public const int Solid = 2624;          // a black tile, laid solid (no alpha region): no parchment, nothing behind
    public const string Text = "#F4F4F4";   // near white
    public const string Heading = "#FFD54A"; // warm yellow: the four headings and the title
    public const string Intro = "#BFE3FF";  // pale blue: the Goblin's one line, plain
    public const string Staff = "#FF9E7A";  // soft orange: the staff account's note
    public const int FooterHue = 0x3B2;     // gray: the task id
    public const int TextHue = 1153;        // near white labels
    public const int WorkedHue = 68;        // green
    public const int ProblemHue = 34;       // light red
    public const int ActionHue = 90;        // light cyan

    /// <summary>Body words: font 2, plain.</summary>
    public static string Font(string color, string text) => $"<BASEFONT SIZE=2 COLOR={color}>{text}</BASEFONT>";

    /// <summary>The title and the headings: font 1, bold.</summary>
    public static string HeadFont(string color, string text) => $"<BASEFONT SIZE=4 COLOR={color}>{text}</BASEFONT>";

    public static void Panel(Gump g, int width, int height)
    {
        g.AddBackground(0, 0, width, height, Frame);
        g.AddImageTiled(10, 10, width - 20, height - 20, Solid);
    }
}

/// <summary>
/// cc-P789: what a tester sees of a card is plain words only. Prompt numbers (P762, cc-P773, P762-0), play-test rows
/// (T-012, PT-4) and task ids (G-0001) are taken out of every line shown; the task id is shown once, in the footer.
/// </summary>
public static class PlayTestText
{
    private const string Id = @"(?:cc-)?P\d{2,4}(?:-\d+)?|PT-\d+|T-\d{2,4}|G-\d{3,}";

    private static readonly System.Text.RegularExpressions.Regex Parenthetical =
        new(@"\s*\([^()]*\b(?:" + Id + @")\b[^()]*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex Bare =
        new(@"\b(?:(?:test|task|prompt|item|card|row)\s+)?(?:" + Id + @")\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex Spaces = new(@"\s{2,}");
    private static readonly System.Text.RegularExpressions.Regex BeforePunct = new(@"\s+([.,;:!?])");
    private static readonly System.Text.RegularExpressions.Regex Empty = new(@"\(\s*[,;:]?\s*\)");

    /// <summary>A line with every prompt, test and task id taken out.</summary>
    public static string Scrub(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }

        s = Parenthetical.Replace(s, "");
        s = Bare.Replace(s, "");
        s = Empty.Replace(s, "");
        s = BeforePunct.Replace(s, "$1");
        s = Spaces.Replace(s, " ");
        return s.Trim().TrimStart(',', ';', ':').Trim();
    }

    /// <summary>True when a line still carries an id a tester must not see (the tests' check).</summary>
    public static bool HasId(string s) =>
        !string.IsNullOrEmpty(s) && new System.Text.RegularExpressions.Regex(@"\b(?:" + Id + @")\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).IsMatch(s);
}

/// <summary>The tester's open tasks, one page each.</summary>
public sealed class PlayTestTasksGump : Gump
{
    public const int ReportButton = 2;
    public const int Width = 620, Height = 666;
    public const int BodyTop = 96, BodyHeight = 420;
    public const string StaffLine = "This one needs a player account. Try it on your player character.";

    private readonly List<PlayTestTask> _tasks;
    private readonly List<string> _shown = new();
    private readonly List<string> _footers = new();

    public IReadOnlyList<PlayTestTask> Tasks => _tasks;

    /// <summary>Every piece of text the window shows a tester, but the footers (the tests read it).</summary>
    public IReadOnlyList<string> Shown => _shown;

    /// <summary>The small gray footer lines, one per page: the task id, so a report ties back.</summary>
    public IReadOnlyList<string> Footers => _footers;

    public static int ButtonFor(int index, string button) =>
        100 + index * 10 + button switch { "worked" => 1, "wrong" => 2, _ => 3 };

    public PlayTestTasksGump(List<PlayTestTask> tasks, bool staff = false) : base(40, 30)
    {
        _tasks = tasks;
        AddPage(0);
        PlayTestStyle.Panel(this, Width, Height);
        AddButton(24, Height - 64, 4011, 4013, ReportButton);
        Label(60, Height - 62, PlayTestStyle.ActionHue, "Report something else");

        for (var i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            AddPage(i + 1);
            Label(24, 18, PlayTestStyle.TextHue, $"Play test task {i + 1} of {tasks.Count}");
            Html(24, 42, Width - 48, 24, PlayTestStyle.HeadFont(PlayTestStyle.Heading, Escape(PlayTestText.Scrub(t.Title))), false);
            if (t.Intro.Length > 0)
            {
                Html(24, 68, Width - 48, 24, PlayTestStyle.Font(PlayTestStyle.Intro, Escape(PlayTestText.Scrub(t.Intro))), false);
            }

            Html(24, BodyTop, Width - 48, BodyHeight, Body(t, staff), NeedsScroll(t, staff));

            Label(24, 524, PlayTestStyle.TextHue, "What happened? (needed for the last two buttons)");
            AddBackground(24, 544, Width - 48, 28, 9350);
            AddTextEntry(30, 548, Width - 60, 20, 0, i, "");

            AddButton(24, 580, 4005, 4007, ButtonFor(i, "worked"));
            Label(58, 582, PlayTestStyle.WorkedHue, "Worked");
            AddButton(170, 580, 4005, 4007, ButtonFor(i, "wrong"));
            Label(204, 582, PlayTestStyle.ProblemHue, "Something was wrong");
            AddButton(390, 580, 4005, 4007, ButtonFor(i, "couldnt"));
            Label(424, 582, PlayTestStyle.ProblemHue, "Couldn't do it");

            if (i > 0)
            {
                AddButton(360, Height - 64, 4014, 4016, 0, GumpButtonType.Page, i);
                Label(394, Height - 62, PlayTestStyle.TextHue, "Back");
            }

            if (i < tasks.Count - 1)
            {
                AddButton(470, Height - 64, 4005, 4007, 0, GumpButtonType.Page, i + 2);
                Label(504, Height - 62, PlayTestStyle.TextHue, "Next task");
            }

            var footer = $"Task {t.Id}";
            _footers.Add(footer);
            AddLabel(24, Height - 34, PlayTestStyle.FooterHue, footer);
        }
    }

    private void Label(int x, int y, int hue, string text)
    {
        _shown.Add(text);
        AddLabel(x, y, hue, text);
    }

    private void Html(int x, int y, int w, int h, string html, bool scroll)
    {
        _shown.Add(html);
        AddHtml(x, y, w, h, html, false, scroll);
    }

    private static string Section(string heading) => PlayTestStyle.HeadFont(PlayTestStyle.Heading, heading) + "<BR>";

    // Font 2 in the 572 x 420 text area, measured on the after shot (notes): about 105 characters a line and 18 pixels a
    // line, so 23 lines. Counted with a margin: 90 characters a line, 22 lines. The longest isle card is 17.
    public const int LineChars = 90, BodyLines = 22;

    /// <summary>A scroll bar only for a card too long for the text area (none of the 16 isle cards is).</summary>
    public static bool NeedsScroll(PlayTestTask t, bool staff = false)
    {
        int Lines(string s) => Math.Max(1, (PlayTestText.Scrub(s).Length + LineChars - 1) / LineChars);
        var n = staff && t.NeedsPlayer == true ? 2 : 0;
        n += t.Why.Length > 0 ? 2 + Lines(t.Why) : 0;
        n += t.Where.Length > 0 || t.Coords.Length > 0 ? 2 + Lines(t.Where + " (" + t.Coords + ")") : 0;
        n += t.Steps.Count > 0 ? 2 + t.Steps.Sum(Lines) : 0;
        n += t.LookFor.Length > 0 ? 1 + Lines(t.LookFor) : 0;
        return n > BodyLines;
    }

    private static string Words(string text) => PlayTestStyle.Font(PlayTestStyle.Text, Escape(PlayTestText.Scrub(text)));

    /// <summary>The task's words in the window: why, where, steps, what to look for, a blank line between sections.
    /// Plain player language: no prompt, test or task ids. staff: the account is staff, so a card that needs a player
    /// account says so first.</summary>
    public static string Body(PlayTestTask t, bool staff = false)
    {
        var parts = new List<string>();
        if (staff && t.NeedsPlayer == true)
        {
            parts.Add(PlayTestStyle.HeadFont(PlayTestStyle.Staff, StaffLine));
        }

        if (t.Why.Length > 0)
        {
            parts.Add(Section("Why it matters") + Words(t.Why));
        }

        if (t.Where.Length > 0 || t.Coords.Length > 0)
        {
            var where = PlayTestText.Scrub(t.Where);
            var coords = PlayTestText.Scrub(t.Coords);
            var text = coords.Length > 0 ? (where.Length > 0 ? $"{where} ({coords})" : coords) : where;
            parts.Add(Section("Where to go") + PlayTestStyle.Font(PlayTestStyle.Text, Escape(text)));
        }

        if (t.Steps.Count > 0)
        {
            var sb = new StringBuilder(Section("Steps"));
            for (var i = 0; i < t.Steps.Count; i++)
            {
                sb.Append(PlayTestStyle.Font(PlayTestStyle.Text, $"{i + 1}. {Escape(PlayTestText.Scrub(t.Steps[i]))}"));
                if (i < t.Steps.Count - 1)
                {
                    sb.Append("<BR>");
                }
            }

            parts.Add(sb.ToString());
        }

        if (t.LookFor.Length > 0)
        {
            parts.Add(Section("What to look for") + Words(t.LookFor));
        }

        return string.Join("<BR><BR>", parts);
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

/// <summary>[report: a box for anything odd, not tied to a task. cc-P789: the tasks window's look.</summary>
public sealed class PlayTestReportGump : Gump
{
    public const int SendButton = 1;
    public const int Width = 560, Height = 250;

    public PlayTestReportGump() : base(80, 80)
    {
        PlayTestStyle.Panel(this, Width, Height);
        AddHtml(24, 18, Width - 48, 24, PlayTestStyle.HeadFont(PlayTestStyle.Heading, "Report something to Chase"), false, false);
        AddHtml(
            24, 50, Width - 48, 80,
            PlayTestStyle.Font(
                PlayTestStyle.Text,
                "Anything odd, broken, confusing or fun: say what happened and what you expected. Where you stand and the time are saved with it."
            ),
            false, false
        );
        AddBackground(24, 138, Width - 48, 28, 9350);
        AddTextEntry(30, 142, Width - 60, 20, 0, 0, "");
        AddButton(24, 190, 4005, 4007, SendButton);
        AddLabel(58, 192, PlayTestStyle.ActionHue, "Send");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is { } m && info.ButtonID == SendButton)
        {
            PlayTestTasks.Answer(m, null, "report", info.GetTextEntry(0));
        }
    }
}
