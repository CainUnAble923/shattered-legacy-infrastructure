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
//   3. No component sits on the hall's doorway (3667-3668,2562) or a tile next to it, or on the tile of the
//      Registrar, the Appraiser or the Sanitation Warden. (cc-P678: the office moved into SoS Haven's hall,
//      walls x3658-3674, y2547-2562, floor z0; its doorway is an open arch, no door item. cc-P17's two doors
//      belong to the old New Haven house and are not this office's any more.)
//   4. The one sign reports the League's name; nothing else outside does.
//   5. The Registrar's seeded facing is South (cc-P678; West to the old desk before), with the desk, the
//      counter East of her, beside her. A Registrar on her tile facing any other way is repaired.
//   6. (cc-P31) On a world seeded by cc-P19 (built from cleanup/layout_p19.json), replace leaves exactly
//      the current layout, no cc-P19-only component, and every unrelated item where it was. Twice is once.
//   7. (cc-P31) replace dryrun changes nothing, and lists what it would delete and place.
//   8. (cc-P42 Part D) replace dryrun does not report an addon it would delete as "already on its tiles".
//
// The test host has no map files (cc-P17 section 1.3), so nothing here asks the map about z. The z
// values were checked offline against the server's map files (notes, Part A; cc-P678: against SoS, by
// shard-migration notes/cc-P678-tools/office/check.py).

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
    // cc-P678: the hall's doorway in SoS Haven, two tiles under a lintel in the south wall.
    private static readonly Point2D[] HallDoorway = [new(3667, 2562), new(3668, 2562)];

    // The other two NPCs in the office (ClusterFRequisitionSeeder.AppraiserPosts; the Warden's guild post).
    private static readonly Point2D AppraiserTile = new(3660, 2551);
    private static readonly Point2D WardenTile = new(3662, 2549);

    // The old New Haven house's front door tile (cc-P17), where fact 6 leaves an unrelated door.
    private static readonly Point3D OldFrontDoor = new(3459, 2596, 15);

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
    public void NoComponentSitsOnTheDoorwayBesideItOrAnOfficeNpcTile()
    {
        ClearOffice();
        try
        {
            ClusterFRegistrarOfficeSeeder.Seed(false);

            var registrar = ClusterFNewHavenSeeder.Entries.Single(e => e.Type == typeof(LeagueRegistrar)).GetLocation();
            Assert.Equal(new Point3D(3660, 2549, 0), registrar);
            var appraiser = ClusterFRequisitionSeeder.AppraiserPosts.Single(p => p.Where == "League field office");
            Assert.Equal(AppraiserTile, new Point2D(appraiser.X, appraiser.Y));

            var keepClear = new Dictionary<Point2D, string>
            {
                [new Point2D(registrar.X, registrar.Y)] = "the Registrar's tile",
                [AppraiserTile] = "the Appraiser's tile",
                [WardenTile] = "the Sanitation Warden's tile",
            };

            // The doorway's two tiles and every tile around them, inside and out.
            foreach (var d in HallDoorway)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        keepClear.TryAdd(new Point2D(d.X + dx, d.Y + dy), "the hall doorway or a tile next to it");
                    }
                }
            }

            Assert.Equal(3 + 12, keepClear.Count);

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

            // Every component is in the hall (walls x3658-3674, y2547-2562) or, for the outside group, south of it.
            Assert.All(ClusterFRegistrarOfficeLayout.Office.Concat(ClusterFRegistrarOfficeLayout.Quarters),
                c => Assert.True(c.X is > 3658 and < 3674 && c.Y is > 2547 and < 2562, $"{c.Name} at {c.Location}"));
            Assert.All(ClusterFRegistrarOfficeLayout.Outside,
                c => Assert.True(c.X is >= 3658 and <= 3674 && c.Y == 2563, $"{c.Name} at {c.Location}"));
        }
        finally
        {
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

            // Exactly one sign: 0x0BCF, outside the hall's south doorway (cc-P678; on the old house's east wall
            // before). cc-P19's north sign 0x0BD0 left the design (cc-P31).
            var signs = outside.Components.Where(c => c.ItemID is 0x0BCF or 0x0BD0).ToList();
            var sign = Assert.Single(signs);
            Assert.Equal(0x0BCF, sign.ItemID);
            Assert.Equal(new Point3D(3670, 2563, 0), sign.Location);
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
    public void TheRegistrarFacesSouthBesideHerDesk()
    {
        var entry = ClusterFNewHavenSeeder.Entries.Single(e => e.Type == typeof(LeagueRegistrar));
        Assert.Equal(Direction.South, entry.Direction);

        // cc-P678: her desk is the hall's counter East of her (3661,2549, top z6), where the books, inkwell,
        // scroll and hourglass stand.
        var at = entry.GetLocation();
        Assert.Equal(new Point3D(3660, 2549, 0), at);
        Assert.Equal(4, ClusterFRegistrarOfficeLayout.Office.Count(c => c.X == at.X + 1 && c.Y == at.Y && c.Z > at.Z));

        var registrar = new LeagueRegistrar();
        try
        {
            // Facing South on her tile: neither touches her.
            Place(registrar, at, Direction.South);
            Assert.False(ClusterFNewHavenSeeder.NeedsRepair(registrar, entry, acceptLegacyTiles: true));
            Assert.False(ClusterFNewHavenSeeder.NeedsRepair(registrar, entry));

            // Any other facing, both do.
            foreach (var facing in new[] { Direction.North, Direction.West, Direction.East })
            {
                Place(registrar, at, facing);
                Assert.True(ClusterFNewHavenSeeder.NeedsRepair(registrar, entry, acceptLegacyTiles: true));
                Assert.True(ClusterFNewHavenSeeder.NeedsRepair(registrar, entry));
            }
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

            // What only cc-P19 had, from the two files: since cc-P678 moved the office into SoS Haven's hall,
            // every cc-P19 component (cc-P31 kept the old house; it had five cc-P19-only components then).
            var current = ClusterFRegistrarOfficeLayout.Components.Select(c => (c.ItemID, c.Location)).ToHashSet();
            var p19Only = P19Layout().Where(c => !current.Contains((c.ItemID, c.Location))).Select(c => (c.ItemID, c.Location)).ToList();
            Assert.Equal(18, p19Only.Count);
            Assert.Empty(OfficeComponentsIn(NewHall));

            // A plain seed does not update it: each type is already on Trammel.
            var plain = ClusterFRegistrarOfficeSeeder.Seed(false);
            Assert.Contains("placed 0 of 3", plain.Last());
            Assert.Equal(18, OfficeComponentsInTheHouseBox().Count);

            // Things replace must not touch: statics with cc-P19's bulletin board and north signpost graphics
            // on their old tiles, a door on the old front door tile, a plain item beside the candelabra's new tile.
            unrelated.Add((new Static(0x1E5F), new Point3D(3458, 2598, 18)));
            unrelated.Add((new Static(0x0B98), new Point3D(3460, 2595, 15)));
            unrelated.Add((new DarkWoodDoor(DoorFacing.WestCCW), OldFrontDoor));
            unrelated.Add((new Item(0x0EED), new Point3D(3659, 2549, 0)));
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

    // ---------------------------------------------------------------- 8

    // cc-P42 Part D (p31-checks-2026-10-02.md). After each "would place", the dry run said "already on its tiles, left
    // alone: <Addon> 0x0001 at <point>": the old addon it would delete (0x0001 is BaseAddon's item id, not a serial),
    // which a real run has deleted by then. The dry run no longer reports what it is removing; an item that is really
    // on a tile is still reported.
    [Fact]
    public void ReplaceDryRunDoesNotReportTheAddonsItRemoves()
    {
        ClearOffice();
        var stranger = new Item(0x0EED);
        try
        {
            var p19 = SeedP19Office();
            var tile = ClusterFRegistrarOfficeLayout.Office[0].Location;
            stranger.MoveToWorld(tile, Map.Trammel);

            var lines = ClusterFRegistrarOfficeSeeder.Replace(true);
            _out.WriteLine(string.Join("\n", lines));

            var leftAlone = lines.Where(l => l.Contains("already on its tiles, left alone", StringComparison.Ordinal)).ToList();
            foreach (var a in p19)
            {
                Assert.DoesNotContain(leftAlone, l => l.Contains($"{a.GetType().Name} 0x", StringComparison.Ordinal));
            }

            Assert.Contains(leftAlone, l => l.Contains($"Item 0x0EED at {tile}", StringComparison.Ordinal));
        }
        finally
        {
            stranger.Delete();
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

    // The two places an office can be: the old New Haven house (a world seeded by cc-P19 or cc-P31) and, since cc-P678,
    // SoS Haven's hall with the outside group south of it.
    private static readonly (int X1, int Y1, int X2, int Y2) OldHouse = (3448, 2590, 3468, 2608);
    private static readonly (int X1, int Y1, int X2, int Y2) NewHall = (3653, 2542, 3679, 2567);

    private static List<AddonComponent> OfficeComponentsIn((int X1, int Y1, int X2, int Y2) box) =>
        World.Items.Values.OfType<AddonComponent>()
            .Where(c => !c.Deleted && c.Map == Map.Trammel && c.X >= box.X1 && c.X <= box.X2 && c.Y >= box.Y1 && c.Y <= box.Y2)
            .ToList();

    // Every AddonComponent on Trammel in either box, whoever owns it: catches a component left behind.
    private static List<AddonComponent> OfficeComponentsInTheHouseBox() =>
        OfficeComponentsIn(OldHouse).Concat(OfficeComponentsIn(NewHall)).ToList();

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
        Assert.Empty(OfficeComponentsIn(OldHouse));
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
