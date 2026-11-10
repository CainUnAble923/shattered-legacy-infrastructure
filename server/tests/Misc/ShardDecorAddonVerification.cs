// ShardDecorAddonVerification.cs
//
// cc-P855: the data-driven house addon (Items/Addons/ShardDecor/ShardDecorAddon.cs) and the housing furniture batch as
// its data files (server/data/Decoration/Addons). Notes in shard-migration notes/cc-P855-decor-addon-housing-furniture.md.
//
// Facts:
//   1. Every shipped design file reads (none refused): the 30 files, 14 deeds (groups) with their facings, each with a
//      name and its author, every piece stock client art (no ID in our art package's ranges).
//   2. The reader takes pinned's [Decorate text (Hue, Light, Name parameters; comments; blank lines between lists) and
//      refuses what an addon cannot be made of (another type, a bad coordinate, an unknown parameter, no name).
//   3. An addon made from a design puts each piece at its offset with its hue, light and name, keeps its own hues
//      (no shared hue), shows the author on each piece, gives its group's deed back, and survives a save.
//   4. A deed with several facings asks which (one button per facing) and places the one chosen; a one-facing deed goes
//      straight to the target; placing outside a house is refused and the deed is kept.
//   5. Every deed is sold: the Hunt Master and feasting tables in the League shop's hunt row (Renown), the fireplaces,
//      couch and kitchens in the Achievement store, the harpsichord at the bards' guild, the holiday fireplace in the
//      Festival's winter stock; an existing price file that never heard of the new rows sells them at their defaults.
//   6. Staff dressing: [DecorAddon place stands a design at a point with no house rules, and clear removes exactly the
//      data addons near a point (not one farther off, not other items).

#nullable enable

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Server;
using Server.Engines.LeagueRewards;
using Server.Engines.RewardStores;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ShardDecorAddonVerification
{
    private readonly ITestOutputHelper _out;

    public ShardDecorAddonVerification(ITestOutputHelper output)
    {
        _out = output;
        var folder = Folder();
        Assert.NotNull(folder);
        if (ShardDecorDesigns.FolderOverride != folder)
        {
            ShardDecorDesigns.FolderOverride = folder;
            ShardDecorDesigns.Reload();
        }
    }

    // The shipped files: the test assembly's folder, then up (never Core.BaseDirectory, as SosDecorationVerification).
    private static string? Folder()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(d.FullName, "Data", "ShatteredLegacy", "Decoration", "Addons"),
                         Path.Combine(d.FullName, "Distribution", "Data", "ShatteredLegacy", "Decoration", "Addons"),
                         Path.Combine(d.FullName, "server", "data", "Decoration", "Addons")
                     })
            {
                if (Directory.Exists(candidate) && Directory.GetFiles(candidate, "*.cfg").Length > 0)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    // The deeds and their facings, as the files ship them (counted from the files: notes, "The data").
    private static readonly (string Group, string[] Facings, string Credit)[] Expected =
    [
        ("barbecue", ["east", "south"], "Lagatha"),
        ("blue-couch", ["east", "north", "south", "west"], "Diesel"),
        ("buffet-table", ["east-west", "north-south"], "manwitch"),
        ("country-kitchen", ["east", "south"], "Lagatha"),
        ("feasting-table", ["style 1", "style 2", "style 3"], "Olórin"),
        ("formal-dining-table", ["east-west", "north-south"], "manwitch"),
        ("grey-fireplace-grand", ["east", "south"], "Diesel"),
        ("grey-fireplace-large", ["east", "south"], "Diesel"),
        ("grey-fireplace-low", ["east", "south"], "Diesel"),
        ("grey-fireplace-medium", ["east", "south"], "Diesel"),
        ("grey-fireplace-small", ["east", "south"], "Diesel"),
        ("harpsichord", ["east", "south"], "Lagatha"),
        ("holiday-fireplace", ["south"], "Lagatha, after Gwenivere (Knights of the Round Table shard)"),
        ("small-kitchen", ["northwest", "southeast"], "Lagatha"),
    ];

    // Our art package's ID ranges (registry.csv, as RewardStoreVerification): stock art lies outside every one.
    private static bool IsInOurArtRanges(int id) => id is >= 0x3C10 and <= 0x3D0D or >= 0x6D70 and <= 0x6E86 or >= 0x3384 and <= 0x33B6;

    private static PlayerMobile NewCharacter(Point3D at)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Decorator" };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(at, Map.Felucca);
        return pm;
    }

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EveryShippedDesignReadsWithItsAuthorAndOnlyStockArt()
    {
        Assert.Empty(ShardDecorDesigns.Refused);
        Assert.Equal(30, ShardDecorDesigns.All.Count);
        Assert.Equal(Expected.Select(e => e.Group), ShardDecorDesigns.Groups());

        foreach (var (group, facings, credit) in Expected)
        {
            var designs = ShardDecorDesigns.Facings(group);
            Assert.Equal(facings, designs.Select(d => d.Facing));
            foreach (var d in designs)
            {
                _out.WriteLine($"{d.Key}: {d.Name}, {d.Parts.Count} pieces, by {d.Credit}");
                Assert.Equal(credit, d.Credit);
                Assert.False(string.IsNullOrWhiteSpace(d.Name));
                Assert.NotEmpty(d.Parts);
                Assert.All(d.Parts, p => Assert.InRange(p.ItemID, 1, 0xFFFF));
                Assert.All(d.Parts, p => Assert.False(IsInOurArtRanges(p.ItemID), $"{d.Key}: 0x{p.ItemID:X4} is not stock art"));
                Assert.All(d.Parts, p => Assert.InRange(Math.Max(Math.Abs(p.X), Math.Abs(p.Y)), 0, 3));
            }
        }

        // The pieces counted from the downloads' component lists (convert.py's report, recounted from the files): 649 in
        // all, less the holiday fireplace's own fieldstone wall box (7 pieces, cc-P867): 642.
        Assert.Equal(642, ShardDecorDesigns.All.Values.Sum(d => d.Parts.Count));

        // cc-P867: the holiday fireplace stands against a house's own north wall, so it brings no wall of its own (the
        // fieldstone post 0x001D, south pieces 0x001C and east pieces 0x001B stood a second wall in front of the
        // house's); its hearth row is its back row, y -1.
        var holiday = ShardDecorDesigns.All["holiday-fireplace"];
        Assert.Equal(28, holiday.Parts.Count);
        Assert.DoesNotContain(holiday.Parts, p => p.ItemID is 0x001B or 0x001C or 0x001D);
        Assert.Equal(-1, holiday.Parts.Min(p => p.Y));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheReaderTakesDecorateTextAndRefusesWhatAnAddonCannotBeMadeOf()
    {
        const string good =
            "# a comment\n" +
            "#! name: a test bench (east)\n" +
            "#! credit: Someone\n" +
            "#! group: test-bench\n" +
            "#! facing: east\n" +
            "\n" +
            "Static 0x0B2C\n" +
            "0 0 0\n" +
            "0 1 0\n" +
            "\n" +
            "Static 0x19AB (Hue=33; Light=Circle300; Name=a small fire)\n" +
            "# a comment inside a list\n" +
            "-1 2 5\n";

        var d = ShardDecorDesigns.Parse("test-bench-east", good);
        Assert.Equal(("a test bench (east)", "a test bench", "Someone", "test-bench", "east"),
            (d.Name, d.GroupName, d.Credit, d.Group, d.Facing));
        Assert.Equal(
            new[]
            {
                new ShardDecorPart(0x0B2C, 0, 0, 0, 0, null, null),
                new ShardDecorPart(0x0B2C, 0, 1, 0, 0, null, null),
                new ShardDecorPart(0x19AB, -1, 2, 5, 33, LightType.Circle300, "a small fire")
            },
            d.Parts);

        // Group and facing default to the key and "as made".
        var plain = ShardDecorDesigns.Parse("plain", "#! name: a thing\n#! credit: X\nStatic 0x0B2C\n0 0 0\n");
        Assert.Equal(("plain", "as made"), (plain.Group, plain.Facing));

        foreach (var bad in new[]
                 {
                     "#! name: a\n#! credit: X\nDarkWoodDoor (Facing=WestCCW)\n0 0 0\n", // not a Static
                     "#! name: a\n#! credit: X\nStatic 0x0B2C\n0 0\n",                    // a bad coordinate
                     "#! name: a\n#! credit: X\nStatic 0x0B2C (Amount=5)\n0 0 0\n",       // an unknown parameter
                     "#! name: a\n#! credit: X\nStatic 0x0B2C (Light=Sunshine)\n0 0 0\n", // not a light
                     "#! name: a\n#! credit: X\nStatic zz\n0 0 0\n",                      // a bad ID
                     "#! credit: X\nStatic 0x0B2C\n0 0 0\n",                              // no name
                     "#! name: a\nStatic 0x0B2C\n0 0 0\n",                                // no credit
                     "#! name: a\n#! credit: X\n"                                         // no pieces
                 })
        {
            Assert.Throws<FormatException>(() => ShardDecorDesigns.Parse("bad", bad));
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AnAddonPutsEachPieceAtItsOffsetKeepsItsHuesAndCreditsItsAuthor()
    {
        var design = ShardDecorDesigns.Get("blue-couch-east")!;
        Assert.NotNull(design);
        var addon = new ShardDecorAddon("blue-couch-east");
        var at = new Point3D(5720, 1190, 0);
        try
        {
            addon.MoveToWorld(at, Map.Felucca);
            Assert.Equal(design.Parts.Count, addon.Components.Count);
            for (var i = 0; i < design.Parts.Count; i++)
            {
                var part = design.Parts[i];
                var c = addon.Components[i];
                Assert.IsType<ShardDecorComponent>(c);
                Assert.Equal((part.ItemID, new Point3D(at.X + part.X, at.Y + part.Y, at.Z + part.Z), part.Hue),
                    (c.ItemID, c.Location, c.Hue));
                if (part.Light is { } light)
                {
                    Assert.Equal(light, c.Light);
                }

                if (part.Name != null)
                {
                    Assert.Equal(part.Name, c.Name);
                }

                Assert.False(c.Movable);
            }

            // The couch's pieces carry two hues (the cushions' and the table's); none is overwritten by the addon's.
            Assert.True(design.Parts.Select(p => p.Hue).Distinct().Count() > 1);
            Assert.False(addon.ShareHue);

            var props = Props(addon.Components[0]);
            Assert.Contains("Design by Diesel", props);

            // Chopping gives the group's deed back (pinned BaseAddon.OnChop hands over addon.Deed).
            var deed = Assert.IsType<ShardDecorDeed>(addon.Deed);
            Assert.Equal("blue-couch", deed.Group);
            Assert.Equal("a deed for a blue couch", deed.Name);
            deed.Delete();

            // A save keeps which design it is, its group and its author.
            var copy = RoundTrip(addon);
            Assert.Equal(("blue-couch-east", "blue-couch", addon.Credit),
                (copy.Design, copy.Group, copy.Credit));
            Assert.Equal(addon.Components.Count, copy.Components.Count);
        }
        finally
        {
            addon.Delete();
        }
    }

    // The property list's text arguments, as the client is sent them (ItemKnowledgeVerification.Sent).
    private static string Props(Item item)
    {
        item.InvalidateProperties();
        var buffer = item.PropertyList.Buffer;
        var entries = new List<string>();
        var pos = 15;
        while (pos + 6 <= buffer.Length)
        {
            var number = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(pos));
            if (number == 0)
            {
                break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(pos + 4));
            entries.Add(Encoding.Unicode.GetString(buffer, pos + 6, length));
            pos += 6 + length;
        }

        return string.Join("\n", entries);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ADeedAsksWhichFacingThenPlacesTheOneChosenAndRefusesOutsideAHouse()
    {
        var pm = NewCharacter(new Point3D(5730, 1190, 0));
        try
        {
            var deed = new ShardDecorDeed("blue-couch");
            pm.Backpack.DropItem(deed);
            Assert.Equal("a deed for a blue couch", deed.Name);
            Assert.Contains("Design by Diesel", Props(deed));
            Assert.Contains("Facings: east, north, south, west", Props(deed));

            // Several facings: the deed asks (a window, one button per facing) instead of targeting.
            deed.OnDoubleClick(pm);
            Assert.Null(pm.Target);
            var gump = new ShardDecorFacingGump(deed, ShardDecorDesigns.Facings("blue-couch"));
            Assert.Equal(new[] { "east", "north", "south", "west" }, gump.Choices.Select(d => d.Facing));

            // Choosing "south" makes a south couch and starts pinned's placement target.
            Assert.True(deed.ChooseFacing(pm, 2));
            Assert.Equal("blue-couch-south", deed.Design);
            Assert.NotNull(pm.Target);
            var made = Assert.IsType<ShardDecorAddon>(deed.Addon);
            Assert.Equal("blue-couch-south", made.Design);
            Assert.Equal(ShardDecorDesigns.Get("blue-couch-south")!.Parts.Count, made.Components.Count);
            made.Delete();
            Assert.False(deed.ChooseFacing(pm, 4)); // no fifth facing

            // Outside a house: refused, nothing placed, the deed kept.
            var before = World.Items.Count;
            pm.Target!.Invoke(pm, new LandTarget(new Point3D(5731, 1190, 0), Map.Felucca));
            Assert.False(deed.Deleted);
            Assert.Equal(before, World.Items.Count);
            foreach (var near in Map.Felucca.GetItemsInRange(new Point3D(5731, 1190, 0), 3))
            {
                Assert.IsNotType<ShardDecorComponent>(near);
            }

            // One facing: straight to the target.
            pm.Target = null;
            var fireplace = new ShardDecorDeed("holiday-fireplace");
            pm.Backpack.DropItem(fireplace);
            fireplace.OnDoubleClick(pm);
            Assert.Equal("holiday-fireplace", fireplace.Design);
            Assert.NotNull(pm.Target);

            // A deed whose design is gone says so and does nothing.
            pm.Target = null;
            var lost = new ShardDecorDeed("no-such-piece");
            pm.Backpack.DropItem(lost);
            lost.OnDoubleClick(pm);
            Assert.Null(pm.Target);
            Assert.Equal("a deed for a piece of house decor", lost.Name);
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void EveryDeedIsSoldAndAnOldPriceFileSellsTheNewRowsAtTheirDefaults()
    {
        var sold = new Dictionary<string, string>();

        void Note(string where, Func<PlayerMobile, List<Item>>? create)
        {
            if (create == null)
            {
                return;
            }

            var items = create(null!);
            foreach (var deed in items.OfType<ShardDecorDeed>())
            {
                sold[deed.Group!] = where;
            }

            foreach (var item in items)
            {
                item.Delete();
            }
        }

        // ShardDecorDeed rows only make deeds, so they need no buyer; the rows are found by their keys.
        foreach (var key in new[] { "hunt.buffet", "hunt.formaldining", "hunt.feasting" })
        {
            var row = LeagueShop.Find(key);
            Assert.NotNull(row);
            Assert.Equal(LeagueShopKind.Decor, row!.Kind);
            Assert.False(row.Boxable);
            Assert.StartsWith("Hunt reward: ", row.Label);
            Note("League hunt row", row.Create);
        }

        foreach (var row in AchievementStock.Rows.Where(r => r.Key.StartsWith("ap.decor.", StringComparison.Ordinal)))
        {
            Note("Achievement store", row.Create);
        }

        Note("Bards' guild", new GuildStoreDef("bards", null).AllRows().Single(r => r.Key == "guild.bards.harpsichord").Create);
        Note("Festival winter", FestivalStock.For(FestivalSeasons.Winter).Single(r => r.Key == "festival.winter.holidayfireplace").Create);

        foreach (var (group, where) in sold.OrderBy(kv => kv.Key))
        {
            _out.WriteLine($"{group}: {where}");
        }

        Assert.Equal(ShardDecorDesigns.Groups(), sold.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("League hunt row", sold["buffet-table"]);
        Assert.Equal("League hunt row", sold["formal-dining-table"]);
        Assert.Equal("League hunt row", sold["feasting-table"]);

        // An existing world's price file lists only the rows it was written with: the new rows keep their defaults.
        var old = new RewardStorePrices { Source = "an older file" };
        old.Rows["ap.painting.x"] = new RewardStorePrices.Entry(1, 0);
        var fireplace = AchievementStock.Rows.Single(r => r.Key == "ap.decor.grey-fireplace-grand");
        Assert.Equal((250, 1000), (old.PriceOf(fireplace), old.OpensAtOf(fireplace)));
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void StaffPlaceADesignAnywhereAndClearOnlyTheDataAddonsNearAPoint()
    {
        var map = Map.Felucca;
        var near = new Point3D(5740, 1200, 0);
        var far = new Point3D(5760, 1200, 0);
        var other = new Static(0x0B2C);
        var a = ShardDecorDesigns.Place("barbecue-east", map, near);
        var b = ShardDecorDesigns.Place("barbecue-south", map, far);
        try
        {
            other.MoveToWorld(new Point3D(near.X + 1, near.Y + 1, 0), map);
            Assert.Equal((near, 3), (a.Location, a.Components.Count));
            Assert.Equal(1, ShardDecorDesigns.Clear(map, near, 6));
            Assert.True(a.Deleted);
            Assert.All(a.Components, c => Assert.True(c.Deleted));
            Assert.False(b.Deleted);
            Assert.False(other.Deleted);
        }
        finally
        {
            a.Delete();
            b.Delete();
            other.Delete();
        }
    }
}
