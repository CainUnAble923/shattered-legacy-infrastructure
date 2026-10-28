// cc-P80: the tutorial's pages. Notes: shard-migration notes/cc-P80-new-player-tutorial.md.
//
// TazUO (our client) takes ordinary server gumps only: no command a server can send opens one of its own windows or
// highlights a control (Part A item 5). So each station teaches its TazUO features in words, names the key, menu or
// "-command" to use, and links the tazuo.org wiki page in the gump text (an htmlgump link opens only when clicked).
// cc-P103: our patched TazUO also runs a whitelisted client command the server sends (TazUOClientCommand). A station
// whose TazUO features name the World Map, the Journal or the options window gets a button that opens it, for a client
// that answered that handshake; every other client keeps the words only.
// cc-P480: light text on the dark background from TutorialStyle; Rook's menu shows the core only until it is done (Chase
// 10-23); the companion pick shows each pet's picture and what it grows into.
// cc-P453: the account's other characters get Rook's page read-only (no buttons, and a reply from them runs nothing).
// cc-P663: the same page is Tamsin's on the Wakening Isle (guide: the core and the skip, no wings or graduation); Rook's
// adds a button that sends a trainee with core lessons left to the isle (TutorialStart.SendToIsle), and a core lesson
// pressed in his window does the same.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.TazUO;

namespace Server.Tutorial;

/// <summary>The recruiter's page: the core lessons, then (once the core is done) the wings, skip, remedial, graduate.</summary>
public class TutorialRecruiterGump : Gump
{
    public const int BtnSkip = 1;
    public const int BtnRemedial = 2;
    public const int BtnGraduate = 3;
    public const int BtnCompanion = 4;
    public const int BtnToIsle = 5;
    public const int BtnStationBase = 100;

    private const int Width = 620;
    private const int Row = 20;

    private readonly PlayerMobile _pm;

    /// <summary>cc-P663: Tamsin's page on the isle rather than Rook's.</summary>
    public readonly bool Guide;

    /// <summary>cc-P453: another character on the account sees the page with no buttons.</summary>
    public readonly bool ReadOnly;

    /// <summary>cc-P480: the optional lessons show once the core is done, or in remedial training (a skipper chose to).</summary>
    public static bool WingsOpen(TutorialRecord record) =>
        record.State == TutorialState.Remedial || ClusterFTutorial.CoreDone(record);

    public static string WingName(TutorialWing wing) => wing switch
    {
        TutorialWing.Thieving => "Thieving and stealth",
        TutorialWing.ShardSystems => "Shard features",
        TutorialWing.Client => "Client settings",
        _ => wing.ToString()
    };

    private static string StateText(TutorialState state) => state switch
    {
        TutorialState.InCore => "Finish the main lessons in order.",
        TutorialState.InWings => "You finished the main lessons and can graduate. Optional lessons are also open.",
        TutorialState.Remedial => "You restarted the tutorial and can earn its usual rewards.",
        TutorialState.Skipped => "You skipped the tutorial.",
        TutorialState.Graduated => "You finished the tutorial.",
        _ => "You have not started the tutorial."
    };

    public TutorialRecruiterGump(PlayerMobile pm, bool guide = false) : base(60, 40)
    {
        _pm = pm;
        Guide = guide;
        var record = ClusterFTutorial.RecordOf(pm);
        ReadOnly = record is { State: not TutorialState.NotStarted } && !ClusterFTutorial.IsRecordedCharacter(pm, record);
        var active = !ReadOnly && record is { IsActive: true };
        var wings = active && !guide && WingsOpen(record!);
        var toIsle = active && !guide && !ClusterFTutorial.CoreDone(record!);

        // The optional lessons, grouped under their wing, split into two columns at a group boundary.
        var left = new List<(TutorialWing Wing, TutorialStation? Station)>();
        var right = new List<(TutorialWing Wing, TutorialStation? Station)>();
        if (wings)
        {
            var groups = TutorialStations.All.Where(s => !s.IsCore && !TutorialStations.IsAnytime(s.Key)).GroupBy(s => s.Wing).ToList();
            var total = groups.Sum(g => g.Count() + 1);
            foreach (var g in groups)
            {
                var column = left.Count < total / 2 ? left : right;
                column.Add((g.Key, null));
                column.AddRange(g.Select(s => (g.Key, (TutorialStation?)s)));
            }
        }

        var core = TutorialStations.Core.ToList();
        var anytime = TutorialStations.Anytime.ToList(); // cc-P718: optional at any time
        var h = 100;
        if (active)
        {
            h += 24 + core.Count * Row + (wings ? 30 + Math.Max(left.Count, right.Count) * Row : 30) + 50 + (toIsle ? 30 : 0) +
                 (anytime.Count > 0 ? 30 + anytime.Count * Row : 0);
        }
        else if (ReadOnly || record != null && ClusterFTutorial.CompanionPending(record))
        {
            h += 60;
        }

        AddPage(0);
        TutorialStyle.Panel(this, Width, h);
        AddLabel(24, 18, TutorialStyle.HeadingHue, guide ? "Tamsin: the Wakening Isle" : "Rook Hallowell: New Haven tutorial");
        AddLabel(24, 42, TutorialStyle.TextHue, record == null ? "You have not started the tutorial on this account." : StateText(record.State));

        var y = 72;
        if (ReadOnly)
        {
            var name = World.FindMobile((Serial)record!.CharacterSerial)?.Name ?? "another character";
            AddLabel(24, y, TutorialStyle.TextHue, $"This account's tutorial was started by {name}.");
            AddLabel(24, y + 24, TutorialStyle.TextHue, "Use that character to take lessons, skip, graduate, or choose a pet.");
        }
        else if (active)
        {
            AddLabel(24, y, TutorialStyle.HeadingHue, "Main lessons: take these in order");
            y += 24;
            var next = ClusterFTutorial.CoreLeft(record!).FirstOrDefault();
            for (var i = 0; i < core.Count; i++, y += Row)
            {
                var s = core[i];
                var done = record!.StationsDone.Contains(s.Key);
                AddButton(24, y, 4005, 4007, BtnStationBase + Array.IndexOf(TutorialStations.All, s));
                AddLabel(60, y + 2, done ? TutorialStyle.DoneHue : s == next ? TutorialStyle.ActionHue : TutorialStyle.TextHue,
                    $"{i + 1}. {s.Title}{(done ? " (done)" : s == next ? " (next)" : "")}");
            }

            if (anytime.Count > 0)
            {
                AddLabel(24, y + 6, TutorialStyle.HeadingHue, "Optional, at any time (you can skip these)");
                y += 30;
                foreach (var s in anytime)
                {
                    var done = record!.StationsDone.Contains(s.Key);
                    AddButton(24, y, 4005, 4007, BtnStationBase + Array.IndexOf(TutorialStations.All, s));
                    AddLabel(60, y + 2, done ? TutorialStyle.DoneHue : TutorialStyle.TextHue, $"{s.Title}{(done ? " (done)" : "")}");
                    y += Row;
                }
            }

            if (wings)
            {
                AddLabel(24, y + 6, TutorialStyle.HeadingHue, "Optional trade and skill lessons");
                y += 30;
                var top = y;
                DrawColumn(left, 24, top, record!);
                DrawColumn(right, Width / 2 + 10, top, record!);
                y = top + Math.Max(left.Count, right.Count) * Row;
            }
            else
            {
                AddLabel(24, y + 6, TutorialStyle.TextHue, guide
                    ? "The moongate on the dock is your last lesson. Rook in New Haven graduates you and opens trade and skill lessons."
                    : "Finish the main lessons to open trade and skill lessons.");
                y += 30;
            }

            if (toIsle)
            {
                AddButton(24, y, 4005, 4007, BtnToIsle);
                AddLabel(60, y + 2, TutorialStyle.ActionHue, "Go to the Wakening Isle to take your lessons");
                y += 30;
            }

            y += 10;
            if (record!.State != TutorialState.Remedial)
            {
                AddButton(24, y, 4017, 4019, BtnSkip);
                AddLabel(60, y + 2, TutorialStyle.WarnHue, "Skip the tutorial");
            }

            if (!guide && ClusterFTutorial.CoreDone(record))
            {
                AddButton(Width / 2 + 10, y, 4023, 4025, BtnGraduate);
                AddLabel(Width / 2 + 46, y + 2, TutorialStyle.DoneHue, "Graduate");
            }
        }
        else if (record is { State: TutorialState.Skipped })
        {
            AddButton(24, y, 4005, 4007, BtnRemedial);
            AddLabel(60, y + 2, TutorialStyle.ActionHue, "Restart the tutorial and earn its rewards");
        }
        else if (record != null && ClusterFTutorial.CompanionPending(record))
        {
            AddButton(24, y, 4023, 4025, BtnCompanion);
            AddLabel(60, y + 2, TutorialStyle.HeadingHue, "Choose your pet");
            AddLabel(60, y + 24, TutorialStyle.TextHue, "You earned a pet and still need to choose one.");
        }
        else
        {
            AddLabel(24, y, TutorialStyle.TextHue, "You can check your tutorial progress here whenever you return to New Haven.");
        }
    }

    private void DrawColumn(List<(TutorialWing Wing, TutorialStation? Station)> rows, int x, int y, TutorialRecord record)
    {
        foreach (var (wing, s) in rows)
        {
            if (s == null)
            {
                AddLabel(x, y + 2, TutorialStyle.HeadingHue, WingName(wing));
            }
            else
            {
                var done = record.StationsDone.Contains(s.Key);
                AddButton(x, y, 4005, 4007, BtnStationBase + Array.IndexOf(TutorialStations.All, s));
                AddLabel(x + 36, y + 2, done ? TutorialStyle.DoneHue : TutorialStyle.TextHue, $"{s.Title}{(done ? " (done)" : "")}");
            }

            y += Row;
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != _pm || ReadOnly)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case BtnSkip:
                _pm.SendGump(new TutorialSkipGump(1));
                return;
            case BtnRemedial:
                ClusterFTutorial.StartRemedial(_pm); // cc-P663: and the trip to the isle
                return;
            case BtnToIsle:
                if (!Guide && ClusterFTutorial.IsParticipant(_pm) && ClusterFTutorial.RecordOf(_pm) is { } trainee &&
                    !ClusterFTutorial.CoreDone(trainee) && TutorialCoreTrials.Nearby(_pm, TutorialRecruiter.Spot, 4, HavenSpots.Map))
                {
                    TutorialStart.SendToIsle(_pm);
                }

                return;
            case BtnCompanion:
                ClusterFTutorial.OfferCompanion(_pm);
                return;
            case BtnGraduate:
                if (Guide || !TutorialCoreTrials.Nearby(_pm, TutorialRecruiter.Spot, 4, HavenSpots.Map))
                {
                    ClusterFTutorial.SystemLine(_pm, "Follow the arrow back to Rook on the New Haven plaza. Double-click Rook and choose Graduate.");
                    TutorialRecruiter.PointTo(_pm);
                    return;
                }
                if (ClusterFTutorial.Graduate(_pm) is { } why)
                {
                    _pm.SendMessage(0x22, why);
                }

                return;
        }

        var index = info.ButtonID - BtnStationBase;
        if (index < 0 || index >= TutorialStations.All.Length)
        {
            return;
        }

        // A wing's button is not shown before the core is done; a reply naming one anyway runs nothing (staff use [Tutorial).
        var station = TutorialStations.All[index];
        if (!station.IsCore && !TutorialStations.IsAnytime(station.Key) &&
            (ClusterFTutorial.RecordOf(_pm) is not { } record || !WingsOpen(record)))
        {
            return;
        }

        // cc-P663: core lessons are taught on the isle; pressed in Rook's window, one sends the trainee there.
        if (station.IsCore && !Guide && !TutorialWorld.IsOnIsle(_pm) && ClusterFTutorial.IsParticipant(_pm) &&
            ClusterFTutorial.RecordOf(_pm) is { } r && !ClusterFTutorial.CoreDone(r))
        {
            TutorialStart.SendToIsle(_pm);
            return;
        }

        ClusterFTutorial.RunStation(_pm, station);
    }
}

/// <summary>The skip, confirmed twice in the System voice.</summary>
public class TutorialSkipGump : Gump
{
    public const int BtnYes = 1;
    public readonly int Step;

    public static readonly string[] Prompts =
    [
        "Skip the remaining tutorial lessons? You can return to Rook later to take them and earn their rewards.",
        "Skipping earns the Didn't Read the Manual achievement. Skip the tutorial now?"
    ];

    public TutorialSkipGump(int step) : base(160, 160)
    {
        Step = Math.Clamp(step, 1, 2);
        AddPage(0);
        TutorialStyle.Panel(this, 420, 150);
        AddHtml(24, 20, 372, 70, TutorialStyle.Html(Prompts[Step - 1]), false, false);
        AddButton(24, 108, 4023, 4025, BtnYes);
        AddLabel(60, 110, TutorialStyle.WarnHue, Step == 1 ? "Yes, skip" : "Skip now");
        AddButton(240, 108, 4017, 4019, 0);
        AddLabel(276, 110, TutorialStyle.ActionHue, "Keep learning");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile pm || info.ButtonID != BtnYes)
        {
            return;
        }

        if (Step == 1)
        {
            pm.SendGump(new TutorialSkipGump(2));
        }
        else
        {
            ClusterFTutorial.Skip(pm);
        }
    }
}

/// <summary>A station's plan, while its trial is a stub (TODO: the trial prompts).</summary>
public class TutorialStationGump : Gump
{
    public const string WikiBase = "https://tazuo.org/";
    public const int BtnWindowBase = 1;

    /// <summary>A station's TazUO feature, the client command that opens its window, and the button's words (cc-P103).</summary>
    public static readonly (string Feature, string Command, string Label)[] Windows =
    [
        ("World Map", "worldmap", "Open the World Map"),
        ("Journal and filters", "journal", "Open the Journal"),
        ("Options", "options", "Open Options")
    ];

    private readonly TutorialStation _station;

    /// <summary>The commands this gump offers as buttons, in button order (empty for a client that cannot run them).</summary>
    public readonly string[] Offered;

    public TutorialStationGump(TutorialStation s, bool canOpenWindows = false) : base(100, 100)
    {
        _station = s;
        var offered = new List<(string Command, string Label)>();
        if (canOpenWindows)
        {
            foreach (var w in Windows)
            {
                if (Array.IndexOf(s.TazUO, w.Feature) >= 0)
                {
                    offered.Add((w.Command, w.Label));
                }
            }
        }

        Offered = offered.ConvertAll(o => o.Command).ToArray();
        var lines = new List<string>
        {
            $"Go to: {s.Where}.",
            s.HandsOn.Length > 0 ? $"Practice: {string.Join(", ", s.HandsOn)}." : "",
            s.Covered.Length > 0 ? $"Related skills: {string.Join(", ", s.Covered)}." : "",
            s.Explained.Length > 0 ? $"Learn about: {string.Join(", ", s.Explained)}." : "",
            s.TazUO.Length > 0 ? $"TazUO features: {string.Join(", ", s.TazUO)}. Help: <a href=\"{WikiBase}\">tazuo.org</a>" : "",
            s.Tool != null ? $"Lesson supplies: {s.Tool}." : "",
            "This optional lesson has no activity yet. You can graduate after finishing the main lessons."
        };
        lines.RemoveAll(string.IsNullOrEmpty);

        var buttons = offered.Count * 24;
        AddPage(0);
        TutorialStyle.Panel(this, 460, 70 + lines.Count * 36 + buttons);
        AddLabel(24, 18, TutorialStyle.HeadingHue, s.Title);
        for (var i = 0; i < lines.Count; i++)
        {
            AddHtml(24, 44 + i * 36, 412, 34, TutorialStyle.Html(lines[i]), false, false);
        }

        var y = 44 + lines.Count * 36;
        for (var i = 0; i < offered.Count; i++, y += 24)
        {
            AddButton(24, y, 4005, 4007, BtnWindowBase + i);
            AddLabel(60, y + 2, TutorialStyle.ActionHue, offered[i].Label);
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var i = info.ButtonID - BtnWindowBase;
        if (sender.Mobile is not PlayerMobile pm || i < 0 || i >= Offered.Length)
        {
            return;
        }

        TazUOClientCommand.Send(pm, Offered[i]);
        pm.SendGump(new TutorialStationGump(_station, TazUOClientCommand.CanRunCommands(pm.NetState)));
    }
}

/// <summary>
/// Graduation's evo starter pet pick: dog, cat, fox or bird. cc-P480 Part 7 (Chase 10-23: "It was not clear I could pick a
/// pet"): a picture, the name and what it grows into for each, and a Choose button. Rook's menu offers it again until a
/// pet is chosen (ClusterFTutorial.CompanionPending).
/// </summary>
public class TutorialPetPickGump : Gump
{
    // The picture for each line, in StarterPetLines order: the stage-1 body's shrink art (pinned Data/shrink.json: 217 dog,
    // 201 cat, 1423 the fox kit, 254 the crane chick), the fox's tinted orange.
    public static readonly (int ItemId, int Hue, string Text)[] Pictures =
    [
        (0x2588, 0, "A dog companion, shown as a puppy. It grows into a larger hound as you play together."),
        (0x211B, 0, "A cat companion, shown as a kitten. It grows into larger cats as you play together."),
        (0x211C, 0x4EC, "A fox companion, shown as a fox kit. After graduation and enough growth, it can become a bake kitsune, a magical fox."),
        (0x2764, 0, "A bird companion, shown as a chick. It can grow into a firebird; more stages are still being added.")
    ];

    public TutorialPetPickGump() : base(140, 100)
    {
        var lines = ClusterFTutorial.StarterPetLines;
        AddPage(0);
        TutorialStyle.Panel(this, 520, 90 + lines.Length * 70);
        AddLabel(24, 18, TutorialStyle.HeadingHue, "You earned a pet: choose one");
        AddLabel(24, 40, TutorialStyle.TextHue, "Your pet grows as you play and can be brought back after death.");
        for (var i = 0; i < lines.Length; i++)
        {
            var y = 70 + i * 70;
            AddItem(24, y + 4, Pictures[i].ItemId, Pictures[i].Hue);
            AddLabel(100, y, TutorialStyle.HeadingHue, char.ToUpperInvariant(lines[i][0]) + lines[i][1..]);
            AddHtml(100, y + 20, 300, 40, TutorialStyle.Html(Pictures[i].Text), false, false);
            AddButton(410, y + 10, 4005, 4007, 1 + i);
            AddLabel(446, y + 12, TutorialStyle.ActionHue, "Choose");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var i = info.ButtonID - 1;
        if (sender.Mobile is not PlayerMobile pm || i < 0 || i >= ClusterFTutorial.StarterPetLines.Length)
        {
            return;
        }

        if (ClusterFTutorial.ChooseCompanion(pm, ClusterFTutorial.StarterPetLines[i]) == null &&
            ClusterFTutorial.RecordOf(pm) is { } record && ClusterFTutorial.CompanionPending(record))
        {
            pm.SendMessage(0x22, "You cannot control another pet right now. Stable one of your pets with an animal trainer, then ask Rook again.");
        }
    }
}
