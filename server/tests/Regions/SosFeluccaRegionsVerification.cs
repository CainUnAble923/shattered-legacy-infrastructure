// SosFeluccaRegionsVerification.cs
//
// cc-P686 (SoS swap S13): Felucca's regions on the Siege of Sosaria land. Felucca reads Trammel's files since P675, so
// Data/ShatteredLegacy/regions-felucca.json (made by shard-migration notes/cc-P686-tools/gen_felucca.py from S3's
// Trammel work) takes the place of pinned's Felucca regions, as regions-trammel.json does Trammel's
// (RegionJsonSerializer-shard-maps.patch reads every regions-*.json). Notes: shard-migration
// notes/cc-P686-felucca-regions.md. The land facts (every rect on dry SoS land or its own water, no guard zone over
// open wild, no champion area in a guard zone) need the map and are in shard-migration tools/tests/test_regions_sos.py.
//
// S3's facts (SosTrammelRegionsVerification R2 to R6), for Felucca, from the shipped files:
//   F1. The shipped file: Felucca only, pinned's region types only, names unique, each parent before its children,
//       every rect inside the facet and not empty.
//   F2. Every Felucca region name our shipped code looks up by name is there (17 names; gen_felucca.py looked_up).
//   F3. Felucca's rules: a guarded region is one of pinned's Felucca guarded regions or one of the author's 7 new towns
//       (and Lord British's castle); the 7 are guarded TownRegions; Buccaneer's Den keeps its guards off; no wild
//       area, isle or unfinished land is guarded.
//   F4. The author's dungeons are DungeonRegions; S14's 4 unfinished areas are no-housing regions over their rects.
//   F5. The towns match: every region named on both facets has the same rects on both, and Ocllo (Felucca's name for
//       the pre-UOML Haven town) has pinned Trammel Haven's rects, the ones Trammel's New Haven is registered over.
//   F6. Pinned's regions.json merged with both files, as the loader does: Felucca's and Trammel's regions are the
//       files', the other maps' pinned's.

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

public class SosFeluccaRegionsVerification
{
    private const string FileName = "regions-felucca.json";
    private const string TrammelFileName = "regions-trammel.json";

    private static readonly string[] Lookups =
    [
        "Avatar Isle Cave", "Blighted Grove", "Covetous", "Despise", "Destard", "Fire", "Hythloth", "Ice",
        "Ice Isle Cave 2", "Jail", "Mt Kendall", "North Territory Mine 2", "Orc Cave", "Sanctuary", "Shame",
        "Terathan Keep", "Wrong"
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
        // Never Core.BaseDirectory (SosTrammelRegionsVerification says why): the test assembly's folder.
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

    private static List<JsonObject> ShippedFile(string file)
    {
        var path = Find("ShatteredLegacy", file);
        Assert.NotNull(path);
        return Parse(path!).Select(n => n!.AsObject()).ToList();
    }

    private static List<JsonObject> Shipped() => ShippedFile(FileName);

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

    [Fact]
    public void F1_TheShippedFileIsFeluccaOnlyInPinnedsTypesParentsFirst()
    {
        var regions = Shipped();
        Assert.True(regions.Count > PinnedOf("Felucca").Count, $"{regions.Count} regions");

        var seen = new HashSet<string>();
        foreach (var r in regions)
        {
            var what = Name(r) ?? "(unnamed)";
            Assert.Equal("Felucca", (string)r["Map"]!);
            Assert.Contains(Type(r), PinnedTypes);
            if (ParentName(r) is { } parent)
            {
                Assert.True(seen.Contains(parent), $"{what}: its parent {parent} comes later or is missing");
                Assert.Equal("Felucca", (string)r["Parent"]!["Map"]!);
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
    public void F2_EveryRegionOurCodeLooksUpByNameIsThere()
    {
        var names = Shipped().Select(Name).Where(n => n != null).ToHashSet();
        var missing = Lookups.Where(n => !names.Contains(n)).ToArray();
        Assert.True(missing.Length == 0, "missing: " + string.Join(", ", missing));
    }

    [Fact]
    public void F3_FeluccasRulesStayGuardsOnlyOnTownsAndBucsDenUnguarded()
    {
        var regions = Shipped();
        var pinnedGuarded = PinnedOf("Felucca").Where(r => Guarded.Contains(Type(r))).Select(Name).ToHashSet();
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
            Assert.Null(r["GuardsDisabled"]);
        }

        var bucs = regions.Single(r => Name(r) == "Buccaneer's Den");
        Assert.Equal("TownRegion", Type(bucs));
        Assert.True((bool?)bucs["GuardsDisabled"] == true, "Buccaneer's Den stays unguarded");
        foreach (var town in new[] { "Britain", "Minoc", "Vesper", "Yew", "Moonglow", "Trinsic", "Ocllo" })
        {
            Assert.Contains(Type(regions.Single(r => Name(r) == town)), Guarded);
            Assert.Null(regions.Single(r => Name(r) == town)["GuardsDisabled"]);
        }

        foreach (var r in regions.Where(r => Name(r) is { } n && (n.StartsWith("an isle at ") || n.StartsWith("Unfinished land: "))))
        {
            Assert.DoesNotContain(Type(r), Guarded);
        }

        Assert.Equal("BaseRegion", Type(regions.Single(r => Name(r) == "Glimmerwood")));
        Assert.Equal("BaseRegion", Type(regions.Single(r => Name(r) == "Nujelm Desert")));
    }

    [Fact]
    public void F4_TheAuthorsDungeonsAreDungeonRegionsAndTheUnfinishedLandHasNoHousing()
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
    public void F5_TheTownsMatchTrammels()
    {
        var felucca = Shipped().Where(r => Name(r) != null).ToDictionary(r => Name(r)!);
        var trammel = ShippedFile(TrammelFileName).Where(r => Name(r) != null).ToDictionary(r => Name(r)!);

        var differ = felucca.Keys.Where(n => trammel.ContainsKey(n) && !Rects(felucca[n]).SequenceEqual(Rects(trammel[n])))
            .Order().ToArray();
        Assert.True(differ.Length == 0, "different rects on the two facets: " + string.Join(", ", differ));
        foreach (var town in NewTowns.Concat(["Britain", "Minoc", "Vesper", "Yew", "Moonglow", "Magincia", "Buccaneer's Den"]))
        {
            Assert.True(trammel.ContainsKey(town) && felucca.ContainsKey(town), $"{town} on both facets");
        }

        var haven = PinnedOf("Trammel").Single(r => Name(r) == "Haven");
        Assert.Equal(Rects(haven).ToArray(), Rects(felucca["Ocllo"]).ToArray());
    }

    [Fact]
    public void F6_PinnedMergedWithBothFilesHasTheFilesFeluccaAndTrammelAndPinnedsOtherMaps()
    {
        var path = Find("regions.json");
        Assert.NotNull(path);
        var pinned = Parse(path!);
        var felucca = Parse(Find("ShatteredLegacy", FileName)!);
        var trammel = Parse(Find("ShatteredLegacy", TrammelFileName)!);

        // the loader's order: Directory.GetFiles(...).Order(), so regions-felucca.json before regions-trammel.json
        var merged = RegionJsonSerializer.MergeShardRegions(pinned, [felucca, trammel]);

        string Key(JsonNode? n) => n!.ToJsonString();
        IEnumerable<string> Of(JsonArray a, string map) => a.Where(n => (string)n!["Map"]! == map).Select(Key);
        Assert.Equal(felucca.Select(Key), Of(merged, "Felucca"));
        Assert.Equal(trammel.Select(Key), Of(merged, "Trammel"));
        Assert.Equal(
            pinned.Where(n => (string)n!["Map"]! is not ("Felucca" or "Trammel")).Select(Key),
            merged.Where(n => (string)n!["Map"]! is not ("Felucca" or "Trammel")).Select(Key)
        );
    }
}
