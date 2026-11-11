// StaffColorWarsGump.cs
//
// cc-P863: the GM's Color Wars gump, opened from the Staff Hub's Events tab ("Color Wars") or [ColorWars. GameMaster and
// up, as the hub. What the event does is StaffColorWars'; this file draws and dispatches, and holds the command.
// Order of play: mark two arena corners, open it for 2 or 4 teams, place the gate where players gather, start the
// battle when the teams are in, then end it (the team with the most kills wins) or call it off.

using System.Collections.Generic;
using Server.Commands;
using Server.Gumps;
using Server.Network;
using Server.Targeting;

namespace Server;

public class StaffColorWarsGump : Gump
{
    public const int BtnCorner1 = 1;
    public const int BtnCorner2 = 2;
    public const int BtnCornerTarget = 3;
    public const int BtnOpen2 = 4;
    public const int BtnOpen4 = 5;
    public const int BtnGateHere = 6;
    public const int BtnStart = 7;
    public const int BtnEnd = 8;
    public const int BtnCancel = 9;
    public const int BtnBack = 20;

    public static void Configure() => Register();

    /// <summary>Registers [ColorWars; idempotent, and the facts call it.</summary>
    public static void Register()
    {
        if (!CommandSystem.Entries.ContainsKey("ColorWars"))
        {
            CommandSystem.Register("ColorWars", AccessLevel.GameMaster, OnCommand);
        }
    }

    public StaffColorWarsGump(Mobile gm) : base(60, 60)
    {
        const int w = 520, h = 300;
        AddBackground(0, 0, w, h, 9270);
        AddImageTiled(16, 16, w - 32, h - 32, 2624);
        AddLabel(26, 22, StaffHubGump.HueGold, "Color Wars: a team battle (after Omni's Color Wars)");

        var open = StaffColorWars.Active != null;
        var y = 50;
        foreach (var line in StaffColorWars.Status())
        {
            AddLabelCropped(26, y, 470, 18, open ? StaffHubGump.HueGreen : StaffHubGump.HueWhite, line);
            y += 18;
        }

        var c = StaffColorWars.CornersOf(gm);
        AddLabelCropped(26, 92, 470, 18, StaffHubGump.HueGrey,
            c == null ? "No arena corners marked."
                : $"Arena corners: {c.Value.First.X},{c.Value.First.Y} and {(c.Value.Second is { } s ? $"{s.X},{s.Y}" : "-")} in {c.Value.Map.Name}");

        AddLabel(26, 116, StaffHubGump.HueGold, "1. The arena");
        Button(26, 138, BtnCorner1, "First corner", !open);
        Button(190, 138, BtnCorner2, "Second corner", !open);
        Button(354, 138, BtnCornerTarget, "Corners: target", !open);
        Button(26, 166, BtnOpen2, "Open: 2 teams", !open && c?.Second != null);
        Button(190, 166, BtnOpen4, "Open: 4 teams", !open && c?.Second != null);

        AddLabel(26, 196, StaffHubGump.HueGold, "2. The gate, the battle, the end");
        var fighting = StaffColorWars.Active?.Phase == ColorWarsPhase.Fighting;
        Button(26, 218, BtnGateHere, "Gate here", open);
        Button(190, 218, BtnStart, "Start the battle", open && !fighting);
        Button(354, 218, BtnEnd, "End: most kills win", open);
        Button(26, 246, BtnCancel, "Call it off", open);
        Button(354, 246, BtnBack, "Staff Hub", true);
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
            case BtnCorner1:
                StaffColorWars.MarkCorner(from, from.Map, from.Location, true);
                break;
            case BtnCorner2:
                StaffColorWars.MarkCorner(from, from.Map, from.Location, false);
                break;
            case BtnCornerTarget:
                from.SendMessage("Target the arena's first corner, then the second.");
                from.BeginTarget(-1, true, TargetFlags.None, (f, t1) =>
                {
                    if (t1 is not IPoint3D p1)
                    {
                        return;
                    }

                    StaffColorWars.MarkCorner(f, f.Map, new Point3D(p1), true);
                    f.BeginTarget(-1, true, TargetFlags.None, (g, t2) =>
                    {
                        if (t2 is IPoint3D p2)
                        {
                            StaffColorWars.MarkCorner(g, g.Map, new Point3D(p2), false);
                        }

                        g.SendGump(new StaffColorWarsGump(g));
                    });
                });
                reopen = false;
                break;
            case BtnOpen2:
            case BtnOpen4:
                Say(from, StaffColorWars.Open(from, id == BtnOpen2 ? 2 : 4, out message), message);
                break;
            case BtnGateHere:
                Say(from, StaffColorWars.PlaceGate(from.Map, from.Location, out message), message);
                break;
            case BtnStart:
                Say(from, StaffColorWars.StartBattle(out message), message);
                break;
            case BtnEnd:
                Say(from, StaffColorWars.End(true, out message), message);
                break;
            case BtnCancel:
                Say(from, StaffColorWars.End(false, out message), message);
                break;
            case BtnBack:
                ClusterFStaffHub.Open(from);
                reopen = false;
                break;
        }

        if (reopen)
        {
            from.SendGump(new StaffColorWarsGump(from));
        }
    }

    private static void Say(Mobile from, bool ok, string message) =>
        from.SendMessage(ok ? StaffHubGump.HueGreen : StaffHubGump.HueRed, message);

    [Usage("ColorWars [status|corner1|corner2|open 2|open 4|gate|start|end|cancel]")]
    [Description("Color Wars (cc-P863, after Omni's Color Wars): with no word, opens the Color Wars gump. status shows the arena, teams and kills; corner1 and corner2 mark the arena's corners where you stand; open 2 or open 4 opens it for that many teams; gate puts the join gate where you stand; start begins the battle; end pays the team with the most kills and sends everyone home; cancel ends it with no prize.")]
    [ShardCommand(CommandCategory.Grant, Shard = CommandShard.Any, Summary = "Runs Color Wars, the staff team battle event.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var words = string.Join(" ", e.Arguments).Trim().ToLowerInvariant();
        string message;
        var ok = true;
        List<string> lines;

        switch (words)
        {
            case "":
                from.SendGump(new StaffColorWarsGump(from));
                return;
            case "status":
                lines = StaffColorWars.Status();
                break;
            case "corner1":
            case "corner2":
                StaffColorWars.MarkCorner(from, from.Map, from.Location, words.EndsWith('1'));
                lines = [$"Arena corner marked at {from.X}, {from.Y}."];
                break;
            case "open 2":
            case "open 4":
                ok = StaffColorWars.Open(from, words.EndsWith('2') ? 2 : 4, out message);
                lines = [message];
                break;
            case "gate":
                ok = StaffColorWars.PlaceGate(from.Map, from.Location, out message);
                lines = [message];
                break;
            case "start":
                ok = StaffColorWars.StartBattle(out message);
                lines = [message];
                break;
            case "end":
                ok = StaffColorWars.End(true, out message);
                lines = [message];
                break;
            case "cancel":
                ok = StaffColorWars.End(false, out message);
                lines = [message];
                break;
            default:
                ok = false;
                lines = ["Usage: [ColorWars [status|corner1|corner2|open 2|open 4|gate|start|end|cancel]"];
                break;
        }

        foreach (var line in lines)
        {
            from.SendMessage(ok ? StaffHubGump.HueWhite : StaffHubGump.HueRed, line);
        }
    }
}
