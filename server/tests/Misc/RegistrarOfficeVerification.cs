// RegistrarOfficeVerification.cs
//
// cc-P19: props for the League of Extraordinary Citizens Field Office in New Haven (Trammel), and the
// League Registrar turned to face her desk. Notes in shard-migration notes/cc-P19-registrar-office-decor.md.
// cc-P31: the design's cleanup (18 components to 14) and the replace mode that swaps a cc-P19 office for
// the current one. Notes in shard-migration notes/cc-P31-registrar-office-cleanup.md.
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
//   4. The one sign reports the League's name; nothing else outside does.
//   5. The Registrar's seeded facing is West, to her desk. World load leaves one facing North on her
//      tile alone; the repair command turns her.
//   6. (cc-P31) On a world seeded by cc-P19 (built from cleanup/layout_p19.json), replace leaves exactly
//      the current layout, no cc-P19-only component, and every unrelated item where it was. Twice is once.
//   7. (cc-P31) replace dryrun changes nothing, and lists what it would delete and place.
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

        // Counted here, not taken from the brief: 14 = 3 outside + 8 office + 3 quarters (cc-P19: 18 = 5 + 10 + 3).
        Assert.Equal(14, loaded.Length);
        Assert.Equal(3, ClusterFRegistrarOfficeLayout.Outside.Length);
        Assert.Equal(8, ClusterFRegistrarOfficeLayout.Office.Length);
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

            Assert.Equal(14, components);
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
    public void TheSignReportsTheLeagueName()
    {
        ClearOffice();
        try
        {
            ClusterFRegistrarOfficeSeeder.Seed(false);
            var outside = OfficeAddons().OfType<RegistrarOfficeOutsideAddon>().Single();

            // Exactly one sign: 0x0BCF on the east wall. cc-P19's north sign 0x0BD0 left the design (cc-P31).
            var signs = outside.Components.Where(c => c.ItemID is 0x0BCF or 0x0BD0).ToList();
            var sign = Assert.Single(signs);
            Assert.Equal(0x0BCF, sign.ItemID);
            Assert.Equal(new Point3D(3463, 2597, 13), sign.Location);
            Assert.Single(ClusterFRegistrarOfficeLayout.Components, c => c.Name == "wooden sign");

            Assert.Equal("League of Extraordinary Citizens", sign.Name);

            // What a single click sends with tooltips on (Item.OnAosSingleClick, Item.cs:4050-4067).
            sign.InvalidateProperties();
            Assert.Equal("League of Extraordinary Citizens", sign.PropertyList.HeaderArgs);

            // The signpost and every other prop keep their tile names.
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

    // ---------------------------------------------------------------- 6

    [Fact]
    public void ReplaceSwapsAP19OfficeForTheCurrentLayoutAndNothingElse()
    {
        ClearOffice();
        var unrelated = new List<(Item Item, Point3D At)>();
        try
        {
            var p19 = SeedP19Office();
            Assert.Equal(18, p19.Sum(a => a.Components.Count));
            Assert.Equal(18, OfficeComponentsInTheHouseBox().Count);

            // What only cc-P19 had, from the two files, not from the brief: exactly the brief's five.
            var current = ClusterFRegistrarOfficeLayout.Components.Select(c => (c.ItemID, c.Location)).ToHashSet();
            var p19Only = P19Layout().Where(c => !current.Contains((c.ItemID, c.Location))).Select(c => (c.ItemID, c.Location)).ToList();
            var briefsFive = new List<(int ItemID, Point3D Location)>
            {
                (0x0B98, new Point3D(3460, 2595, 15)), (0x0BD0, new Point3D(3460, 2595, 15)),
                (0x1E5F, new Point3D(3458, 2598, 18)), (0x0B26, new Point3D(3461, 2600, 18)),
                (0x1047, new Point3D(3461, 2598, 18)),
            };
            Assert.Equal(briefsFive, p19Only);

            // A plain seed does not update it: each type is already on Trammel.
            var plain = ClusterFRegistrarOfficeSeeder.Seed(false);
            Assert.Contains("placed 0 of 3", plain.Last());
            Assert.Equal(18, OfficeComponentsInTheHouseBox().Count);

            // Things replace must not touch: statics with cc-P19's bulletin board and north signpost graphics
            // on their old tiles, a door on the front door tile, a plain item beside the candelabra's new tile.
            unrelated.Add((new Static(0x1E5F), new Point3D(3458, 2598, 18)));
            unrelated.Add((new Static(0x0B98), new Point3D(3460, 2595, 15)));
            unrelated.Add((new DarkWoodDoor(DoorFacing.WestCCW), FrontDoor));
            unrelated.Add((new Item(0x0EED), new Point3D(3461, 2601, 18)));
            foreach (var (item, at) in unrelated)
            {
                item.MoveToWorld(at, Map.Trammel);
            }

            var first = ClusterFRegistrarOfficeSeeder.Replace(false);
            _out.WriteLine(string.Join("\n", first));
            Assert.Contains("ClusterF registrar office replace: deleted 3 addons (18 components).", first);
            Assert.Contains("placed 3 of 3", first.Last());
            Assert.All(p19, a => Assert.True(a.Deleted));
            Assert.All(p19.SelectMany(a => a.Components), c => Assert.True(c.Deleted));

            var once = Layout();
            AssertTheCurrentLayoutAndNoP19Component(p19Only);

            // Twice is once: the second run swaps the current office for an identical one.
            var second = ClusterFRegistrarOfficeSeeder.Replace(false);
            _out.WriteLine(string.Join("\n", second));
            Assert.Contains("ClusterF registrar office replace: deleted 3 addons (14 components).", second);
            Assert.Equal(once, Layout());
            AssertTheCurrentLayoutAndNoP19Component(p19Only);

            foreach (var (item, at) in unrelated)
            {
                Assert.False(item.Deleted, $"{item.GetType().Name} 0x{item.ItemID:X4} at {at} was deleted");
                Assert.Equal(at, item.Location);
                Assert.Same(Map.Trammel, item.Map);
            }
        }
        finally
        {
            foreach (var (item, _) in unrelated)
            {
                item.Delete();
            }

            ClearOffice();
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void ReplaceDryRunChangesNothing()
    {
        ClearOffice();
        try
        {
            var p19 = SeedP19Office();
            var before = LayoutWithSerials();
            var items = World.Items.Count;

            var lines = ClusterFRegistrarOfficeSeeder.Replace(true);
            _out.WriteLine(string.Join("\n", lines));

            Assert.Equal(items, World.Items.Count);
            Assert.Equal(before, LayoutWithSerials());
            Assert.All(p19, a => Assert.False(a.Deleted));

            // It names what it would delete (serial, location, component count) and what it would place.
            foreach (var a in p19)
            {
                Assert.Contains(
                    $"{a.GetType().Name} 0x{a.Serial.Value:X8}: would delete at {a.Location} (Trammel), {a.Components.Count} components.",
                    lines
                );
            }

            Assert.Contains("ClusterF registrar office replace: would delete 3 addons (18 components).", lines);
            foreach (var (type, _, layout) in ClusterFRegistrarOfficeSeeder.Addons)
            {
                Assert.Contains($"{type.Name}: would place at {layout[0].Location} (Trammel), {layout.Length} components.", lines);
            }

            Assert.Equal("ClusterF registrar office dry run: would place 3 of 3 addons.", lines.Last());
        }
        finally
        {
            ClearOffice();
        }
    }

    // ---------------------------------------------------------------- helpers

    private static MineCampComponent[] P19Layout()
    {
        var json = RegistrarOfficeLayoutP19Copy.Json.Replace("\r\n", "\n");
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(json))).ToLowerInvariant();
        Assert.Equal(RegistrarOfficeLayoutP19Copy.Sha256, sha);

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("components").EnumerateArray().Select(d => new MineCampComponent(
            d.GetProperty("area").GetString(),
            Convert.ToInt32(d.GetProperty("id").GetString(), 16),
            d.GetProperty("name").GetString(),
            d.GetProperty("x").GetInt32(),
            d.GetProperty("y").GetInt32(),
            d.GetProperty("z").GetInt32()
        )).ToArray();
    }

    // A world seeded by cc-P19: the same three addon types, built as cc-P19's constructors built them (the
    // area's components from layout_p19.json, both signs named) and placed at the area's first component.
    private static List<BaseAddon> SeedP19Office()
    {
        var layout = P19Layout();
        var made = new List<BaseAddon>();

        for (var i = 0; i < ClusterFRegistrarOfficeSeeder.Addons.Length; i++)
        {
            var (type, create, _) = ClusterFRegistrarOfficeSeeder.Addons[i];
            var area = ClusterFRegistrarOfficeLayout.Areas[i].Area;
            var components = layout.Where(c => c.Group == area).ToArray();

            var addon = create();
            Assert.IsType(type, addon);
            foreach (var c in addon.Components.ToList())
            {
                c.Addon = null; // so deleting it does not delete the addon (AddonComponent.OnAfterDelete)
                c.Delete();
            }

            addon.Components.Clear();
            ClusterFMineCampSeeder.AddComponents(addon, components);
            foreach (var c in addon.Components.Where(c => c.ItemID is 0x0BCF or 0x0BD0))
            {
                c.Name = ClusterFRegistrarOfficeSeeder.SignName;
            }

            addon.MoveToWorld(ClusterFMineCampSeeder.OriginOf(components), Map.Trammel);
            made.Add(addon);
        }

        return made;
    }

    // Every AddonComponent on Trammel in the house box, whoever owns it: catches a component left behind.
    private static List<AddonComponent> OfficeComponentsInTheHouseBox() =>
        World.Items.Values.OfType<AddonComponent>()
            .Where(c => !c.Deleted && c.Map == Map.Trammel && c.X is >= 3448 and <= 3468 && c.Y is >= 2590 and <= 2608).ToList();

    private static List<string> Layout() =>
        OfficeAddons().SelectMany(a => a.Components.Select(c => $"{a.GetType().Name} 0x{c.ItemID:X4} {c.Location} {c.Name}"))
            .Order().ToList();

    private static List<string> LayoutWithSerials() =>
        OfficeAddons().SelectMany(a => a.Components.Select(c =>
                $"{a.GetType().Name} 0x{a.Serial.Value:X8} {a.Location} 0x{c.Serial.Value:X8} 0x{c.ItemID:X4} {c.Location} {c.Name}"
            ))
            .Order().ToList();

    private static void AssertTheCurrentLayoutAndNoP19Component(List<(int ItemID, Point3D Location)> p19Only)
    {
        var addons = OfficeAddons();
        Assert.Equal(3, addons.Count);
        Assert.Single(addons, a => a is RegistrarOfficeOutsideAddon);
        Assert.Single(addons, a => a is RegistrarOfficeAddon);
        Assert.Single(addons, a => a is RegistrarQuartersAddon);

        var placed = OfficeComponentsInTheHouseBox();
        Assert.Equal(14, placed.Count);
        Assert.Equal(
            ClusterFRegistrarOfficeLayout.Components.Select(c => $"0x{c.ItemID:X4} {c.Location}").Order(),
            placed.Select(c => $"0x{c.ItemID:X4} {c.Location}").Order()
        );

        foreach (var (id, at) in p19Only)
        {
            Assert.DoesNotContain(placed, c => c.ItemID == id && c.Location == at);
        }

        Assert.DoesNotContain(placed, c => c.ItemID is 0x0B98 or 0x0BD0 or 0x1E5F or 0x1047);
        Assert.Equal("League of Extraordinary Citizens", Assert.Single(placed, c => c.Name != null).Name);
    }


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
