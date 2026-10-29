// SosGatesVerification.cs
//
// cc-P679 (SoS swap S4): the public moongates, the stock teleporters and the staff go-menu on the Siege of Sosaria land.
// Notes: shard-migration notes/cc-P679-sos-s4-gates-and-teleporters.md. The test host has no map files, so these facts
// hold the code and the shipped files to the decided spots; the docs repo's tools/tests/test_s4_gates.py holds the
// spots to the SoS land itself (every gate and every teleporter end stands, and every source fires).
//
// Facts:
//   G1. PMList's Trammel gates: each of the author's stone circles is centred on the stock tile at a new height, so a
//       gate keeps its x, y and takes the circle's floor; New Haven's gate is HavenSpots.PublicMoongate (cc-P678).
//   G2. PMList's Felucca gates: the same eight, and Buccaneer's Den where stock has it.
//   G3. The author's own gate spots (Hammerhill, Kameron Kove, Dragonstorm) are not public gates at 1.0 (P669 5b).
//   T1. SosTeleporters.Merge keeps pinned's entries that touch neither Trammel nor Felucca, in order, then ours; an entry
//       of ours with no end on either facet is refused.
//   T2. The shipped teleporters-sos.json: 817 entries, each with an end on Trammel or Felucca, and none of the entries
//       the review dropped (Blackthorn's entrance, Bucs' Den's lost ladders, Felucca's map0-only land).
//   L1. The shipped go-menu: trammel.json and felucca.json are the author's 410 spots, named for their facet.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Json;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

// In the collection that sets the test host's maps up: PMList's static fields read Map.Trammel (Magincia's Z), and a
// first touch before the maps exist fails its type initializer for the whole run.
[Collection("Sequential UOContent Tests")]
public class SosGatesVerification
{
    private static string? Find(params string[] rel)
    {
        // Never Core.BaseDirectory (see SosTrammelRegionsVerification.Find): the test assembly's folder, then up to a
        // Distribution/Data folder.
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

    private static Point3D Gate(PMList list, int number) => Assert.Single(list.Entries, e => e.Number == number).Location;

    private static readonly (int Number, Point3D Spot)[] SharedGates =
    [
        (1012003, new Point3D(4467, 1283, 23)), // Moonglow
        (1012004, new Point3D(1336, 1997, 15)), // Britain: the rock ring round a tree
        (1012005, new Point3D(1499, 3771, 5)),  // Jhelom
        (1012006, new Point3D(771, 752, 15)),   // Yew
        (1012007, new Point3D(2701, 692, 20)),  // Minoc
        (1012008, new Point3D(1828, 2948, -16)), // Trinsic
        (1012009, new Point3D(643, 2067, 5))    // Skara Brae
    ];

    [Fact]
    public void G1_TrammelsGatesStandInTheAuthorsCircles()
    {
        Assert.Equal(9, PMList.Trammel.Entries.Length);
        foreach (var (number, spot) in SharedGates)
        {
            Assert.Equal(spot, Gate(PMList.Trammel, number));
        }

        var magincia = Gate(PMList.Trammel, 1012010); // its Z is read off the map, as pinned does
        Assert.Equal((3563, 2139), (magincia.X, magincia.Y));
        Assert.Equal(HavenSpots.PublicMoongate, Gate(PMList.Trammel, 1078098));
        Assert.Equal(new Point3D(3696, 2570, 14), Gate(PMList.Trammel, 1078098));
    }

    [Fact]
    public void G2_FeluccasGatesAreTheSameAndBucsDen()
    {
        Assert.Equal(9, PMList.Felucca.Entries.Length);
        foreach (var (number, spot) in SharedGates)
        {
            Assert.Equal(spot, Gate(PMList.Felucca, number));
        }

        Assert.Equal(new Point3D(2711, 2234, 0), Gate(PMList.Felucca, 1019001)); // Buccaneer's Den
        Assert.DoesNotContain(PMList.Felucca.Entries, e => e.Number == 1078098);
    }

    [Fact]
    public void G3_TheAuthorsOwnGateSpotsAreNotPublicGates()
    {
        (int X, int Y)[] his = [(1541, 1081), (1353, 2451), (3186, 3884)]; // Hammerhill, Kameron Kove, Dragonstorm
        foreach (var list in new[] { PMList.Trammel, PMList.Felucca })
        {
            foreach (var e in list.Entries)
            {
                Assert.DoesNotContain((e.Location.X, e.Location.Y), his);
            }
        }
    }

    private static TeleporterDefinition Tel(Map from, int x, Map to, int y) =>
        new() { Source = new WorldLocation(x, 1, 0, from), Destination = new WorldLocation(y, 2, 0, to) };

    [Fact]
    public void T1_MergeReplacesOnlyTheEntriesOnTheSosLand()
    {
        var pinned = new List<TeleporterDefinition>
        {
            Tel(Map.Ilshenar, 1, Map.Ilshenar, 2), Tel(Map.Trammel, 3, Map.Trammel, 4), Tel(Map.Malas, 5, Map.Tokuno, 6),
            Tel(Map.TerMur, 7, Map.Felucca, 8), Tel(Map.Felucca, 9, Map.TerMur, 10)
        };
        var ours = new List<TeleporterDefinition> { Tel(Map.Trammel, 30, Map.Trammel, 40), Tel(Map.Felucca, 90, Map.TerMur, 100) };

        var merged = SosTeleporters.Merge(pinned, ours);

        Assert.Equal([1, 5, 30, 90], merged.Select(d => d.Source.X).ToArray());
        Assert.Equal(5, pinned.Count);
        Assert.Throws<InvalidDataException>(() => SosTeleporters.Merge(pinned, [Tel(Map.Ilshenar, 1, Map.Malas, 2)]));
    }

    [Fact]
    public void T2_TheShippedTeleportersAreOnTheSosLandWithoutTheDroppedOnes()
    {
        var path = Find("ShatteredLegacy", "teleporters-sos.json");
        Assert.NotNull(path);
        var ours = JsonConfig.Deserialize<List<TeleporterDefinition>>(path!)!;

        Assert.Equal(817, ours.Count);
        Assert.All(ours, d => Assert.True(SosTeleporters.OnSosLand(d), d.ToString()));

        bool At(WorldLocation w, Map map, int x, int y) => w.Map == map && w.X == x && w.Y == y;
        Assert.DoesNotContain(ours, d => At(d.Source, Map.Trammel, 1477, 1471) || At(d.Source, Map.Trammel, 6440, 2677)); // Blackthorn
        Assert.DoesNotContain(ours, d => At(d.Source, Map.Trammel, 2685, 2063)); // Bucs' Den's lost ladder
        Assert.DoesNotContain(ours, d => d.Source.Map == Map.Felucca && d.Source.X >= 6900 || d.Destination.Map == Map.Felucca && d.Destination.X >= 6900);

        // The Lost Lands pass, lowered by the author from Z ~98 to -5, keeps its way to Britain's cave.
        Assert.Contains(ours, d => At(d.Source, Map.Trammel, 5126, 3143) && d.Source.Z == -5 && At(d.Destination, Map.Trammel, 5849, 432));
    }

    [Fact]
    public void L1_TheGoMenuIsTheAuthorsFourHundredAndTenSpots()
    {
        static int Count(GoCategory c) => (c.Locations?.Length ?? 0) + (c.Categories?.Sum(Count) ?? 0);

        foreach (var (file, name) in new[] { ("trammel.json", "Trammel"), ("felucca.json", "Felucca") })
        {
            var path = Find("ShatteredLegacy", "Locations", file);
            Assert.NotNull(path);
            var root = JsonConfig.Deserialize<GoCategory>(path!)!;
            Assert.Equal(name, root.Name);
            Assert.Equal(410, Count(root));

            var haven = root.Categories!.Single(c => c.Name == "Towns").Categories!.Single(c => c.Name == "Haven");
            Assert.Contains(haven.Locations!, l => l.Name == "Docks" && l.Location == new Point3D(3650, 2653, 0));
        }
    }
}
