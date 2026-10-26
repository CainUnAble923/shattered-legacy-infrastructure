// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Generate.cs (cc-P408). Brings Treasures of Sorcerer's
// Dungeon up and down. Notes in shard-migration notes/cc-P408-sorcerers-dungeon-halloween.md.
//
// Up (ServUO Generate, then Enabled = true), each step skipped when already there, so running it twice places once:
//   1. The researcher at Ilshenar 536,456,-53, unless one stands (SorcerersDungeonResearcher.Instance).
//   2. ServUO's event dressing at the dungeon entrance, stock art (the client's "Jumbo Pumpkin A/B/C" 0x9D2B-0x9D2D at
//      546,460,6 / 548,460,6 / 548,458,6; two "Scarecrow_Flaming" 0x9F34 at 545,462,-53 and 550,462,-53; the
//      "Pumpkin_Owl" 0x9F28 at 545,463,-55 and "Pumpkin_Rose" 0x9F24 at 550,463,-55), each unless a Static stands on
//      its spot (ServUO's map.FindItem<Static>; the three pumpkins hang on the first spot's check, as there).
//   3. The spawner (TOSDSpawner), unless one runs: it pauses the stock spawners in its nine rooms and starts.
// Down (ServUO Remove, then Enabled = false): the spawner stops, its creatures and boss go, the paused stock spawners
// start again full. The researcher and the dressing STAY, as in ServUO (players keep their points and can still
// spend them after the season; the next season finds them and places nothing twice). cc-P624: Enabled goes false
// first, so nothing deleted on the way down can earn, and "its creatures" is everything the spawner owns (earlier
// rooms' survivors, Jack's pumpkins and what they hatched), never a sweep of the rooms.
// Remove (ours, staff only): Down, then the researcher and the dressing this generator placed, by serial.
//
// When: ServUO turns it on with its SeasonalEventSystem (October 1 for 60 days) and checks on every world save. Here
// it runs on the Halloween season switch (ClusterFSeasonalSwitch, cc-P267): the dates are the Halloween settings,
// clusterf.seasonal.halloween.start / .end (default 10-24 to 11-02) and .automatic, read through datesFrom, with the
// switch's own saved state under the name "sorcerersdungeon"; the hourly check acts only when the season changes, so
// a staff on or off stands until the next change (deviation 1).
//
// Everything placed is remembered BY SERIAL in ClusterFSeasonalState.Placed("sorcerersdungeon"), saved with the world,
// so Remove deletes exactly what this placed and never a same-art item someone else put there.
//
// [SorcerersDungeon status | on | off | remove | boss <1-9> [dryrun]]   (Administrator; also "tosd ..." on the console)
//   boss (ours, a staff and test aid like [MinaxArtifact): moves the rotation to that room (default: the current one)
//   and raises its boss now, without the 80 kills; the rotation carries on from there when the boss is gone.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Commands;
using Server.Engines.Points;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

public static class SorcerersDungeonGenerate
{
    public const string SeasonName = "sorcerersdungeon";
    public const string Label = "SorcerersDungeon";

    public static readonly Point3D ResearcherLocation = new(536, 456, -53);

    // ServUO Generate: (art, where), in its order.
    public static readonly (int ItemID, Point3D At)[] Dressing =
    {
        (0x9D2B, new Point3D(546, 460, 6)),
        (0x9D2C, new Point3D(548, 460, 6)),
        (0x9D2D, new Point3D(548, 458, 6)),
        (0x9F34, new Point3D(545, 462, -53)),
        (0x9F34, new Point3D(550, 462, -53)),
        (0x9F28, new Point3D(545, 463, -55)),
        (0x9F24, new Point3D(550, 463, -55))
    };

    public static readonly ClusterFSeasonalSwitch Season = new(
        SeasonName,
        Label,
        ClusterFHalloweenDecor.DefaultStart,
        ClusterFHalloweenDecor.DefaultEnd,
        Up,
        Down,
        ClusterFHalloweenDecor.SeasonName
    );

    public static Map Map => Map.Ilshenar;

    private static HashSet<Serial> Tracked => ClusterFSeasonalState.Placed(SeasonName);

    public static void Configure()
    {
        Season.Configure();
        CommandSystem.Register("SorcerersDungeon", AccessLevel.Administrator, SorcerersDungeon_OnCommand);
        ConsoleInputHandler.RegisterCommand(
            ["tosd"],
            "Treasures of Sorcerer's Dungeon: tosd status|on|off|remove|boss <1-9> [dryrun]",
            args => Core.LoopContext.Post(() =>
            {
                foreach (var line in Run((args ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)))
                {
                    Console.WriteLine(line);
                }
            })
        );
    }

    [Usage("SorcerersDungeon [status|on|off|remove|boss <1-9>] [dryrun]")]
    [Description("Treasures of Sorcerer's Dungeon: reports its state, or turns the event on or off by hand.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Treasures of Sorcerer's Dungeon (Ilshenar): status, or on / off by hand. It turns on by itself with the Halloween dates (10-24 to 11-02) and off after them. on places the researcher and the entrance pumpkins (once) and starts the boss spawner; off stops the spawner and removes its creatures but leaves the researcher; remove also takes down the researcher and pumpkins it placed; boss <1-9> raises that room's boss now (9 is Jack the Pumpkin King). Dry run: add 'dryrun'.")]
    public static void SorcerersDungeon_OnCommand(CommandEventArgs e)
    {
        foreach (var line in Run(e.Arguments))
        {
            e.Mobile.SendMessage(line);
        }
    }

    /// <summary>The verbs, shared by the in-game command and the console command. args: [verb] [dryrun].</summary>
    public static List<string> Run(string[] args)
    {
        var verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
        var dryRun = args.Any(a => a.Equals("dryrun", StringComparison.OrdinalIgnoreCase));

        switch (verb)
        {
            case "on":
            case "up":
                {
                    var lines = new List<string>();

                    if (!Season.IsInSeason(Season.Clock()) && !dryRun)
                    {
                        lines.Add($"[{Label}] Outside the dates ({Season.Window}): turning it on anyway. It stays on until 'off', or until the season next ends.");
                    }

                    lines.AddRange(Up(dryRun));
                    return lines;
                }
            case "off":
            case "down":
                return Down(dryRun);
            case "remove":
                return Remove(dryRun);
            case "boss":
                {
                    var room = 0;
                    var number = args.Skip(1).FirstOrDefault(a => !a.Equals("dryrun", StringComparison.OrdinalIgnoreCase));

                    if (number != null && (!int.TryParse(number, out room) || room is < 1 or > 9))
                    {
                        return new List<string> { $"[{Label}] boss takes a room number, 1 to 9 (9 is Jack the Pumpkin King)." };
                    }

                    return RaiseBoss(room, dryRun);
                }
            case "status":
            case "dryrun":
                return Status();
            default:
                return new List<string> { "Usage: SorcerersDungeon [status|on|off|remove|boss <1-9>] [dryrun]" };
        }
    }

    /// <summary>ServUO Generate, then Enabled = true. Returns what it did (or would do).</summary>
    public static List<string> Up(bool dryRun)
    {
        var lines = new List<string>();
        var verb = dryRun ? "would place" : "placed";

        if (SorcerersDungeonResearcher.Instance?.Deleted == false)
        {
            lines.Add($"[{Label}] The researcher already stands at {SorcerersDungeonResearcher.Instance.Location}.");
        }
        else
        {
            if (!dryRun)
            {
                var researcher = new SorcerersDungeonResearcher();
                SorcerersDungeonResearcher.Instance = researcher;
                researcher.MoveToWorld(ResearcherLocation, Map);
                Tracked.Add(researcher.Serial);
            }

            lines.Add($"[{Label}] {verb} the researcher at {ResearcherLocation} (Ilshenar).");
        }

        var placed = 0;

        // ServUO checks the first pumpkin's spot for all three pumpkins, then each other piece's own spot.
        if (!HasStatic(Dressing[0].At))
        {
            for (var i = 0; i < 3; i++)
            {
                placed += PlaceStatic(Dressing[i].ItemID, Dressing[i].At, dryRun);
            }
        }

        for (var i = 3; i < Dressing.Length; i++)
        {
            if (!HasStatic(Dressing[i].At))
            {
                placed += PlaceStatic(Dressing[i].ItemID, Dressing[i].At, dryRun);
            }
        }

        lines.Add($"[{Label}] {verb} {placed} of the {Dressing.Length} entrance pieces ({Dressing.Length - placed} already there).");

        if (TOSDSpawner.Instance != null)
        {
            lines.Add($"[{Label}] The spawner already runs (room {TOSDSpawner.Instance.Index + 1} of 9).");
        }
        else
        {
            if (!dryRun)
            {
                var spawner = new TOSDSpawner();
                spawner.BeginTimer();
                lines.Add($"[{Label}] Started the spawner; paused {spawner.PausedSpawners.Count} stock spawner(s) in its rooms.");
            }
            else
            {
                lines.Add($"[{Label}] would start the spawner.");
            }
        }

        if (!dryRun)
        {
            SorcerersDungeonData.Instance.Enabled = true;
            lines.Add($"[{Label}] Enabled: kills in Sorcerer's Dungeon now roll artifacts of Enchanted Origin.");
        }

        return lines;
    }

    /// <summary>ServUO Remove, then Enabled = false. The researcher and the dressing stay.</summary>
    public static List<string> Down(bool dryRun)
    {
        var lines = new List<string>();
        var spawner = TOSDSpawner.Instance;

        if (spawner == null)
        {
            lines.Add($"[{Label}] The spawner is not running.");
        }
        else
        {
            var (creatures, pumpkins) = spawner.CountOwned();
            var paused = spawner.PausedSpawners.Count;

            if (!dryRun)
            {
                SorcerersDungeonData.Instance.Enabled = false;
                spawner.Deactivate();
            }

            lines.Add($"[{Label}] {(dryRun ? "would stop" : "Stopped")} the spawner: {creatures} creature(s) and {pumpkins} pumpkin(s) of the event's {(dryRun ? "would go" : "removed")}, {paused} stock spawner(s) {(dryRun ? "would restart" : "restarted")}.");
        }

        if (!dryRun)
        {
            SorcerersDungeonData.Instance.Enabled = false;
            lines.Add($"[{Label}] Disabled. The researcher stays, so points can still be spent.");
        }

        return lines;
    }

    /// <summary>Ours: Down, then the researcher and the dressing this generator placed, by serial.</summary>
    public static List<string> Remove(bool dryRun)
    {
        var lines = Down(dryRun);
        var count = 0;

        foreach (var serial in Tracked.ToList())
        {
            var entity = World.FindEntity<IEntity>(serial);

            if (entity is SorcerersDungeonResearcher or Static && !entity.Deleted)
            {
                count++;

                if (!dryRun)
                {
                    entity.Delete();
                }
            }

            if (!dryRun)
            {
                Tracked.Remove(serial);
            }
        }

        lines.Add($"[{Label}] {(dryRun ? "would remove" : "Removed")} {count} placed object(s) (the researcher and the entrance pieces).");
        return lines;
    }

    /// <summary>
    ///     Ours: moves the rotation to room 1-9 (0: stay in the current room) and raises its boss now. Nothing when the
    ///     spawner is not running or a boss is already up.
    /// </summary>
    public static List<string> RaiseBoss(int room, bool dryRun)
    {
        var spawner = TOSDSpawner.Instance;

        if (spawner == null)
        {
            return new List<string> { $"[{Label}] The spawner is not running; 'on' first." };
        }

        if (spawner.Boss?.Deleted == false)
        {
            return new List<string> { $"[{Label}] A boss is already up: {spawner.Boss.Name} at {spawner.Boss.Location}." };
        }

        var index = room > 0 ? room - 1 : spawner.Index;
        var entry = spawner.Entries[index];

        if (dryRun)
        {
            return new List<string> { $"[{Label}] would raise {entry.Boss.Name} in room {index + 1} now." };
        }

        spawner.Index = index;
        spawner.DoSpawn(entry.Boss, true);

        return new List<string>
        {
            spawner.Boss?.Deleted == false
                ? $"[{Label}] Raised {spawner.Boss.Name}{(string.IsNullOrEmpty(spawner.Boss.Title) ? "" : $" {spawner.Boss.Title}")} in room {index + 1} at {spawner.Boss.Location}."
                : $"[{Label}] No open spot for the {entry.Boss.Name} boss in room {index + 1} after 20 tries; try again."
        };
    }

    public static List<string> Status()
    {
        var local = Season.Clock();
        var spawner = TOSDSpawner.Instance;
        var researcher = SorcerersDungeonResearcher.Instance;
        var lines = new List<string>
        {
            $"[{Label}] {local:yyyy-MM-dd}: {Season.SeasonText(local)}; {Season.AutomaticText()}",
            $"[{Label}] Enabled: {SorcerersDungeonData.Instance.Enabled}. Researcher: {(researcher?.Deleted == false ? $"at {researcher.Location} ({researcher.Map})" : "none")}. " +
            $"Entrance pieces standing: {Dressing.Count(d => HasStatic(d.At))} of {Dressing.Length}."
        };

        if (spawner == null)
        {
            lines.Add($"[{Label}] Spawner: not running.");
        }
        else
        {
            var entry = spawner.Entries[spawner.Index];
            var (creatures, pumpkins) = spawner.CountOwned();
            lines.Add(
                $"[{Label}] Spawner: room {spawner.Index + 1} of 9 ({entry.Boss.Name}), {spawner.Spawn.Count}/{entry.MaxSpawn} spawned, " +
                $"{spawner.KillCount}/{entry.ToKill} killed, boss {(spawner.Boss?.Deleted == false ? $"up at {spawner.Boss.Location}" : "not up")}, " +
                $"{spawner.PausedSpawners.Count} stock spawner(s) paused."
            );
            lines.Add(
                $"[{Label}] Owned by the event (what off deletes): {creatures} creature(s) in all rooms, {pumpkins} unholy pumpkin(s)."
            );
        }

        // Read from the world, not from the spawner's lists, so an "off" can be seen to have taken them away.
        var bosses = World.Mobiles.Values
            .Where(m => !m.Deleted && m is BaseCreature && m is not SorcerersDungeonResearcher && m.GetType().Namespace == typeof(TOSDSpawner).Namespace)
            .ToList();
        var inRooms = World.Mobiles.Values.Count(m =>
            !m.Deleted && m is BaseCreature { Controlled: false } && m.Map == Map && RoomsContain(m.Location));
        lines.Add(
            $"[{Label}] In the world: {bosses.Count} event boss(es){(bosses.Count > 0 ? $" ({string.Join(", ", bosses.Select(b => $"{b.Name} at {b.Location}"))})" : "")}; " +
            $"{inRooms} creature(s) inside the nine rooms."
        );

        var rewards = SorcerersDungeonRewards.Rewards;
        lines.Add(
            rewards == null
                ? $"[{Label}] Reward list: not loaded."
                : $"[{Label}] Reward list ({rewards.Count}): {string.Join(", ", rewards.Select(r => $"{r.Type.Name} {r.Points}"))}."
        );

        lines.Add(
            $"[{Label}] Artifacts of Enchanted Origin in the world: {EnchantedOriginArtifacts.Count}; players holding points: " +
            $"{SorcerersDungeonData.Instance.PlayerTable.Count}; kill tallies: {SorcerersDungeonData.Instance.DungeonPoints.Count}."
        );

        return lines;
    }

    /// <summary>A point in one of the spawner's nine rooms, ends included as ServUO's spawn points draw them.</summary>
    public static bool RoomsContain(Point3D p)
    {
        foreach (var r in Rooms)
        {
            if (p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height)
            {
                return true;
            }
        }

        return false;
    }

    private static Rectangle2D[] _rooms;

    private static Rectangle2D[] Rooms => _rooms ??= TOSDSpawner.CreateEntries().Select(e => e.SpawnArea).ToArray();

    public static bool HasStatic(Point3D p)
    {
        foreach (var item in Map.GetItemsAt(p))
        {
            if (item is Static && item.Z == p.Z)
            {
                return true;
            }
        }

        return false;
    }

    private static int PlaceStatic(int itemID, Point3D at, bool dryRun)
    {
        if (!dryRun)
        {
            var st = new Static(itemID);
            st.MoveToWorld(at, Map);
            Tracked.Add(st.Serial);
        }

        return 1;
    }
}
