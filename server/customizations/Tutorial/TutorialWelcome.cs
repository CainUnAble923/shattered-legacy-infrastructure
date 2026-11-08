// cc-P802 item 7 (Chase 11-07, a fresh character waking on the Wakening Isle: "Still too much going on in system messages.
// it should all be gumps."). His shot showed ten system lines at once over Tamsin's greeting: the young status, four "Your
// skill in ... has increased by 30.0" lines, the play test tasks note, "Your account's tutorial continues", the wake text,
// "The chat system has been disabled" and the get-up hint. The first minutes are quiet now:
//
//   - One welcome window (the lesson window's style), shown as the trainee lies on the sand (TutorialWake.Play): the wake
//     text, how to get up, and a Notes part: the young status in one line, the play test tasks note, the tutorial-
//     continues line. Each of those comes here instead of the System window while the trainee is holding (lying, getting
//     up, or about to: TutorialWake.IsHolding), through Note. Once they stand, the window turns to the first step (follow
//     the arrow to Tamsin) and the System window echoes it in a few words.
//   - The skill lines were TazUO's (UpdateSkills.cs prints a single-skill update's change when "show skills changed" is
//     on): ClusterFSkillCaps set every cap at the first login, each cap that changed sent a single-skill update, and the
//     client, which had no skill values yet, read 0 -> 30. The caps are set at creation now (CharacterCreation, before the
//     client is attached), so the login pass changes nothing and sends nothing. Real gains still print.
//   - The chat line was pinned's answer to the open-chat request TazUO sends as it enters the world (ChatPackets.cs:34).
//     Our handler (Initialize, after pinned's Configure) answers it only for a request made after QuietChatSeconds in the
//     world, so a player who presses Chat still hears that chat is off; the one at login is not answered.
// Notes: shard-migration notes/cc-P802-haven-notes-2.md.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using Server.Engines.Chat;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Tutorial;

public static class TutorialWelcome
{
    public const string Title = "The Wakening Isle";
    public const string StandHint = "To get up, take a step: hold the right mouse button on the ground, or press an arrow key.";
    public const string FirstStep =
        "You are up. Follow the arrow to Tamsin, your guide, and double-click her (two quick left clicks). Her window starts each lesson.";
    public const string FirstStepEcho = "Follow the arrow to Tamsin and double-click her.";
    public const string VisitorStep =
        "Follow the road east to the moongate at the end of the dock; it takes you to New Haven.";
    public const string VisitorEcho = "Follow the road east to the moongate.";
    public const int QuietChatSeconds = 30;

    // The notes held for each trainee's welcome window (not saved: a restart's login says them again).
    private static readonly Dictionary<Serial, List<string>> _notes = new();
    private static readonly HashSet<Serial> _standing = new();
    private static readonly Dictionary<Serial, string> _stepText = new();

    /// <summary>The notes the window shows for <paramref name="m"/> (tests).</summary>
    public static IReadOnlyList<string> NotesOf(Mobile m) => _notes.TryGetValue(m.Serial, out var list) ? list : [];

    /// <summary>
    /// A line a new player must read at login: into the welcome window while <paramref name="pm"/> is holding (the window is
    /// refreshed if open), otherwise to the System window as before (<paramref name="hue"/>). True when the window took it.
    /// </summary>
    public static bool Note(PlayerMobile pm, string line, int hue = 0x3B2)
    {
        if (!TutorialWake.IsHolding(pm))
        {
            pm.SendMessage(hue, line);
            return false;
        }

        if (!_notes.TryGetValue(pm.Serial, out var list))
        {
            _notes[pm.Serial] = list = new List<string>();
        }

        if (!list.Contains(line))
        {
            list.Add(line);
        }

        if (pm.HasGump<TutorialWelcomeGump>())
        {
            Show(pm);
        }

        return true;
    }

    /// <summary>TutorialWake.Play: the window as the trainee lies on the sand.</summary>
    public static void Lying(PlayerMobile pm)
    {
        _standing.Remove(pm.Serial);
        Show(pm);
    }

    /// <summary>
    /// TutorialStart's first lines, once the trainee is up: the window turns to the first step (<paramref name="text"/> for a
    /// visitor's), and the System window echoes it in a few words.
    /// </summary>
    public static void Standing(PlayerMobile pm, string? text = null)
    {
        _standing.Add(pm.Serial);
        if (text != null) { _stepText[pm.Serial] = text; } else { _stepText.Remove(pm.Serial); }
        Show(pm);
        ClusterFTutorial.SystemLine(pm, text != null ? VisitorEcho : FirstStepEcho);
    }

    public static bool IsStanding(Mobile m) => _standing.Contains(m.Serial);

    public static void Show(PlayerMobile pm)
    {
        pm.CloseGump<TutorialWelcomeGump>();
        pm.SendGump(new TutorialWelcomeGump(Body(pm), NotesOf(pm)));
    }

    /// <summary>The window's main text for <paramref name="pm"/> now.</summary>
    public static string Body(Mobile pm) => !_standing.Contains(pm.Serial)
        ? $"{TutorialWake.LyingLine}<br><br>{StandHint}"
        : _stepText.GetValueOrDefault(pm.Serial) ?? FirstStep;

    /// <summary>Closing the window ends its notes (they were read).</summary>
    public static void Forget(Mobile m)
    {
        _notes.Remove(m.Serial);
        _standing.Remove(m.Serial);
        _stepText.Remove(m.Serial);
    }

    // -- The open-chat request at login --

    private static readonly Dictionary<Serial, DateTime> _enteredAt = new();

    public static unsafe void Initialize()
    {
        EventSink.Disconnected += m => { _enteredAt.Remove(m.Serial); Forget(m); };
        IncomingPackets.Register(0xB5, 0x40, true, &OpenChatWindowRequest);
    }

    [ModernUO.CodeGeneratedEvents.OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm) => _enteredAt[pm.Serial] = Core.Now;

    /// <summary>True when an open-chat request now is the client's own at login (not answered while chat is off).</summary>
    public static bool IsLoginChatRequest(Mobile m) =>
        m is PlayerMobile pm && (TutorialWake.IsHolding(pm) ||
                                 _enteredAt.TryGetValue(pm.Serial, out var at) && Core.Now - at < TimeSpan.FromSeconds(QuietChatSeconds));

    public static void OpenChatWindowRequest(NetState state, SpanReader reader)
    {
        if (!ChatSystem.Enabled && state.Mobile is { } from && IsLoginChatRequest(from))
        {
            return; // cc-P802: the client asks by itself as it enters the world; nobody pressed Chat
        }

        ChatPackets.OpenChatWindowRequest(state, reader);
    }
}

/// <summary>cc-P802 item 7: the welcome window on the Wakening Isle's beach.</summary>
public sealed class TutorialWelcomeGump : Gump
{
    public string Text { get; }
    public IReadOnlyList<string> Notes { get; }

    // cc-P802: the upper left of the game window, clear of Tamsin (east of the trainee) and above the trainee; the
    // first TazUO shot had it at 80,80 over Tamsin, and the young line clipped at two lines.
    public const int Left = 20, Top = 60, Width = 440, NoteHeight = 66;

    public TutorialWelcomeGump(string text, IReadOnlyList<string> notes) : base(Left, Top)
    {
        Text = text;
        Notes = [.. notes];
        var notesHeight = Notes.Count == 0 ? 0 : 30 + Notes.Count * NoteHeight;
        TutorialStyle.Panel(this, Width, 200 + notesHeight);
        AddLabel(24, 18, TutorialStyle.HeadingHue, TutorialWelcome.Title);
        AddHtml(24, 46, Width - 48, 110, TutorialStyle.Html(text), false, false);
        var y = 160;
        if (Notes.Count > 0)
        {
            AddLabel(24, y, TutorialStyle.HeadingHue, "Notes");
            y += 26;
            foreach (var note in Notes)
            {
                AddHtml(24, y, Width - 48, NoteHeight - 2, TutorialStyle.Html(note), false, false);
                y += NoteHeight;
            }
        }

        AddButton(24, y + 4, 4005, 4007, 1);
        AddLabel(60, y + 6, TutorialStyle.ActionHue, "Close");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 1 && sender.Mobile is PlayerMobile pm && TutorialWelcome.IsStanding(pm))
        {
            TutorialWelcome.Forget(pm);
        }
    }
}

/// <summary>cc-P802 item 1: a lesson's words in a window that stays until closed (a demonstration, a pointer).</summary>
public sealed class TutorialNoteGump : Gump
{
    public string Title { get; }
    public string Text { get; }

    public TutorialNoteGump(string title, string text) : base(80, 80)
    {
        Title = title;
        Text = text;
        TutorialStyle.Panel(this, 480, 250);
        AddLabel(24, 18, TutorialStyle.HeadingHue, title);
        AddHtml(24, 46, 432, 150, TutorialStyle.Html(text), false, false);
        AddButton(24, 206, 4005, 4007, 1);
        AddLabel(60, 208, TutorialStyle.ActionHue, "Close");
    }
}
