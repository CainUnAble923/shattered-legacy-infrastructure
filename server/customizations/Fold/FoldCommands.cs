// FoldCommands.cs
//
// cc-P661 (P481 F1): staff travel onto the Fold (FoldMaps.cs). Test shard only to start, as the prompt sets: the isle is
// not built yet (F2) and the copies are not handed out (F4).
//
//   [Fold goto <n> [x y]     n 0 is the Fold (map 32), 1 to 16 the tutorial copies (maps 33 to 48). Without x y, the
//                            isle's arrival beach (design 11.2, stop 0). Z is the land's average there, as pinned's
//                            [Go x y picks it (Handlers.cs:537).

using Server.Commands;
using Server.Misc;

namespace Server;

public static class FoldCommands
{
    /// <summary>The Wakening Isle's west beach, where a trainee wakes (design 11.2, stop 0; cc-P675: site A, TutorialWorld.Beach).</summary>
    public static Point2D Arrival => Server.Tutorial.TutorialWorld.Beach;

    public const string LiveRefusal = "[Fold works on the test shard only, for now.";
    public const string Usage = "Usage: [Fold goto <n> [x y]. n 0 is the Fold (map 32), 1 to 16 a tutorial copy (maps 33 to 48).";

    public static bool IsTestShard => TestCenter.Enabled;

    public static void Configure()
    {
        CommandSystem.Register("Fold", AccessLevel.GameMaster, OnFold);
    }

    [Usage("Fold goto <n> [x y]")]
    [Description("Test shard only. Moves you onto the Fold: n 0 is the Fold itself (map 32), 1 to 16 a tutorial copy (maps 33 to 48). Without x y, the isle's arrival beach (6638, 3993); Z is the land's height there.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.TestOnly, Summary = "Staff travel onto the Fold (0) or one of its 16 tutorial copies, at the arrival beach or at x y.")]
    private static void OnFold(CommandEventArgs e) => e.Mobile.SendMessage(Goto(e.Mobile, e.Arguments, IsTestShard));

    /// <summary>Runs [Fold with <paramref name="args"/> for <paramref name="from"/>; returns the line to show.</summary>
    public static string Goto(Mobile from, string[] args, bool testShard)
    {
        if (!testShard)
        {
            return LiveRefusal;
        }

        if (args.Length is not (2 or 4) || !args[0].InsensitiveEquals("goto") || !int.TryParse(args[1], out var n))
        {
            return Usage;
        }

        var map = FoldMaps.Copy(n);
        if (map == null)
        {
            return Usage;
        }

        var x = Arrival.X;
        var y = Arrival.Y;
        if (args.Length == 4 && (!int.TryParse(args[2], out x) || !int.TryParse(args[3], out y)))
        {
            return Usage;
        }

        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
        {
            return $"{x}, {y} is outside {map.Name} ({map.Width} x {map.Height}).";
        }

        var to = new Point3D(x, y, map.GetAverageZ(x, y));
        from.MoveToWorld(to, map);
        return $"You are on {map.Name} (map {map.MapIndex}) at {to}.";
    }
}
