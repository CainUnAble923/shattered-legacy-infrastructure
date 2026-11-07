// StaffEventsGump.cs
//
// cc-P810: the egg hunt and the mine maze, opened from the Staff Hub's Events tab ("Egg hunt, maze") or [StaffEvents.
// GameMaster and up, as the hub. What each event does is StaffEggHunt's and StaffMineMaze's; this file draws and
// dispatches, and holds the command.

using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Gumps;
using Server.Network;
using Server.Targeting;

namespace Server;

public class StaffEventsGump : Gump
{
    public const int BtnEggHere = 1;
    public const int BtnEggTarget = 2;
    public const int BtnEggEnd = 3;
    public const int BtnCorner1 = 10;
    public const int BtnCorner2 = 11;
    public const int BtnCornerTarget = 12;
    public const int BtnMazeStart = 13;
    public const int BtnMazeReset = 14;
    public const int BtnMazeEnd = 15;
    public const int BtnBack = 20;

    public static void Configure()
    {
        foreach (var line in StaffEventsSettings.LoadOrWriteDefaults(StaffEventsSettings.FilePath))
        {
            Console.WriteLine($"[StaffEvents] {line}");
        }

        Register();
    }

    /// <summary>Registers [StaffEvents; idempotent, and the facts call it without the settings file.</summary>
    public static void Register()
    {
        if (!CommandSystem.Entries.ContainsKey("StaffEvents"))
        {
            CommandSystem.Register("StaffEvents", AccessLevel.GameMaster, OnCommand);
        }
    }

    public StaffEventsGump(Mobile gm) : base(60, 60)
    {
        const int w = 520, h = 330;
        AddBackground(0, 0, w, h, 9270);
        AddImageTiled(16, 16, w - 32, h - 32, 2624);
        AddLabel(26, 22, StaffHubGump.HueGold, "Staff events: egg hunt and mine maze");

        // Egg hunt.
        AddLabel(26, 50, StaffHubGump.HueGold, "Egg hunt");
        var y = 72;
        foreach (var line in StaffEggHunt.Status())
        {
            AddLabelCropped(26, y, 470, 18, StaffEggHunt.Running ? StaffHubGump.HueGreen : StaffHubGump.HueWhite, line);
            y += 18;
        }

        Button(26, 112, BtnEggHere, "Hide egg here", !StaffEggHunt.Running);
        Button(190, 112, BtnEggTarget, "Hide egg: target", !StaffEggHunt.Running);
        Button(354, 112, BtnEggEnd, "End hunt", StaffEggHunt.Running);

        // Mine maze.
        AddLabel(26, 150, StaffHubGump.HueGold, "Mine maze");
        y = 172;
        foreach (var line in StaffMineMaze.Status())
        {
            AddLabelCropped(26, y, 470, 18, StaffMineMaze.Active != null ? StaffHubGump.HueGreen : StaffHubGump.HueWhite, line);
            y += 18;
        }

        var c = StaffMineMaze.CornersOf(gm);
        AddLabelCropped(26, 210, 470, 18, StaffHubGump.HueGrey,
            c == null ? "No corners marked. The start row is at the first corner's end."
                : $"Corners: {c.Value.First.X},{c.Value.First.Y} and {(c.Value.Second is { } s ? $"{s.X},{s.Y}" : "-")} in {c.Value.Map.Name}");

        var laid = StaffMineMaze.Active != null;
        Button(26, 234, BtnCorner1, "First corner", !laid);
        Button(190, 234, BtnCorner2, "Second corner", !laid);
        Button(354, 234, BtnCornerTarget, "Corners: target", !laid);
        Button(26, 262, BtnMazeStart, "Lay the maze", !laid && c?.Second != null);
        Button(190, 262, BtnMazeReset, "Reset: mines move", laid);
        Button(354, 262, BtnMazeEnd, "Remove maze", laid);

        Button(26, 294, BtnBack, "Staff Hub", true);
    }

    private void Button(int x, int y, int id, string text, bool enabled)
    {
        if (enabled)
        {
            AddButton(x, y, 4005, 4007, id);
        }

        AddLabelCropped(x + 34, y + 2, 126, 18, enabled ? StaffHubGump.HueWhite : StaffHubGump.HueGrey, text);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is { } from)
        {
            Respond(from, info.ButtonID);
        }
    }

    /// <summary>A button press, without a client packet; public for the facts. Reopens the gump where it should.</summary>
    public static void Respond(Mobile from, int id)
    {
        if (from.AccessLevel < AccessLevel.GameMaster || id == 0)
        {
            return;
        }

        string message;
        var reopen = true;

        switch (id)
        {
            case BtnEggHere:
                Say(from, StaffEggHunt.Start(from, from.Map, from.Location, out message), message);
                break;
            case BtnEggTarget:
                from.SendMessage("Target where the egg hides.");
                from.BeginTarget(-1, true, TargetFlags.None, (f, t) =>
                {
                    if (t is IPoint3D p)
                    {
                        Say(f, StaffEggHunt.Start(f, f.Map, new Point3D(p), out var m), m);
                    }

                    f.SendGump(new StaffEventsGump(f));
                });
                reopen = false;
                break;
            case BtnEggEnd:
                Say(from, StaffEggHunt.End(from, out message), message);
                break;
            case BtnCorner1:
                StaffMineMaze.MarkCorner(from, from.Map, from.Location, true);
                break;
            case BtnCorner2:
                StaffMineMaze.MarkCorner(from, from.Map, from.Location, false);
                break;
            case BtnCornerTarget:
                from.SendMessage("Target the first corner (the start row's end), then the second.");
                from.BeginTarget(-1, true, TargetFlags.None, (f, t1) =>
                {
                    if (t1 is not IPoint3D p1)
                    {
                        return;
                    }

                    StaffMineMaze.MarkCorner(f, f.Map, new Point3D(p1), true);
                    f.BeginTarget(-1, true, TargetFlags.None, (g, t2) =>
                    {
                        if (t2 is IPoint3D p2)
                        {
                            StaffMineMaze.MarkCorner(g, g.Map, new Point3D(p2), false);
                        }

                        g.SendGump(new StaffEventsGump(g));
                    });
                });
                reopen = false;
                break;
            case BtnMazeStart:
                Say(from, StaffMineMaze.Start(from, out message), message);
                break;
            case BtnMazeReset:
                Say(from, StaffMineMaze.Reset(out message), message);
                break;
            case BtnMazeEnd:
                Say(from, StaffMineMaze.End(out message), message);
                break;
            case BtnBack:
                ClusterFStaffHub.Open(from);
                reopen = false;
                break;
        }

        if (reopen)
        {
            from.SendGump(new StaffEventsGump(from));
        }
    }

    private static void Say(Mobile from, bool ok, string message) =>
        from.SendMessage(ok ? StaffHubGump.HueGreen : StaffHubGump.HueRed, message);

    [Usage("StaffEvents [status|reload|egg here|egg end|maze corner1|maze corner2|maze lay|maze reset|maze end|maze path]")]
    [Description("Staff events (cc-P810): with no word, opens the egg hunt and mine maze gump. status lists both; reload reads Configuration/staff-events.json again (a bad file is refused whole); egg here hides the egg where you stand, egg end calls the hunt off; maze corner1 and corner2 mark the corners where you stand (the start row is the first corner's end), maze lay lays it, maze reset moves the mines, maze end removes it, maze path lists the safe path for staff.")]
    [ShardCommand(CommandCategory.Grant, Shard = CommandShard.Any, Summary = "Runs the egg hunt and mine maze events.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var words = string.Join(" ", e.Arguments).Trim().ToLowerInvariant();
        string message;
        var ok = true;
        List<string> lines = null;

        switch (words)
        {
            case "":
                from.SendGump(new StaffEventsGump(from));
                return;
            case "status":
                lines = [..StaffEggHunt.Status(), ..StaffMineMaze.Status()];
                break;
            case "reload":
                lines = StaffEventsSettings.Reload(StaffEventsSettings.FilePath);
                break;
            case "egg here":
                ok = StaffEggHunt.Start(from, from.Map, from.Location, out message);
                lines = [message];
                break;
            case "egg end":
                ok = StaffEggHunt.End(from, out message);
                lines = [message];
                break;
            case "maze corner1":
            case "maze corner2":
                StaffMineMaze.MarkCorner(from, from.Map, from.Location, words.EndsWith('1'));
                lines = [$"Corner marked at {from.X}, {from.Y}."];
                break;
            case "maze lay":
                ok = StaffMineMaze.Start(from, out message);
                lines = [message];
                break;
            case "maze reset":
                ok = StaffMineMaze.Reset(out message);
                lines = [message];
                break;
            case "maze end":
                ok = StaffMineMaze.End(out message);
                lines = [message];
                break;
            case "maze path":
                lines = [StaffMineMaze.PathLine()];
                break;
            default:
                ok = false;
                lines = ["Usage: [StaffEvents [status|reload|egg here|egg end|maze corner1|maze corner2|maze lay|maze reset|maze end|maze path]"];
                break;
        }

        foreach (var line in lines)
        {
            from.SendMessage(ok ? StaffHubGump.HueWhite : StaffHubGump.HueRed, line);
        }
    }
}
