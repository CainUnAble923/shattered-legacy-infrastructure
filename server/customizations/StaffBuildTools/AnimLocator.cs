// AnimLocator.cs
//
// cc-P812 (P798 rank 2): the animation locator, a staff command. Pick one of our bodies (ShardBodyTypes.Entries: the client
// bodies our creatures use and the ones that are ours alone) and a stand of that body appears beside you, frozen and blessed,
// and plays each action in turn in the real client, saying its action number over its head. So a builder sees what each
// action of a body does without reading .vd files.
//
//   [AnimLocate                 a gump of our bodies; pick one
//   [AnimLocate <body> [from]   that body, from action <from> (default 0) to the last its body type has
//   [AnimLocate target [from]   play the actions on a mobile you target instead (nothing is made)
//   [AnimLocate stop            stop, and the stand goes
//
// How many actions a body type has, the pace and the frame count are in Configuration/staff-build-tools.json. The stand is
// never left behind: it goes when the run ends or stops, and one left in a save deletes itself after load. Staff only
// (GameMaster). After Father Time's Freeze Frame and zerodowned's Animation Locator (third-party, reference only): the idea
// of stepping actions on a mobile live. No code was taken.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server;

public sealed class AnimRun
{
    public Mobile Staff;
    public Mobile Subject;
    public bool MadeStand;
    public int Body;
    public int Next;
    public int Last;
    public TimerExecutionToken Timer;
}

public static class AnimLocator
{
    private static StaffBuildToolsSettings S => StaffBuildToolsSettings.Current;

    public static readonly Dictionary<Mobile, AnimRun> Runs = new();

    public static void Configure() => Register();

    public static void Register()
    {
        if (!CommandSystem.Entries.ContainsKey("AnimLocate"))
        {
            CommandSystem.Register("AnimLocate", AccessLevel.GameMaster, OnCommand);
        }
    }

    /// <summary>How many actions a body has to step through, by its type.</summary>
    public static int ActionCount(int body) => S.ActionsFor(new Body(body).Type);

    public static string NameOf(int body)
    {
        foreach (var e in ShardBodyTypes.Entries)
        {
            if (e.Body == body)
            {
                return e.What;
            }
        }

        return $"body {body}";
    }

    /// <summary>Starts a run on a new stand of <paramref name="body"/> beside the staff member.</summary>
    public static AnimRun Start(Mobile staff, int body, int from = 0)
    {
        if (body is <= 0 or > 0xFFFF)
        {
            staff.SendMessage(0x22, "That is not a body number.");
            return null;
        }

        var stand = new AnimLocatorStand(body);
        var d = S.StandDistance;
        stand.MoveToWorld(new Point3D(staff.X + d, staff.Y + d, staff.Z), staff.Map);
        stand.Direction = Direction.North | Direction.Running;
        return Begin(staff, stand, true, from);
    }

    /// <summary>Starts a run on a mobile already in the world.</summary>
    public static AnimRun Begin(Mobile staff, Mobile subject, bool madeStand, int from = 0)
    {
        Stop(staff);
        var last = ActionCount(subject.Body) - 1;
        var run = new AnimRun
        {
            Staff = staff, Subject = subject, MadeStand = madeStand, Body = subject.Body,
            Next = Math.Clamp(from, 0, last), Last = last
        };
        Runs[staff] = run;

        staff.SendMessage(0x43, $"Animation locator: {NameOf(run.Body)} (body {run.Body}, {new Body(run.Body).Type}), actions {run.Next} to {run.Last}, one every {S.SecondsPerAction} s. [AnimLocate stop ends it.");
        Step(run);
        Timer.StartTimer(TimeSpan.FromSeconds(S.SecondsPerAction), TimeSpan.FromSeconds(S.SecondsPerAction), () => Step(run), out run.Timer);
        return run;
    }

    /// <summary>Plays the run's next action and says its number; ends the run after the last. Returns the action played, or -1.</summary>
    public static int Step(AnimRun run)
    {
        if (run.Subject is not { Deleted: false } || run.Next > run.Last || !Runs.TryGetValue(run.Staff, out var cur) || cur != run)
        {
            Finish(run);
            return -1;
        }

        var action = run.Next++;
        run.Subject.Animate(action, S.FrameCount, 1, true, false, 0);
        run.Subject.PublicOverheadMessage(MessageType.Regular, 0x3B2, false, $"action {action}");
        run.Staff.SendMessage(0x3B2, $"Body {run.Body}: action {action} of {run.Last}.");

        // After the last action the next tick finds nothing left and ends the run, so the last one plays out in full.
        return action;
    }

    public static void Finish(AnimRun run)
    {
        run.Timer.Cancel();
        if (Runs.TryGetValue(run.Staff, out var cur) && cur == run)
        {
            Runs.Remove(run.Staff);
            run.Staff.SendMessage(0x43, $"Animation locator: body {run.Body} done.");
        }

        if (run.MadeStand)
        {
            run.Subject?.Delete();
        }
    }

    public static bool Stop(Mobile staff)
    {
        if (!Runs.Remove(staff, out var run))
        {
            return false;
        }

        run.Timer.Cancel();
        if (run.MadeStand)
        {
            run.Subject?.Delete();
        }

        return true;
    }

    [Usage("AnimLocate [<body> [from] | target [from] | stop]")]
    [Description("cc-P812: the animation locator. With no word, a gump of our bodies; with a body number, a frozen stand of that body beside you plays each action in turn with its number over its head; target plays them on a mobile you pick; stop ends it. Numbers in Configuration/staff-build-tools.json.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.Any, Summary = "Plays each action of one of our bodies, with its number, so builders see what each does.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var word = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";
        var start = e.Length > 1 ? e.GetInt32(1) : 0;

        if (word == "")
        {
            from.SendGump(new AnimLocatorGump(0));
        }
        else if (word == "stop")
        {
            from.SendMessage(Stop(from) ? "Animation locator stopped." : "No animation locator is running.");
        }
        else if (word == "target")
        {
            from.SendMessage("Target the mobile whose actions to play.");
            from.BeginTarget(-1, false, TargetFlags.None, (f, t) =>
            {
                if (t is Mobile m)
                {
                    Begin(f, m, false, start);
                }
            });
        }
        else if (int.TryParse(word, out var body) || word.StartsWith("0x") && int.TryParse(word[2..], System.Globalization.NumberStyles.HexNumber, null, out body))
        {
            Start(from, body, start);
        }
        else
        {
            from.SendMessage(0x22, "Usage: [AnimLocate [<body> [from] | target [from] | stop]");
        }
    }
}

/// <summary>Our bodies, a page at a time; a button plays one.</summary>
public class AnimLocatorGump : Gump
{
    public const int PerPage = 14;
    public const int BtnPrev = 1;
    public const int BtnNext = 2;
    public const int BtnBodyBase = 100;

    private readonly int _page;

    public AnimLocatorGump(int page) : base(60, 60)
    {
        var entries = ShardBodyTypes.Entries;
        var pages = Math.Max(1, (entries.Length + PerPage - 1) / PerPage);
        _page = Math.Clamp(page, 0, pages - 1);

        AddBackground(0, 0, 460, 90 + PerPage * 22, 9270);
        AddImageTiled(14, 14, 432, 62 + PerPage * 22, 2624);
        AddLabel(26, 22, 0x386, $"Animation locator: our bodies (page {_page + 1} of {pages})");

        for (var i = 0; i < PerPage && _page * PerPage + i < entries.Length; i++)
        {
            var (body, type, what) = entries[_page * PerPage + i];
            var y = 50 + i * 22;
            AddButton(26, y, 4005, 4007, BtnBodyBase + _page * PerPage + i);
            AddLabel(60, y + 2, 1153, body.ToString());
            AddLabelCropped(110, y + 2, 320, 18, 0x3B2, $"{type}: {what}");
        }

        if (_page > 0)
        {
            AddButton(26, 56 + PerPage * 22, 4014, 4016, BtnPrev);
        }

        if (_page < pages - 1)
        {
            AddButton(400, 56 + PerPage * 22, 4005, 4007, BtnNext);
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is { } m)
        {
            Respond(m, info.ButtonID);
        }
    }

    public void Respond(Mobile from, int id)
    {
        if (from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        if (id == BtnPrev || id == BtnNext)
        {
            from.SendGump(new AnimLocatorGump(_page + (id == BtnNext ? 1 : -1)));
        }
        else if (id >= BtnBodyBase && id - BtnBodyBase < ShardBodyTypes.Entries.Length)
        {
            AnimLocator.Start(from, ShardBodyTypes.Entries[id - BtnBodyBase].Body);
        }
    }
}

/// <summary>The frozen, blessed stand the locator plays on. Never kept: one left in a save deletes itself after load.</summary>
[SerializationGenerator(0, false)]
public partial class AnimLocatorStand : BaseCreature
{
    [Constructible]
    public AnimLocatorStand() : this(400)
    {
    }

    public AnimLocatorStand(int body) : base(AIType.AI_Animal, FightMode.None)
    {
        Body = body;
        Blessed = true;
        Frozen = true;
        CantWalk = true;
        SetStr(10);
        SetHits(10);
    }

    public override string DefaultName => $"body {Body}";

    public override bool CanBeRenamedBy(Mobile from) => false;

    [AfterDeserialization]
    private void AfterDeserialization() => Timer.DelayCall(TimeSpan.Zero, Delete);
}
