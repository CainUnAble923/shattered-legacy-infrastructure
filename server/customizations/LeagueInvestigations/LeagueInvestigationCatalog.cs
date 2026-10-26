// cc-P629 (P565 design, stage 1): League investigation warrants, the closed catalog. Notes: shard-migration
// notes/cc-P629-build-league-warrants.md; design notes/cc-P565-design-league-reward-tokens.md.
//
// A warrant (LeagueInvestigationWarrant) names one encounter here; an encounter names one site and one exact existing
// mini-boss type. Nothing a player or staff member types picks a creature: the keys and factories are fixed in this
// file, unlike the surge templates (StaffHub/ClusterFSurgeTemplates.cs), which take any constructible type.
//
// Chase's choices (2026-10-25 evening, the design's defaults): 500 AP opens the row and 300 Renown buys it; every
// consenting participant's account shares one 24-hour cooldown; one ordinary corpse of loot for the group; a run the
// server interrupts gives the same warrant back. Stage 1 holds those numbers and the site checks only: there is no shop
// row, no runner and no spawn yet, and the switch (Settings.Enabled) is off.
//
// cc-P647 (P641 review): CheckSite refuses a facet whose map or statics files are not loaded (MapDataProblem), checks
// every region touching the policy area at any height (RegionsTouching), and each spawn tile's region at its own Z.
//
// Sites, checked on the 7.0.117.0 Trammel map (map1LegacyMUL.uop, statics1.mul; notes section 2):
//   covetous-hall   5472,1877,0. The design's candidate (the lake shore, 5440,2023) lies inside the Covetous Void
//                   Pool region (VoidPoolRegion.Bounds, 5383,1960 236x80), a wave event's region, so it is refused by
//                   the design's special-region rule. This is Covetous's harpy hall (pinned's stock Harpy spawner,
//                   Spawns/shared/trammel/Covetous.json), 71 tiles north of the pool's bounds.
//   yew-woods       1059,1166,5. The design's candidate, the stock Outdoors spawner tile in the Yew woods; no region
//                   within 32 tiles in pinned's regions.json.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Mobiles;
using Server.Music;
using Server.Regions;

namespace Server.Engines.LeagueInvestigations;

/// <summary>One place an investigation can be called: a fixed tile, its facet, and whether it may be used.</summary>
public sealed record LeagueInvestigationSite(string Key, string Name, Map Map, Point3D Center, bool Enabled, string Evidence);

/// <summary>One encounter: the boss it calls (an exact existing type, never a typed name) and where.</summary>
public sealed record LeagueInvestigationEncounter(string Key, string BossName, Type BossType, Func<BaseCreature> Create, string SiteKey);

public static class LeagueInvestigationCatalog
{
    /// <summary>Bumped when a saved run's meaning would change (stage 2 records it with each run).</summary>
    public const int Revision = 1;

    /// <summary>The design's numbers in one place (P565 "Numbers and encounter catalog").</summary>
    public static class Settings
    {
        /// <summary>The feature switch. Off until the later stages pass; nothing sells or spawns while it is off.</summary>
        public static bool Enabled { get; set; }

        public const int ApRequired = 500;
        public const int RenownPrice = 300;

        public static readonly TimeSpan Cooldown = TimeSpan.FromHours(24);
        public static readonly TimeSpan ReadyCheck = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan Engagement = TimeSpan.FromSeconds(120);
        public static readonly TimeSpan Absence = TimeSpan.FromSeconds(120);
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

        public const int MaxRoster = 10;       // Party.Capacity
        public const int CallerRange = 4;      // the caller stands this close to the site to call
        public const int AcceptRange = 12;     // party members within this to accept and start
        public const int PresenceRange = 24;   // within this counts as present, and for credit
        public const int SpawnRadius = 4;      // the boss's spawn tile is within this of the centre
        public const int SpawnAttempts = 20;
        public const int MinSpawnTiles = 20;   // the site needs at least this many safe spawn tiles
        public const int Leash = 8;
        public const int PolicyRadius = 32;
        public const int MaxActive = 4;
    }

    public const string CovetousHall = "covetous-hall";
    public const string YewWoods = "yew-woods";

    public static readonly LeagueInvestigationSite[] Sites =
    [
        new(CovetousHall, "Covetous, the harpy hall", Map.Trammel, new Point3D(5472, 1877, 0), true,
            "Trammel map 7.0.117.0: land 0x0245 Z 0, no statics; 75 of 81 tiles walkable within 4, 235 of 289 within 8. " +
            "Region: Covetous (DungeonRegion) only. Void Pool bounds 71 tiles south; Cora's spawner (5457,1808) 69 tiles north."),
        new(YewWoods, "the Yew woods", Map.Trammel, new Point3D(1059, 1166, 5), true,
            "Trammel map 7.0.117.0: land 0x00C6 Z 5, grass static 0x0CAF; 79 of 81 tiles walkable within 4. " +
            "No region within 32 tiles (pinned regions.json). Natural Hollow Oak spawner 1010,1250 is 97 tiles away.")
    ];

    public static readonly LeagueInvestigationEncounter[] Encounters =
    [
        new("investigation.siegebreaker", "The Siegebreaker", typeof(TheSiegebreaker), () => new TheSiegebreaker(), CovetousHall),
        new("investigation.hollow-oak", "The Hollow Oak", typeof(TheHollowOak), () => new TheHollowOak(), YewWoods)
    ];

    public static LeagueInvestigationEncounter? FindEncounter(string? key) =>
        key == null ? null : Encounters.FirstOrDefault(e => e.Key == key);

    public static LeagueInvestigationSite? FindSite(string? key) =>
        key == null ? null : Sites.FirstOrDefault(s => s.Key == key);

    public static LeagueInvestigationSite? SiteOf(LeagueInvestigationEncounter encounter) => FindSite(encounter.SiteKey);

    // -- Validation ---------------------------------------------------------------------------------------------

    /// <summary>
    /// What is wrong with the catalog as data: duplicate or dangling keys, a site off Trammel/Felucca, a site whose
    /// leash area reaches a natural mini-boss spawner's home range. Empty when it is sound. Needs no map files.
    /// </summary>
    public static List<string> ValidateData() => ValidateData(Sites, Encounters);

    public static List<string> ValidateData(LeagueInvestigationSite[] sites, LeagueInvestigationEncounter[] encounters)
    {
        var problems = new List<string>();

        foreach (var dup in sites.GroupBy(s => s.Key).Where(g => g.Count() > 1))
        {
            problems.Add($"site key {dup.Key} is used {dup.Count()} times");
        }

        foreach (var dup in encounters.GroupBy(e => e.Key).Where(g => g.Count() > 1))
        {
            problems.Add($"encounter key {dup.Key} is used {dup.Count()} times");
        }

        foreach (var e in encounters)
        {
            if (sites.All(s => s.Key != e.SiteKey))
            {
                problems.Add($"encounter {e.Key} names unknown site {e.SiteKey}");
            }

            if (!typeof(BaseCreature).IsAssignableFrom(e.BossType))
            {
                problems.Add($"encounter {e.Key} boss {e.BossType.Name} is not a creature");
            }
        }

        foreach (var s in sites)
        {
            if (s.Map != Map.Trammel && s.Map != Map.Felucca)
            {
                problems.Add($"site {s.Key} is on {s.Map}, not Trammel or Felucca");
            }

            foreach (var row in ClusterFLeagueBestiary.Spawners)
            {
                if (!IsMiniBossRow(row))
                {
                    continue;
                }

                var reach = Settings.Leash + row.HomeRange;
                if (Math.Max(Math.Abs(row.X - s.Center.X), Math.Abs(row.Y - s.Center.Y)) <= reach)
                {
                    problems.Add($"site {s.Key} leash overlaps the natural spawner \"{row.SpawnerName}\"");
                }
            }
        }

        return problems;
    }

    private static bool IsMiniBossRow(ClusterFLeagueBestiary.SpawnRow row) =>
        row.Types.Any(t => ClusterFLeagueBestiary.Entries.Any(b => b.Use == BestiaryUse.MiniBoss && b.Type.Name == t));

    /// <summary>
    /// Whether a region an investigation's area may lie in: the facet's default region, a dungeon, or a plain or music
    /// region (no rules of its own). Anything else, a town or other guarded region, a house, the Void Pool, the
    /// tutorial, a sanctum, is refused: an allow list, so a special region added later is refused until reviewed.
    /// </summary>
    public static bool IsAllowedRegion(Region region)
    {
        for (var r = region; r != null; r = r.Parent)
        {
            if (r.IsDefault)
            {
                continue;
            }

            var t = r.GetType();
            if (t != typeof(DungeonRegion) && t != typeof(BaseRegion) && t != typeof(ShardMusicRegion))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// cc-P647 (P641 finding 2): why this facet's terrain cannot be trusted, or null when its land, static index and static
    /// data files are all open. The tests replace it.
    /// </summary>
    public static Func<Map, string?> MapDataProblem { get; set; } =
        map => MissingMapData(map.Name, map.Tiles.MapStream != null, map.Tiles.IndexStream != null, map.Tiles.DataStream != null);

    public static string? MissingMapData(string facet, bool land, bool staticIndex, bool staticData)
    {
        if (!land)
        {
            return $"{facet}'s land map file is not loaded";
        }

        return staticIndex && staticData ? null : $"{facet}'s statics files are not loaded";
    }

    /// <summary>
    /// What stops this site being used on the world as it is now: disabled, a facet that is not loaded, a refused region
    /// anywhere in its policy area, the facet's map files not loaded (cc-P647), or fewer than MinSpawnTiles safe spawn
    /// tiles. Empty when it is usable.
    /// </summary>
    public static List<string> CheckSite(LeagueInvestigationSite site)
    {
        var problems = new List<string>();

        if (!site.Enabled)
        {
            problems.Add($"site {site.Key} is disabled");
        }

        var map = site.Map;
        if (map == null || map == Map.Internal)
        {
            problems.Add($"site {site.Key} has no facet");
            return problems;
        }

        // cc-P647 (P641 finding 3): every region that covers a tile of the policy area at any height, not only the one
        // Region.Find picks there (the highest priority, at one Z): an allowed region laid over a town, or a town only below
        // the centre's Z, must still refuse. The policy volume is the full column, -128 to 127; stage 2's policy region
        // has to be registered full height to match.
        var c = site.Center;
        var refused = new HashSet<string>();
        foreach (var region in RegionsTouching(map, c, Settings.PolicyRadius))
        {
            if (!IsAllowedRegion(region))
            {
                refused.Add($"{region.Name ?? "(unnamed)"} ({region.GetType().Name})");
            }
        }

        foreach (var name in refused.Order())
        {
            problems.Add($"site {site.Key} policy area reaches region {name}");
        }

        // A Map object exists whether or not its files opened; without them the engine reads blank open ground at Z 0
        // (TileMatrix's fallback blocks), which would pass any fit check. Refused, and not counted as a crowded site.
        if (MapDataProblem(map) is { } missing)
        {
            problems.Add($"site {site.Key}: {missing}");
            return problems;
        }

        var spawnable = SpawnTiles(site).Count;
        if (spawnable < Settings.MinSpawnTiles)
        {
            problems.Add($"site {site.Key} has {spawnable} safe spawn tiles within {Settings.SpawnRadius}, needs {Settings.MinSpawnTiles}");
        }

        return problems;
    }

    /// <summary>Every registered region on this facet whose area covers a tile within radius of the centre, at any Z.</summary>
    public static HashSet<Region> RegionsTouching(Map map, Point3D center, int radius)
    {
        var found = new HashSet<Region>();
        for (var x = center.X - radius; x <= center.X + radius; x++)
        {
            for (var y = center.Y - radius; y <= center.Y + radius; y++)
            {
                var p = new Point2D(x, y);
                foreach (var region in map.GetSector(x, y).Regions)
                {
                    if (found.Contains(region))
                    {
                        continue;
                    }

                    foreach (var rect in region.Area)
                    {
                        if (rect.Contains(p))
                        {
                            found.Add(region);
                            break;
                        }
                    }
                }
            }
        }

        return found;
    }

    /// <summary>
    /// The tiles within SpawnRadius of the centre a creature can stand on, at the Z it would stand at, each in an allowed
    /// region at that Z (cc-P647). None when the facet's
    /// map data is not loaded.
    /// </summary>
    public static List<Point3D> SpawnTiles(LeagueInvestigationSite site)
    {
        var list = new List<Point3D>();
        if (site.Map == null || site.Map == Map.Internal || MapDataProblem(site.Map) != null)
        {
            return list;
        }

        var c = site.Center;
        for (var x = c.X - Settings.SpawnRadius; x <= c.X + Settings.SpawnRadius; x++)
        {
            for (var y = c.Y - Settings.SpawnRadius; y <= c.Y + Settings.SpawnRadius; y++)
            {
                var z = site.Map.GetAverageZ(x, y);
                if (site.Map.CanSpawnMobile(x, y, z) && IsAllowedRegion(Region.Find(new Point3D(x, y, z), site.Map)))
                {
                    list.Add(new Point3D(x, y, z));
                }
            }
        }

        return list;
    }
}
