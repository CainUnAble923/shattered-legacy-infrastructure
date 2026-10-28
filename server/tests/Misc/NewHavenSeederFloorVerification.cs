// NewHavenSeederFloorVerification.cs
//
// cc-P17 PT-05: the Anatomy quest giver (Andreas Vesalius) stood 5 below the Healer's Hall floor,
// reachable only with the client's show-all-names key. He was one of 18 ClusterFNewHavenSeeder
// NPCs standing inside a floor, table, wall, water or rock (cc-P15 section 10.1). Cause: an entry
// without a z took Map.GetAverageZ (pinned Projects/Server/Maps/Map.cs:243-285), the land height,
// which is under a building's floor. Notes in shard-migration notes/cc-P17-playtest-bugs-1.md.
//
// The test host has no map files, so no fact here can ask the map where the floor is. The floors
// below were measured offline: cc-P678 re-measured every one on the Siege of Sosaria files with the playbook's walk
// (pinned's movement rules and GetAverageZ, shard-migration notes/cc-P678-tools/soswalk.py; edit_floorfact.py writes
// this table and stops if a tile is not its floor). Before, a port of pinned Map.CanFit over the old client map. What the facts pin:
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
        ["Sir Helper"] = new(3634, 2586, 0),
        ["Andric"] = new(3743, 2577, 40),
        ["Kashiel"] = new(3743, 2579, 40),
        ["Asandos"] = new(3665, 2609, 0),
        ["Clairesse"] = new(3689, 2465, 0),
        ["Gervis"] = new(3501, 2741, 1), // cc-P677: the mine camp on SoS
        ["Mugg"] = new(3497, 2747, 4),
        ["Lowel"] = new(3633, 2497, 0),
        ["Lyle"] = new(3641, 2465, 0),
        ["Nibbet"] = new(3664, 2496, 0),
        ["Norton"] = new(3650, 2654, 0),
        ["Sadrah"] = new(3610, 2497, 0),
        ["Hargrove"] = new(3635, 2498, 0),
        ["Aelorn"] = new(3729, 2578, 40),
        ["Dimethro"] = new(3731, 2576, 40),
        ["Churchill"] = new(3731, 2578, 40),
        ["Robyn"] = new(3729, 2580, 40),
        ["Recaro"] = new(3733, 2575, 40),
        ["Alden Armstrong"] = new(3733, 2577, 40),
        ["Jockles"] = new(3731, 2580, 40),
        ["Tyl Ariadne"] = new(3733, 2579, 40),
        ["Alefian"] = new(3641, 2467, 0),
        ["Gustar"] = new(3643, 2466, 0),
        ["Jillian"] = new(3643, 2468, 0),
        ["Kaelynna"] = new(3641, 2469, 0),
        ["Mithneral"] = new(3645, 2465, 0),
        ["Amelia Youngstone"] = new(3664, 2498, 0),
        ["Andreas Vesalius"] = new(3660, 2577, 0),
        ["Avicenna"] = new(3660, 2579, 0),
        ["Sarsmea Smythe"] = new(3624, 2616, 0),
        ["Ryuichi"] = new(3739, 2562, 40),
        ["Chiyo"] = new(3738, 2564, 40),
        ["Jun"] = new(3740, 2564, 40),
        ["Walker"] = new(3737, 2566, 40),
        ["Hamato"] = new(3741, 2562, 40),
        ["Mulcivikh"] = new(3729, 2635, 40),
        ["Morganna"] = new(3731, 2633, 40),
        ["Jacob Waltz"] = new(3501, 2743, 1),
        ["George Hephaestus"] = new(3634, 2616, 0),
        ["League Registrar"] = new(3660, 2549, 0),
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

        // The Anatomy quest giver, by name (cc-P678: in the Healers of Haven).
        Assert.Equal(MeasuredFloor["Andreas Vesalius"], entries.Single(e => e.Type == typeof(AndreasVesalius)).GetLocation());
    }

    [Fact]
    public void WorldLoadLeavesAnNpcOnItsOldTileAndTheRepairCommandMovesIt()
    {
        // cc-P678: Jun's legacy tile went with the ML plaza (a fresh world on swap day); Jacob Waltz at the mine camp keeps
        // his (the tile before PT-05, 3511,2744,0).
        var entry = ClusterFNewHavenSeeder.Entries.Single(e => e.Type == typeof(JacobWaltz));
        Assert.True(entry.HasLegacyLocation);

        var jun = new JacobWaltz();
        try
        {
            // As the seeder placed him before PT-05.
            Place(jun, new Point3D(3511, 2744, 0));
            Assert.False(ClusterFNewHavenSeeder.NeedsRepair(jun, entry, acceptLegacyTiles: true));
            Assert.True(ClusterFNewHavenSeeder.NeedsRepair(jun, entry));

            // On the new tile, neither moves him.
            Place(jun, entry.GetLocation());
            Assert.False(ClusterFNewHavenSeeder.NeedsRepair(jun, entry, acceptLegacyTiles: true));
            Assert.False(ClusterFNewHavenSeeder.NeedsRepair(jun, entry));

            // Anywhere else, both do, as before PT-05.
            Place(jun, new Point3D(3515, 2750, 0));
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
