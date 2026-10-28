// AnkhVerification.cs
//
// cc-P137: the ankh by New Haven's League Registrar did nothing (Chase, 2026-10-16). It is a map static; the shard's
// working ankhs are pinned's AnkhWest/AnkhNorth items. ClusterFAnkhs places OSI's working ankhs that pinned's
// [Decorate lacks (ServUO's data) and Chase's New Haven one, and surveys every ankh graphic. Notes in shard-migration
// notes/cc-P137-usable-ankhs-and-playtest-fixes.md.
//
// Facts:
//   1. Seeding puts a working AnkhWest, art 0x3 over 0x2, by New Haven's League office (3676,2562-2563,0; cc-P678,
//      was over the static at 3464,2600-2601,27, forest on SoS), places every
//      entry once, and seeding again places nothing.
//   2. A working ankh already within 2 tiles of an entry is kept and nothing is stacked beside it.
//   3. A ghost that walks up to the New Haven ankh is offered resurrection: it reaches the offer's last gate.
//   4. A living player gets OSI's post-AOS menu from the ankh: Resurrect and Tithe Gold, which opens the
//      tithing gump. Lock Karma is pinned's pre-AOS menu only (Ankhs.cs:17), so it is not offered here.
//   5. The survey reads ankh statics from a facet's staidx/statics files and calls each one working, waiting for
//      the seeder, or decoration; both halves of a pinned ankh count as working.
//
// The test host has no map files, so Map.CanFit is false everywhere (NewHavenServicesVerification says the same):
// the ankh's offer ends with 502391 "Thou can not be resurrected there!" where the shard, on a standable tile, sends
// the ResurrectGump (Ankhs.cs:40-47).

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class AnkhVerification
{
    private static readonly Point3D NewHaven = new(3676, 2562, 0);

    private static HashSet<Serial> ItemsNow() => World.Items.Keys.ToHashSet();

    private static void DeleteNewSince(HashSet<Serial> before)
    {
        foreach (var item in World.Items.Values.Where(i => !before.Contains(i.Serial)).ToList())
        {
            item.Delete();
        }
    }

    // The map enumerables are not IEnumerable: copied into lists.
    private static List<Item> ItemsAt(Map map, int x, int y)
    {
        var list = new List<Item>();
        foreach (var item in map.GetItemsAt(x, y))
        {
            list.Add(item);
        }

        return list;
    }

    private static List<Item> ItemsInRange(Map map, Point3D p, int range)
    {
        var list = new List<Item>();
        foreach (var item in map.GetItemsInRange(p, range))
        {
            list.Add(item);
        }

        return list;
    }

    private static AnkhWest NewHavenAnkh() => ItemsAt(Map.Trammel, NewHaven.X, NewHaven.Y).OfType<AnkhWest>().FirstOrDefault();

    private const int CannotBeResurrectedThere = 502391;

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

    private static ContextMenuEntry[] Entries(Item item, Mobile from)
    {
        var list = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            item.GetContextMenuEntries(from, ref list);
            var result = new ContextMenuEntry[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                result[i] = list[i];
            }

            return result;
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void SeedingMakesTheNewHavenAnkhWorkAndSeedingAgainPlacesNothing()
    {
        var before = ItemsNow();
        try
        {
            Assert.Null(NewHavenAnkh());

            var first = ClusterFAnkhs.Seed(false);
            Assert.Equal(ClusterFAnkhs.Placements.Length, first.Placed);

            var ankh = NewHavenAnkh();
            Assert.NotNull(ankh);
            Assert.Equal(0x3, ankh.ItemID);
            Assert.Equal(NewHaven.Z, ankh.Z);
            var foot = ItemsAt(Map.Trammel, NewHaven.X, NewHaven.Y + 1).FirstOrDefault(ClusterFAnkhs.IsWorkingAnkh);
            Assert.NotNull(foot);
            Assert.Equal(0x2, foot.ItemID);

            foreach (var p in ClusterFAnkhs.Placements)
            {
                Assert.True(ClusterFAnkhs.WorkingAnkhNear(p.Map, p.Location), $"{p.Map} {p.Location} ({p.Source})");
            }

            var second = ClusterFAnkhs.Seed(false);
            Assert.Equal((0, ClusterFAnkhs.Placements.Length), (second.Placed, second.Present));
            Assert.Single(ItemsInRange(Map.Trammel, NewHaven, 2).OfType<AnkhWest>());
        }
        finally
        {
            DeleteNewSince(before);
        }
    }

    [Fact]
    public void AWorkingAnkhAlreadyBesideAnEntryIsKept()
    {
        var before = ItemsNow();
        try
        {
            var handPlaced = new AnkhNorth();
            handPlaced.MoveToWorld(new Point3D(NewHaven.X + 2, NewHaven.Y, NewHaven.Z), Map.Trammel);

            var dry = ClusterFAnkhs.Seed(true);
            Assert.Equal(ClusterFAnkhs.Placements.Length - 1, dry.Placed);
            ClusterFAnkhs.Seed(false);

            Assert.Null(NewHavenAnkh());
            Assert.False(handPlaced.Deleted);
        }
        finally
        {
            DeleteNewSince(before);
        }
    }

    [Fact]
    public void AGhostWalkingUpToTheNewHavenAnkhIsOfferedResurrection()
    {
        var before = ItemsNow();
        var ghost = new PlayerMobile { Player = true };
        var ns = PacketTestUtilities.CreateTestNetState();
        try
        {
            ClusterFAnkhs.Seed(false);
            var ankh = NewHavenAnkh();

            ghost.MoveToWorld(new Point3D(NewHaven.X - 5, NewHaven.Y, NewHaven.Z), Map.Trammel);
            ghost.NetState = ns;
            ns.Mobile = ghost;
            ghost.Body = 0x192;
            Assert.False(ghost.Alive);

            var mark = ns.SendBuffer.GetReadSpan().Length;
            var old = ghost.Location;
            ghost.MoveToWorld(new Point3D(NewHaven.X - 1, NewHaven.Y, NewHaven.Z), Map.Trammel);
            ankh.OnMovement(ghost, old);

            Assert.True(SentLocalized(ns, mark, CannotBeResurrectedThere) || ghost.HasGump<ResurrectGump>());
        }
        finally
        {
            ghost.NetState = null;
            ghost.Delete();
            DeleteNewSince(before);
        }
    }

    [Fact]
    public void ALivingPlayerGetsOsisMenuAtTheNewHavenAnkh()
    {
        // Pinned's era rule (Ankhs.cs:15-27): Lock Karma is on the ankh only between UOTD and AOS; this shard is
        // past AOS, so the menu is Resurrect and Tithe Gold, as on OSI since AOS.
        Assert.True(Core.AOS);
        var before = ItemsNow();
        var pm = new PlayerMobile { Player = true };
        var ns = PacketTestUtilities.CreateTestNetState();
        try
        {
            ClusterFAnkhs.Seed(false);
            var ankh = NewHavenAnkh();
            pm.MoveToWorld(new Point3D(NewHaven.X - 1, NewHaven.Y, NewHaven.Z), Map.Trammel);
            pm.NetState = ns;
            ns.Mobile = pm;

            var menu = Entries(ankh, pm);
            // Cliloc 3006195 Resurrect (enabled for anyone; Ankhs.Resurrect does nothing for the living) and 3006198 Tithe Gold.
            Assert.Equal(new[] { 3006195, 3006198 }, menu.Select(e => e.Number).ToArray());
            Assert.DoesNotContain(menu, e => e is Ankhs.LockKarmaEntry);

            menu.Single(e => e.Number == 3006198).OnClick(pm, ankh);
            Assert.True(pm.HasGump<TithingGump>());
        }
        finally
        {
            pm.NetState = null;
            pm.Delete();
            DeleteNewSince(before);
        }
    }

    [Fact]
    public void TheSurveyReadsStaticsAndSaysWhichAnkhsWork()
    {
        var before = ItemsNow();
        var dir = Directory.CreateTempSubdirectory("ankh-survey").FullName;
        try
        {
            // A 2 x 2 block facet: block (0,0) holds a static ankh at 5,6 (head 0x3, foot 0x2) and block (1,1) one
            // at 12,13 (0x4) and a non-ankh static.
            var idx = Path.Combine(dir, "staidx.mul");
            var sta = Path.Combine(dir, "statics.mul");
            using (var s = new BinaryWriter(File.Create(sta)))
            using (var x = new BinaryWriter(File.Create(idx)))
            {
                void Static(int id, int sx, int sy, int z)
                {
                    s.Write((ushort)id); s.Write((byte)sx); s.Write((byte)sy); s.Write((sbyte)z); s.Write((ushort)0);
                }

                x.Write(0); x.Write(14); x.Write(0);   // block 0 = (0,0)
                Static(0x3, 5, 6, 10);
                Static(0x2, 5, 7, 10);
                x.Write(-1); x.Write(-1); x.Write(0);  // block 1 = (0,1), empty
                x.Write(-1); x.Write(-1); x.Write(0);  // block 2 = (1,0), empty
                x.Write(14); x.Write(14); x.Write(0);  // block 3 = (1,1)
                Static(0x4, 4, 5, 0);
                Static(0x1F13, 1, 1, 0);
            }

            var found = ClusterFAnkhs.StaticSightings(Map.Malas, 2, idx, sta);
            Assert.Equal(
                new[] { (5, 6, 0x3), (5, 7, 0x2), (12, 13, 0x4) },
                found.Select(f => (f.Location.X, f.Location.Y, f.ItemId)).ToArray()
            );

            // A working ankh over the first; nothing over the second.
            var working = new AnkhWest();
            working.MoveToWorld(new Point3D(5, 6, 10), Map.Malas);
            Assert.True(ClusterFAnkhs.IsWorkingAnkh(ItemsAt(Map.Malas, 5, 7).Single(i => i != working)), "the foot half");

            var newHaven = new ClusterFAnkhs.Sighting(Map.Trammel, NewHaven, 0x3, "map static");
            var rows = ClusterFAnkhs.Classify(found.Append(newHaven));
            Assert.Equal(
                new[] { ClusterFAnkhs.AnkhState.Working, ClusterFAnkhs.AnkhState.Decoration, ClusterFAnkhs.AnkhState.Placed },
                rows.Select(r => r.State).ToArray()
            );

            ClusterFAnkhs.Seed(false);
            Assert.Equal(ClusterFAnkhs.AnkhState.Working, ClusterFAnkhs.Classify(new[] { newHaven }).Single().State);
        }
        finally
        {
            Directory.Delete(dir, true);
            DeleteNewSince(before);
        }
    }
}
