// WinterDecorVerification.cs
//
// cc-P34 (F-22): the dated winter decor for New Haven. Notes in shard-migration notes/cc-P34-winter-decor.md.
//
// What the facts pin:
//   1. The placement table loads: keys unique, no item twice on a tile, every item ID is one of our 47 registered
//      winter IDs or one of the OSI holiday IDs pinned's own classes use, all on Trammel.
//   2. No row sits on a door, a door's swing tile, an NPC post, a spawner tile, the quest board, the arrival tile or
//      the Reset Stone, or on a League field office component. The doors, posts and office are the seeders' own
//      tables (ClusterFNewHavenServicesSeeder.Decoration with pinned BaseDoor.GetOffset, ClusterFNewHavenSeeder.Entries,
//      ClusterFResetStoneSeeder, ClusterFRegistrarOfficeLayout), not copies.
//   3. The date gate, on an injected clock: in season from December 1 to January 6 inclusive. The automatic check
//      acts only when the season changes: it puts the decor up on December 1, leaves a staff 'down' in December
//      alone, and takes it down after January 6; a world first seen out of season is left as it is.
//   4. up twice places once; down removes exactly the decor and leaves an unrelated item on the same tile; up after
//      down restores the same set; dryrun changes nothing.
//   5. WinterDecor round-trips with its key; the saved state round-trips null, up and down.
//   6. The server's in-memory records for the winter art are in place after ShatteredLegacyArt.Apply.
// The art itself (registry, PNGs, records.json against the C# table) is checked by player-package\tests.
//
// The test host has no map files, so nothing here asks the map about z: the rows were checked offline against the
// server's map files (notes/cc-P34-tools/validate.py).

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class WinterDecorVerification
{
    // The OSI IDs the rows may use, each from pinned's own class: Snowman.cs:5, Wreath.cs:254/258, Mistletoe.cs:266/270,
    // Stockings.cs:6/20, HolidayBell.cs:29, SnowPiles.cs:8, SnowyTree.cs:9.
    private static readonly HashSet<int> OsiIds = new()
    {
        0x2328, 0x2329, 0x232C, 0x232D, 0x2374, 0x2375, 0x2BD9, 0x2BDA, 0x2BDB, 0x2BDC, 0x1C12,
        0x08E2, 0x08E0, 0x08E6, 0x08E5, 0x08E3, 0x2377
    };

    private static readonly (int X, int Y) QuestBoard = (3502, 2573);
    private static readonly (int X, int Y) ArrivalTile = (3503, 2574);

    private static void Clean()
    {
        foreach (var i in World.Items.Values.OfType<WinterDecor>().ToList())
        {
            i.Delete();
        }

        ClusterFWinterDecorState.LastAutomatic = null;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ThePlacementTableLoadsAndEveryItemExists()
    {
        var rows = ClusterFWinterDecorLayout.Rows;
        Assert.True(rows.Length >= 40, $"{rows.Length} rows");
        Assert.Equal(rows.Length, rows.Select(r => r.Key).Distinct().Count());
        Assert.Equal(rows.Length, rows.Select(r => (r.X, r.Y, r.ItemId)).Distinct().Count());
        foreach (var r in rows)
        {
            var ours = r.ItemId is >= ClusterFWinterDecorArt.FirstId and <= ClusterFWinterDecorArt.LastId;
            Assert.True(ours || OsiIds.Contains(r.ItemId), $"{r.Key}: 0x{r.ItemId:X4} is neither ours nor an OSI holiday ID");
            Assert.False(string.IsNullOrWhiteSpace(r.Name), r.Key);
            Assert.InRange(r.X, 3400, 3560);
            Assert.InRange(r.Y, 2460, 2660);
        }

        // Every layer of F-22 is there: the square's tree, statue and firepit, the inn's fireplace and a stove, the sleigh.
        foreach (var id in new[] { 0x3C52, 0x3C40, 0x3C45, 0x3C60, 0x3C4A, 0x3C4E, 0x3C6E })
        {
            Assert.Contains(rows, r => r.ItemId == id);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void NoRowSitsOnADoorItsSwingAnNpcASpawnerOrTheSquaresFixedTiles()
    {
        var bad = new Dictionary<(int, int), string>();
        void Add(int x, int y, string why) => bad.TryAdd((x, y), why);

        var doors = 0;
        foreach (var d in ClusterFNewHavenServicesSeeder.Decoration)
        {
            if (!d.TypeName.EndsWith("Door", StringComparison.Ordinal))
            {
                continue;
            }

            doors++;
            var facing = Enum.Parse<DoorFacing>(d.Header.Split("Facing=")[1].TrimEnd(')'));
            var off = BaseDoor.GetOffset(facing);
            Add(d.Location.X, d.Location.Y, $"door {d.Header}");
            Add(d.Location.X + off.X, d.Location.Y + off.Y, $"swing of {d.Header} at {d.Location}");
        }

        Assert.True(doors >= 60, $"{doors} doors read");

        foreach (var e in ClusterFNewHavenSeeder.Entries)
        {
            var p = e.GetLocation();
            Add(p.X, p.Y, $"NPC {e.Label}");
        }

        foreach (var (x, y) in ClusterFWinterDecorLayout.SpawnerTiles)
        {
            Add(x, y, "spawner");
        }

        Add(QuestBoard.X, QuestBoard.Y, "quest board");
        Add(ArrivalTile.X, ArrivalTile.Y, "arrival tile");
        Add(ClusterFResetStoneSeeder.Location.X, ClusterFResetStoneSeeder.Location.Y, "Reset Stone");
        foreach (var c in ClusterFRegistrarOfficeLayout.Components)
        {
            Add(c.X, c.Y, $"office {c.Name}");
        }

        Assert.Equal(ArrivalTile, (ClusterFNewHavenSeeder.SpotOf(typeof(Server.Engines.MLQuests.Mobiles.SirHelper)).X, ClusterFNewHavenSeeder.SpotOf(typeof(Server.Engines.MLQuests.Mobiles.SirHelper)).Y));

        foreach (var r in ClusterFWinterDecorLayout.Rows)
        {
            Assert.False(bad.TryGetValue((r.X, r.Y), out var why), $"{r.Key} at {r.X},{r.Y} is on a protected tile: {why}");
        }
    }

    // ---------------------------------------------------------------- 3

    [Theory]
    [InlineData(2026, 11, 30, 23, false)]
    [InlineData(2026, 12, 1, 0, true)]
    [InlineData(2026, 12, 25, 12, true)]
    [InlineData(2026, 12, 31, 23, true)]
    [InlineData(2027, 1, 1, 0, true)]
    [InlineData(2027, 1, 6, 23, true)]
    [InlineData(2027, 1, 7, 0, false)]
    [InlineData(2027, 7, 4, 12, false)]
    public void TheSeasonIsDecemberFirstToJanuarySixthInclusive(int y, int m, int d, int h, bool inSeason) =>
        Assert.Equal(inSeason, ClusterFWinterDecor.IsInSeason(new DateTime(y, m, d, h, 59, 0)));

    [Fact]
    public void TheAutomaticCheckActsOnlyWhenTheSeasonChanges()
    {
        var rows = ClusterFWinterDecorLayout.Rows.Length;
        try
        {
            Clean();

            // A world first seen in November: nothing placed, the state remembered.
            Assert.Empty(ClusterFWinterDecor.Sync(new DateTime(2026, 11, 20)));
            Assert.Empty(ClusterFWinterDecor.Placed());
            Assert.False(ClusterFWinterDecorState.LastAutomatic);

            // Staff put it up early for testing: November checks leave it up.
            ClusterFWinterDecor.Up(false);
            Assert.Empty(ClusterFWinterDecor.Sync(new DateTime(2026, 11, 25)));
            Assert.Equal(rows, ClusterFWinterDecor.Placed().Count);

            // December 1: up (already up: nothing doubled).
            Assert.NotEmpty(ClusterFWinterDecor.Sync(new DateTime(2026, 12, 1, 0, 30, 0)));
            Assert.Equal(rows, ClusterFWinterDecor.Placed().Count);
            Assert.True(ClusterFWinterDecorState.LastAutomatic);

            // Staff take it down on December 10: the December checks leave it down.
            ClusterFWinterDecor.Down(false);
            Assert.Empty(ClusterFWinterDecor.Sync(new DateTime(2026, 12, 11)));
            Assert.Empty(ClusterFWinterDecor.Placed());

            // Up again by hand, then January 7: down.
            ClusterFWinterDecor.Up(false);
            Assert.Empty(ClusterFWinterDecor.Sync(new DateTime(2027, 1, 6, 23, 0, 0)));
            Assert.Equal(rows, ClusterFWinterDecor.Placed().Count);
            Assert.NotEmpty(ClusterFWinterDecor.Sync(new DateTime(2027, 1, 7, 0, 30, 0)));
            Assert.Empty(ClusterFWinterDecor.Placed());
            Assert.False(ClusterFWinterDecorState.LastAutomatic);

            // A world first seen in season (a fresh test world on December 3): brought up.
            Clean();
            Assert.NotEmpty(ClusterFWinterDecor.Sync(new DateTime(2026, 12, 3)));
            Assert.Equal(rows, ClusterFWinterDecor.Placed().Count);
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void UpTwicePlacesOnceAndDownRemovesExactlyTheDecor()
    {
        var rows = ClusterFWinterDecorLayout.Rows;
        var first = rows[0];
        var stranger = new Item(0x0EED) { Movable = false };   // a gold coin pile, nothing to do with the decor
        var lookalike = new Item(first.ItemId) { Movable = false, Name = first.Name };   // same art, not ours
        try
        {
            Clean();
            stranger.MoveToWorld(new Point3D(first.X, first.Y, first.Z), Map.Trammel);
            lookalike.MoveToWorld(new Point3D(first.X, first.Y, first.Z), Map.Trammel);

            var before = World.Items.Count;
            ClusterFWinterDecor.Up(true);
            Assert.Equal(before, World.Items.Count);

            ClusterFWinterDecor.Up(false);
            ClusterFWinterDecor.Up(false);
            var placed = ClusterFWinterDecor.Placed();
            Assert.Equal(rows.Length, placed.Count);
            Assert.Equal(rows.Select(r => r.Key).OrderBy(k => k), placed.Select(p => p.Key).OrderBy(k => k));
            foreach (var p in placed)
            {
                var r = rows.Single(x => x.Key == p.Key);
                Assert.Equal(new Point3D(r.X, r.Y, r.Z), p.Location);
                Assert.Equal(Map.Trammel, p.Map);
                Assert.Equal(r.ItemId, p.ItemID);
                Assert.Equal(r.Hue, p.Hue);
                Assert.False(p.Movable);
                Assert.False(p.Decays);
            }

            before = World.Items.Count;
            ClusterFWinterDecor.Down(true);
            Assert.Equal(before, World.Items.Count);

            ClusterFWinterDecor.Down(false);
            ClusterFWinterDecor.Down(false);
            Assert.Empty(ClusterFWinterDecor.Placed());
            Assert.False(stranger.Deleted);
            Assert.False(lookalike.Deleted);
            Assert.Equal(new Point3D(first.X, first.Y, first.Z), stranger.Location);

            ClusterFWinterDecor.Up(false);
            Assert.Equal(rows.Select(r => r.Key).OrderBy(k => k), ClusterFWinterDecor.Placed().Select(p => p.Key).OrderBy(k => k));
        }
        finally
        {
            Clean();
            stranger.Delete();
            lookalike.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void WinterDecorAndTheStateRoundTrip()
    {
        var row = ClusterFWinterDecorLayout.Rows.First(r => r.Hue != 0);
        var item = new WinterDecor(row);
        try
        {
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            item.Serialize(writer);
            writer.Flush();
            var copy = (WinterDecor)Activator.CreateInstance(typeof(WinterDecor), item.Serial)!;
            copy.Deserialize(new BufferReader(buffer));
            Assert.Equal(row.Key, copy.Key);
            Assert.Equal(row.ItemId, copy.ItemID);
            Assert.Equal(row.Hue, copy.Hue);
            Assert.Equal(row.Name, copy.Name);
            Assert.False(copy.Movable);

            foreach (var state in new bool?[] { null, true, false })
            {
                ClusterFWinterDecorState.LastAutomatic = state;
                var b = new byte[64];
                var w = new BufferWriter(b, true);
                ClusterFWinterDecorState.WriteTo(w);
                w.Flush();
                ClusterFWinterDecorState.LastAutomatic = !state ?? true;
                ClusterFWinterDecorState.ReadFrom(new BufferReader(b));
                Assert.Equal(state, ClusterFWinterDecorState.LastAutomatic);
            }
        }
        finally
        {
            item.Delete();
            ClusterFWinterDecorState.LastAutomatic = null;
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheServerHasTheWinterArtRecords()
    {
        ShatteredLegacyArt.Apply();
        foreach (var r in ClusterFWinterDecorArt.Records)
        {
            var data = TileData.ItemTable[r.Id];
            Assert.Equal(r.Name, data.Name);
            Assert.Equal(r.Flags, data.Flags);
            Assert.Equal(r.Height, data.Height);
            Assert.InRange(r.Id, ClusterFWinterDecorArt.FirstId, ClusterFWinterDecorArt.LastId);
            Assert.True(r.Name.Length <= 19, r.Name);
        }

        Assert.True(TileData.ItemTable[0x3C52].Impassable, "the tree blocks");
        Assert.True((TileData.ItemTable[0x3C40].Flags & TileFlag.Animation) != 0, "the statue animates");
    }
}
