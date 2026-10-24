// ServUO: Services/Expansions/TimeOfLegends.cs, DecorateTOL_OnCommand (cc-P198), the parts this shard can place:
//
//   1. The Valley of Eodon public moongate at Ter Mur 719,1863,40 (ServUO :75-76). Pinned ModernUO lists the
//      destination (PublicMoongate.cs:268-276, TerMurEodon, shown to TOL clients) but nothing in pinned places a gate
//      there: [MoonGen generates Trammel, Felucca, Ilshenar, Malas and Tokuno only (PublicMoongate.cs:105-109) and
//      pinned has no [DecorateTOL. Our ClusterFRoyalCitySeeder places the Royal City gate and no other. So the ring
//      of dirt at 719,1863 had a destination and no gate on every world, live and test; no command had been
//      skipped.
//   2. The two climbing vines (ClimbingVine.Initialize in ServUO, which re-adds them at every startup; see that file).
//   3. The Eodon spawners (EodonSpawns): ServUO's "[XmlLoad Spawns/Eodon.xml" (:79).
//
// Not placed, each in the notes: the Ter Mur and Felucca TOL decoration files (Decorate "tol"; pinned ships no
// Data/Decoration/TimeOfLegends), the two Dragon Turtle champion spawns (pinned's ChampionSpawnType.DragonTurtle is
// commented out, ChampionSpawnInfo.cs:19 and :123), Shadowguard and the Myrmidex quest (out of scope), and the macaw
// spawner (Quests/Eodon, not this row).
//
// Everything placed is tagged into the "eodon" collection and [DeleteEodon takes it down; running the generator
// again deletes first, so it replaces rather than doubles. The gate is skipped, and said so, when a public moongate
// that is not ours already stands within a tile of 719,1863 (a hand-placed one on live would otherwise be doubled).
// "[GenEodon dryrun" changes nothing and reports what a run would do, every public moongate already on Ter Mur
// among it.

using System.Collections.Generic;
using System.Linq;
using Server.Commands;
using Server.Items;

namespace Server.Engines.Eodon;

public static class EodonGeneration
{
    public const string CollectionKey = "eodon";

    public static readonly Point3D MoongateLocation = new(719, 1863, 40);

    // ServUO ClimbingVine.Initialize: (where it stands, where it climbs to).
    public static readonly (Point3D At, Point3D ClimbTo)[] Vines =
    {
        (new Point3D(678, 1759, 40), new Point3D(679, 1757, 100)),
        (new Point3D(678, 1759, 60), new Point3D(679, 1757, 100))
    };

    public static void Initialize()
    {
        CommandSystem.Register("GenEodon", AccessLevel.Administrator, Generate_OnCommand);
        CommandSystem.Register("DeleteEodon", AccessLevel.Administrator, Delete_OnCommand);

        // The same work on the server console (the test shard runs with a tty), so it can be run and read back from
        // the log without a client, as ClusterFHalloweenDecor's console verb does (cc-P267). Console commands run on a
        // background thread, so the work is posted to the game loop as pinned's ConsoleCommands.cs does for "save".
        ConsoleInputHandler.RegisterCommand(
            ["eodon"],
            "Valley of Eodon: eodon status|dryrun|generate|delete",
            args => Core.LoopContext.Post(() =>
            {
                foreach (var line in Console_Run((args ?? "").Trim().ToLowerInvariant()))
                {
                    System.Console.WriteLine($"[Eodon] {line}");
                }
            })
        );

    }

    public static void Configure()
    {
        // One line at every start, so a log shows whether this world has had [GenEodon. In Configure: the world loads
        // after Configure and before Initialize, so a handler added in Initialize never sees the load.
        EventSink.WorldLoad += () =>
        {
            foreach (var line in Status())
            {
                System.Console.WriteLine($"[Eodon] {line}");
            }
        };
    }

    private static List<string> Console_Run(string verb) =>
        verb switch
        {
            "" or "status" => Status(),
            "dryrun"       => Run(dryRun: true),
            "generate"     => Run(dryRun: false),
            "delete"       => new List<string> { $"removed {Delete()} objects GenEodon placed." },
            _              => new List<string> { "usage: eodon status|dryrun|generate|delete" }
        };

    /// <summary>What this world holds: the objects GenEodon placed, and whether the Eodon gate stands.</summary>
    public static List<string> Status()
    {
        var gate = World.Items.Values.OfType<PublicMoongate>()
            .FirstOrDefault(g => !g.Deleted && g.Map == Map.TerMur && g.GetDistanceToSqrt(MoongateLocation) <= 1.5);

        return new List<string>
        {
            $"{PlacedCount()} objects placed by GenEodon; Eodon moongate at {MoongateLocation}: " +
            (gate == null ? "MISSING (run [GenEodon)." : $"present ({(IsOurs(gate) ? "GenEodon's" : "not GenEodon's")}).")
        };
    }

    [Usage("GenEodon [dryrun]")]
    [Description("Places the Valley of Eodon moongate, the two climbing vines and the Eodon spawners on Ter Mur.")]
    [ShardCommand(CommandCategory.WorldGeneration, Rerun = CommandRerun.Replaces, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Valley of Eodon on Ter Mur: the public moongate at 719,1863,40 (skipped if another gate is there), the two climbing vines to the Barako clifftop, and the 46 Eodon spawners (dinosaurs, tribes, wildlife). Deletes its earlier placement first, so it can run again.")]
    public static void Generate_OnCommand(CommandEventArgs e)
    {
        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

        if (mode is not ("" or "dryrun"))
        {
            e.Mobile.SendMessage("Usage: [GenEodon [dryrun]");
            return;
        }

        foreach (var line in Run(dryRun: mode == "dryrun"))
        {
            e.Mobile.SendMessage(line);
        }
    }

    [Usage("DeleteEodon")]
    [Description("Deletes everything GenEodon placed.")]
    [ShardCommand(CommandCategory.WorldRemoval, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.TestFirst, NoDryRun = true, Summary = "DELETES everything GenEodon placed (the Eodon moongate, the two vines and the Eodon spawners with their spawn).")]
    private static void Delete_OnCommand(CommandEventArgs e)
    {
        var deleted = Delete();
        e.Mobile.SendMessage($"Eodon generation removed ({deleted} objects).");
    }

    public static int Delete() => WeakEntityCollection.Delete(CollectionKey);

    public static int PlacedCount() =>
        WeakEntityCollection.HasCollection(CollectionKey)
            ? WeakEntityCollection.GetCollection(CollectionKey).Count(ent => ent is { Deleted: false })
            : 0;

    private static bool IsOurs(IEntity entity) =>
        WeakEntityCollection.HasCollection(CollectionKey) &&
        WeakEntityCollection.GetCollection(CollectionKey).Contains(entity);

    /// <summary>Public moongates on Ter Mur that this generator did not place.</summary>
    public static List<PublicMoongate> OtherTerMurGates() =>
        World.Items.Values.OfType<PublicMoongate>()
            .Where(g => !g.Deleted && g.Map == Map.TerMur && !IsOurs(g))
            .ToList();

    /// <summary>[DecorateTOL's moongate, vines and spawn load, as this shard can place them. Returns report lines.</summary>
    public static List<string> Run(bool dryRun)
    {
        var lines = new List<string>();
        var others = OtherTerMurGates();

        foreach (var g in others)
        {
            lines.Add($"Public moongate already on Ter Mur at {g.Location} (not placed by GenEodon).");
        }

        var blocked = others.Any(g => g.GetDistanceToSqrt(MoongateLocation) <= 1.5);
        var earlier = PlacedCount();

        if (dryRun)
        {
            lines.Add($"Dry run, nothing changed. A run would delete {earlier} objects of an earlier GenEodon, then place:");
            lines.Add(
                blocked
                    ? $"  no Eodon moongate: another public moongate stands within a tile of {MoongateLocation}."
                    : $"  the Valley of Eodon public moongate at {MoongateLocation} on Ter Mur."
            );
            lines.Add($"  {Vines.Length} climbing vines at 678,1759 (z 40 and 60), climbing to 679,1757,100.");
            lines.Add(
                $"  {EodonSpawns.Definitions.Length} spawners asking for {EodonSpawns.Definitions.Sum(d => d.Capacity)} creatures."
            );
            return lines;
        }

        var deleted = Delete();

        if (deleted > 0)
        {
            lines.Add($"{deleted} objects of an earlier GenEodon deleted.");
        }

        var gates = 0;

        if (blocked)
        {
            lines.Add($"Eodon moongate skipped: another public moongate stands within a tile of {MoongateLocation}.");
        }
        else
        {
            // cc-P363: the stock gate, in Carl Hamilton's red spinning look (a new art ID; OSI's 0xF6C is untouched).
            var gate = new PublicMoongate { ItemID = ShardItemArt.GateItemId(ShardGateColor.Red) };
            WeakEntityCollection.Add(CollectionKey, gate);
            gate.MoveToWorld(MoongateLocation, Map.TerMur);
            gates++;
        }

        foreach (var (at, climbTo) in Vines)
        {
            var vine = new ClimbingVine(climbTo) { Movable = false };
            WeakEntityCollection.Add(CollectionKey, vine);
            vine.MoveToWorld(at, Map.TerMur);
        }

        var spawners = EodonSpawns.Generate();

        lines.Add($"Eodon: {gates} moongate, {Vines.Length} climbing vines, {spawners.Count} spawners.");
        lines.Add("Eodon generation complete.");
        return lines;
    }
}
