// SosTrammelRegionsVerification.cs
//
// cc-P676 (SoS swap S3): Trammel's regions on the Siege of Sosaria land. Data/ShatteredLegacy/regions-trammel.json
// (made by shard-migration notes/cc-P676-tools/gen_regions.py) takes the place of pinned's Trammel regions
// (RegionJsonSerializer-shard-maps.patch, MergeShardRegions); every other map keeps pinned's. Notes: shard-migration
// notes/cc-P676-sos-trammel-regions.md. The land facts (every rect on dry SoS land or its own water, no guard zone over
// open wild) need the map and are in shard-migration tools/tests/test_regions_sos.py.
//
// Facts, from the shipped files (the test host registers no regions, and these facts register none):
//   R1. MergeShardRegions puts the shard file's regions where the named map's first pinned region stood and drops
//       pinned's of that map; every other map's are pinned's, in order.
//   R2. The shipped file: Trammel only, pinned's region types only, names unique, each parent before its children,
//       every rect inside the facet and not empty.
//   R3. Every Trammel region name our shipped code looks up by name is there (21 names; namelookups.py).
//   R4. Guards: a guarded region is one of pinned's Trammel guarded regions or one of the author's 7 new towns (and
//       Lord British's castle); the 7 are guarded TownRegions; no wild area, isle or unfinished land is guarded.
//   R5. The author's dungeons are DungeonRegions; S14's 4 unfinished areas are no-housing regions over their rects.
//   R6. Pinned's regions.json merged with the file: Trammel's regions are the file's, Felucca's and the rest pinned's.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Xunit;

namespace ShatteredLegacy.Tests;

public class SosTrammelRegionsVerification
{
    private const string FileName = "regions-trammel.json";

    private static readonly string[] Lookups =
    [
        "Avatar Isle Cave", "Blighted Grove", "Covetous", "Despise", "Destard", "Fire", "Haven", "Haven Island",
        "Hythloth", "Ice", "Ice Isle Cave 2", "Jail", "Mt Kendall", "New Haven", "New Haven Moongate",
        "North Territory Mine 2", "Orc Cave", "Sanctuary", "Shame", "Terathan Keep", "Wrong"
    ];

    private static readonly string[] NewTowns =
        ["Valunstead", "Hammerhill", "Kameron Kove", "Elmhaven", "Kettleburg", "Fort Yondall", "Elandrim Nur Shaz"];

    private static readonly string[] PinnedTypes =
    [
        "BaseRegion", "TownRegion", "GuardedRegion", "DungeonRegion", "NoHousingRegion", "JailRegion", "GreenAcresRegion"
    ];

    private static readonly HashSet<string> Guarded = ["TownRegion", "GuardedRegion"];

    // S14's list (shard-migration notes/cc-P673-sos-swap-plan.md section 2), x1, y1, x2, y2 inclusive
    private static readonly (string Name, int X1, int Y1, int X2, int Y2)[] Unfinished =
    [
        ("Unfinished land: the rooster island", 2840, 1324, 3039, 1523),
        ("Unfinished land: the north-east snow isles", 4224, 256, 4479, 767),
        ("Unfinished land: the bare land east of Haven", 3072, 2304, 3199, 2559),
        ("Unfinished land: the cells round Echoback and Darkfathom", 4096, 2560, 4223, 2815)
    ];

    private static string? Find(params string[] rel)
    {
        // Never Core.BaseDirectory: it caches its first answer, and read before a fixture has set the application
        // assembly it caches "" for every test after (Server.dll then loads as a relative path). The test assembly's folder.
        var root = AppContext.BaseDirectory;
        var beside = Path.Combine([root, "Data", .. rel]);
        if (File.Exists(beside))
        {
            return beside;
        }

        for (var d = new DirectoryInfo(root); d != null; d = d.Parent)
        {
            var candidate = Path.Combine([d.FullName, "Distribution", "Data", .. rel]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static JsonArray Parse(string path) =>
        JsonNode.Parse(
            File.ReadAllText(path),
            documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }
        )!.AsArray();

    private static List<JsonObject> Shipped()
    {
        var path = Find("ShatteredLegacy", FileName);
        Assert.NotNull(path);
        return Parse(path!).Select(n => n!.AsObject()).ToList();
    }

    private static List<JsonObject> PinnedOf(string map)
    {
        var path = Find("regions.json");
        Assert.NotNull(path);
        return Parse(path!).Select(n => n!.AsObject()).Where(r => (string)r["Map"]! == map).ToList();
    }

    private static string? Name(JsonObject r) => (string?)r["Name"];
    private static string Type(JsonObject r) => (string?)r["$type"] ?? "Region";
    private static string? ParentName(JsonObject r) => (string?)r["Parent"]?["Name"];

    private static IEnumerable<(int X1, int Y1, int X2, int Y2)> Rects(JsonObject r) =>
        r["Area"]!.AsArray().Select(a => ((int)a!["x1"]!, (int)a["y1"]!, (int)a["x2"]!, (int)a["y2"]!));

    private static JsonObject Region(string map, string name, string? parent = null)
    {
        var r = new JsonObject { ["Map"] = map, ["Name"] = name, ["Area"] = new JsonArray() };
        if (parent != null)
        {
            r["Parent"] = new JsonObject { ["Name"] = parent, ["Map"] = map };
        }

        return r;
    }

    [Fact]
    public void R1_MergeReplacesOnlyTheMapsTheShardFileNames()
    {
        var pinned = new JsonArray(
            Region("Felucca", "A"), Region("Trammel", "B"), Region("Trammel", "C", "B"), Region("Ilshenar", "D")
        );
        var ours = new JsonArray(Region("Trammel", "X"), Region("Trammel", "Y", "X"));

        var merged = RegionJsonSerializer.MergeShardRegions(pinned, [ours]);

        Assert.Equal(
            ["Felucca A", "Trammel X", "Trammel Y", "Ilshenar D"],
            merged.Select(n => $"{(string)n!["Map"]!} {(string)n["Name"]!}").ToArray()
        );
        Assert.Equal(4, pinned.Count); // the inputs are not changed
    }

    [Fact]
    public void R2_TheShippedFileIsTrammelOnlyInPinnedsTypesParentsFirst()
    {
        var regions = Shipped();
        Assert.True(regions.Count > 145, $"{regions.Count} regions");

        var seen = new HashSet<string>();
        foreach (var r in regions)
        {
            var what = Name(r) ?? "(unnamed)";
            Assert.Equal("Trammel", (string)r["Map"]!);
            Assert.Contains(Type(r), PinnedTypes);
            if (ParentName(r) is { } parent)
            {
                Assert.True(seen.Contains(parent), $"{what}: its parent {parent} comes later or is missing");
            }

            if (Name(r) is { } name)
            {
                Assert.True(seen.Add(name), $"{name} twice");
            }

            Assert.NotEmpty(Rects(r));
            foreach (var (x1, y1, x2, y2) in Rects(r))
            {
                Assert.True(x1 >= 0 && y1 >= 0 && x2 <= 7168 && y2 <= 4096 && x2 > x1 && y2 > y1, $"{what}: {x1},{y1},{x2},{y2}");
            }
        }
    }

    [Fact]
    public void R3_EveryRegionOurCodeLooksUpByNameIsThere()
    {
        var names = Shipped().Select(Name).Where(n => n != null).ToHashSet();
        var missing = Lookups.Where(n => !names.Contains(n)).ToArray();
        Assert.True(missing.Length == 0, "missing: " + string.Join(", ", missing));
    }

    [Fact]
    public void R4_GuardsAreOnlyTownsAndTheSevenNewTownsAreGuarded()
    {
        var regions = Shipped();
        var pinnedGuarded = PinnedOf("Trammel").Where(r => Guarded.Contains(Type(r))).Select(Name).ToHashSet();
        foreach (var r in regions.Where(r => Guarded.Contains(Type(r))))
        {
            var name = Name(r);
            Assert.True(
                pinnedGuarded.Contains(name) || NewTowns.Contains(name) || name == "Lord British Castle",
                $"{name ?? "(unnamed)"} is guarded"
            );
        }

        foreach (var town in NewTowns)
        {
            var r = regions.Single(x => Name(x) == town);
            Assert.Equal("TownRegion", Type(r));
            Assert.Null(r["Parent"]);
        }

        foreach (var r in regions.Where(r => Name(r) is { } n && (n.StartsWith("an isle at ") || n.StartsWith("Unfinished land: "))))
        {
            Assert.DoesNotContain(Type(r), Guarded);
        }

        Assert.Equal("BaseRegion", Type(regions.Single(r => Name(r) == "Glimmerwood")));
        Assert.Equal("BaseRegion", Type(regions.Single(r => Name(r) == "Nujelm Desert")));
    }

    [Fact]
    public void R5_TheAuthorsDungeonsAreDungeonRegionsAndTheUnfinishedLandHasNoHousing()
    {
        var regions = Shipped();
        foreach (var dungeon in new[] { "Azurite Caverns", "Stone Burrow Mines", "Wylarn Caverns", "Hammerhill Sewers", "Khaldun" })
        {
            Assert.Equal("DungeonRegion", Type(regions.Single(r => Name(r) == dungeon)));
        }

        foreach (var (name, x1, y1, x2, y2) in Unfinished)
        {
            var r = regions.Single(x => Name(x) == name);
            Assert.Equal("NoHousingRegion", Type(r));
            Assert.Equal([(x1, y1, x2 + 1, y2 + 1)], Rects(r).ToArray());
        }
    }

    [Fact]
    public void R6_PinnedMergedWithTheFileHasTheFilesTrammelAndPinnedsOtherMaps()
    {
        var path = Find("regions.json");
        Assert.NotNull(path);
        var pinned = Parse(path!);
        var ours = Parse(Find("ShatteredLegacy", FileName)!);

        var merged = RegionJsonSerializer.MergeShardRegions(pinned, [ours]);

        string Key(JsonNode? n) => n!.ToJsonString();
        Assert.Equal(ours.Select(Key), merged.Where(n => (string)n!["Map"]! == "Trammel").Select(Key));
        Assert.Equal(
            pinned.Where(n => (string)n!["Map"]! != "Trammel").Select(Key),
            merged.Where(n => (string)n!["Map"]! != "Trammel").Select(Key)
        );
    }
}
