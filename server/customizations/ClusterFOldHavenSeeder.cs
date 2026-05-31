using System;
using System.Collections.Generic;
using Server.Engines.Spawners;

namespace Server;

/// <summary>
/// Seeds respawning creature spawners in Old Haven ruins (Trammel).
///
/// Old Haven centre is near (3677, 2625) on Trammel.  The seeder places:
///   • Several OldHavenMage spawners scattered through the ruins — 2–3 mages
///     each, 2–5 minute respawn.  Ideal for Magic Resist training.
///   • One DrelgorTheImpaler spawner (the warlord who led the assault) with a
///     longer 8–15 minute respawn so he feels like a mini-boss.
///
/// Bounds check used to identify "Old Haven" world area:
///   X 3620–3760, Y 2420–2600 on Trammel
/// </summary>
public static class ClusterFOldHavenSeeder
{
    // -----------------------------------------------------------------------
    // Spawn table
    // Each entry drives one Spawner item placed at (X, Y, auto-Z).
    // -----------------------------------------------------------------------
    private static readonly SpawnConfig[] Entries =
    {
        // --- Drelgor the Impaler (mini-boss, 1 at a time, slow respawn) ---
        new(
            label:    "Drelgor the Impaler",
            typeName: "DrelgorTheImpaler",
            x: 3622, y: 2488,
            count: 1,
            homeRange: 6,
            minDelay: TimeSpan.FromMinutes(8),
            maxDelay: TimeSpan.FromMinutes(15)
        ),

        // --- OldHavenMage clusters (scattered through the ruins) ---
        new(
            label:    "Old Haven Mage – north gate",
            typeName: "OldHavenMage",
            x: 3588, y: 2457,
            count: 3,
            homeRange: 10,
            minDelay: TimeSpan.FromMinutes(2),
            maxDelay: TimeSpan.FromMinutes(5)
        ),
        new(
            label:    "Old Haven Mage – west ruins",
            typeName: "OldHavenMage",
            x: 3556, y: 2487,
            count: 2,
            homeRange: 10,
            minDelay: TimeSpan.FromMinutes(2),
            maxDelay: TimeSpan.FromMinutes(5)
        ),
        new(
            label:    "Old Haven Mage – town square",
            typeName: "OldHavenMage",
            x: 3600, y: 2510,
            count: 3,
            homeRange: 12,
            minDelay: TimeSpan.FromMinutes(2),
            maxDelay: TimeSpan.FromMinutes(5)
        ),
        new(
            label:    "Old Haven Mage – south courtyard",
            typeName: "OldHavenMage",
            x: 3618, y: 2540,
            count: 2,
            homeRange: 10,
            minDelay: TimeSpan.FromMinutes(2),
            maxDelay: TimeSpan.FromMinutes(5)
        ),
        new(
            label:    "Old Haven Mage – east rubble",
            typeName: "OldHavenMage",
            x: 3645, y: 2495,
            count: 2,
            homeRange: 10,
            minDelay: TimeSpan.FromMinutes(2),
            maxDelay: TimeSpan.FromMinutes(5)
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
    }

    // -----------------------------------------------------------------------
    // Command
    // -----------------------------------------------------------------------
    [Usage("ClusterFSeedOldHaven [missing|dryrun|replace]")]
    [Description("Seeds or replaces the ClusterF Old Haven respawn spawners.")]
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
    private static string Seed(bool dryRun, bool replace)
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
                if (string.Equals(e.SpawnedName, config.TypeName, StringComparison.OrdinalIgnoreCase))
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
            managed.Add(e.TypeName);

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
    // Spawn config descriptor (internal — not the engine's SpawnerEntry type)
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

        public SpawnConfig(
            string   label,
            string   typeName,
            int      x,
            int      y,
            int      count,
            int      homeRange,
            TimeSpan minDelay,
            TimeSpan maxDelay)
        {
            Label     = label;
            TypeName  = typeName;
            X         = x;
            Y         = y;
            Count     = count;
            HomeRange = homeRange;
            MinDelay  = minDelay;
            MaxDelay  = maxDelay;
        }
    }
}
