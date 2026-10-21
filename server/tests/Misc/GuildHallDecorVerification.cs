// GuildHallDecorVerification.cs
//
// cc-P265: the guild hall decor packs and their staff seeders. Notes in shard-migration
// notes/cc-P265-guild-hall-decor-packs.md.
//
// What the facts pin:
//   1. The server's templates are the design file (design/guild-hall-decor/templates.json, through its generated
//      copy): every set, group and row, each row's art ID resolved from its piece the way the generator does.
//   2. Every row is one of this set's registered pieces (a base ID of GuildHallDecorArt), one of cc-P237's two marble
//      railings, or a stock item below 0x4000; no two rows of a set on one tile at one z; one command per set.
//   3. On the live shard (isTestShard false) place and clear refuse and change nothing.
//   4. On the test shard: a dry run changes nothing; place puts every row at the target plus its offset, not movable,
//      named and hued as its row; place again at the same point adds nothing; a group key places that group alone;
//      clear removes only this set's items near the caller (a same-art lookalike, a gold pile and another set's
//      placement stay), and clear all removes the rest.
//   5. GuildHallDecor round-trips its set, group, row and anchor.
//   6. The server's in-memory records are in place after ShatteredLegacyArt.Apply: every piece's name, flags, weight,
//      light and height; every animated piece (more than one frame) carries Animation; frames carry no record.
// The art itself (registry, PNGs, records.json against GuildHallDecorArt) is checked by player-package\tests.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Server;
using Server.Items;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuildHallDecorVerification
{
    private static readonly Dictionary<string, int> CommunityRails = new() { ["marble-70321"] = 0x3C8F, ["marble-70324"] = 0x3C90 };

    private static void Clean()
    {
        foreach (var i in World.Items.Values.OfType<GuildHallDecor>().ToList())
        {
            i.Delete();
        }
    }

    private static int ResolveRow(JsonElement r)
    {
        if (r.TryGetProperty("item", out var item))
        {
            return Convert.ToInt32(item.GetString(), 16);
        }

        var piece = r.GetProperty("piece").GetString();
        return CommunityRails.TryGetValue(piece!, out var id) ? id : GuildHallDecorArt.Find(piece)!.Id;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheTemplatesAreTheDesignFile()
    {
        var json = GuildHallDecorDesignCopy.Json.Replace("\r\n", "\n");
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(json))).ToLowerInvariant();
        Assert.Equal(GuildHallDecorDesignCopy.Sha256, sha);
        Assert.Equal(GuildHallDecorTemplates.DesignSha256, sha);

        using var doc = JsonDocument.Parse(json);
        var sets = doc.RootElement.GetProperty("sets").EnumerateArray().ToArray();
        Assert.Equal(sets.Length, GuildHallDecorTemplates.Sets.Length);
        for (var s = 0; s < sets.Length; s++)
        {
            var set = GuildHallDecorTemplates.Sets[s];
            Assert.Equal(sets[s].GetProperty("key").GetString(), set.Key);
            Assert.Equal(sets[s].GetProperty("command").GetString(), set.Command);
            var groups = sets[s].GetProperty("groups").EnumerateArray().ToArray();
            Assert.Equal(groups.Length, set.Groups.Length);
            for (var g = 0; g < groups.Length; g++)
            {
                Assert.Equal(groups[g].GetProperty("key").GetString(), set.Groups[g].Key);
                var rows = groups[g].GetProperty("rows").EnumerateArray().ToArray();
                Assert.Equal(rows.Length, set.Groups[g].Rows.Length);
                for (var r = 0; r < rows.Length; r++)
                {
                    var row = set.Groups[g].Rows[r];
                    var where = $"{set.Key}/{set.Groups[g].Key}/{r}";
                    Assert.True(ResolveRow(rows[r]) == row.ItemId, $"{where}: art ID");
                    Assert.True(rows[r].GetProperty("dx").GetInt32() == row.Dx, $"{where}: dx");
                    Assert.True(rows[r].GetProperty("dy").GetInt32() == row.Dy, $"{where}: dy");
                    Assert.True(rows[r].GetProperty("dz").GetInt32() == row.Dz, $"{where}: dz");
                    var hue = rows[r].TryGetProperty("hue", out var h) ? h.GetInt32() : 0;
                    Assert.True(hue == row.Hue, $"{where}: hue");
                }
            }
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void EveryRowIsARegisteredPieceOrStockAndNoTwoRowsShareATileAndZ()
    {
        var bases = GuildHallDecorArt.Pieces.ToDictionary(p => p.Id);
        Assert.Equal(new[] { "alembic", "smiths", "artificers", "structure" }, GuildHallDecorTemplates.Sets.Select(s => s.Key));
        Assert.Equal(
            new[] { "DecorAlembicHall", "DecorSmithsHall", "DecorArtificersHall", "DecorHallStructure" },
            GuildHallDecorTemplates.Sets.Select(s => s.Command)
        );

        var used = new HashSet<int>();
        foreach (var set in GuildHallDecorTemplates.Sets)
        {
            var spots = set.Groups.SelectMany(g => g.Rows).GroupBy(r => (r.Dx, r.Dy, r.Dz)).Where(x => x.Count() > 1);
            Assert.Empty(spots.Select(x => $"{set.Key} {x.Key}"));
            foreach (var row in set.Groups.SelectMany(g => g.Rows))
            {
                var ok = bases.ContainsKey(row.ItemId) || CommunityRails.ContainsValue(row.ItemId) || row.ItemId < 0x4000;
                Assert.True(ok, $"{set.Key} {row.Piece}: 0x{row.ItemId:X4} is neither a piece's base ID nor stock");
                Assert.False(string.IsNullOrWhiteSpace(row.Name), row.Piece);
                if (bases.TryGetValue(row.ItemId, out var p))
                {
                    Assert.Equal(p.Name, row.Name);
                    used.Add(p.Id);
                }
            }
        }

        // the catalogue: 179 pieces filling 0x6D70-0x6E86 with their frames, no gap and no overlap
        Assert.Equal(179, GuildHallDecorArt.Pieces.Length);
        Assert.Equal(0x6D70, GuildHallDecorArt.FirstId);
        Assert.Equal(0x6E86, GuildHallDecorArt.LastId);
        Assert.Equal(GuildHallDecorArt.LastId - GuildHallDecorArt.FirstId + 1, GuildHallDecorArt.Pieces.Sum(p => p.Frames));
        for (var i = 1; i < GuildHallDecorArt.Pieces.Length; i++)
        {
            var prev = GuildHallDecorArt.Pieces[i - 1];
            Assert.True(GuildHallDecorArt.Pieces[i].Id == prev.Id + prev.Frames, $"{GuildHallDecorArt.Pieces[i].Key} follows {prev.Key}");
        }
        Assert.Contains(GuildHallDecorArt.Find("vat-5")!.Id, used);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void OnTheLiveShardPlaceAndClearRefuseAndChangeNothing()
    {
        try
        {
            Clean();
            var set = GuildHallDecorTemplates.Find("alembic");
            var before = World.Items.Count;
            var place = GuildHallDecorSeeder.Place(set, null, Map.Trammel, new Point3D(1000, 1000, 0), false, false);
            Assert.Single(place);
            Assert.StartsWith("Refused: the guild hall decor seeders run on the test shard only", place[0]);
            Assert.Equal(before, World.Items.Count);

            GuildHallDecorSeeder.Place(set, null, Map.Trammel, new Point3D(1000, 1000, 0), false, true);
            var placed = World.Items.Count;
            var clear = GuildHallDecorSeeder.Clear(set, Map.Trammel, new Point3D(1000, 1000, 0), true, false, false);
            Assert.StartsWith("Refused:", clear[0]);
            Assert.Equal(placed, World.Items.Count);
            Assert.Null(GuildHallDecorSeeder.Refusal(true));
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void PlaceTwiceAddsNothingAGroupPlacesAloneAndClearRemovesOnlyItsOwn()
    {
        var lookalike = new Item(0x6D80);
        var gold = new Gold(10);
        try
        {
            Clean();
            var set = GuildHallDecorTemplates.Find("alembic");
            var total = set.Groups.Sum(g => g.Rows.Length);
            var anchor = new Point3D(1200, 1200, 10);

            var before = World.Items.Count;
            var dry = GuildHallDecorSeeder.Place(set, null, Map.Trammel, anchor, true, true);
            Assert.Equal(before, World.Items.Count);
            Assert.Equal(total + 1, dry.Count);

            var first = GuildHallDecorSeeder.Place(set, null, Map.Trammel, anchor, false, true);
            Assert.Contains($"placed {total} item(s)", first[^1]);
            Assert.Equal(before + total, World.Items.Count);
            foreach (var g in set.Groups)
            {
                for (var r = 0; r < g.Rows.Length; r++)
                {
                    var row = g.Rows[r];
                    var at = new Point3D(anchor.X + row.Dx, anchor.Y + row.Dy, anchor.Z + row.Dz);
                    var item = World.Items.Values.OfType<GuildHallDecor>().Single(i => i.GroupKey == g.Key && i.Row == r);
                    Assert.Equal(at, item.Location);
                    Assert.Equal(row.ItemId, item.ItemID);
                    Assert.Equal(row.Name, item.Name);
                    Assert.Equal(row.Hue, item.Hue);
                    Assert.False(item.Movable);
                    Assert.False(item.Decays);
                    Assert.Equal(anchor, item.Anchor);
                }
            }

            var again = GuildHallDecorSeeder.Place(set, null, Map.Trammel, anchor, false, true);
            Assert.Contains("placed 0 item(s)", again[^1]);
            Assert.Contains($"{total} already there", again[^1]);
            Assert.Equal(before + total, World.Items.Count);

            // a group alone, elsewhere; and another set nearby
            var vat = set.Groups.Single(g => g.Key == "vat");
            GuildHallDecorSeeder.Place(set, "vat", Map.Trammel, new Point3D(1210, 1200, 10), false, true);
            Assert.Equal(before + total + vat.Rows.Length, World.Items.Count);
            var unknown = GuildHallDecorSeeder.Place(set, "nope", Map.Trammel, anchor, false, true);
            Assert.StartsWith("No group 'nope'", unknown[0]);
            var smiths = GuildHallDecorTemplates.Find("smiths");
            GuildHallDecorSeeder.Place(smiths, null, Map.Trammel, new Point3D(1205, 1205, 10), false, true);
            var smithCount = smiths.Groups.Sum(g => g.Rows.Length);

            // not ours: a same-art lookalike on the vat's centre tile and a gold pile beside it
            lookalike.MoveToWorld(anchor, Map.Trammel);
            gold.MoveToWorld(new Point3D(anchor.X + 1, anchor.Y, anchor.Z), Map.Trammel);
            var withOthers = World.Items.Count;

            var clearDry = GuildHallDecorSeeder.Clear(set, Map.Trammel, anchor, false, true, true);
            Assert.Contains($"would remove {total + vat.Rows.Length} item(s) from 2 placement(s)", clearDry[0]);
            Assert.Equal(withOthers, World.Items.Count);

            // near a far point: nothing of this set is anchored within range
            GuildHallDecorSeeder.Clear(set, Map.Trammel, new Point3D(2000, 2000, 0), false, false, true);
            Assert.Equal(withOthers, World.Items.Count);

            GuildHallDecorSeeder.Clear(set, Map.Trammel, anchor, false, false, true);
            Assert.Equal(withOthers - total - vat.Rows.Length, World.Items.Count);
            Assert.False(lookalike.Deleted);
            Assert.False(gold.Deleted);
            Assert.Equal(smithCount, GuildHallDecorSeeder.Placed("smiths").Count);
            Assert.Empty(GuildHallDecorSeeder.Placed("alembic"));

            GuildHallDecorSeeder.Clear(smiths, Map.Felucca, new Point3D(0, 0, 0), true, false, true);
            Assert.Empty(GuildHallDecorSeeder.Placed("smiths"));
        }
        finally
        {
            Clean();
            lookalike.Delete();
            gold.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void GuildHallDecorRoundTrips()
    {
        var row = GuildHallDecorTemplates.Find("artificers").Groups[0].Rows[0];
        var item = new GuildHallDecor("artificers", "gate", 0, new Point3D(10, 20, 30), row);
        try
        {
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            item.Serialize(writer);
            writer.Flush();
            var copy = (GuildHallDecor)Activator.CreateInstance(typeof(GuildHallDecor), item.Serial)!;
            copy.Deserialize(new BufferReader(buffer));
            Assert.Equal("artificers", copy.SetKey);
            Assert.Equal("gate", copy.GroupKey);
            Assert.Equal(0, copy.Row);
            Assert.Equal(new Point3D(10, 20, 30), copy.Anchor);
            Assert.Equal(row.ItemId, copy.ItemID);
            Assert.Equal(row.Name, copy.Name);
            Assert.False(copy.Movable);
        }
        finally
        {
            item.Delete();
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheServerHasTheGuildHallDecorRecords()
    {
        ShatteredLegacyArt.Apply();
        foreach (var p in GuildHallDecorArt.Pieces)
        {
            var data = TileData.ItemTable[p.Id];
            Assert.Equal(p.Name, data.Name);
            Assert.Equal(p.Flags, data.Flags);
            Assert.Equal(p.Weight, data.Weight);
            Assert.Equal(p.Light, data.Quality);
            Assert.Equal(p.Height, data.Height);
            Assert.True(p.Name.Length <= 19, p.Name);
            Assert.True(p.Frames == 1 || (p.Flags & TileFlag.Animation) != 0, $"{p.Key}: animated without Animation");
            for (var f = 1; f < p.Frames; f++)
            {
                Assert.True(
                    TileData.ItemTable[p.Id + f].Flags == TileFlag.None && string.IsNullOrEmpty(TileData.ItemTable[p.Id + f].Name),
                    $"{p.Key}: frame {f} has a record"
                );
            }
        }

        Assert.True(TileData.ItemTable[GuildHallDecorArt.Find("vat-5")!.Id].Impassable, "the vat blocks");
        Assert.True(TileData.ItemTable[GuildHallDecorArt.Find("large-2a")!.Id].Wall, "a tower wall is a wall");
        Assert.True(TileData.ItemTable[GuildHallDecorArt.Find("crystal-green")!.Id].LightSource, "a floating crystal gives light");
        Assert.False(TileData.ItemTable[GuildHallDecorArt.Find("orb-floor")!.Id].Impassable, "the orb field is walk-through");
    }
}
