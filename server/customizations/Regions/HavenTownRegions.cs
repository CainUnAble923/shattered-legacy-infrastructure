// cc-P678 (SoS swap S7, part 3): New Haven's regions over the author's Haven town.
//
// On the Siege of Sosaria map New Haven is the author's Haven town, OSI's pre-UOML Haven rebuilt, and the ML plaza that
// pinned's "New Haven" region covers (3416-3545, 2480-2648) is forest. Pinned's regions.json already draws the town: the
// TownRegion "Haven" and its 23 shop regions (the Second Bank of Haven, the Healers of Haven, the docks and the rest), but
// they end at SE (MaxExpansion), so on this shard's expansion RegionJsonSerializer never registers them, and it registers
// the ML "New Haven" over the forest instead.
//
// So, every start after the regions load (Initialize, as ApprenticeTrainingRegion and the Burrows do):
//   1. pinned's ML "New Haven" with every region under it, and its "New Haven Moongate", are unregistered;
//   2. "New Haven" is registered again, a guarded TownRegion with pinned "Haven"'s rectangles, under "Haven Island", with
//      the ML region's rune name and music and HavenSpots.Start as its go location, so every lookup by the name
//      "New Haven" (the rune, the staff travel rows, the town rules) finds the new town;
//   3. pinned "Haven"'s shop regions are registered under it with their own names and settings, read from regions.json.
//      "The Gentle Rest Inn in New Haven" is left out: pinned draws it on the ML plaza (3493-3510, 2513-2529), not in the
//      town. In its place the inn building itself (3664-3680, 2608-2624, where pinned's pre-UOML InnKeeper spawns at
//      3671,2615) becomes "the New Haven Inn", the ML inn's name, with the stock inn's rune name and no logout delay, so the
//      townsfolk's innkeeper rules (ClusterFTalkRules) and logging out at the inn work as before.
// The region data is read from the shipped regions.json, not copied, so it is pinned's to the byte.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Server.Regions;

public static class HavenTownRegions
{
    public const string TownName = "New Haven";

    /// <summary>The pinned region whose rectangles and shops become New Haven's.</summary>
    public const string StockTownName = "Haven";

    /// <summary>Pinned shop regions under "Haven" that are not in the town (drawn on the ML plaza).</summary>
    public static readonly HashSet<string> LeftOut = ["The Gentle Rest Inn in New Haven"];

    /// <summary>The inn: the ML region's name over the pre-UOML inn building (x 3664-3680, y 2608-2624 inclusive).</summary>
    public const string InnName = "the New Haven Inn";

    public static readonly Rectangle3D InnArea = new(new Point3D(3664, 2608, -128), new Point3D(3681, 2625, 127));

    public static TownRegion? Town { get; private set; }

    public static List<Region> Shops { get; } = [];

    public static string RegionsFile => Path.Combine(Core.BaseDirectory, "Data", "regions.json");

    public static void Initialize()
    {
        if (Town != null || Map.Trammel == null)
        {
            return;
        }

        var old = Region.Find(TownName, Map.Trammel);
        var rune = (old as BaseRegion)?.RuneName ?? TownName;
        var music = old?.Music ?? MusicName.Ocllo;

        // 1. the ML plaza's regions, children first
        var gone = Region.Regions.Where(r => r.Map == Map.Trammel && (r.IsPartOf(TownName) || r.Name == "New Haven Moongate"))
            .OrderByDescending(r => r.ChildLevel)
            .ToList();
        foreach (var r in gone)
        {
            r.Unregister();
        }

        // 2. and 3. from pinned's "Haven"
        var stock = ReadStock();
        var town = stock.FirstOrDefault(s => s.Name == StockTownName);
        if (town == null)
        {
            return;
        }

        var island = Region.Find("Haven Island", Map.Trammel);
        Town = new TownRegion(TownName, Map.Trammel, island, town.Area)
        {
            RuneName = rune,
            Music = music,
            GoLocation = Tutorial.HavenSpots.Start
        };
        Town.Register();

        foreach (var shop in stock.Where(s => s.Parent == StockTownName && !LeftOut.Contains(s.Name)))
        {
            var r = new NoHousingRegion(shop.Name, Map.Trammel, Town, shop.Area)
            {
                RuneName = shop.RuneName,
                NoLogoutDelay = shop.NoLogoutDelay
            };
            if (shop.Music is { } m)
            {
                r.Music = m;
            }

            r.Register();
            Shops.Add(r);
        }

        var inn = new NoHousingRegion(InnName, Map.Trammel, Town, InnArea)
        {
            RuneName = "The Gentle Rest Inn in New Haven",
            NoLogoutDelay = true
        };
        inn.Register();
        Shops.Add(inn);
    }

    public sealed record StockRegion(string Name, string? Parent, string? RuneName, bool NoLogoutDelay, MusicName? Music, Rectangle3D[] Area);

    /// <summary>Pinned regions.json's Trammel "Haven" and the regions whose parent it is, in file order.</summary>
    public static List<StockRegion> ReadStock()
    {
        var list = new List<StockRegion>();
        using var doc = JsonDocument.Parse(File.ReadAllText(RegionsFile));
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            if (Str(e, "Map") != "Trammel" || Str(e, "Name") is not { } name)
            {
                continue;
            }

            var parent = e.TryGetProperty("Parent", out var p) ? Str(p, "Name") : null;
            if (name != StockTownName && parent != StockTownName)
            {
                continue;
            }

            // as pinned's Rectangle3DConverter reads x1/y1/x2/y2 with no z: z -128 to 127, the end exclusive
            var area = e.GetProperty("Area").EnumerateArray()
                .Select(a => new Rectangle3D(
                    new Point3D(a.GetProperty("x1").GetInt32(), a.GetProperty("y1").GetInt32(), -128),
                    new Point3D(a.GetProperty("x2").GetInt32(), a.GetProperty("y2").GetInt32(), 127)))
                .ToArray();
            MusicName? music = Str(e, "Music") is { } m && Enum.TryParse<MusicName>(m, out var mn) ? mn : null;
            var noLogout = e.TryGetProperty("NoLogoutDelay", out var nl) && nl.GetBoolean();
            list.Add(new StockRegion(name, parent, Str(e, "RuneName"), noLogout, music, area));
        }

        return list;
    }

    private static string? Str(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
