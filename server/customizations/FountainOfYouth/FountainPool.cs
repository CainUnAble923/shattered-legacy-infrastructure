// cc-P192: where the roaming Fountain of Youth may appear. SECRET: staff only. Never document this on the wiki, the
// website, the player patch notes or any player text. Admin notes: shard-migration notes/admin/secrets.md.
//
// The pool is data: Configuration/fountain-of-youth.json, written with the defaults below on first start if missing and
// read again at every move, so a new area is a file edit (no restart). Each entry names an area and what it takes to get
// there:
//   key      unique; the fountain never lands in the same entry twice in a row
//   map      a facet name (Trammel, Felucca, Ilshenar, Malas, Tokuno, TerMur)
//   region   a region name from pinned Data/regions.json (its rectangles are the area; a tile must also lie in it), or
//   rects    [[x1, y1, x2, y2], ...] inclusive, for an area no region names (mountain tops)
//   access   "none" (on foot), "boat", "climbing", ...: the entry is used only while AccessAvailable(access) is true
//   why      for staff: why it is hard but fair
//
// A spot is valid when (FountainPool.Valid): the whole 4x4 fountain fits on one flat surface where a mobile can stand
// (pinned Map.CanSpawnMobile, Map.cs:1137, the check BaseSpawner.GetSpawnPosition uses, BaseSpawner.cs:468), no house
// covers it (BaseHouse.FindHouseAt), the tile is inside the entry's region and not in a town, guarded, house, jail,
// no-travel or tutorial region (a fountain's own sanctum is looked through: IsPartOf walks to the region under it),
// and it is not a sealed pocket: at least MinOpenTiles standable tiles join it within OpenRadius steps (a flood fill
// over the same check; a step may climb or drop at most MaxStep). That last check is
// local: it proves the spot is not walled in, not that a route from a town exists. Whether the area as a whole is
// reached on foot, by boat or by climbing is what the entry's access says, and it was chosen by hand.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Multis;
using Server.Regions;

namespace Server.Engines.FountainOfYouth;

public sealed class FountainPoolEntry
{
    public string Key { get; set; } = "";
    public string Map { get; set; } = "Trammel";
    public string? Region { get; set; }
    public int[][]? Rects { get; set; }
    public string Access { get; set; } = "none";
    public string Why { get; set; } = "";
}

public static class FountainPool
{
    public const string FileName = "fountain-of-youth.json";

    public const int MinOpenTiles = 40;
    public const int OpenRadius = 10;
    public const int MaxStep = 10;
    public const int TriesPerEntry = 400;

    /// <summary>The fountain's footprint around its location (pinned StoneFountainAddon offsets, -2..+1 on both axes).</summary>
    public const int FootMin = -2;
    public const int FootMax = 1;

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    /// <summary>Tests set this to use their own pool instead of the file.</summary>
    public static List<FountainPoolEntry>? Override { get; set; }

    // ---------------------------------------------------------------- access kinds

    /// <summary>
    /// What each access kind needs on this shard. Boats exist (High Seas is on, Configuration/expansion.json). Climbing
    /// exists once a type named ClimbingBoots is in the build (Chase 2026-10-19 15:05: "when mountain climbing boots
    /// are added"); until then climbing entries stay unused. An unknown kind is never available.
    /// </summary>
    public static readonly Dictionary<string, Func<bool>> AccessChecks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["none"] = () => true,
        ["boat"] = () => true,
        ["climbing"] = () => AssemblyHandler.FindTypeByName("ClimbingBoots") != null
    };

    public static bool AccessAvailable(string access) =>
        AccessChecks.TryGetValue(access, out var check) && check();

    // ---------------------------------------------------------------- the file

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ToJson(List<FountainPoolEntry> pool) => JsonSerializer.Serialize(pool, Json);

    /// <summary>The pool in force: the override, else the file (written with the defaults when missing), else the defaults.</summary>
    public static List<FountainPoolEntry> Load()
    {
        if (Override != null)
        {
            return Override;
        }

        try
        {
            if (!File.Exists(FilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, ToJson(Defaults));
                return Defaults;
            }

            return JsonSerializer.Deserialize<List<FountainPoolEntry>>(File.ReadAllText(FilePath), Json) ?? Defaults;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Fountain of Youth: {FilePath} could not be read ({e.Message}); using the built-in pool.");
            return Defaults;
        }
    }

    // ---------------------------------------------------------------- choosing

    public static Map? MapOf(FountainPoolEntry e) => Server.Map.Parse(e.Map);

    /// <summary>The entry's area: its region's rectangles, or its own.</summary>
    public static List<Rectangle3D> AreaOf(FountainPoolEntry e)
    {
        var list = new List<Rectangle3D>();
        var map = MapOf(e);

        if (map == null)
        {
            return list;
        }

        if (e.Region != null)
        {
            if (Server.Region.Find(e.Region, map) is { } region)
            {
                list.AddRange(region.Area);
            }

            return list;
        }

        foreach (var r in e.Rects ?? [])
        {
            if (r.Length == 4)
            {
                list.Add(new Rectangle3D(new Point3D(r[0], r[1], Server.Region.MinZ), new Point3D(r[2] + 1, r[3] + 1, Server.Region.MaxZ)));
            }
        }

        return list;
    }

    /// <summary>The entries a move may use: access available, an area that exists, and not the current one.</summary>
    public static List<FountainPoolEntry> Eligible(List<FountainPoolEntry> pool, string? currentKey) =>
        pool.Where(e => !string.Equals(e.Key, currentKey, StringComparison.OrdinalIgnoreCase) &&
                        AccessAvailable(e.Access) && MapOf(e) != null && AreaOf(e).Count > 0)
            .ToList();

    /// <summary>A random valid spot in a random eligible entry, or false when none was found.</summary>
    public static bool TryPick(string? currentKey, out FountainPoolEntry entry, out Map map, out Point3D spot)
    {
        var eligible = Eligible(Load(), currentKey);

        while (eligible.Count > 0)
        {
            var e = eligible[Utility.Random(eligible.Count)];
            eligible.Remove(e);

            var m = MapOf(e)!;
            var area = AreaOf(e);
            var total = area.Sum(r => (long)r.Width * r.Height);

            for (var i = 0; i < TriesPerEntry; i++)
            {
                // Area-weighted rectangle, then a uniform tile in it.
                var pick = Utility.RandomDouble() * total;
                var rect = area[^1];

                foreach (var r in area)
                {
                    pick -= (long)r.Width * r.Height;
                    if (pick < 0)
                    {
                        rect = r;
                        break;
                    }
                }

                var x = Utility.RandomMinMax(rect.Start.X, rect.End.X - 1);
                var y = Utility.RandomMinMax(rect.Start.Y, rect.End.Y - 1);

                if (Valid(e, m, x, y, out var z))
                {
                    entry = e;
                    map = m;
                    spot = new Point3D(x, y, z);
                    return true;
                }
            }
        }

        entry = null!;
        map = null!;
        spot = default;
        return false;
    }

    // ---------------------------------------------------------------- the checks

    /// <summary>True when the fountain may stand with its centre at (x, y) for this entry; z is the surface.</summary>
    public static bool Valid(FountainPoolEntry e, Map map, int x, int y, out int z)
    {
        z = 0;

        if (!map.CanSpawnMobile(x, y, Server.Region.MinZ, Server.Region.MaxZ, false, false, out z, out _))
        {
            return false;
        }

        for (var dx = FootMin; dx <= FootMax; dx++)
        {
            for (var dy = FootMin; dy <= FootMax; dy++)
            {
                var p = new Point3D(x + dx, y + dy, z);

                if (!map.CanSpawnMobile(p.X, p.Y, z, z, false, false, out var fz, out _) || fz != z)
                {
                    return false; // not one flat, open surface
                }

                if (BaseHouse.FindHouseAt(p, map, 16) != null || !AllowedRegion(e, Server.Region.Find(p, map)))
                {
                    return false;
                }
            }
        }

        return OpenTiles(map, new Point3D(x, y, z)) >= MinOpenTiles;
    }

    /// <summary>Inside the entry's region (when it names one) and in no region the fountain must avoid.</summary>
    public static bool AllowedRegion(FountainPoolEntry e, Region region)
    {
        if (e.Region != null && !region.IsPartOf(e.Region))
        {
            return false;
        }

        return !region.IsPartOf<GuardedRegion>() && !region.IsPartOf<TownRegion>() && !region.IsPartOf<HouseRegion>() &&
               !region.IsPartOf<NoTravelSpellsAllowedRegion>() && !region.IsPartOf<OldHavenBurrowsRegion>() &&
               !region.IsPartOf("Jail");
    }

    /// <summary>Standable tiles joined to the start within OpenRadius steps (capped at MinOpenTiles).</summary>
    public static int OpenTiles(Map map, Point3D start)
    {
        var seen = new HashSet<(int, int)> { (start.X, start.Y) };
        var queue = new Queue<Point3D>();
        queue.Enqueue(start);

        while (queue.Count > 0 && seen.Count < MinOpenTiles)
        {
            var p = queue.Dequeue();

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var nx = p.X + dx;
                    var ny = p.Y + dy;

                    if ((dx == 0 && dy == 0) || Math.Abs(nx - start.X) > OpenRadius || Math.Abs(ny - start.Y) > OpenRadius ||
                        seen.Contains((nx, ny)))
                    {
                        continue;
                    }

                    if (map.CanSpawnMobile(nx, ny, p.Z - MaxStep, p.Z + MaxStep, false, false, out var nz, out _))
                    {
                        seen.Add((nx, ny));
                        queue.Enqueue(new Point3D(nx, ny, nz));
                    }
                }
            }
        }

        return seen.Count;
    }

    // ---------------------------------------------------------------- the built-in pool

    /// <summary>
    /// The first pool (cc-P192), written to the file on first start. Region names are pinned regions.json's, loaded on
    /// Endless Journey. Access is the author's reading of the world, not measured: the notes say which to check in game.
    /// </summary>
    public static readonly List<FountainPoolEntry> Defaults =
    [
        new() { Key = "covetous", Map = "Trammel", Region = "Covetous", Why = "Deep dungeon behind Vesper's mountains; on foot, through the levels." },
        new() { Key = "destard", Map = "Trammel", Region = "Destard", Why = "Dragon dungeon west of Trinsic; on foot, but the drakes see you first." },
        new() { Key = "shame", Map = "Trammel", Region = "Shame", Why = "Elemental dungeon west of Britain; long, on foot." },
        new() { Key = "wrong", Map = "Trammel", Region = "Wrong", Why = "Prison dungeon north of Yew; on foot, mazy." },
        new() { Key = "hythloth", Map = "Trammel", Region = "Hythloth", Why = "Daemon dungeon; its own isle, reached through Hythloth's entrance (check: on foot or by boat).", Access = "boat" },
        new() { Key = "fire", Map = "Trammel", Region = "Fire", Why = "Fire dungeon on Fire Island; boat to the island, then on foot.", Access = "boat" },
        new() { Key = "ice", Map = "Trammel", Region = "Ice", Why = "Ice dungeon on Dagger Isle; boat, then on foot.", Access = "boat" },
        new() { Key = "terathan-keep", Map = "Trammel", Region = "Terathan Keep", Why = "Terathan fortress past the swamps; on foot, fought through." },
        new() { Key = "orc-cave", Map = "Trammel", Region = "Orc Cave", Why = "Three-level orc warren; on foot, crowded." },
        new() { Key = "ice-isle-cave", Map = "Trammel", Region = "Ice Isle Cave 2", Why = "A small cave on Ice Isle; an island on purpose.", Access = "boat" },
        new() { Key = "avatar-isle-cave", Map = "Trammel", Region = "Avatar Isle Cave", Why = "The cave on Avatar Isle; an island on purpose.", Access = "boat" },
        new() { Key = "north-territory-mine", Map = "Trammel", Region = "North Territory Mine 2", Why = "A forgotten mine in the north; on foot, nobody goes there." },
        new() { Key = "mt-kendall", Map = "Trammel", Region = "Mt Kendall", Why = "Mine tunnels under Mt Kendall by Minoc; on foot." },
        new() { Key = "spectre", Map = "Ilshenar", Region = "Spectre Dungeon", Why = "Ilshenar's undead dungeon; moongate, then a long walk." },
        new() { Key = "sorcerers", Map = "Ilshenar", Region = "Sorcerer's Dungeon", Why = "Five levels of casters; moongate and on foot." },
        new() { Key = "ancient-lair", Map = "Ilshenar", Region = "Ancient Lair", Why = "The ancient wyrm's lair; on foot, very dangerous." },
        new() { Key = "labyrinth", Map = "Malas", Region = "Labyrinth", Why = "The Labyrinth under Malas; a maze by design (check its entry in game)." },
        new() { Key = "yomotsu", Map = "Malas", Region = "Yomotsu Mines", Why = "The Yomotsu mines; on foot, through the elder gazers." },
        new() { Key = "peak-kendall", Map = "Trammel", Rects = [[2576, 360, 2600, 384]], Access = "climbing", Why = "A summit above Minoc (coordinates to check in game): only climbing reaches it." },
        new() { Key = "peak-serpent", Map = "Trammel", Rects = [[1680, 600, 1704, 624]], Access = "climbing", Why = "A crag in the Serpent's Spine (coordinates to check in game): only climbing reaches it." }
    ];
}
