using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server.Engines.Spawners;

namespace Server;

/// <summary>
/// Shattered Legacy (cc-P42 Parts B and J). The spawners pinned's own spawn files place, read from the files the
/// server ships (Core.BaseDirectory/Data/Spawns), so a cleanup can tell a stock spawner from a stray one without a
/// hand-kept list. The folders are the two this shard imports: post-uoml/** and shared/** (never uoml/**, P32).
/// </summary>
public static class ClusterFStockSpawnData
{
    public static readonly string[] ShardFolders = ["post-uoml", "shared"];

    public sealed record StockSpawner(string File, Guid Guid, Point3D Location, Map Map, string[] Names)
    {
        /// <summary>
        /// The world spawner is this stock one: the same guid (the importer keeps the data's guid), or, for a spawner
        /// placed before guids were kept, the same x,y,z and the same entry names.
        /// </summary>
        public bool Matches(BaseSpawner spawner)
        {
            if (spawner.Guid == Guid)
            {
                return true;
            }

            if (spawner.Map != Map || spawner.Location != Location || spawner.Entries.Count != Names.Length)
            {
                return false;
            }

            var names = spawner.Entries.Select(e => e.SpawnedName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
            return names.SequenceEqual(Names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public static string SpawnsDirectory => Path.Combine(Core.BaseDirectory, "Data", "Spawns");

    /// <summary>Every spawner on <paramref name="map"/> in the given files (paths relative to Data/Spawns, globs allowed
    /// only as "folder/**").</summary>
    public static List<StockSpawner> Load(Map map, params string[] folders)
    {
        var result = new List<StockSpawner>();
        foreach (var folder in folders)
        {
            var dir = Path.Combine(SpawnsDirectory, folder);
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).Order())
            {
                result.AddRange(LoadFile(file, map));
            }
        }

        return result;
    }

    /// <summary>Every spawner on <paramref name="map"/> in one file, by its path relative to Data/Spawns.</summary>
    public static List<StockSpawner> LoadRelative(string relativePath, Map map)
    {
        var file = Path.Combine(SpawnsDirectory, relativePath);
        return File.Exists(file) ? LoadFile(file, map) : [];
    }

    private static List<StockSpawner> LoadFile(string file, Map map)
    {
        var result = new List<StockSpawner>();
        var rel = Path.GetRelativePath(SpawnsDirectory, file).Replace('\\', '/');

        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var s in doc.RootElement.EnumerateArray())
        {
            if (!s.TryGetProperty("map", out var mapEl) || Map.Parse(mapEl.GetString()) != map)
            {
                continue;
            }

            if (!s.TryGetProperty("guid", out var guidEl) || !Guid.TryParse(guidEl.GetString(), out var guid))
            {
                continue;
            }

            var loc = s.GetProperty("location");
            var location = new Point3D(loc[0].GetInt32(), loc[1].GetInt32(), loc[2].GetInt32());

            var names = new List<string>();
            if (s.TryGetProperty("entries", out var entries))
            {
                foreach (var e in entries.EnumerateArray())
                {
                    names.Add(e.GetProperty("name").GetString());
                }
            }

            result.Add(new StockSpawner(rel, guid, location, map, names.ToArray()));
        }

        return result;
    }
}
