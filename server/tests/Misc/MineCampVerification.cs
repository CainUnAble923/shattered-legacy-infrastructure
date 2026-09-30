// MineCampVerification.cs
//
// cc-P16: two tents at the New Haven mine camp (Trammel) and the Miners' Compact Liaison and the Survey
// Archivist moved to their openings. Notes in shard-migration notes/cc-P16-mine-camp-tents.md.
//
// What the facts pin:
//   1. The layout the server loads (ClusterFMineCampLayout) is design/mine-camp-tents/layout.json,
//      component for component, and so are the NPC moves.
//   2. The seeder, run twice, places exactly two tents, every component at its design tile, not movable,
//      not decaying, with no deed. A dry run places nothing.
//   3. No component sits on either NPC tile or on the tile directly east of a tent opening, and that
//      tile is the NPC's.
//   4. The move command, run twice, moves each NPC once and creates none; the Guild Directory arrow for
//      the Miners' Compact targets the Liaison before and after the move.
//   5. World load (ClusterFInstitutionSeeder) leaves an NPC standing near its old spot where it is and
//      creates nothing beside it; on a world without them it places both at the openings, facing East.
//
// The test host has no map files (cc-P17 section 1.3), so nothing here asks the map about z. The z
// values were checked offline against the server's map files (notes, Part A).

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
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class MineCampVerification
{
    private readonly ITestOutputHelper _out;

    public MineCampVerification(ITestOutputHelper output) => _out = output;

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheLoadedLayoutIsTheDesignFile()
    {
        var json = MineCampLayoutDesignCopy.Json.Replace("\r\n", "\n");
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(json))).ToLowerInvariant();
        Assert.Equal(MineCampLayoutDesignCopy.Sha256, sha);
        Assert.Equal(ClusterFMineCampLayout.DesignSha256, sha);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(ClusterFMineCampLayout.MapName, root.GetProperty("map").GetString());

        var addons = root.GetProperty("addons").EnumerateArray().ToArray();
        Assert.Equal(addons.Length, ClusterFMineCampLayout.Addons.Length);

        var total = 0;
        for (var i = 0; i < addons.Length; i++)
        {
            var (name, loaded) = ClusterFMineCampLayout.Addons[i];
            Assert.Equal(addons[i].GetProperty("name").GetString(), name);

            var design = addons[i].GetProperty("components").EnumerateArray().ToArray();
            Assert.Equal(design.Length, loaded.Length);

            for (var j = 0; j < design.Length; j++)
            {
                var d = design[j];
                var expected = new MineCampComponent(
                    d.GetProperty("group").GetString(),
                    Convert.ToInt32(d.GetProperty("id").GetString(), 16),
                    d.GetProperty("name").GetString(),
                    d.GetProperty("x").GetInt32(),
                    d.GetProperty("y").GetInt32(),
                    d.GetProperty("z").GetInt32()
                );
                Assert.Equal(expected, loaded[j]);
            }

            total += loaded.Length;
        }

        // The brief's counts, 21 and 22, counted here rather than taken from it.
        Assert.Equal(21, ClusterFMineCampLayout.MinersCompactTent.Length);
        Assert.Equal(22, ClusterFMineCampLayout.SurveyArchivistTent.Length);
        Assert.Equal(43, total);

        var npcs = root.GetProperty("npcs").EnumerateArray().ToArray();
        Assert.Equal(npcs.Length, ClusterFMineCampLayout.Npcs.Length);
        for (var i = 0; i < npcs.Length; i++)
        {
            var n = npcs[i];
            var move = ClusterFMineCampLayout.Npcs[i];
            Assert.Equal(n.GetProperty("type").GetString(), move.TypeName);
            Assert.Equal(Point(n.GetProperty("from")), move.From);
            Assert.Equal(Point(n.GetProperty("to")), move.To);
            Assert.Equal(Enum.Parse<Direction>(n.GetProperty("facing").GetString()), move.Facing);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void SeedingTwicePlacesExactlyTwoTents()
    {
        ClearCamp();
        try
        {
            var dry = ClusterFMineCampSeeder.Seed(true);
            _out.WriteLine(string.Join("\n", dry));
            Assert.Empty(Tents());

            var first = ClusterFMineCampSeeder.Seed(false);
            var second = ClusterFMineCampSeeder.Seed(false);
            _out.WriteLine(string.Join("\n", first));
            _out.WriteLine(string.Join("\n", second));

            var tents = Tents();
            Assert.Equal(2, tents.Count);
            Assert.Single(tents, t => t is MinersCompactTentAddon);
            Assert.Single(tents, t => t is SurveyArchivistTentAddon);
            Assert.Contains("placed 2 of 2", first.Last());
            Assert.Contains("placed 0 of 2", second.Last());

            var components = 0;
            foreach (var (type, _, layout) in ClusterFMineCampSeeder.Tents)
            {
                var tent = tents.Single(t => t.GetType() == type);
                Assert.Same(Map.Trammel, tent.Map);
                Assert.Null(tent.Deed);
                Assert.False(tent.Decays);
                Assert.Equal(layout.Length, tent.Components.Count);

                for (var i = 0; i < layout.Length; i++)
                {
                    var c = tent.Components[i];
                    Assert.Equal(typeof(AddonComponent), c.GetType());
                    Assert.Equal(layout[i].ItemID, c.ItemID);
                    Assert.Equal(layout[i].Location, c.Location);
                    Assert.Same(Map.Trammel, c.Map);
                    Assert.False(c.Movable);
                    Assert.False(c.Decays);
                }

                components += tent.Components.Count;
            }

            Assert.Equal(43, components);
            Assert.Equal(43, World.Items.Values.Count(i => !i.Deleted && i is AddonComponent { Addon: MinersCompactTentAddon or SurveyArchivistTentAddon }));
        }
        finally
        {
            ClearCamp();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void NoComponentSitsOnAnNpcTileOrInFrontOfAnOpening()
    {
        var occupied = new Dictionary<Point2D, string>();
        foreach (var (name, components) in ClusterFMineCampLayout.Addons)
        {
            foreach (var c in components)
            {
                occupied.TryAdd(new Point2D(c.X, c.Y), $"{name} 0x{c.ItemID:X4} {c.Name}");
            }
        }

        var wrong = new List<string>();
        for (var i = 0; i < ClusterFMineCampLayout.Addons.Length; i++)
        {
            var (name, components) = ClusterFMineCampLayout.Addons[i];
            var tent = components.Where(c => c.Group == "tent").ToArray();

            // The opening: the tent's east column tile that has no wall piece.
            var eastX = tent.Max(c => c.X);
            var openings = tent.Where(c => c.X == eastX).Select(c => c.Y).Distinct()
                .Where(y => !tent.Any(c => c.X == eastX && c.Y == y && c.Name == "tent wall")).ToArray();
            var opening = Assert.Single(openings);
            var inFront = new Point2D(eastX + 1, opening);
            _out.WriteLine($"{name}: opening {eastX},{opening}; in front {inFront}");

            // The tile in front of the opening is where that tent's NPC stands.
            var npc = ClusterFMineCampLayout.Npcs[i];
            Assert.Equal(inFront, new Point2D(npc.To.X, npc.To.Y));

            if (occupied.TryGetValue(inFront, out var what))
            {
                wrong.Add($"{what} is in front of {name}'s opening at {inFront}");
            }
        }

        foreach (var npc in ClusterFMineCampLayout.Npcs)
        {
            if (occupied.TryGetValue(new Point2D(npc.To.X, npc.To.Y), out var what))
            {
                wrong.Add($"{what} is on {npc.TypeName}'s tile {npc.To}");
            }
        }

        _out.WriteLine(string.Join("\n", wrong));
        Assert.Empty(wrong);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheMoveCommandRunTwiceMovesEachNpcOnceAndCreatesNone()
    {
        ClearCamp();
        var liaisonMove = ClusterFMineCampLayout.Npcs.Single(n => n.TypeName == nameof(MinersCompactLiaison));
        var archivistMove = ClusterFMineCampLayout.Npcs.Single(n => n.TypeName == nameof(SurveyArchivist));
        var mining = GuildLocations.For("mining").Single();
        var pm = new PlayerMobile { Player = true };

        try
        {
            // With neither on the world, it creates nothing.
            var none = ClusterFMineCampSeeder.MoveNpcs(false);
            _out.WriteLine(string.Join("\n", none));
            Assert.Empty(CampNpcs());
            Assert.Contains("0 of 2", none.Last());

            // As the institution seeder placed them before cc-P16.
            var liaison = new MinersCompactLiaison();
            var archivist = new SurveyArchivist();
            OldPlace(liaison, liaisonMove.From, Direction.South);
            OldPlace(archivist, archivistMove.From, Direction.West);

            // The directory arrow finds the Liaison at its old spot too, and targets the mobile.
            pm.MoveToWorld(new Point3D(mining.Point.X - 5, mining.Point.Y, mining.Point.Z), Map.Trammel);
            Assert.StartsWith("Follow the arrow", ClusterFGuildStarter.ShowTheWay(pm, mining));
            Assert.Same(liaison, Assert.IsType<GuildDirectionArrow>(pm.QuestArrow).Target);
            pm.QuestArrow = null;

            var mobilesBefore = World.Mobiles.Count;

            var dry = ClusterFMineCampSeeder.MoveNpcs(true);
            _out.WriteLine(string.Join("\n", dry));
            Assert.Equal(liaisonMove.From, liaison.Location);
            Assert.Equal(archivistMove.From, archivist.Location);

            var first = ClusterFMineCampSeeder.MoveNpcs(false);
            _out.WriteLine(string.Join("\n", first));
            Assert.Contains("2 of 2", first.Last());
            AssertInPlace(liaison, liaisonMove);
            AssertInPlace(archivist, archivistMove);

            var second = ClusterFMineCampSeeder.MoveNpcs(false);
            _out.WriteLine(string.Join("\n", second));
            Assert.Contains("0 of 2", second.Last());
            AssertInPlace(liaison, liaisonMove);
            AssertInPlace(archivist, archivistMove);

            Assert.Equal(mobilesBefore, World.Mobiles.Count);
            var npcs = CampNpcs();
            Assert.Equal(2, npcs.Count);
            Assert.Contains(liaison, npcs);
            Assert.Contains(archivist, npcs);

            // After the move the arrow targets the moved Liaison at the tent.
            Assert.Equal(liaisonMove.To, mining.Point);
            Assert.StartsWith("Follow the arrow", ClusterFGuildStarter.ShowTheWay(pm, mining));
            var arrow = Assert.IsType<GuildDirectionArrow>(pm.QuestArrow);
            Assert.Same(liaison, arrow.Target);
            Assert.Equal(liaisonMove.To, arrow.Point);
            pm.QuestArrow = null;
        }
        finally
        {
            pm.Delete();
            ClearCamp();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void WorldLoadLeavesMineCampNpcsNearTheirOldSpotsAndPlacesNewOnesAtTheTents()
    {
        ClearCamp();
        var liaisonMove = ClusterFMineCampLayout.Npcs.Single(n => n.TypeName == nameof(MinersCompactLiaison));
        var archivistMove = ClusterFMineCampLayout.Npcs.Single(n => n.TypeName == nameof(SurveyArchivist));
        var created = new List<Mobile>();

        try
        {
            // An existing world: both where the seeder put them before cc-P16. The Archivist's new tile is
            // 20 tiles from her old one, outside the seeder's 15-tile duplicate check.
            Assert.True(Math.Max(Math.Abs(archivistMove.To.X - archivistMove.From.X), Math.Abs(archivistMove.To.Y - archivistMove.From.Y)) > 15);

            var liaison = new MinersCompactLiaison();
            var archivist = new SurveyArchivist();
            OldPlace(liaison, liaisonMove.From, Direction.South);
            OldPlace(archivist, archivistMove.From, Direction.West);

            created.AddRange(RunInstitutionSeeder());
            _out.WriteLine($"existing world, created: {string.Join(", ", created.Select(m => m.GetType().Name))}");

            Assert.Equal(2, CampNpcs().Count);
            Assert.Equal(liaisonMove.From, liaison.Location);
            Assert.Equal(Direction.South, liaison.Direction);
            Assert.Equal(archivistMove.From, archivist.Location);
            Assert.Equal(Direction.West, archivist.Direction);

            // A fresh world: neither exists, so both are placed at the tent openings, facing East.
            liaison.Delete();
            archivist.Delete();

            var fresh = RunInstitutionSeeder();
            created.AddRange(fresh);

            var newLiaison = Assert.IsType<MinersCompactLiaison>(Assert.Single(fresh, m => m is MinersCompactLiaison));
            var newArchivist = Assert.IsType<SurveyArchivist>(Assert.Single(fresh, m => m is SurveyArchivist));
            AssertInPlace(newLiaison, liaisonMove);
            AssertInPlace(newArchivist, archivistMove);

            // And world load run again places nothing more.
            var again = RunInstitutionSeeder();
            created.AddRange(again);
            Assert.DoesNotContain(again, m => m is MinersCompactLiaison or SurveyArchivist);
        }
        finally
        {
            foreach (var m in created)
            {
                m.Delete();
            }

            ClearCamp();
        }
    }

    // ---------------------------------------------------------------- helpers

    private static Point3D Point(JsonElement e)
    {
        var a = e.EnumerateArray().Select(v => v.GetInt32()).ToArray();
        return new Point3D(a[0], a[1], a[2]);
    }

    // Runs world load's seeding and returns what it created.
    private static List<Mobile> RunInstitutionSeeder()
    {
        var before = new HashSet<Mobile>(World.Mobiles.Values);
        ClusterFInstitutionSeeder.Seed(verbose: false);
        return World.Mobiles.Values.Where(m => !before.Contains(m)).ToList();
    }

    private static void OldPlace(BaseCreature npc, Point3D at, Direction dir)
    {
        npc.Direction = dir;
        npc.CantWalk = true;
        npc.Home = at;
        npc.RangeHome = 0;
        npc.MoveToWorld(at, Map.Trammel);
    }

    private static void AssertInPlace(BaseCreature npc, MineCampNpcMove move)
    {
        Assert.False(npc.Deleted);
        Assert.Same(Map.Trammel, npc.Map);
        Assert.Equal(move.To, npc.Location);
        Assert.Equal(move.Facing, npc.Direction);
        Assert.Equal(move.To, npc.Home);
        Assert.Equal(0, npc.RangeHome);
        Assert.True(npc.CantWalk);
    }

    private static List<BaseAddon> Tents() =>
        World.Items.Values.OfType<BaseAddon>()
            .Where(a => !a.Deleted && a is MinersCompactTentAddon or SurveyArchivistTentAddon).ToList();

    private static List<Mobile> CampNpcs() =>
        World.Mobiles.Values.Where(m => !m.Deleted && m is MinersCompactLiaison or SurveyArchivist).ToList();

    private static void ClearCamp()
    {
        foreach (var t in Tents())
        {
            t.Delete();
        }

        foreach (var m in CampNpcs())
        {
            m.Delete();
        }
    }
}
