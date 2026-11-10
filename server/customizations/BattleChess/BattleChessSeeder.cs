// BattleChessSeeder.cs
//
// cc-P862: puts the Battle Chess board in the world by itself, on a new world and on an existing one: at every world load the
// board in battle-chess.json is placed unless one already stands there. Idempotent; [BattleChess seed runs the same pass.
// [BattleChess status shows the board and any game; [BattleChess end stops a game without a result; reload reads the file.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Commands;

namespace Server.BattleChess;

public static class BattleChessSeeder
{
    public static void Configure()
    {
        foreach (var line in BattleChessSettings.LoadOrWriteDefaults(BattleChessSettings.FilePath))
        {
            Console.WriteLine($"[BattleChess] {line}");
        }

        EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () =>
        {
            if (Seed())
            {
                Console.WriteLine("[BattleChess] placed the board.");
            }
        });

        Register();
    }

    public static void Register()
    {
        if (!CommandSystem.Entries.ContainsKey("BattleChess"))
        {
            CommandSystem.Register("BattleChess", AccessLevel.GameMaster, OnCommand);
        }
    }

    /// <summary>The board standing at the configured spot, if any.</summary>
    public static BattleChessBoard Find()
    {
        var s = BattleChessSettings.Current;
        var map = BattleChessSettings.FacetOf(s.Map);
        if (map == null)
        {
            return null;
        }

        foreach (var item in map.GetItemsAt(s.X, s.Y))
        {
            if (item is BattleChessBoard { Deleted: false } board)
            {
                return board;
            }
        }

        return null;
    }

    /// <summary>Places the board when it is missing; true when it placed one. Nothing when Battle Chess is off.</summary>
    public static bool Seed()
    {
        var s = BattleChessSettings.Current;
        if (!s.Enabled || Find() != null)
        {
            return false;
        }

        var map = BattleChessSettings.FacetOf(s.Map);
        if (map == null)
        {
            return false;
        }

        var board = new BattleChessBoard { Where = s.Where };
        board.MoveToWorld(new Point3D(s.X, s.Y, s.Z), map);
        return true;
    }

    public static List<string> Status()
    {
        var s = BattleChessSettings.Current;
        var board = Find();
        var lines = new List<string>
        {
            board == null
                ? $"No board at {s.X},{s.Y},{s.Z} on {s.Map}{(s.Enabled ? " (run [BattleChess seed)" : " (Battle Chess is off)")}."
                : $"The board stands at {board.Location} on {board.Map} ({board.Where}), set {board.Set}."
        };

        foreach (var g in BattleChessGame.Games)
        {
            lines.Add(g.Phase == GamePhase.Inviting
                ? $"{g.Owner?.Name} is inviting {g.Guest?.Name ?? "someone"}."
                : $"{g.White?.Name} (White) against {g.Black?.Name} (Black): {g.Phase}, {ChessRules.SideName(g.Position.ToMove)} to move, move {g.Position.FullMoves}.");
        }

        lines.Add($"Records kept: {BattleChessRecords.Records.Count} player(s), {BattleChessRecords.Records.Values.Sum(r => r.Games) / 2} game(s).");
        return lines;
    }

    [Usage("BattleChess [status|seed|end|reload]")]
    [Description("Battle Chess (cc-P862): status shows the board and any game; seed places the board if it is missing; end stops every game without a result; reload reads Configuration/battle-chess.json again (a bad file is refused whole).")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.Any, NoDryRun = true,
        Summary = "Shows the Battle Chess board and games; seed places the board if it is missing.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
        List<string> lines;
        switch (verb)
        {
            case "status":
                lines = Status();
                break;
            case "seed":
                lines = [Seed() ? "Placed the board." : "Nothing to place: the board stands there already, or Battle Chess is off."];
                break;
            case "end":
                var n = BattleChessGame.Games.Count;
                foreach (var g in BattleChessGame.Games.ToList())
                {
                    g.Abort("Staff stopped the game. No result was kept.");
                }

                lines = [$"Stopped {n} game(s)."];
                break;
            case "reload":
                lines = BattleChessSettings.Reload(BattleChessSettings.FilePath);
                break;
            default:
                lines = ["Usage: [BattleChess [status|seed|end|reload]"];
                break;
        }

        foreach (var line in lines)
        {
            e.Mobile.SendMessage(line);
        }
    }
}
