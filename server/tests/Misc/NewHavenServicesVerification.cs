// NewHavenServicesVerification.cs
//
// cc-P17 PT-01 and PT-07: New Haven had no forge, no anvil, no ankh and no Healer on a fresh world.
// Pinned has no New Haven decoration set, and pinned's spawner import deletes the Healer spawner under
// the HealerGuildmaster's (ClusterFNewHavenServicesSeeder.cs says why). The seeder places ServUO's New
// Haven set and a Healer spawner. Notes in shard-migration notes/cc-P17-playtest-bugs-1.md.
//
// Facts:
//   1. Seeding gives New Haven's smithy a forge and an anvil that Blacksmithy accepts
//      (DefBlacksmithy.CheckAnvilAndForge), and seeding again places nothing.
//   2. A door already on a ServUO door's tile is kept and no second door is stacked on it.
//   2b. As on live (items staff placed by hand on 2026-05-15 and 05-27): an Anvil and a Forge already on ServUO's
//       smithy tiles, and an ankh 5 tiles from ServUO's, are kept, and nothing is stacked beside them.
//   3. After seeding, a ghost in New Haven gets through every gate of the Healer's offer and of the ankh's
//      but the last, and the Healer is willing (CheckResurrect).
//
// The test host has no map files, so Map.CanFit is false on every tile there (probed: range, line of sight
// and Frozen all passed and CanFit did not). The last gate of both offers is that CanFit, so here each ends
// with 502391 "Thou can not be resurrected there!" where the shard, on a standable tile, sends the gump
// (BaseHealer.cs:135-139, Ankhs.cs:40-47). The Healer tile, the ankh tile and the tiles a ghost stands on
// beside them were checked standable offline with the port of pinned Map.CanFit (PT-05's tool).

using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class NewHavenServicesVerification
{
    private readonly ITestOutputHelper _out;

    public NewHavenServicesVerification(ITestOutputHelper output) => _out = output;

    // Everything the seeder can touch on Trammel, top level: the New Haven region and ServUO's entries
    // outside it (the Necromancers Guild Hall doors reach x 3559, y 2452), so a fact cleans up all it made.
    private static bool InSeededArea(Point3D p) => p.X >= 3400 && p.X <= 3570 && p.Y >= 2440 && p.Y <= 2660;

    private static HashSet<IEntity> InNewHaven()
    {
        var set = new HashSet<IEntity>();
        foreach (var item in World.Items.Values)
        {
            if (!item.Deleted && item.Parent == null && item.Map == Map.Trammel && InSeededArea(item.Location))
            {
                set.Add(item);
            }
        }

        foreach (var mobile in World.Mobiles.Values)
        {
            if (!mobile.Deleted && mobile.Map == Map.Trammel && InSeededArea(mobile.Location))
            {
                set.Add(mobile);
            }
        }

        return set;
    }

    private static int OtherSmithyAt(int x, int y, Item except)
    {
        var count = 0;
        foreach (var item in Map.Trammel.GetItemsAt(x, y))
        {
            if (item != except && ClusterFNewHavenServicesSeeder.IsSmithy(item))
            {
                count++;
            }
        }

        return count;
    }

    private const int CannotBeResurrectedThere = 502391;

    // A 0xC1 localized message with this cliloc on the wire since `from`.
    private static bool SentLocalized(Server.Network.NetState ns, int from, int cliloc)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        for (var i = 0; i + 18 <= span.Length; i++)
        {
            if (span[i] == 0xC1 && System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(span[(i + 14)..]) == cliloc)
            {
                return true;
            }
        }

        return false;
    }

    private static void DeleteAll(IEnumerable<IEntity> entities)
    {
        foreach (var e in entities.ToList())
        {
            e.Delete();
        }
    }

    [Fact]
    public void SeedingGivesTheSmithyAForgeAndAnvilAndSeedingAgainPlacesNothing()
    {
        var before = InNewHaven();
        var smith = new PlayerMobile();

        try
        {
            var first = ClusterFNewHavenServicesSeeder.Seed(false);
            _out.WriteLine(first.Message);
            _out.WriteLine("kept out: " + string.Join("; ", first.KeptOut));

            // A fresh world gets every entry: nothing is kept out and nothing is taken for already there.
            Assert.Equal(ClusterFNewHavenServicesSeeder.Decoration.Length, first.Placed);
            Assert.Equal(0, first.Present);
            Assert.Equal(0, first.KindAlreadyThere);
            Assert.True(first.HealerPlaced);

            // Between the east anvil (3467,2540,36) and the forges (3467,2539 and 3467,2541, z 36).
            smith.MoveToWorld(new Point3D(3468, 2540, 36), Map.Trammel);
            var afterFirst = InNewHaven();
            DefBlacksmithy.CheckAnvilAndForge(smith, 2, out var anvil, out var forge);
            Assert.True(anvil, "no anvil Blacksmithy accepts in range of the smithy");
            Assert.True(forge, "no forge Blacksmithy accepts in range of the smithy");

            // Again: nothing more.
            var second = ClusterFNewHavenServicesSeeder.Seed(false);
            _out.WriteLine(second.Message);
            Assert.Equal(0, second.Placed);
            Assert.False(second.HealerPlaced);
            Assert.Equal(afterFirst.Count, InNewHaven().Count);

            // The dry run agrees.
            Assert.Equal(0, ClusterFNewHavenServicesSeeder.Seed(true).Placed);
        }
        finally
        {
            smith.Delete();
            DeleteAll(InNewHaven().Except(before));
        }
    }

    [Fact]
    public void ADoorAlreadyOnTheTileIsKeptAndNoSecondIsStacked()
    {
        var before = InNewHaven();

        // NewHaven.cfg:42 wants a DarkWoodDoor facing EastCCW at 3500,2518,47. Put one facing WestCW
        // there first, as the test world has it. And one 5 above NewHaven.cfg:46's 3495,2516,47: pinned's
        // FindItem would delete it (any door within 8 z, Decorate.cs:1165-1168).
        var existing = new DarkWoodDoor(DoorFacing.WestCW);
        existing.MoveToWorld(new Point3D(3500, 2518, 47), Map.Trammel);
        var above = new DarkWoodDoor(DoorFacing.WestCW);
        above.MoveToWorld(new Point3D(3495, 2516, 52), Map.Trammel);

        try
        {
            var result = ClusterFNewHavenServicesSeeder.Seed(false);
            _out.WriteLine(result.Message);
            Assert.True(result.KindAlreadyThere >= 1);

            var doors = new List<BaseDoor>();
            foreach (var item in Map.Trammel.GetItemsAt(3500, 2518))
            {
                if (item is BaseDoor door)
                {
                    doors.Add(door);
                }
            }

            Assert.Single(doors);
            Assert.Same(existing, doors[0]);

            Assert.False(above.Deleted);
            var doorsAbove = new List<BaseDoor>();
            foreach (var item in Map.Trammel.GetItemsAt(3495, 2516))
            {
                if (item is BaseDoor door)
                {
                    doorsAbove.Add(door);
                }
            }

            Assert.Single(doorsAbove);
        }
        finally
        {
            existing.Delete();
            above.Delete();
            DeleteAll(InNewHaven().Except(before));
        }
    }

    [Fact]
    public void AHandPlacedAnvilForgeAndAnkhAreKeptAndNothingIsStackedOnThem()
    {
        var before = InNewHaven();

        // The live world's own: Anvil 3467,2540,36 (ServUO's AnvilEastAddon tile), Forge 3469,2535,41 (its
        // SmallForgeAddon tile) and AnkhNorth 3526,2511,65 (ServUO's is 3526,2516,25).
        var anvil = new Anvil();
        anvil.MoveToWorld(new Point3D(3467, 2540, 36), Map.Trammel);
        var forge = new Forge();
        forge.MoveToWorld(new Point3D(3469, 2535, 41), Map.Trammel);
        var ankh = new AnkhNorth();
        ankh.MoveToWorld(new Point3D(3526, 2511, 65), Map.Trammel);

        try
        {
            var result = ClusterFNewHavenServicesSeeder.Seed(false);
            _out.WriteLine(result.Message);

            Assert.Equal(0, OtherSmithyAt(3467, 2540, anvil));
            Assert.Equal(0, OtherSmithyAt(3469, 2535, forge));

            var ankhs = InNewHaven().Where(e => e is AnkhNorth or AnkhWest).ToList();
            Assert.Single(ankhs);
            Assert.Same(ankh, ankhs[0]);
        }
        finally
        {
            DeleteAll(InNewHaven().Except(before));
        }
    }

    [Fact]
    public void AGhostIsOfferedResurrectionInNewHavenByTheHealerAndAtTheAnkh()
    {
        var before = InNewHaven();
        // Onto the map first: a facet change sends the account's features, and a test connection has no account.
        var ghost = new PlayerMobile { Player = true };
        ghost.MoveToWorld(new Point3D(3500, 2600, 0), Map.Trammel);
        var ns = PacketTestUtilities.CreateTestNetState();
        ghost.NetState = ns; // a gump needs a connection to be held (GumpSystem.HasGump)
        ns.Mobile = ghost;

        try
        {
            ClusterFNewHavenServicesSeeder.Seed(false);

            var spawner = InNewHaven().OfType<Spawner>().Single(
                s => s.Entries.Any(e => e.SpawnedName == "Healer")
            );
            Assert.Equal(ClusterFNewHavenServicesSeeder.HealerSpawnerLocation, spawner.Location);

            var healer = InNewHaven().OfType<Healer>().FirstOrDefault();
            Assert.NotNull(healer);
            _out.WriteLine($"Healer at {healer.Location}");

            ghost.Body = 0x192; // a ghost body on a Player: Mobile.Alive reads false
            Assert.False(ghost.Alive);

            // The Healer will resurrect this ghost (Healer.CheckResurrect: not criminal, not a murderer).
            Assert.True(healer.CheckResurrect(ghost));

            // Walks from outside the Healer's 4 tiles to within them (BaseHealer.OnMovement).
            var at = new Point3D(healer.X + 2, healer.Y, healer.Z);
            ghost.MoveToWorld(at, Map.Trammel);
            var mark = ns.SendBuffer.GetReadSpan().Length;
            healer.OnMovement(ghost, new Point3D(healer.X + 10, healer.Y, healer.Z));
            Assert.True(
                SentLocalized(ns, mark, CannotBeResurrectedThere) || ghost.HasGump<ResurrectGump>(),
                $"the Healer never reached its last gate: InLOS {healer.InLOS(ghost)}, Frozen {ghost.Frozen}, InRange {healer.InRange(ghost, 4)}"
            );
            ghost.CloseGump<ResurrectGump>();

            // The ankh, from its context menu's Resurrect (Ankhs.cs:30-49): in range, so it too reaches CanFit.
            var ankh = InNewHaven().OfType<AnkhNorth>().Single();
            Assert.Equal(new Point3D(3526, 2516, 25), ankh.Location);
            ghost.MoveToWorld(new Point3D(3526, 2518, 25), Map.Trammel);
            mark = ns.SendBuffer.GetReadSpan().Length;
            Ankhs.Resurrect(ghost, ankh);
            Assert.False(SentLocalized(ns, mark, 500446), "the ankh says it is too far away");
            Assert.True(SentLocalized(ns, mark, CannotBeResurrectedThere) || ghost.HasGump<ResurrectGump>());
        }
        finally
        {
            ghost.NetState = null;
            ns.Mobile = null;
            ghost.Delete();
            DeleteAll(InNewHaven().Except(before));
        }
    }
}
