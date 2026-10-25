// GuildHallDecorVerification.cs
//
// cc-P265: the guild hall decor packs and their staff seeders. Notes in shard-migration
// notes/cc-P265-guild-hall-decor-packs.md; cc-P459's changes in notes/cc-P459-hall-decor-seeder.md.
//
// What the facts pin:
//   1. The server's templates are the design file: design/guild-hall-decor/templates.json itself (the build copies it
//      to /design in the test image, cc-P459), every set, group and row, each row's art ID resolved from its piece the
//      way the generator does, and its door and hidden switches. An edit to the JSON alone goes red.
//   2. Every row is one of this set's registered pieces (a base ID of GuildHallDecorArt), one of cc-P237's two marble
//      railings, or a stock item below 0x4000; no two rows of a set on one tile at one z; one command per set.
//   3. On the live shard (isTestShard false) place and clear refuse and change nothing.
//   4. On the test shard: a dry run changes nothing; place puts every row at the target plus its offset, not movable,
//      named and hued as its row; place again at the same point adds nothing; a group key places that group alone;
//      clear removes only this set's items near the caller (a same-art lookalike, a gold pile and another set's
//      placement stay), and clear all removes the rest.
//   5. GuildHallDecor round-trips its set, group, row, anchor and template hash; a tower door its closed art.
//   6. The server's in-memory records are in place after ShatteredLegacyArt.Apply: every piece's name, flags, weight,
//      light and height; every animated piece (more than one frame) carries Animation; frames carry no record.
//   7. cc-P459 (P419 #1): a placement one tile off the same set's is refused, and places nothing.
//   8. cc-P459 (P419 #4): after a template edit, placing at the old point is refused until it is cleared.
//   9. cc-P459 (P419 #2): a row in an impassable item, on a door or in a door's swing is refused; the dry run says why.
//  10. cc-P459 (Chase 10-23): every tower is closed until its door opens; opened, the door tile and every floor tile
//      inside are walkable (pinned's own movement check); the door closes itself once nobody stands in it.
// The art itself (registry, PNGs, records.json against GuildHallDecorArt) is checked by player-package\tests.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuildHallDecorVerification
{
    // Where the build puts the design file (docker/uo/Dockerfile); SL_DESIGN_ROOT overrides it for a local run.
    private static string DesignPath =>
        Path.Combine(Environment.GetEnvironmentVariable("SL_DESIGN_ROOT") ?? "/", GuildHallDecorTemplates.DesignFile);

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

    private static bool Switch(JsonElement r, string name) => r.TryGetProperty(name, out var v) && v.GetBoolean();

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheTemplatesAreTheDesignFile()
    {
        Assert.True(File.Exists(DesignPath), $"{DesignPath} is not in the test image (docker/uo/Dockerfile copies it)");
        var json = File.ReadAllText(DesignPath).Replace("\r\n", "\n").TrimEnd('\n');
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(json))).ToLowerInvariant();
        Assert.True(
            GuildHallDecorTemplates.DesignSha256 == sha,
            "templates.json changed without regenerating GuildHallDecorTemplates.cs (notes/cc-P265-tools/decor.py layout)"
        );

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
                    Assert.True(Switch(rows[r], "door") == row.Door, $"{where}: door");
                    Assert.True(Switch(rows[r], "hidden") == row.Hidden, $"{where}: hidden");
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
                    Assert.Equal(GuildHallDecorSeeder.TemplateHash(g), item.TemplateHash);
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
            Assert.Equal(smithCount, GuildHallDecorSeeder.Placed("smiths").Count);

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
        var doorRow = GuildHallDecorTemplates.Find("structure").Groups.Single(g => g.Key == "tower-small").Rows.Single(r => r.Door);
        var door = new GuildHallDecorDoor("structure", "tower-small", 3, new Point3D(40, 50, 0), doorRow);
        try
        {
            var copy = RoundTrip(item);
            Assert.Equal("artificers", copy.SetKey);
            Assert.Equal("gate", copy.GroupKey);
            Assert.Equal(0, copy.Row);
            Assert.Equal(new Point3D(10, 20, 30), copy.Anchor);
            Assert.Equal(row.ItemId, copy.ItemID);
            Assert.Equal(row.Name, copy.Name);
            Assert.False(copy.Movable);
            Assert.Equal(GuildHallDecorSeeder.TemplateHash("artificers", "gate"), copy.TemplateHash);
            Assert.Equal(16, copy.TemplateHash.Length);

            var doorCopy = Assert.IsType<GuildHallDecorDoor>(RoundTrip(door));
            Assert.Equal(doorRow.ItemId, doorCopy.ClosedId);
            Assert.Equal("tower-small", doorCopy.GroupKey);
            Assert.False(doorCopy.Open);
            doorCopy.Delete();
            copy.Delete();
        }
        finally
        {
            item.Delete();
            door.Delete();
        }
    }

    private static T RoundTrip<T>(T original) where T : Item
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();
        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
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

    // ---------------------------------------------------------------- 7

    [Fact]
    public void APlacementOneTileOffTheSameSetIsRefusedAndPlacesNothing()
    {
        try
        {
            Clean();
            var set = GuildHallDecorTemplates.Find("alembic");
            var vat = set.Groups.Single(g => g.Key == "vat");
            var anchor = new Point3D(1300, 1300, 0);
            GuildHallDecorSeeder.Place(set, "vat", Map.Trammel, anchor, false, true);
            var count = World.Items.Count;

            // P419's case: one tile east, three rows of the new vat would land on the old one's tiles
            var shifted = GuildHallDecorSeeder.Place(set, "vat", Map.Trammel, new Point3D(1301, 1300, 0), false, true);
            Assert.Contains("Refused: this would overlap the same set placed from another point", shifted[^1]);
            Assert.Contains(shifted, l => l.Contains("it meets vat placed from 1300,1300,0"));
            Assert.Equal(count, World.Items.Count);

            // another group of the set that would reach the vat's tiles is refused too; the dry run as well
            var dry = GuildHallDecorSeeder.Place(set, "scrying", Map.Trammel, new Point3D(1300 - 2, 1300 - 5, 0), true, true);
            Assert.Contains("Refused", dry[^1]);
            Assert.Equal(count, World.Items.Count);

            // clear of its tiles, the same group places
            var apart = GuildHallDecorSeeder.Place(set, "vat", Map.Trammel, new Point3D(1310, 1300, 0), false, true);
            Assert.Contains($"placed {vat.Rows.Length} item(s)", apart[^1]);
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void AfterATemplateEditPlacingAtTheOldPointIsRefusedUntilCleared()
    {
        var set = GuildHallDecorTemplates.Find("alembic");
        var scrying = set.Groups.Single(g => g.Key == "scrying");
        var original = scrying.Rows[1];
        var anchor = new Point3D(1340, 1300, 0);
        try
        {
            Clean();
            GuildHallDecorSeeder.Place(set, "scrying", Map.Trammel, anchor, false, true);
            var count = World.Items.Count;
            var oldHash = GuildHallDecorSeeder.TemplateHash(scrying);

            // the crystal ball's row moved one tile east in the template (as F-49 will tune them)
            scrying.Rows[1] = original with { Dx = original.Dx + 1 };
            Assert.NotEqual(oldHash, GuildHallDecorSeeder.TemplateHash(scrying));

            var again = GuildHallDecorSeeder.Place(set, "scrying", Map.Trammel, anchor, false, true);
            Assert.Contains("Refused: the template changed since it was placed at 1340,1300,0", again[^1]);
            Assert.Contains("clear", again[^1]);
            Assert.Contains(again, l => l.Contains("scrying: 2 item(s) here were placed from another version"));
            Assert.Equal(count, World.Items.Count);

            // an item saved before template hashes (version 0, no hash) asks for the same
            scrying.Rows[1] = original;
            GuildHallDecorSeeder.Placed("alembic").First().TemplateHash = null;
            Assert.Contains("Refused", GuildHallDecorSeeder.Place(set, "scrying", Map.Trammel, anchor, false, true)[^1]);

            // cleared, the edited template places at the same point
            scrying.Rows[1] = original with { Dx = original.Dx + 1 };
            GuildHallDecorSeeder.Clear(set, Map.Trammel, anchor, false, false, true);
            var fresh = GuildHallDecorSeeder.Place(set, "scrying", Map.Trammel, anchor, false, true);
            Assert.Contains("placed 2 item(s)", fresh[^1]);
            var ball = GuildHallDecorSeeder.Placed("alembic").Single(i => i.Row == 1);
            Assert.Equal(new Point3D(anchor.X + original.Dx + 1, anchor.Y + original.Dy, anchor.Z + original.Dz), ball.Location);
        }
        finally
        {
            scrying.Rows[1] = original;
            Clean();
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void ARowInSomethingImpassableOrADoorwayIsRefusedAndTheDryRunSaysWhy()
    {
        ShatteredLegacyArt.Apply(); // the vat's record: impassable
        using var tiles = StockTiles();
        var set = GuildHallDecorTemplates.Find("alembic");
        var scrying = set.Groups.Single(g => g.Key == "scrying");
        var anchor = new Point3D(1380, 1300, 0);
        var cloth = new Point3D(anchor.X + scrying.Rows[0].Dx, anchor.Y + scrying.Rows[0].Dy, anchor.Z);
        var crate = new Item(GuildHallDecorArt.Find("vat-5")!.Id) { Movable = false };
        var door = new MetalDoor(DoorFacing.WestCW);
        var swinging = new MetalDoor(DoorFacing.WestCW);
        try
        {
            Clean();
            var count = World.Items.Count;

            crate.MoveToWorld(cloth, Map.Trammel);
            var dry = GuildHallDecorSeeder.Place(set, "scrying", Map.Trammel, anchor, true, true);
            Assert.Contains(dry, l => l.Contains($"scrying ball-cloth at {cloth.X},{cloth.Y},{cloth.Z}: an impassable Item"));
            Assert.Contains("dry run: would be refused", dry[^1]);
            var real = GuildHallDecorSeeder.Place(set, "scrying", Map.Trammel, anchor, false, true);
            Assert.Contains("Refused: 2 row(s)", real[^1]); // the cloth and the ball above it both stand in the vat
            Assert.Equal(count, World.Items.Count);
            crate.Internalize();

            door.MoveToWorld(cloth, Map.Trammel);
            Assert.Contains(GuildHallDecorSeeder.Place(set, "scrying", Map.Trammel, anchor, false, true), l => l.Contains("a door (MetalDoor) on this tile"));
            door.Internalize();

            // a door one tile north-east whose swing (west, clockwise: one west, one south) lands on the cloth's tile
            swinging.MoveToWorld(new Point3D(cloth.X + 1, cloth.Y - 1, cloth.Z), Map.Trammel);
            Assert.Contains(GuildHallDecorSeeder.Place(set, "scrying", Map.Trammel, anchor, false, true), l => l.Contains("the swing of a door"));
            swinging.Internalize();
            Assert.Equal(count, World.Items.Count); // nothing of ours was made

            Assert.Empty(GuildHallDecorSeeder.Placed("alembic"));
            var clear = GuildHallDecorSeeder.Place(set, "scrying", Map.Trammel, anchor, false, true);
            Assert.Contains("placed 2 item(s)", clear[^1]);
        }
        finally
        {
            crate.Delete();
            door.Delete();
            swinging.Delete();
            Clean();
        }
    }

    // ---------------------------------------------------------------- 10

    // The test host has no tiledata and masks every item ID to row 0 (Route/TestHostTileData.cs): a fact that needs walls
    // to block and floors to hold seeds the stock rows the towers use, which also raises the mask for the decor pieces'
    // own rows (ShatteredLegacyArt.Apply writes those).
    private static TestTileRows.Scope StockTiles() =>
        TestTileRows.Seed(TileRows.NoDraw, TileRows.InvisibleWall, TileRows.StonePavers, TileRows.WhiteMarble);

    private static readonly Direction[] Directions =
    {
        Direction.North, Direction.Right, Direction.East, Direction.Down,
        Direction.South, Direction.Left, Direction.West, Direction.Up
    };

    // Every tile a player can walk to from start inside the box, by pinned's movement check.
    private static HashSet<Point2D> Walkable(Mobile m, Map map, Point3D start, int x0, int y0, int x1, int y1)
    {
        var seen = new HashSet<Point2D> { new(start.X, start.Y) };
        var queue = new Queue<Point3D>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var d in Directions)
            {
                if (!Server.Movement.Movement.CheckMovement(m, map, p, d, out var z))
                {
                    continue;
                }

                int x = p.X, y = p.Y;
                Server.Movement.Movement.Offset(d, ref x, ref y);
                if (x < x0 || x > x1 || y < y0 || y > y1 || !seen.Add(new Point2D(x, y)))
                {
                    continue;
                }

                queue.Enqueue(new Point3D(x, y, z));
            }
        }

        return seen;
    }

    [Fact]
    public void EveryTowerIsClosedUntilItsDoorOpensOntoAWalkableInside()
    {
        ShardTestClock.Arm();
        ShatteredLegacyArt.Apply();
        if (Server.Movement.Movement.Impl == null)
        {
            Server.Movement.MovementImpl.Configure();
        }

        using var tiles = StockTiles();
        var set = GuildHallDecorTemplates.Find("structure");
        var anchor = new Point3D(1500, 1600, 0);
        var walker = new PlayerMobile { Player = true, Young = false };
        walker.MoveToWorld(new Point3D(1450, 1550, 0), Map.Trammel); // away from the towers: it is only the check's mobile
        try
        {
            Clean();
            var placed = GuildHallDecorSeeder.Place(set, null, Map.Trammel, anchor, false, true);
            Assert.Contains("placed", placed[^1]);

            var towers = set.Groups.Where(g => g.Key.StartsWith("tower-", StringComparison.Ordinal)).ToList();
            Assert.Equal(5, towers.Count);
            var doors = GuildHallDecorSeeder.Placed("structure").OfType<GuildHallDecorDoor>().ToList();
            Assert.Equal(towers.Select(g => g.Key).OrderBy(k => k), doors.Select(d => d.GroupKey).OrderBy(k => k));
            Assert.All(towers, g => Assert.Single(g.Rows, r => r.Door));
            Assert.All(GuildHallDecorSeeder.Placed("structure").Where(i => i.ItemID == 0x21A3), i => Assert.False(i.Visible));

            Point2D At(GuildHallDecorTemplates.Row r) => new(anchor.X + r.Dx, anchor.Y + r.Dy);
            var floors = towers.ToDictionary(
                g => g.Key,
                g => g.Rows.Where(r => r.ItemId is 0x051A or 0x050E).Select(At).ToList()
            );
            Assert.Equal(new[] { 14, 5, 0, 11, 11 }, towers.Select(g => floors[g.Key].Count));

            int x0 = anchor.X - 4, y0 = anchor.Y - 26, x1 = anchor.X + 28, y1 = anchor.Y + 8;
            var start = new Point3D(anchor.X - 3, anchor.Y - 3, anchor.Z);

            // closed: no door tile and no floor tile can be reached from outside
            var closed = Walkable(walker, Map.Trammel, start, x0, y0, x1, y1);
            Assert.True(closed.Count > 500, $"only {closed.Count} tiles walkable outside");
            foreach (var g in towers)
            {
                Assert.DoesNotContain(At(g.Rows.Single(r => r.Door)), closed);
                Assert.All(floors[g.Key], f => Assert.False(closed.Contains(f), $"{g.Key}: {f} is reachable with the door shut"));
            }

            // opened by a player beside each door: the doorway turns walkable and unseen
            foreach (var door in doors)
            {
                walker.Location = new Point3D(door.X + 1, door.Y + 1, door.Z);
                door.OnDoubleClick(walker);
                Assert.True(door.Open, $"{door.GroupKey}: the door did not open");
                Assert.False(door.Visible);
                Assert.Equal(GuildHallDecorDoor.OpenedId, door.ItemID);
            }

            walker.Location = new Point3D(1450, 1550, 0);
            var open = Walkable(walker, Map.Trammel, start, x0, y0, x1, y1);
            foreach (var g in towers)
            {
                Assert.True(open.Contains(At(g.Rows.Single(r => r.Door))), $"{g.Key}: its open doorway is not walkable");
                Assert.All(floors[g.Key], f => Assert.True(open.Contains(f), $"{g.Key}: floor {f} is not walkable with the door open"));
            }

            // a player standing in the doorway keeps it open; it closes once the doorway is free
            var stander = new PlayerMobile { Player = true, Young = false };
            stander.MoveToWorld(doors[0].Location, Map.Trammel);
            ShardTestClock.AdvanceSeconds(21);
            Assert.True(doors[0].Open, "the door shut on a player standing in it");
            Assert.All(doors.Skip(1), d => Assert.False(d.Open, $"{d.GroupKey}: still open after 21 seconds"));
            Assert.All(doors.Skip(1), d => Assert.True(d.Visible));
            stander.Delete();
            ShardTestClock.AdvanceSeconds(11);
            Assert.False(doors[0].Open);
            Assert.Equal(doors[0].ClosedId, doors[0].ItemID);

            // from too far a player cannot open it
            walker.Location = new Point3D(doors[0].X + 3, doors[0].Y + 3, doors[0].Z);
            doors[0].OnDoubleClick(walker);
            Assert.False(doors[0].Open);
        }
        finally
        {
            walker.Delete();
            Clean();
        }
    }
}
