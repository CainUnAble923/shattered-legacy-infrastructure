// cc-P100: the Visual Builder (server/customizations/ClusterFVisualBuilder.cs), after Kamras's ServUO Visual Builder.
// Notes in shard-migration notes/cc-P100-visual-builder.md.
//
// Facts:
//   1. Wipe then undo restores every item, as it was (ID, hue, name, place), and a protected wipe leaves alone what the
//      session did not place.
//   2. World mode is refused below Administrator: a GameMaster cannot turn it on and cannot erase or wipe a static the
//      builder did not place. An Administrator can, only plain Statics, and undo puts them back.
//   3. The fill box cap: 21 tiles on a side is refused with nothing placed; 20 x 20 places 400; undo removes them.
//   4. Undo of an erase puts the item back under a new serial, and the older paint step then means the new item.
//   5. The undo history survives a save and load (the world's own GenericPersistence payload); world mode does not.
//   6. Hue and nudge are undo steps too.
//   7. The palette's search: by name, by decimal ID, by hex ID; unnamed tiles are left out.
//
// The fixture area is Trammel x 5900 to 5925, y 3950 to 3975, away from anything this repo seeds and from P98's facts.
// Every fact deletes what it placed.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class VisualBuilderVerification : IDisposable
{
    private static readonly Map M = Map.Trammel;
    private const int X0 = 5900, Y0 = 3950;

    private readonly List<IEntity> _made = new();

    public VisualBuilderVerification() => ClusterFVisualBuilder.ResetAll();

    public void Dispose()
    {
        foreach (var i in ItemsIn(X0 - 1, Y0 - 1, X0 + 26, Y0 + 26))
        {
            i.Delete();
        }

        foreach (var e in _made)
        {
            e.Delete();
        }

        ClusterFVisualBuilder.ResetAll();
    }

    private PlayerMobile Staff(AccessLevel level)
    {
        var pm = new PlayerMobile { Player = true, AccessLevel = level, Name = "VB " + level };
        _made.Add(pm);
        return pm;
    }

    // The map's item enumerable is a ref struct, so a loop, not LINQ.
    private static List<Item> ItemsIn(int x0, int y0, int x1, int y1)
    {
        var list = new List<Item>();
        foreach (var i in M.GetItemsInBounds<Item>(new Rectangle2D(x0, y0, x1 - x0 + 1, y1 - y0 + 1), true))
        {
            if (i.X >= x0 && i.X <= x1 && i.Y >= y0 && i.Y <= y1)
            {
                list.Add(i);
            }
        }

        return list;
    }

    private static string Shape(IEnumerable<Item> items) =>
        string.Join(";", items.Select(i => $"{i.GetType().Name} 0x{i.ItemID:X4} h{i.Hue} '{i.Name}' {i.X},{i.Y},{i.Z}").OrderBy(s => s, StringComparer.Ordinal));

    private static Static Paint(Mobile by, int id, int x, int y, int z, int hue = 0)
    {
        var s = ClusterFVisualBuilder.Paint(by, id, hue, M, new Point3D(x, y, z), out var refused);
        Assert.True(s != null, refused);
        return s;
    }

    private static Static WorldStatic(int id, int x, int y, int z, int hue = 0, string name = null)
    {
        var s = new Static(id) { Hue = hue, Name = name };
        s.MoveToWorld(new Point3D(x, y, z), M);
        return s;
    }

    [Fact]
    public void WipeThenUndoRestoresEveryItem()
    {
        var gm = Staff(AccessLevel.GameMaster);
        Paint(gm, 0x0A99, X0, Y0, 0, 0x481);
        Paint(gm, 0x0B26, X0 + 2, Y0 + 1, 5);
        var named = Paint(gm, 0x11C9, X0 + 3, Y0 + 3, 3, 0x21);
        named.Name = "a fixture tree";
        Assert.Equal(4, ClusterFVisualBuilder.Fill(gm, 0x0519, 0x3B2, M, new Point2D(X0 + 5, Y0), new Point2D(X0 + 6, Y0 + 1), 0, out var r));
        Assert.Null(r);
        var notOurs = WorldStatic(0x0E42, X0 + 1, Y0 + 2, 0);
        var mine = ItemsIn(X0, Y0, X0 + 6, Y0 + 3).Where(i => i != notOurs).ToList();
        var before = Shape(mine);
        Assert.Equal(7, mine.Count);

        var wiped = ClusterFVisualBuilder.Wipe(gm, M, new Point2D(X0, Y0), new Point2D(X0 + 6, Y0 + 3), sbyte.MinValue, sbyte.MaxValue, out r);
        Assert.Null(r);
        Assert.Equal(7, wiped);
        Assert.All(mine, i => Assert.True(i.Deleted));
        Assert.False(notOurs.Deleted);   // protected: not placed by the session, so not wiped
        Assert.Single(ItemsIn(X0, Y0, X0 + 6, Y0 + 3));

        Assert.Equal(7, ClusterFVisualBuilder.Undo(gm));
        var back = ItemsIn(X0, Y0, X0 + 6, Y0 + 3).Where(i => i != notOurs).ToList();
        Assert.Equal(before, Shape(back));
        Assert.All(back, i => Assert.True(ClusterFVisualBuilder.CanModify(gm, i)));   // still the session's
    }

    [Fact]
    public void WorldModeIsRefusedBelowAdministrator()
    {
        var gm = Staff(AccessLevel.GameMaster);
        var seer = Staff(AccessLevel.Seer);
        var admin = Staff(AccessLevel.Administrator);
        var a = WorldStatic(0x0A99, X0 + 10, Y0, 0, 0x481, "bookcase by hand");
        var b = WorldStatic(0x0B26, X0 + 11, Y0, 0);
        var door = new MetalDoor(DoorFacing.WestCW);
        door.MoveToWorld(new Point3D(X0 + 12, Y0, 0), M);

        foreach (var low in new Mobile[] { gm, seer })
        {
            Assert.NotNull(ClusterFVisualBuilder.SetWorldMode(low, true));
            Assert.False(ClusterFVisualBuilder.Get(low).WorldMode);
            // Even with the flag forced on, the access check in the rule itself refuses.
            ClusterFVisualBuilder.Get(low).WorldMode = true;
            Assert.False(ClusterFVisualBuilder.Erase(low, a, out _));
            Assert.Equal(0, ClusterFVisualBuilder.Wipe(low, M, new Point2D(X0 + 10, Y0), new Point2D(X0 + 12, Y0), sbyte.MinValue, sbyte.MaxValue, out _));
            Assert.False(a.Deleted || b.Deleted || door.Deleted);
        }

        Assert.False(ClusterFVisualBuilder.Get(admin).WorldMode);   // off by default
        Assert.Equal(0, ClusterFVisualBuilder.Wipe(admin, M, new Point2D(X0 + 10, Y0), new Point2D(X0 + 12, Y0), sbyte.MinValue, sbyte.MaxValue, out _));
        Assert.Null(ClusterFVisualBuilder.SetWorldMode(admin, true));
        var before = Shape(new Item[] { a, b });
        Assert.Equal(2, ClusterFVisualBuilder.Wipe(admin, M, new Point2D(X0 + 10, Y0), new Point2D(X0 + 12, Y0), sbyte.MinValue, sbyte.MaxValue, out _));
        Assert.True(a.Deleted && b.Deleted);
        Assert.False(door.Deleted);   // only plain Statics, never a door
        Assert.Equal(2, ClusterFVisualBuilder.Undo(admin));
        Assert.Equal(before, Shape(ItemsIn(X0 + 10, Y0, X0 + 11, Y0)));
        // Put back, but not adopted: it was never the session's, so protected mode would not touch it.
        ClusterFVisualBuilder.SetWorldMode(admin, false);
        Assert.All(ItemsIn(X0 + 10, Y0, X0 + 11, Y0), i => Assert.False(ClusterFVisualBuilder.CanModify(admin, i)));
        door.Delete();
    }

    [Fact]
    public void TheFillBoxIsCapped()
    {
        var gm = Staff(AccessLevel.GameMaster);
        Assert.Equal(0, ClusterFVisualBuilder.Fill(gm, 0x0519, 0, M, new Point2D(X0, Y0), new Point2D(X0 + 20, Y0), 0, out var r));
        Assert.Contains("21 x 1", r);
        Assert.Equal(0, ClusterFVisualBuilder.Fill(gm, 0x0519, 0, M, new Point2D(X0, Y0 + 20), new Point2D(X0, Y0), 0, out r));
        Assert.Contains("1 x 21", r);
        Assert.Empty(ItemsIn(X0, Y0, X0 + 25, Y0 + 25));
        Assert.Empty(ClusterFVisualBuilder.Get(gm).Steps);   // a refused fill leaves nothing to undo

        Assert.Equal(400, ClusterFVisualBuilder.Fill(gm, 0x0519, 0, M, new Point2D(X0 + 19, Y0 + 19), new Point2D(X0, Y0), 0, out r));
        Assert.Null(r);
        Assert.Equal(400, ItemsIn(X0, Y0, X0 + 19, Y0 + 19).Count);
        Assert.Equal(400, ClusterFVisualBuilder.Undo(gm));
        Assert.Empty(ItemsIn(X0, Y0, X0 + 25, Y0 + 25));

        // And the wipe box's own cap, 50.
        Assert.Equal(0, ClusterFVisualBuilder.Wipe(gm, M, new Point2D(X0, Y0), new Point2D(X0 + 50, Y0), sbyte.MinValue, sbyte.MaxValue, out r));
        Assert.Contains("51 x 1", r);
    }

    [Fact]
    public void UndoOfAnEraseKeepsTheOlderStepsPointingAtTheItem()
    {
        var gm = Staff(AccessLevel.GameMaster);
        var s = Paint(gm, 0x0A99, X0, Y0 + 5, 0, 0x22);
        Assert.True(ClusterFVisualBuilder.Erase(gm, s, out var r), r);
        Assert.True(s.Deleted);
        Assert.Equal(1, ClusterFVisualBuilder.Undo(gm));        // the erase: put back, a new serial
        var back = Assert.Single(ItemsIn(X0, Y0 + 5, X0, Y0 + 5));
        Assert.NotEqual(s.Serial, back.Serial);
        Assert.Equal(1, ClusterFVisualBuilder.Undo(gm));        // the paint: removes the item that came back
        Assert.True(back.Deleted);
        Assert.Equal(-1, ClusterFVisualBuilder.Undo(gm));
    }

    [Fact]
    public void TheUndoHistorySurvivesASaveAndLoad()
    {
        var admin = Staff(AccessLevel.Administrator);
        Paint(admin, 0x0B26, X0, Y0 + 8, 0, 0x481);
        var hand = WorldStatic(0x0E42, X0 + 1, Y0 + 8, 0, 0, "by hand");
        Assert.Null(ClusterFVisualBuilder.SetWorldMode(admin, true));
        var before = Shape(ItemsIn(X0, Y0 + 8, X0 + 1, Y0 + 8));
        Assert.Equal(2, ClusterFVisualBuilder.Wipe(admin, M, new Point2D(X0, Y0 + 8), new Point2D(X0 + 1, Y0 + 8), sbyte.MinValue, sbyte.MaxValue, out _));
        Assert.True(hand.Deleted);

        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        ClusterFVisualBuilderStore.WriteTo(writer);
        writer.Flush();

        ClusterFVisualBuilder.ResetAll();                      // a restart
        Assert.Equal(-1, ClusterFVisualBuilder.Undo(admin));
        ClusterFVisualBuilderStore.ReadFrom(new BufferReader(buffer));

        Assert.False(ClusterFVisualBuilder.Get(admin).WorldMode);   // off after a restart
        Assert.Equal(2, ClusterFVisualBuilder.Undo(admin));
        Assert.Equal(before, Shape(ItemsIn(X0, Y0 + 8, X0 + 1, Y0 + 8)));
        Assert.Equal(1, ClusterFVisualBuilder.Undo(admin));         // and the paint before it, still there to undo

        var bad = new byte[16];
        var w2 = new BufferWriter(bad, true);
        w2.WriteEncodedInt(99);
        w2.Flush();
        Assert.Throws<System.IO.InvalidDataException>(() => ClusterFVisualBuilderStore.ReadFrom(new BufferReader(bad)));
    }

    [Fact]
    public void HueAndNudgeAreUndoStepsToo()
    {
        var gm = Staff(AccessLevel.GameMaster);
        Assert.Equal(2, ClusterFVisualBuilder.Fill(gm, 0x0519, 0, M, new Point2D(X0, Y0 + 12), new Point2D(X0 + 1, Y0 + 12), 0, out _));
        var one = ItemsIn(X0, Y0 + 12, X0, Y0 + 12)[0];
        Assert.True(ClusterFVisualBuilder.SetHue(gm, one, 0x35, out var r), r);
        Assert.Equal(0x35, one.Hue);
        Assert.Equal(2, ClusterFVisualBuilder.Nudge(gm, 0, 2, 1));
        Assert.Equal(new Point3D(X0, Y0 + 14, 1), one.Location);
        Assert.False(ClusterFVisualBuilder.SetHue(gm, WorldStatic(0x0E42, X0 + 5, Y0 + 12, 0), 0x35, out _));   // not ours

        Assert.Equal(2, ClusterFVisualBuilder.Undo(gm));   // the nudge
        Assert.Equal(new Point3D(X0, Y0 + 12, 0), one.Location);
        Assert.Equal(1, ClusterFVisualBuilder.Undo(gm));   // the hue
        Assert.Equal(0, one.Hue);
        Assert.Equal(2, ClusterFVisualBuilder.Undo(gm));   // the fill
        Assert.True(one.Deleted);
    }

    [Fact]
    public void ThePaletteSearchesByNameAndId()
    {
        var table = new ItemData[0x20];
        table[0x05] = new ItemData { Name = "bookcase" };
        table[0x0A] = new ItemData { Name = "small bookcase" };
        table[0x10] = new ItemData { Name = "candle" };
        table[0x11] = new ItemData { Name = "" };   // unnamed: never listed
        Assert.Equal(new[] { 0x05, 0x0A, 0x10 }, ClusterFVisualBuilder.Search(table, 0x1F, ""));
        Assert.Equal(new[] { 0x05, 0x0A }, ClusterFVisualBuilder.Search(table, 0x1F, "BOOK"));
        Assert.Equal(new[] { 0x10 }, ClusterFVisualBuilder.Search(table, 0x1F, "16"));
        Assert.Equal(new[] { 0x0A }, ClusterFVisualBuilder.Search(table, 0x1F, "0x0a"));
        Assert.Empty(ClusterFVisualBuilder.Search(table, 0x1F, "0x11"));
        Assert.Equal(new[] { 0x05 }, ClusterFVisualBuilder.Search(table, 0x09, "book"));   // capped at max
    }
}
