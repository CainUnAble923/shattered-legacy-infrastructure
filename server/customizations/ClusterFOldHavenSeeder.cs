using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.Spawners;
using Server.Mobiles;

namespace Server;

/// <summary>
/// Seeds respawning creature spawners in Old Haven ruins (Trammel).
///
/// Old Haven centre is near (3677, 2625) on Trammel.  The seeder places:
///   - One Drelgor spawner: ServUO's Drelgor the Impaler, the "Cleansing Old Haven" quest
///     target, where ServUO's own spawn data puts him (Spawns/trammel.xml "Old Haven
///     [Drelgor]": 3651,2509, range 50, one at a time). Added by cc-P129.
///
/// cc-P189 (Chase, 2026-10-19: "too hard for new players"; new players walk through Old Haven to the
/// Rootdeep hole): the five OldHavenMage spawners (2-3 mages each, 2-5 minute respawn) and the
/// VorlakTheImpaler mini-boss spawner (8-15 minute respawn) are no longer seeded here. The classes stay;
/// [ClusterFOldHavenRetire removes the spawners a world already has. Their new home is Chase's choice
/// (shard-migration notes/cc-P189-cont-old-haven-new-player-safe.md). The stock undead are pinned's own
/// spawners (post-uoml/trammel/Outdoors.json) and are not this seeder's.
///
/// Bounds check used to identify "Old Haven" world area (IsOldHavenArea):
///   X 3520-3680, Y 2420-2600 on Trammel
/// </summary>
public static class ClusterFOldHavenSeeder
{
    // -----------------------------------------------------------------------
    // Spawn table
    // Each entry drives one Spawner item placed at (X, Y, auto-Z).
    // -----------------------------------------------------------------------
    private static readonly SpawnConfig[] Entries =
    {
        // --- Drelgor the Impaler, ServUO's (cc-P129): the Old Haven quest target at ServUO's own spawn point.
        //     ServUO's XmlSpawner entry: one Drelgor in a 50-tile home range with a 30-minute refractory. ---
        new(
            label:    "Drelgor the Impaler (ServUO)",
            typeName: "Drelgor",
            x: 3651, y: 2509,
            count: 1,
            homeRange: 50,
            minDelay: TimeSpan.FromMinutes(30),
            maxDelay: TimeSpan.FromMinutes(30)
        ),
    };

    // -----------------------------------------------------------------------
    // Configuration
    // -----------------------------------------------------------------------
    private static bool _enabled;
    private static bool _seedOnWorldLoad;

    public static void Configure()
    {
        _enabled         = ServerConfiguration.GetOrUpdateSetting("clusterf.oldHavenSeeder.enabled",         true);
        _seedOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("clusterf.oldHavenSeeder.seedOnWorldLoad", false);

        EventSink.WorldLoad += OnWorldLoad;
        CommandSystem.Register("ClusterFSeedOldHaven", AccessLevel.Administrator, ClusterFSeedOldHaven_OnCommand);
        CommandSystem.Register("ClusterFOldHavenRetire", AccessLevel.Administrator, ClusterFOldHavenRetire_OnCommand);
    }

    // -----------------------------------------------------------------------
    // Command
    // -----------------------------------------------------------------------
    [Usage("ClusterFSeedOldHaven [missing|dryrun|replace]")]
    [Description("Seeds or replaces the ClusterF Old Haven respawn spawners.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.Unverified, DryRun = "dryrun", Summary = "Places missing Old Haven respawn spawners. replace DELETES every managed spawner and places new ones.")]
    private static void ClusterFSeedOldHaven_OnCommand(CommandEventArgs e)
    {
        if (!_enabled)
        {
            e.Mobile.SendMessage("ClusterF Old Haven seeder is disabled.");
            return;
        }

        var mode    = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "missing";
        var dryRun  = mode is "dryrun" or "status";
        var replace = mode is "replace" or "force";

        if (!dryRun && !replace && mode != "missing")
        {
            e.Mobile.SendMessage("Usage: [ClusterFSeedOldHaven [missing|dryrun|replace]");
            return;
        }

        var result = Seed(dryRun, replace);
        e.Mobile.SendMessage(result);
    }

    // -----------------------------------------------------------------------
    // cc-P189: removing the mages and Vorlak from a world that already has them
    // -----------------------------------------------------------------------

    /// <summary>The types cc-P189 takes out of Old Haven. DrelgorTheImpaler is Vorlak's name before cc-P129.</summary>
    internal static readonly HashSet<string> RetiredTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OldHavenMage", "VorlakTheImpaler", "DrelgorTheImpaler"
    };

    [Usage("ClusterFOldHavenRetire [dryrun]")]
    [Description("Removes the Old Haven Mage and Vorlak spawners, and their creatures, from Old Haven.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "DELETES the Old Haven Mage and Vorlak spawners in Old Haven and their creatures (cc-P189). Any argument but dryrun deletes.")]
    private static void ClusterFOldHavenRetire_OnCommand(CommandEventArgs e)
    {
        var dryRun = e.Length > 0 && e.GetString(0).Equals("dryrun", StringComparison.OrdinalIgnoreCase);

        foreach (var line in Retire(dryRun))
        {
            e.Mobile.SendMessage(line);
        }
    }

    /// <summary>
    /// Deletes every spawner in Old Haven whose entries are all retired types (deleting a spawner deletes what it spawned,
    /// pinned BaseSpawner.OnDelete -> RemoveSpawns, BaseSpawner.cs:1245-1290), and any retired creature in Old Haven that
    /// no spawner owns and no player controls. A spawner that also spawns something else is reported and kept: staff
    /// mixed it, so staff decide. Returns the lines the command shows.
    /// </summary>
    internal static List<string> Retire(bool dryRun)
    {
        var lines = new List<string>();
        var spawners = new List<Spawner>();
        var creatures = 0;

        foreach (var item in World.Items.Values)
        {
            if (item.Deleted || item.Map != Map.Trammel || !IsOldHavenArea(item.Location) || item is not Spawner spawner)
                continue;

            var retired = 0;
            foreach (var entry in spawner.Entries)
            {
                if (RetiredTypes.Contains(entry.SpawnedName))
                    retired++;
            }

            if (retired == 0)
                continue;

            var names = string.Join(", ", spawner.Entries.Select(se => se.SpawnedName));

            if (retired < spawner.Entries.Count)
            {
                lines.Add($"[MIXED, KEPT] Spawner at ({spawner.X},{spawner.Y},{spawner.Z}) entries: {names}");
                continue;
            }

            spawners.Add(spawner);
            creatures += spawner.Spawned.Count;
            lines.Add($"{(dryRun ? "[DRY RUN] " : "")}Spawner at ({spawner.X},{spawner.Y},{spawner.Z}) entries: {names}, {spawner.Spawned.Count} spawned");
        }

        var strays = new List<Mobile>();

        foreach (var m in World.Mobiles.Values)
        {
            if (m.Deleted || m.Map != Map.Trammel || !IsOldHavenArea(m.Location) || !RetiredTypes.Contains(m.GetType().Name))
                continue;

            if (m is BaseCreature { Controlled: true } || m is ISpawnable { Spawner: not null })
                continue;

            strays.Add(m);
            lines.Add($"{(dryRun ? "[DRY RUN] " : "")}{m.GetType().Name} \"{m.Name}\" at ({m.X},{m.Y}), no spawner");
        }

        if (!dryRun)
        {
            foreach (var spawner in spawners)
            {
                Console.WriteLine($"[ClusterFOldHavenRetire] Deleting spawner at ({spawner.X},{spawner.Y},{spawner.Z})");
                spawner.Delete();
            }

            foreach (var m in strays)
            {
                Console.WriteLine($"[ClusterFOldHavenRetire] Deleting {m.GetType().Name} at ({m.X},{m.Y})");
                m.Delete();
            }
        }

        lines.Add(
            $"ClusterF Old Haven retire {(dryRun ? "dry run" : "complete")}: " +
            $"{(dryRun ? "would delete" : "deleted")} {spawners.Count} spawners with {creatures} creatures, " +
            $"and {strays.Count} creatures with no spawner."
        );
        return lines;
    }

    // -----------------------------------------------------------------------
    // World-load hook
    // -----------------------------------------------------------------------
    private static void OnWorldLoad()
    {
        if (!_enabled || !_seedOnWorldLoad)
            return;

        var result = Seed(false, false);
        Console.WriteLine(result);
    }

    // -----------------------------------------------------------------------
    // Core logic
    // -----------------------------------------------------------------------
    internal static string Seed(bool dryRun, bool replace)
    {
        var deleted = 0;
        var created = 0;
        var skipped = 0;

        if (replace)
        {
            var existing = FindExistingSpawners();
            deleted = existing.Count;

            if (!dryRun)
            {
                foreach (var spawner in existing)
                    spawner.Delete();
            }
        }

        foreach (var entry in Entries)
        {
            // Check for an existing spawner at (or near) this location.
            if (!replace && FindExistingSpawner(entry) != null)
            {
                skipped++;
                continue;
            }

            if (!dryRun)
                PlaceSpawner(entry);

            created++;
        }

        return $"ClusterF Old Haven seed {(dryRun ? "dry run" : "complete")}: " +
               $"created {created}, skipped {skipped}, deleted {deleted}.";
    }

    // -----------------------------------------------------------------------
    // Spawner placement
    // -----------------------------------------------------------------------
    private static void PlaceSpawner(SpawnConfig entry)
    {
        var z = Map.Trammel.GetAverageZ(entry.X, entry.Y);

        // Construct with count/delay, then override HomeRange before placing.
        // The constructor with spawnedNames calls AddEntry internally so
        // the spawner is wired up and will start its timer automatically.
        var spawner = new Spawner(entry.Count, entry.MinDelay, entry.MaxDelay, 0, default, entry.TypeName);
        spawner.HomeRange = entry.HomeRange;
        spawner.MoveToWorld(new Point3D(entry.X, entry.Y, z), Map.Trammel);
        spawner.Respawn();
    }

    // -----------------------------------------------------------------------
    // Detection helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Finds the first Spawner in Old Haven whose Entries list contains
    /// the given type name and whose location is within 15 tiles.
    /// </summary>
    private static Spawner FindExistingSpawner(SpawnConfig config)
    {
        foreach (var item in World.Items.Values)
        {
            if (item.Deleted || item.Map != Map.Trammel)
                continue;

            if (item is not Spawner spawner)
                continue;

            if (!IsOldHavenArea(spawner.Location))
                continue;

            // Match by type name and approximate location
            // (within 15 tiles, so manually relocated spawners are still found).
            var hasEntry = false;
            foreach (var e in spawner.Entries)
            {
                if (config.Matches(e.SpawnedName))
                {
                    hasEntry = true;
                    break;
                }
            }

            if (!hasEntry)
                continue;

            var dx = spawner.X - config.X;
            var dy = spawner.Y - config.Y;
            if (Math.Abs(dx) <= 15 && Math.Abs(dy) <= 15)
                return spawner;
        }

        return null;
    }

    /// <summary>
    /// Finds all Spawners in the Old Haven area whose Entries include any
    /// type name this seeder manages.
    /// </summary>
    private static List<Spawner> FindExistingSpawners()
    {
        var managed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in Entries)
        {
            managed.Add(e.TypeName);
            foreach (var alias in e.Aliases)
                managed.Add(alias);
        }

        var results = new List<Spawner>();

        foreach (var item in World.Items.Values)
        {
            if (item.Deleted || item.Map != Map.Trammel || !IsOldHavenArea(item.Location))
                continue;

            if (item is not Spawner spawner)
                continue;

            foreach (var entry in spawner.Entries)
            {
                if (managed.Contains(entry.SpawnedName))
                {
                    results.Add(spawner);
                    break;
                }
            }
        }

        return results;
    }

    private static bool IsOldHavenArea(Point3D loc) =>
        loc.X >= 3520 && loc.X <= 3680 &&
        loc.Y >= 2420 && loc.Y <= 2600;

    // -----------------------------------------------------------------------
    // Spawn config descriptor (internal, not the engine's SpawnerEntry type)
    // -----------------------------------------------------------------------
    private sealed class SpawnConfig
    {
        public string    Label     { get; }
        public string    TypeName  { get; }
        public int       X         { get; }
        public int       Y         { get; }
        public int       Count     { get; }
        public int       HomeRange { get; }
        public TimeSpan  MinDelay  { get; }
        public TimeSpan  MaxDelay  { get; }
        /// <summary>Earlier type names this entry was seeded under (cc-P129): a spawner holding one is this entry's.</summary>
        public string[]  Aliases   { get; }

        public SpawnConfig(
            string   label,
            string   typeName,
            int      x,
            int      y,
            int      count,
            int      homeRange,
            TimeSpan minDelay,
            TimeSpan maxDelay,
            string[] aliases = null)
        {
            Label     = label;
            TypeName  = typeName;
            X         = x;
            Y         = y;
            Count     = count;
            HomeRange = homeRange;
            MinDelay  = minDelay;
            MaxDelay  = maxDelay;
            Aliases   = aliases ?? Array.Empty<string>();
        }

        public bool Matches(string spawnedName)
        {
            if (string.Equals(spawnedName, TypeName, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var alias in Aliases)
            {
                if (string.Equals(spawnedName, alias, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
