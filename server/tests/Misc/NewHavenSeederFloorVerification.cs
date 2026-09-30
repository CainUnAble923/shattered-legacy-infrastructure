// NewHavenSeederFloorVerification.cs
//
// cc-P17 PT-05: the Anatomy quest giver (Andreas Vesalius) stood 5 below the Healer's Hall floor,
// reachable only with the client's show-all-names key. He was one of 18 ClusterFNewHavenSeeder
// NPCs standing inside a floor, table, wall, water or rock (cc-P15 section 10.1). Cause: an entry
// without a z took Map.GetAverageZ (pinned Projects/Server/Maps/Map.cs:243-285), the land height,
// which is under a building's floor. Notes in shard-migration notes/cc-P17-playtest-bugs-1.md.
//
// The test host has no map files, so no fact here can ask the map where the floor is. The floors
// below were measured offline by a port of pinned Map.CanFit (Map.cs:925-1010, height 16) over the
// client map, statics and pinned's New Haven decoration, and every one fits. What the facts pin:
//   1. No entry takes its z from the land height any more, and every entry is on its measured floor.
//   2. World-load repair leaves an NPC on its pre-PT-05 tile alone (an existing world is Chase's to
//      change, by command); the repair command moves it; an NPC anywhere else is repaired by both.

using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.MLQuests.Definitions;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class NewHavenSeederFloorVerification
{
    private readonly ITestOutputHelper _out;

    public NewHavenSeederFloorVerification(ITestOutputHelper output) => _out = output;

    // Measured 2026-09-30 (PT-05); every tile fits at its z.
    private static readonly Dictionary<string, Point3D> MeasuredFloor = new()
    {
        ["Sir Helper"] = new(3503, 2574, 14),
        ["Andric"] = new(3535, 2536, 20),
        ["Kashiel"] = new(3538, 2536, 20),
        ["Asandos"] = new(3504, 2519, 27),
        ["Clairesse"] = new(3497, 2551, 20),
        ["Gervis"] = new(3505, 2749, 0),
        ["Mugg"] = new(3507, 2747, 0),
        ["Lowel"] = new(3444, 2638, 28),
        ["Lyle"] = new(3487, 2498, 52),
        ["Nibbet"] = new(3488, 2568, 20),
        ["Norton"] = new(3507, 2603, 1),
        ["Sadrah"] = new(3462, 2567, 35),
        ["Hargrove"] = new(3446, 2640, 28),
        ["Aelorn"] = new(3524, 2536, 20),
        ["Dimethro"] = new(3527, 2536, 20),
        ["Churchill"] = new(3531, 2531, 20),
        ["Robyn"] = new(3535, 2531, 20),
        ["Recaro"] = new(3535, 2534, 20),
        ["Alden Armstrong"] = new(3535, 2537, 20),
        ["Jockles"] = new(3535, 2544, 20),
        ["Tyl Ariadne"] = new(3525, 2556, 20),
        ["Alefian"] = new(3469, 2492, 71),
        ["Gustar"] = new(3472, 2492, 71),
        ["Jillian"] = new(3476, 2493, 72),
        ["Kaelynna"] = new(3479, 2492, 71),
        ["Mithneral"] = new(3484, 2493, 52),
        ["Amelia Youngstone"] = new(3459, 2529, 53),
        ["Andreas Vesalius"] = new(3458, 2551, 35),
        ["Avicenna"] = new(3461, 2551, 35),
        ["Sarsmea Smythe"] = new(3492, 2577, 15),
        ["Ryuichi"] = new(3422, 2520, 21),
        ["Chiyo"] = new(3424, 2520, 21),
        ["Jun"] = new(3420, 2520, 21),
        ["Walker"] = new(3418, 2520, 21),
        ["Hamato"] = new(3494, 2414, 55),
        ["Mulcivikh"] = new(3555, 2457, 15),
        ["Morganna"] = new(3547, 2462, 15),
        ["Jacob Waltz"] = new(3510, 2745, 0),
        ["George Hephaestus"] = new(3471, 2542, 36),
        ["League Registrar"] = new(3459, 2601, 18),
    };

    [Fact]
    public void EveryNewHavenSeederNpcStandsOnItsMeasuredFloor()
    {
        var entries = ClusterFNewHavenSeeder.Entries;

        Assert.Equal(MeasuredFloor.Count, entries.Length);

        var wrong = new List<string>();
        foreach (var entry in entries)
        {
            if (entry.HasAutoZ)
            {
                wrong.Add($"{entry.Label}: takes its z from the land height");
                continue;
            }

            if (!MeasuredFloor.TryGetValue(entry.Label, out var floor))
            {
                wrong.Add($"{entry.Label}: not measured");
                continue;
            }

            if (entry.GetLocation() != floor)
            {
                wrong.Add($"{entry.Label}: {entry.GetLocation()} is not its floor {floor}");
            }
        }

        _out.WriteLine(string.Join("\n", wrong));
        Assert.Empty(wrong);

        // The Anatomy quest giver, by name.
        Assert.Equal(new Point3D(3458, 2551, 35), entries.Single(e => e.Type == typeof(AndreasVesalius)).GetLocation());
    }

    [Fact]
    public void WorldLoadLeavesAnNpcOnItsOldTileAndTheRepairCommandMovesIt()
    {
        var entry = ClusterFNewHavenSeeder.Entries.Single(e => e.Type == typeof(Jun));
        Assert.True(entry.HasLegacyLocation);

        var jun = new Jun();
        try
        {
            // As the seeder placed him before PT-05: inside a lantern at 3426,2520,21.
            Place(jun, new Point3D(3426, 2520, 21));
            Assert.False(ClusterFNewHavenSeeder.NeedsRepair(jun, entry, acceptLegacyTiles: true));
            Assert.True(ClusterFNewHavenSeeder.NeedsRepair(jun, entry));

            // On the new tile, neither moves him.
            Place(jun, entry.GetLocation());
            Assert.False(ClusterFNewHavenSeeder.NeedsRepair(jun, entry, acceptLegacyTiles: true));
            Assert.False(ClusterFNewHavenSeeder.NeedsRepair(jun, entry));

            // Anywhere else, both do, as before PT-05.
            Place(jun, new Point3D(3430, 2530, 21));
            Assert.True(ClusterFNewHavenSeeder.NeedsRepair(jun, entry, acceptLegacyTiles: true));
            Assert.True(ClusterFNewHavenSeeder.NeedsRepair(jun, entry));
        }
        finally
        {
            jun.Delete();
        }
    }

    // What ApplyEntry does, at a chosen tile.
    private static void Place(Server.Mobiles.BaseCreature npc, Point3D at)
    {
        npc.Direction = Direction.South;
        npc.CantWalk = true;
        npc.Home = at;
        npc.RangeHome = 0;
        npc.MoveToWorld(at, Map.Trammel);
    }
}
