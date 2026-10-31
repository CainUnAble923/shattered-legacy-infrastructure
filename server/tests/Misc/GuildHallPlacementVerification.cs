// GuildHallPlacementVerification.cs
//
// cc-P281 (F-49): the guilds' real halls on the test world. Notes in shard-migration
// notes/cc-P281-guild-halls-placement.md; the map checks of every tile are notes/cc-P281-tools/check.py (the test host
// loads no client files, so they cannot run here).
//
// What the facts pin:
//   1. The server's table is the design file (design/guild-halls/halls.json, through its generated copy): every hall,
//      field by field, and the halls that wait.
//   2. Every hall is a guild (the League apart, placed with its steward: cc-P708), placed with that guild's own
//      guildmaster type; every guild but the
//      Maritime Brotherhood has a hall and the Maritime Brotherhood waits; a new hall is OSI's Guild House; every decor
//      group is one of cc-P265's; no two halls share a guildmaster tile or a sign tile.
//   3. On the live shard (isTestShard false) place, clear and runes refuse and change nothing.
//   4. On the test shard: a dry run changes nothing; place puts the guildmaster on its post (not walking, facing as
//      listed), the sign (its art, its name) hiding a stock sign on its tile, a new hall's building and four doors (the
//      front pair linked), and the decor; place again adds nothing; clear removes exactly those, shows the stock sign
//      again, and leaves a same-type guildmaster elsewhere and another set's decor alone.
//   5. The sign, the building and the door round-trip their hall (and the sign the stock sign it hid).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using static Server.ClusterFGuildHallPlacementData;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuildHallPlacementVerification
{
    private static void Clean()
    {
        ClusterFGuildHallPlacement.Clear(null, false, true);
        foreach (var i in World.Items.Values.Where(i => i is IGuildHallPart or GuildHallDecor).ToList())
        {
            i.Delete();
        }
    }

    private static Point3D P(JsonElement e) =>
        new(e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("z").GetInt32());

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheTableIsTheDesignFile()
    {
        var json = GuildHallPlacementDesignCopy.Json.Replace("\r\n", "\n") + "\n";
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(json))).ToLowerInvariant();
        Assert.Equal(GuildHallPlacementDesignCopy.Sha256, sha);
        Assert.Equal(DesignSha256, sha);

        using var doc = JsonDocument.Parse(json);
        var halls = doc.RootElement.GetProperty("halls").EnumerateArray().ToArray();
        Assert.Equal(halls.Length, Halls.Length);
        for (var i = 0; i < halls.Length; i++)
        {
            var j = halls[i];
            var h = Halls[i];
            var key = j.GetProperty("key").GetString();
            Assert.True(key == h.Key, $"row {i}: key");
            Assert.True(j.GetProperty("guild").GetString() == h.Guild, $"{key}: guild");
            Assert.True(Enum.Parse<Kind>(j.GetProperty("kind").GetString()!, true) == h.Kind, $"{key}: kind");
            Assert.True(j.GetProperty("building_name").GetString() == h.Building, $"{key}: building");
            Assert.True(j.GetProperty("town").GetString() == h.Town, $"{key}: town");
            Assert.True(j.GetProperty("facet").GetInt32() == h.MapIndex, $"{key}: facet");

            var n = j.GetProperty("npc");
            Assert.True(n.GetProperty("type").GetString() == h.NpcType.Name, $"{key}: npc type");
            Assert.True(P(n) == h.Npc, $"{key}: npc tile");
            Assert.True(Enum.Parse<Direction>(n.GetProperty("facing").GetString()!) == h.NpcFacing, $"{key}: npc facing");

            var s = j.GetProperty("sign");
            Assert.True(Enum.Parse<SignType>(s.GetProperty("type").GetString()!) == h.SignType, $"{key}: sign type");
            Assert.True(Enum.Parse<SignFacing>(s.GetProperty("facing").GetString()!) == h.SignFacing, $"{key}: sign facing");
            Assert.True(P(s) == h.Sign, $"{key}: sign tile");
            Assert.True(s.GetProperty("name").GetString() == h.SignName, $"{key}: sign name");

            var t = j.GetProperty("travel");
            Assert.True(P(t) == h.Travel, $"{key}: travel stop");
            Assert.True(t.GetProperty("note").GetString() == h.TravelNote, $"{key}: travel note");

            if (j.TryGetProperty("building", out var b))
            {
                Assert.True(b.GetProperty("multi").GetInt32() == h.MultiId, $"{key}: multi");
                Assert.True(P(b) == h.MultiAt, $"{key}: building point");
            }
            else
            {
                Assert.True(h.MultiId == 0, $"{key}: no building");
            }

            var decor = j.TryGetProperty("decor", out var d) ? d.EnumerateArray().ToArray() : [];
            Assert.True(decor.Length == h.Decor.Length, $"{key}: decor count");
            for (var k = 0; k < decor.Length; k++)
            {
                Assert.True(decor[k].GetProperty("set").GetString() == h.Decor[k].Set, $"{key}: decor {k} set");
                Assert.True(decor[k].GetProperty("group").GetString() == h.Decor[k].Group, $"{key}: decor {k} group");
                Assert.True(P(decor[k]) == h.Decor[k].At, $"{key}: decor {k} anchor");
            }
        }

        var waits = doc.RootElement.GetProperty("waiting").EnumerateArray().Select(w => w.GetProperty("key").GetString()).ToArray();
        Assert.Equal(waits, Waits.Select(w => w.Key));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void EveryGuildButTheMaritimeBrotherhoodHasAHallWithItsOwnGuildmaster()
    {
        ClusterFGuildSystem.EnsureRegistered();
        foreach (var h in Halls)
        {
            Assert.True(typeof(Mobile).IsAssignableFrom(h.NpcType), $"{h.Key}: {h.NpcType.Name} is not a mobile");
            if (h.Key == "league")
            {
                // cc-P708: the headquarters at Valunstead has its own steward; Elara stays in New Haven
                Assert.Equal(typeof(LeagueSteward), h.NpcType);
                continue;
            }

            var def = ClusterFGuildSystem.GetDef(h.Key);
            Assert.True(def != null, $"{h.Key} is not a guild");
            var own = GuildLocations.For(h.Key).Select(l => l.NpcType).Append(def!.GuildmasterType).ToHashSet();
            Assert.True(own.Contains(h.NpcType), $"{h.Key}: {h.NpcType.Name} is not one of the guild's own guildmasters");
            Assert.Same(def, ClusterFGuildSystem.GetDefForGuildmaster(h.NpcType));
        }

        var guilds = ClusterFGuildSystem.AllGuilds.Keys.ToHashSet();
        var placed = Halls.Select(h => h.Key).Where(k => k != "league").ToHashSet();
        Assert.Equal(guilds.Except(new[] { "maritime" }).OrderBy(k => k), placed.OrderBy(k => k));
        Assert.Equal(new[] { "maritime" }, Waits.Select(w => w.Key));
        Assert.Equal(Halls.Length, Halls.Select(h => h.Key).Distinct().Count());

        foreach (var h in Halls.Where(h => h.Kind == Kind.New))
        {
            Assert.Equal(ClusterFGuildHallPlacement.GuildHouseMulti, h.MultiId);
        }

        foreach (var h in Halls.Where(h => h.Kind != Kind.New))
        {
            Assert.Equal(0, h.MultiId);
        }

        foreach (var d in Halls.SelectMany(h => h.Decor))
        {
            Assert.True(
                GuildHallDecorTemplates.Find(d.Set)?.Groups.Any(g => g.Key == d.Group) == true,
                $"no decor group {d.Set}/{d.Group}"
            );
        }

        Assert.Equal(Halls.Length, Halls.Select(h => (h.MapIndex, h.Npc)).Distinct().Count());
        Assert.Equal(Halls.Length, Halls.Select(h => (h.MapIndex, h.Sign)).Distinct().Count());
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void OnTheLiveShardPlaceClearAndRunesRefuseAndChangeNothing()
    {
        try
        {
            Clean();
            var items = World.Items.Count;
            var mobiles = World.Mobiles.Count;

            var place = ClusterFGuildHallPlacement.Place(null, false, false);
            Assert.Single(place);
            Assert.StartsWith("Refused: the guild hall placement runs on the test shard only", place[0]);
            Assert.Equal(items, World.Items.Count);
            Assert.Equal(mobiles, World.Mobiles.Count);

            ClusterFGuildHallPlacement.Place("warriors", false, true);
            var placedItems = World.Items.Count;
            var placedMobiles = World.Mobiles.Count;
            var clear = ClusterFGuildHallPlacement.Clear(null, false, false);
            Assert.StartsWith("Refused:", Assert.Single(clear));
            Assert.Equal(placedItems, World.Items.Count);
            Assert.Equal(placedMobiles, World.Mobiles.Count);

            Assert.Null(ClusterFGuildHallPlacement.Runes(null, false, out var why));
            Assert.StartsWith("Refused:", why);
            Assert.Null(ClusterFGuildHallPlacement.Refusal(true));
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void PlaceTwiceAddsNothingAndClearRemovesExactlyItsOwn()
    {
        var warriors = ClusterFGuildHallPlacement.Find("warriors");
        var smiths = ClusterFGuildHallPlacement.Find("smithing");
        var stockSign = new Sign(SignType.WeaponsGuild, warriors.SignFacing);
        var lookalike = new WarriorGuildmaster();
        var otherDecorSet = GuildHallDecorTemplates.Find("artificers");
        try
        {
            Clean();
            stockSign.MoveToWorld(warriors.Sign, Map.Trammel);
            lookalike.MoveToWorld(new Point3D(warriors.Npc.X + 6, warriors.Npc.Y, warriors.Npc.Z), Map.Trammel);
            GuildHallDecorSeeder.Place(otherDecorSet, null, Map.Trammel, new Point3D(smiths.MultiAt.X + 30, smiths.MultiAt.Y, 15), false, true);
            var otherDecor = GuildHallDecorSeeder.Placed("artificers").Count;

            var items = World.Items.Count;
            var mobiles = World.Mobiles.Count;
            var dry = ClusterFGuildHallPlacement.Place(null, true, true);
            Assert.Equal(items, World.Items.Count);
            Assert.Equal(mobiles, World.Mobiles.Count);
            Assert.Contains("dry run: would place", dry[^1]);

            var first = ClusterFGuildHallPlacement.Place(null, false, true);
            Assert.Contains("0 already standing", first[^1]);

            foreach (var h in Halls)
            {
                var map = ClusterFGuildHallPlacement.MapOf(h);
                var npc = ClusterFGuildHallPlacement.FindNpc(h);
                Assert.True(npc != null, $"{h.Key}: no guildmaster");
                Assert.Equal(h.Npc, npc!.Location);
                Assert.Same(map, npc.Map);
                Assert.True(npc.CantWalk, h.Key);
                Assert.Equal(h.NpcFacing, npc.Direction & Direction.Mask);

                var sign = ClusterFGuildHallPlacement.Parts(h).OfType<GuildHallSign>().Single();
                Assert.Equal(h.Sign, sign.Location);
                Assert.Equal(0xB95 + 2 * (int)h.SignType + (int)h.SignFacing, sign.ItemID);
                Assert.Equal(h.SignName, sign.Name);
                Assert.False(sign.Movable);

                foreach (var d in h.Decor)
                {
                    var rows = GuildHallDecorTemplates.Find(d.Set).Groups.Single(g => g.Key == d.Group).Rows.Length;
                    Assert.Equal(rows, ClusterFGuildHallPlacement.DecorOf(h, d).Count);
                }
            }

            // the stock sign on the reused hall's sign tile is hidden, and remembered
            Assert.False(stockSign.Visible);
            Assert.Same(stockSign, ClusterFGuildHallPlacement.Parts(warriors).OfType<GuildHallSign>().Single().HiddenStockSign);

            // the new hall: OSI's Guild House and its four doors, the front pair linked
            var parts = ClusterFGuildHallPlacement.Parts(smiths);
            var building = parts.OfType<GuildHallBuilding>().Single();
            Assert.Equal(ClusterFGuildHallPlacement.GuildHouseMulti, building.ItemID);
            Assert.Equal(smiths.MultiAt, building.Location);
            var doors = parts.OfType<GuildHallDoor>().ToList();
            Assert.Equal(4, doors.Count);
            foreach (var (dx, dy, dz, _) in ClusterFGuildHallPlacement.GuildHouseDoors)
            {
                var at = new Point3D(smiths.MultiAt.X + dx, smiths.MultiAt.Y + dy, smiths.MultiAt.Z + dz);
                Assert.Single(doors, d => d.Location == at);
            }

            var west = doors.Single(d => d.Location.X == smiths.MultiAt.X - 1 && d.Location.Y == smiths.MultiAt.Y + 6);
            var east = doors.Single(d => d.Location.X == smiths.MultiAt.X && d.Location.Y == smiths.MultiAt.Y + 6);
            Assert.Same(east, west.Link);
            Assert.Same(west, east.Link);
            Assert.All(doors, d => Assert.False(d.Locked));

            // place again: nothing new
            var afterItems = World.Items.Count;
            var afterMobiles = World.Mobiles.Count;
            var second = ClusterFGuildHallPlacement.Place(null, false, true);
            Assert.Contains("placed 0 thing(s)", second[^1]);
            Assert.Equal(afterItems, World.Items.Count);
            Assert.Equal(afterMobiles, World.Mobiles.Count);

            // one hall alone
            ClusterFGuildHallPlacement.Clear("warriors", false, true);
            Assert.True(stockSign.Visible);
            Assert.Null(ClusterFGuildHallPlacement.FindNpc(warriors));
            Assert.NotNull(ClusterFGuildHallPlacement.FindNpc(smiths));
            var one = ClusterFGuildHallPlacement.Place("warriors", false, true);
            Assert.Contains("0 already standing", one[^1]);
            Assert.False(stockSign.Visible);

            // clear: exactly ours
            var clear = ClusterFGuildHallPlacement.Clear(null, false, true);
            Assert.Contains("nothing else is touched", clear[^1]);
            Assert.Empty(World.Items.Values.OfType<IGuildHallPart>());
            Assert.All(Halls, h => Assert.Null(ClusterFGuildHallPlacement.FindNpc(h)));
            Assert.All(Halls.SelectMany(h => h.Decor.Select(d => (h, d))), x => Assert.Empty(ClusterFGuildHallPlacement.DecorOf(x.h, x.d)));
            Assert.True(stockSign.Visible);
            Assert.False(stockSign.Deleted);
            Assert.False(lookalike.Deleted);
            Assert.Equal(otherDecor, GuildHallDecorSeeder.Placed("artificers").Count);

            var unknown = ClusterFGuildHallPlacement.Place("maritime", false, true);
            Assert.StartsWith("No hall 'maritime'", Assert.Single(unknown));
        }
        finally
        {
            Clean();
            stockSign.Delete();
            lookalike.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheSignTheBuildingAndTheDoorRoundTripTheirHall()
    {
        var stock = new Sign(SignType.Bank, SignFacing.North);
        var items = new List<Item>
        {
            new GuildHallSign("warriors", 0xBD9, "The Warriors' Brotherhood") { HiddenStockSign = stock },
            new GuildHallBuilding("smithing", ClusterFGuildHallPlacement.GuildHouseMulti),
            new GuildHallDoor("smithing", DoorFacing.EastCCW)
        };
        try
        {
            foreach (var item in items)
            {
                var buffer = new byte[65536];
                var writer = new BufferWriter(buffer, true);
                item.Serialize(writer);
                writer.Flush();
                var copy = (Item)Activator.CreateInstance(item.GetType(), item.Serial)!;
                copy.Deserialize(new BufferReader(buffer));
                Assert.Equal(((IGuildHallPart)item).HallKey, ((IGuildHallPart)copy).HallKey);
                Assert.Equal(item.ItemID, copy.ItemID);
                if (copy is GuildHallSign s)
                {
                    Assert.Same(stock, s.HiddenStockSign);
                    Assert.Equal("The Warriors' Brotherhood", s.Name);
                    Assert.False(s.Movable);
                }
            }
        }
        finally
        {
            foreach (var i in items)
            {
                i.Delete();
            }

            stock.Delete();
        }
    }
}
