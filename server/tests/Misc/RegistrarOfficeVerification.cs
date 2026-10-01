// RegistrarOfficeVerification.cs
//
// cc-P19: props for the League of Extraordinary Citizens Field Office in New Haven (Trammel), and the
// League Registrar turned to face her desk. Notes in shard-migration notes/cc-P19-registrar-office-decor.md.
//
// What the facts pin:
//   1. The layout the server loads (ClusterFRegistrarOfficeLayout) is design/registrar-office/layout.json,
//      component for component, and its three areas are exactly the design's components split by area.
//   2. The seeder, run twice, places the three addons once, every component at its design tile, not
//      movable, not decaying, with no deed. A dry run places nothing.
//   3. No component sits on cc-P17's front door tile or office doorway tile, or on the tile either door
//      swings into, or on the Registrar's tile; with the props in place both doors open and close.
//      The doors are cc-P17's own entries, placed by cc-P17's code, not doors made up here. Also pinned:
//      on a world where pinned's [DoorGen ran (the test world), the doorway holds DoorGen's SouthCW door
//      instead, which swings onto the desk's north tile, and the props there do not stop it closing.
//   4. Both signs report the League's name; the signposts do not.
//   5. The Registrar's seeded facing is West, to her desk. World load leaves one facing North on her
//      tile alone; the repair command turns her.
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
public class RegistrarOfficeVerification
{
    private static readonly Point3D FrontDoor = new(3459, 2596, 15);
    private static readonly Point3D OfficeDoorway = new(3457, 2599, 18);

    private readonly ITestOutputHelper _out;

    public RegistrarOfficeVerification(ITestOutputHelper output) => _out = output;

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheLoadedLayoutIsTheDesignFile()
    {
        var json = RegistrarOfficeLayoutDesignCopy.Json.Replace("\r\n", "\n");
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(json))).ToLowerInvariant();
        Assert.Equal(RegistrarOfficeLayoutDesignCopy.Sha256, sha);
        Assert.Equal(ClusterFRegistrarOfficeLayout.DesignSha256, sha);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(ClusterFRegistrarOfficeLayout.MapName, root.GetProperty("map").GetString());

        var design = root.GetProperty("components").EnumerateArray().ToArray();
        var loaded = ClusterFRegistrarOfficeLayout.Components;
        Assert.Equal(design.Length, loaded.Length);

        for (var i = 0; i < design.Length; i++)
        {
            var d = design[i];
            var expected = new MineCampComponent(
                d.GetProperty("area").GetString(),
                Convert.ToInt32(d.GetProperty("id").GetString(), 16),
                d.GetProperty("name").GetString(),
                d.GetProperty("x").GetInt32(),
                d.GetProperty("y").GetInt32(),
                d.GetProperty("z").GetInt32()
            );
            Assert.Equal(expected, loaded[i]);
        }

        // Each area is the design's components of that area, in design order, and the areas cover them all.
        var areas = ClusterFRegistrarOfficeLayout.Areas;
        Assert.Equal(["outside", "office", "quarters"], areas.Select(a => a.Area).ToArray());
        foreach (var (area, components) in areas)
        {
            Assert.Equal(loaded.Where(c => c.Group == area).ToArray(), components);
        }

        // Counted here, not taken from the brief: 18 = 5 outside + 10 office + 3 quarters.
        Assert.Equal(18, loaded.Length);
        Assert.Equal(5, ClusterFRegistrarOfficeLayout.Outside.Length);
        Assert.Equal(10, ClusterFRegistrarOfficeLayout.Office.Length);
        Assert.Equal(3, ClusterFRegistrarOfficeLayout.Quarters.Length);
        Assert.Equal(loaded.Length, areas.Sum(a => a.Components.Length));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void SeedingTwicePlacesTheDecorOnce()
    {
        ClearOffice();
        try
        {
            var dry = ClusterFRegistrarOfficeSeeder.Seed(true);
            _out.WriteLine(string.Join("\n", dry));
            Assert.Empty(OfficeAddons());

            var first = ClusterFRegistrarOfficeSeeder.Seed(false);
            var second = ClusterFRegistrarOfficeSeeder.Seed(false);
            _out.WriteLine(string.Join("\n", first));
            _out.WriteLine(string.Join("\n", second));

            var addons = OfficeAddons();
            Assert.Equal(3, addons.Count);
            Assert.Single(addons, a => a is RegistrarOfficeOutsideAddon);
            Assert.Single(addons, a => a is RegistrarOfficeAddon);
            Assert.Single(addons, a => a is RegistrarQuartersAddon);
            Assert.Contains("placed 3 of 3", first.Last());
            Assert.Contains("placed 0 of 3", second.Last());

            var components = 0;
            foreach (var (type, _, layout) in ClusterFRegistrarOfficeSeeder.Addons)
            {
                var addon = addons.Single(a => a.GetType() == type);
                Assert.Same(Map.Trammel, addon.Map);
                Assert.Null(addon.Deed);
                Assert.False(addon.Decays);
                Assert.Equal(layout.Length, addon.Components.Count);

                for (var i = 0; i < layout.Length; i++)
                {
                    var c = addon.Components[i];
                    Assert.Equal(typeof(AddonComponent), c.GetType());
                    Assert.Equal(layout[i].ItemID, c.ItemID);
                    Assert.Equal(layout[i].Location, c.Location);
                    Assert.Same(Map.Trammel, c.Map);
                    Assert.False(c.Movable);
                    Assert.False(c.Decays);
                }

                components += addon.Components.Count;
            }

            Assert.Equal(18, components);
        }
        finally
        {
            ClearOffice();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void NoComponentSitsOnADoorItsSwingOrTheRegistrarTile()
    {
        ClearOffice();
        var made = new List<Item>();
        try
        {
            // cc-P17's two entries for this house, placed by cc-P17's own code (pinned DecorationList).
            var front = PlaceP17Door(FrontDoor, "DarkWoodDoor 0x06A9 (Facing=WestCCW)", DoorFacing.WestCCW, made);
            var doorway = PlaceP17Door(OfficeDoorway, "DarkWoodDoor 0x06B3 (Facing=NorthCW)", DoorFacing.NorthCW, made);

            ClusterFRegistrarOfficeSeeder.Seed(false);

            var registrar = ClusterFNewHavenSeeder.Entries.Single(e => e.Type == typeof(LeagueRegistrar)).GetLocation();
            Assert.Equal(new Point3D(3459, 2601, 18), registrar);

            var keepClear = new Dictionary<Point2D, string>
            {
                [new Point2D(FrontDoor.X, FrontDoor.Y)] = "the front door",
                [Swing(FrontDoor, DoorFacing.WestCCW)] = "where the front door swings",
                [new Point2D(OfficeDoorway.X, OfficeDoorway.Y)] = "the office doorway",
                [Swing(OfficeDoorway, DoorFacing.NorthCW)] = "where the doorway door swings",
                [new Point2D(registrar.X, registrar.Y)] = "the Registrar's tile",
            };
            Assert.Equal(5, keepClear.Count);

            var onThem = new List<string>();
            foreach (var addon in OfficeAddons())
            {
                foreach (var c in addon.Components)
                {
                    if (keepClear.TryGetValue(new Point2D(c.X, c.Y), out var what))
                    {
                        onThem.Add($"0x{c.ItemID:X4} at {c.Location} is on {what}");
                    }
                }
            }

            _out.WriteLine(string.Join("\n", onThem));
            Assert.Empty(onThem);

            // With the props in place, each door opens, is free to close, and closes back onto its tile.
            foreach (var (door, at) in new[] { (front, FrontDoor), (doorway, OfficeDoorway) })
            {
                door.Open = true;
                Assert.True(door.IsFreeToClose());
                door.Open = false;
                Assert.Equal(at, door.Location);
            }

            // Where pinned's [DoorGen ran, the doorway holds its SouthCW door instead (test world, serial
            // 0x40004158), and cc-P17's NorthCW entry is kept out. That door swings onto the desk's north
            // tile, where the books and hourglass stand. Opening is not fit-checked (BaseDoor.Open,
            // BaseDoor.cs:84-93) and closing checks only the closed tile (CanClose, :254-271), so it still works.
            doorway.Delete();
            var doorGen = new DarkWoodDoor(DoorFacing.SouthCW);
            made.Add(doorGen);
            doorGen.MoveToWorld(OfficeDoorway, Map.Trammel);
            Assert.Equal(0x06AD, doorGen.ItemID);
            Assert.Equal(new Point2D(3458, 2600), Swing(OfficeDoorway, DoorFacing.SouthCW));
            Assert.Contains(ClusterFRegistrarOfficeLayout.Office, c => c.X == 3458 && c.Y == 2600);

            doorGen.Open = true;
            Assert.Equal(new Point3D(3458, 2600, 18), doorGen.Location);
            Assert.True(doorGen.IsFreeToClose());
            doorGen.Open = false;
            Assert.Equal(OfficeDoorway, doorGen.Location);
        }
        finally
        {
            foreach (var i in made)
            {
                i.Delete();
            }

            ClearOffice();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void BothSignsReportTheLeagueName()
    {
        ClearOffice();
        try
        {
            ClusterFRegistrarOfficeSeeder.Seed(false);
            var outside = OfficeAddons().OfType<RegistrarOfficeOutsideAddon>().Single();

            var signs = outside.Components.Where(c => c.ItemID is 0x0BCF or 0x0BD0).ToList();
            Assert.Equal(2, signs.Count);
            Assert.Contains(signs, s => s.ItemID == 0x0BCF && s.Location == new Point3D(3463, 2597, 13));
            Assert.Contains(signs, s => s.ItemID == 0x0BD0 && s.Location == new Point3D(3460, 2595, 15));

            foreach (var sign in signs)
            {
                Assert.Equal("League of Extraordinary Citizens", sign.Name);

                // What a single click sends with tooltips on (Item.OnAosSingleClick, Item.cs:4050-4067).
                sign.InvalidateProperties();
                Assert.Equal("League of Extraordinary Citizens", sign.PropertyList.HeaderArgs);
            }

            // The signposts and every other prop keep their tile names.
            Assert.All(outside.Components.Except(signs), c => Assert.Null(c.Name));
        }
        finally
        {
            ClearOffice();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheRegistrarFacesHerDeskAndWorldLoadLeavesAnOldOneAlone()
    {
        var entry = ClusterFNewHavenSeeder.Entries.Single(e => e.Type == typeof(LeagueRegistrar));
        Assert.Equal(Direction.West, entry.Direction);
        Assert.True(entry.HasLegacyLocation);
        Assert.Equal(Direction.North, entry.LegacyDirection);

        // Her desk, the map's table at 3458,2600-2601, is the tile West of her.
        var at = entry.GetLocation();
        Assert.Equal(new Point3D(3459, 2601, 18), at);

        var registrar = new LeagueRegistrar();
        try
        {
            // As placed before cc-P19: on her tile, facing North.
            Place(registrar, at, Direction.North);
            Assert.False(ClusterFNewHavenSeeder.NeedsRepair(registrar, entry, acceptLegacyTiles: true));
            Assert.True(ClusterFNewHavenSeeder.NeedsRepair(registrar, entry));

            // Turned West: neither touches her.
            Place(registrar, at, Direction.West);
            Assert.False(ClusterFNewHavenSeeder.NeedsRepair(registrar, entry, acceptLegacyTiles: true));
            Assert.False(ClusterFNewHavenSeeder.NeedsRepair(registrar, entry));

            // Any other facing, both do.
            Place(registrar, at, Direction.South);
            Assert.True(ClusterFNewHavenSeeder.NeedsRepair(registrar, entry, acceptLegacyTiles: true));
            Assert.True(ClusterFNewHavenSeeder.NeedsRepair(registrar, entry));
        }
        finally
        {
            registrar.Delete();
        }
    }

    // ---------------------------------------------------------------- helpers

    private static Point2D Swing(Point3D closed, DoorFacing facing)
    {
        var o = BaseDoor.GetOffset(facing);
        return new Point2D(closed.X + o.X, closed.Y + o.Y);
    }

    private static BaseDoor PlaceP17Door(Point3D at, string header, DoorFacing facing, List<Item> made)
    {
        foreach (var d in DoorsAt(at))
        {
            d.Delete();
        }

        var entry = ClusterFNewHavenServicesSeeder.Decoration.Single(e => e.Location == at);
        Assert.Equal(header, entry.Header);
        Assert.Equal(1, entry.Read().Generate([Map.Trammel]));

        var door = DoorsAt(at).Single();
        made.Add(door);
        Assert.IsType<DarkWoodDoor>(door);
        Assert.Equal(BaseDoor.GetOffset(facing), door.Offset);
        return door;
    }

    private static List<BaseDoor> DoorsAt(Point3D at) =>
        World.Items.Values.OfType<BaseDoor>()
            .Where(d => !d.Deleted && d.Map == Map.Trammel && d.X == at.X && d.Y == at.Y && d.Z == at.Z).ToList();

    private static void Place(BaseCreature npc, Point3D at, Direction facing)
    {
        npc.Direction = facing;
        npc.CantWalk = true;
        npc.Home = at;
        npc.RangeHome = 0;
        npc.MoveToWorld(at, Map.Trammel);
    }

    private static List<BaseAddon> OfficeAddons() =>
        World.Items.Values.OfType<BaseAddon>()
            .Where(a => !a.Deleted && a is RegistrarOfficeOutsideAddon or RegistrarOfficeAddon or RegistrarQuartersAddon).ToList();

    private static void ClearOffice()
    {
        foreach (var a in OfficeAddons())
        {
            a.Delete();
        }
    }
}
