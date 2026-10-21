// CommunityArtVerification.cs
//
// cc-P237 (F-21): New Haven's community art. Notes in shard-migration notes/cc-P237-new-haven-community-art.md.
//
// What the facts pin:
//   1. The placement table: keys unique, no two rows on one tile at one z, every row's item ID one this set
//      registered (the generated RegisteredIds list, which the package fact checks against registry.csv), all inside
//      Haven's box.
//   2. place twice adds nothing: the second run places 0 and the world's item count does not move; a dry run changes
//      nothing.
//   3. replace and clear remove only this seeder's items: a gold pile and a same-art lookalike on a row's tile stay, a
//      CommunityArt left from an older layout is swapped out by replace, clear removes every CommunityArt; their dry
//      runs change nothing.
//   4. CommunityArt round-trips with its key.
//   5. The server's in-memory records for the art are in place after ShatteredLegacyArt.Apply.
// The art itself (registry, PNGs, records.json against the C# table) is checked by player-package\tests. The test host
// has no map files, so nothing here asks the map about z: the rows were checked offline against the server's map files
// (notes/cc-P237-tools/validate.py).

using System;
using System.Linq;
using Server;
using Server.Items;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CommunityArtVerification
{
    private static void Clean()
    {
        foreach (var i in World.Items.Values.OfType<CommunityArt>().ToList())
        {
            i.Delete();
        }
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EveryRowUsesARegisteredIdAndNoTwoRowsShareATileAndZ()
    {
        var rows = ClusterFCommunityArtLayout.Rows;
        Assert.True(rows.Length >= 40, $"{rows.Length} rows");
        Assert.Equal(rows.Length, rows.Select(r => r.Key).Distinct().Count());
        var tiles = rows.GroupBy(r => (r.X, r.Y, r.Z)).Where(g => g.Count() > 1).Select(g => string.Join("+", g.Select(r => r.Key)));
        Assert.Empty(tiles);

        var registered = ClusterFCommunityArtLayout.RegisteredIds;
        Assert.Equal(34, registered.Length);
        Assert.All(registered, id => Assert.InRange(id, ClusterFCommunityArtRecords.FirstId, ClusterFCommunityArtRecords.LastId));
        foreach (var r in rows)
        {
            Assert.True(registered.Contains(r.ItemId), $"{r.Key}: 0x{r.ItemId:X4} is not a registered ID of this set");
            Assert.False(string.IsNullOrWhiteSpace(r.Name), r.Key);
            Assert.InRange(r.X, 3400, 3660);
            Assert.InRange(r.Y, 2460, 2660);
        }

        foreach (var rec in ClusterFCommunityArtRecords.Records)
        {
            Assert.True(registered.Contains(rec.Id), $"record 0x{rec.Id:X4} is not a registered ID of this set");
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void PlaceTwiceAddsNothing()
    {
        var rows = ClusterFCommunityArtLayout.Rows;
        try
        {
            Clean();
            var before = World.Items.Count;
            ClusterFCommunityArt.Place(true);
            Assert.Equal(before, World.Items.Count);

            ClusterFCommunityArt.Place(false);
            Assert.Equal(before + rows.Length, World.Items.Count);
            var second = ClusterFCommunityArt.Place(false);
            Assert.Equal(before + rows.Length, World.Items.Count);
            Assert.Contains($"placed 0 of {rows.Length}; {rows.Length} already placed", second.Last());

            var placed = ClusterFCommunityArt.Placed();
            Assert.Equal(rows.Select(r => r.Key).OrderBy(k => k), placed.Select(p => p.Key).OrderBy(k => k));
            foreach (var p in placed)
            {
                var r = rows.Single(x => x.Key == p.Key);
                Assert.Equal(new Point3D(r.X, r.Y, r.Z), p.Location);
                Assert.Equal(Map.Trammel, p.Map);
                Assert.Equal(r.ItemId, p.ItemID);
                Assert.Equal(r.Name, p.Name);
                Assert.False(p.Movable);
                Assert.False(p.Decays);
            }
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ReplaceAndClearRemoveOnlyThisSeedersItems()
    {
        var rows = ClusterFCommunityArtLayout.Rows;
        var first = rows[0];
        var stranger = new Item(0x0EED) { Movable = false };   // a gold coin pile, nothing to do with the art
        var lookalike = new Item(first.ItemId) { Movable = false, Name = first.Name };   // same art, not ours
        try
        {
            Clean();
            stranger.MoveToWorld(new Point3D(first.X, first.Y, first.Z), Map.Trammel);
            lookalike.MoveToWorld(new Point3D(first.X, first.Y, first.Z), Map.Trammel);

            // A world seeded from an older layout: one row's item, and one from a row the layout no longer has.
            ClusterFCommunityArt.Place(false);
            var old = new CommunityArt(new ClusterFCommunityArtLayout.Row("an-old-row", "camp", 3590, 2630, 0, first.ItemId, "an old piece"));
            old.MoveToWorld(new Point3D(3590, 2630, 0), Map.Trammel);
            Assert.Equal(rows.Length + 1, ClusterFCommunityArt.Placed().Count);

            var before = World.Items.Count;
            ClusterFCommunityArt.Replace(true);
            ClusterFCommunityArt.Clear(true);
            Assert.Equal(before, World.Items.Count);
            Assert.False(old.Deleted);

            ClusterFCommunityArt.Replace(false);
            Assert.True(old.Deleted);
            Assert.Equal(rows.Select(r => r.Key).OrderBy(k => k), ClusterFCommunityArt.Placed().Select(p => p.Key).OrderBy(k => k));
            Assert.False(stranger.Deleted);
            Assert.False(lookalike.Deleted);

            ClusterFCommunityArt.Clear(false);
            ClusterFCommunityArt.Clear(false);
            Assert.Empty(ClusterFCommunityArt.Placed());
            Assert.False(stranger.Deleted);
            Assert.False(lookalike.Deleted);
            Assert.Equal(new Point3D(first.X, first.Y, first.Z), lookalike.Location);

            ClusterFCommunityArt.Place(false);
            Assert.Equal(rows.Length, ClusterFCommunityArt.Placed().Count);
        }
        finally
        {
            Clean();
            stranger.Delete();
            lookalike.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void CommunityArtRoundTrips()
    {
        var row = ClusterFCommunityArtLayout.Rows.First(r => r.Area == "camp");
        var item = new CommunityArt(row);
        try
        {
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            item.Serialize(writer);
            writer.Flush();
            var copy = (CommunityArt)Activator.CreateInstance(typeof(CommunityArt), item.Serial)!;
            copy.Deserialize(new BufferReader(buffer));
            Assert.Equal(row.Key, copy.Key);
            Assert.Equal(row.ItemId, copy.ItemID);
            Assert.Equal(row.Name, copy.Name);
            Assert.False(copy.Movable);
        }
        finally
        {
            item.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheServerHasTheCommunityArtRecords()
    {
        ShatteredLegacyArt.Apply();
        foreach (var r in ClusterFCommunityArtRecords.Records)
        {
            var data = TileData.ItemTable[r.Id];
            Assert.Equal(r.Name, data.Name);
            Assert.Equal(r.Flags, data.Flags);
            Assert.Equal(r.Height, data.Height);
            Assert.Equal(r.Quality, data.Quality);
            Assert.True(r.Name.Length <= 19, r.Name);
        }

        Assert.Equal(19, ClusterFCommunityArtRecords.Records.Length);
        Assert.True(TileData.ItemTable[0x3C9D].Impassable, "the hut blocks");
        Assert.True(TileData.ItemTable[0x3C88].Impassable, "a cart blocks");
        Assert.True((TileData.ItemTable[0x3C8C].Flags & TileFlag.Animation) != 0, "the lantern animates");
        Assert.True(TileData.ItemTable[0x3C8C].LightSource, "the lantern gives light");
    }
}
