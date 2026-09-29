// Real tile rows for the duration of one fact, and nothing else about the host changed.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// THE TRAP THIS EXISTS FOR (D27, found by D26):
//
//   The test host has no item tile data. TileData's static constructor returns early under xUnit
//   (Projects/Server/TileData.cs:293-301), and only Load sets MaxItemValue (:382), so it stays 0
//   and Item.ItemData, which is ItemTable[itemID & MaxItemValue] (Projects/Server/Items/Item.cs:732),
//   reads row 0 for every item. Row 0 is empty.
//
//   So BaseArmor and BaseWeapon, which take their layer from ItemData.Quality (UOContent/Items/
//   Armor/BaseArmor.cs:166, UOContent/Items/Weapons/BaseWeapon.cs:199), put every piece of armour
//   and every weapon that does not set its own layer on Layer.Invalid. Item.CanEquip refuses
//   Invalid (Item.cs:2150), so Mobile.EquipItem returns false and a path such as character
//   creation packs the piece without complaint. Mobile.AddItem never looks at a layer
//   (Mobile.cs:6616), so an item added that way looks worn and is on no real layer. And every item
//   without a weight of its own weighs 0 (Item.cs:448-466). TestHostTileData.cs pins all of that.
//
//   Seeding a row alone changes nothing: the mask still sends every lookup to row 0. That is the
//   part that is easy to miss, and why this raises MaxItemValue as well.
//
// USE IT when a fact drives a path that checks a layer (EquipItem, character creation, anything
// ending in CanEquip) or reads a tile weight, and the answer is part of what the fact asserts:
//
//   using var tiles = TestTileRows.Seed(
//       TileRows.GargishLeatherChestType1,
//       TileRows.BestialArms
//   );
//
// Declare it BEFORE constructing the items: BaseArmor and BaseWeapon read the row once, in their
// constructors, so an item built before the scope keeps Invalid. Dispose puts back MaxItemValue
// and every row it wrote, so the next fact sees the host exactly as it was.
//
// The rows a fact depends on are the ones named at its call site. Nothing is seeded implicitly.
//
// THE RULES (notes/d27-worn-item-facts.md 4, notes/test-host-truths.md):
//
//   - Rows are constants in TileRows below, never read from tiledata.mul at test time. That file
//     is the game client's, lives in client-data/ (gitignored), and is outside the build context.
//   - Only rows some fact needs. Each says which file it was read from and by what.
//   - This is per fact, never global. Setting MaxItemValue in the fixture would change what 150
//     other facts measure, and TestHostTileData's guard fact goes red if anyone does it. Raising
//     the mask without real rows also makes things worse, not better: an empty row has quality 0,
//     so layers stay Invalid, and Item.DefaultWeight turns its weight 0 into a fictional 1.
//
// PARALLEL EXECUTION: NOT SAFE, AND DOES NOT NEED TO BE.
//
//   TileData.ItemTable and MaxItemValue are static and process-wide. A fact running concurrently
//   with a seeded one would see the raised mask and the seeded rows. That cannot happen here:
//   every shard test class is in [Collection("Sequential UOContent Tests")], and pinned defines
//   that collection with DisableParallelization = true (UOContent.Tests/Fixtures/
//   UOContentFixture.cs:10), so its facts run one at a time and never beside another collection.
//   build.sh runs only the ShatteredLegacy.Tests namespace. A shard test class that leaves the
//   collection breaks this, and much else: ShardTestClock and every fact that moves Core.Now
//   rest on the same condition.
//
//   Scopes must also be disposed in reverse order of creation, which `using var` does. Nested
//   seeds of the same row are fine in that order.
//
// ADDING A ROW: read it from the shard's client-data/classic-client/tiledata.mul (3,188,736 bytes;
// 64-bit flags, 0x10000 item rows) with a parser that follows TileData.Load field for field and
// ends exactly at the end of the file (notes/d26-gargoyle-kilt.md 3.2; rows_p1.py in
// notes/test-host-truths.md re-reads all nine below). Name it after the class that is built on
// that graphic, and say what layer its quality means.

using System;
using System.Collections.Generic;
using System.Reflection;
using Server;
using Server.Items;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

/// <summary>One item tile row, exactly as TileData.Load would have read it.</summary>
public readonly record struct TileRow(
    int ItemID,
    string Name,
    ulong Flags,
    int Weight,
    int Quality,
    int Animation,
    int Quantity,
    int Value,
    int Height
)
{
    public ItemData ToItemData() => new(Name, (TileFlag)Flags, Weight, Quality, Animation, Quantity, Value, Height);
}

/// <summary>
/// The real rows facts need, read from the shard's client-data/classic-client/tiledata.mul.
/// For a wearable, Quality is the layer. All nine were re-read on 2026-09-28 (notes/test-host-truths.md).
/// </summary>
public static class TileRows
{
    // Gargish leather. D28's fact: ServUO's parent graphic, and the chest BestialArms must go on beside.
    public static readonly TileRow GargishLeatherArmsType2 =
        new(0x302, "gargoyle_leather_arm", 0x404002, 4, 19, 567, 0, 0, 1); // 19 Arms
    public static readonly TileRow GargishLeatherChestType1 =
        new(0x304, "gargoyle_leather_che", 0x404002, 4, 13, 569, 0, 0, 1); // 13 InnerTorso

    // BestialArms builds on 0x4052, a chest plate's graphic. Its layer comes from its own constructor
    // since D28; this row is what it would get without that line.
    public static readonly TileRow BestialArms =
        new(0x4052, "Gargoyle Armor Plate", 0x404002, 10, 13, 577, 0, 0, 1); // 13 InnerTorso

    // Gargish cloth. D26's fact: what character creation dresses a gargoyle in.
    public static readonly TileRow GargishClothChestType1 =
        new(0x406, "gargoyle_clothing_ch", 0x404002, 6, 13, 593, 0, 0, 1); // 13 InnerTorso
    public static readonly TileRow GargishClothChestType2 =
        new(0x405, "gargoyle_clothing_ch", 0x404002, 6, 13, 592, 0, 0, 1); // 13 InnerTorso
    public static readonly TileRow GargishClothKiltType1 =
        new(0x408, "gargoyle_clothing_ki", 0x404002, 2, 7, 595, 0, 0, 1); // 7 Gloves
    public static readonly TileRow GargishClothKiltType2 =
        new(0x407, "gargoyle_clothing_ki", 0x404002, 2, 7, 594, 0, 0, 1); // 7 Gloves
    public static readonly TileRow GargishClothLegsType1 =
        new(0x40A, "gargoyle_clothing_le", 0x404002, 4, 4, 597, 0, 0, 1); // 4 Pants
    public static readonly TileRow GargishClothLegsType2 =
        new(0x409, "gargoyle_clothing_le", 0x404002, 4, 4, 596, 0, 0, 1); // 4 Pants
}

/// <summary>
/// Seeds named tile rows and raises MaxItemValue for one fact; Dispose restores both.
/// See the file header for when to use it and why it is not parallel-safe.
/// </summary>
public static class TestTileRows
{
    private static readonly MethodInfo SetMaxItemValue =
        typeof(TileData).GetProperty(nameof(TileData.MaxItemValue))!.GetSetMethod(true)!;

    // What Load leaves it at for a 0x10000-row file like the shard's (TileData.cs:382).
    private static int LoadedMaxItemValue => TileData.ItemTable.Length - 1;

    public static Scope Seed(params TileRow[] rows)
    {
        if (rows.Length == 0)
        {
            throw new ArgumentException("Name the rows the fact depends on.", nameof(rows));
        }

        var ids = new HashSet<int>();
        foreach (var row in rows)
        {
            if (!ids.Add(row.ItemID))
            {
                throw new ArgumentException($"Row 0x{row.ItemID:X} is named twice.", nameof(rows));
            }
        }

        return new Scope(rows);
    }

    public sealed class Scope : IDisposable
    {
        private readonly TileRow[] _rows;
        private readonly ItemData[] _savedRows;
        private readonly int _savedMaxItemValue;
        private bool _disposed;

        internal Scope(TileRow[] rows)
        {
            _rows = rows;
            _savedRows = new ItemData[rows.Length];
            _savedMaxItemValue = TileData.MaxItemValue;

            for (var i = 0; i < rows.Length; i++)
            {
                _savedRows[i] = TileData.ItemTable[rows[i].ItemID];
                TileData.ItemTable[rows[i].ItemID] = rows[i].ToItemData();
            }

            SetMaxItemValue.Invoke(null, [LoadedMaxItemValue]);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            for (var i = _rows.Length - 1; i >= 0; i--)
            {
                TileData.ItemTable[_rows[i].ItemID] = _savedRows[i];
            }

            SetMaxItemValue.Invoke(null, [_savedMaxItemValue]);
        }
    }
}

/// <summary>
/// Proves the scope does the two things it is for and leaves nothing behind. The facts that use it
/// assert on worn items; this asserts on the scope, so a scope that silently stopped seeding or
/// stopped restoring goes red here by name rather than as a confusing failure somewhere else.
/// </summary>
[Collection("Sequential UOContent Tests")]
public class TestTileRowsVerification
{
    private readonly ITestOutputHelper _out;

    public TestTileRowsVerification(ITestOutputHelper output) => _out = output;

    [Fact]
    public void SeedPutsAStockPieceOnItsRealLayerAndDisposePutsTheHostBack()
    {
        var row = TileRows.GargishLeatherArmsType2;
        var maxBefore = TileData.MaxItemValue;
        var rowBefore = TileData.ItemTable[row.ItemID];

        var outside = new GargishLeatherArmsType2();
        Item inside = null, after = null;

        try
        {
            int maxInside;
            ItemData rowInside;

            using (TestTileRows.Seed(row))
            {
                maxInside = TileData.MaxItemValue;
                rowInside = TileData.ItemTable[row.ItemID];
                inside = new GargishLeatherArmsType2();
            }

            var maxAfter = TileData.MaxItemValue;
            var rowAfter = TileData.ItemTable[row.ItemID];
            after = new GargishLeatherArmsType2();

            _out.WriteLine(
                $"before: MaxItemValue={maxBefore} row 0x{row.ItemID:X} quality={rowBefore.Quality} layer={outside.Layer}; " +
                $"inside: MaxItemValue={maxInside} quality={rowInside.Quality} layer={inside.Layer}; " +
                $"after: MaxItemValue={maxAfter} quality={rowAfter.Quality} layer={after.Layer}"
            );

            // Inside the scope the row is written AND reachable: a stock piece built there reads quality 19.
            Assert.Equal(TileData.ItemTable.Length - 1, maxInside);
            Assert.Equal(row.ToItemData(), rowInside);
            Assert.Equal(Layer.Arms, inside.Layer);

            // After it, the host is exactly as it was, whatever that was.
            Assert.Equal(maxBefore, maxAfter);
            Assert.Equal(rowBefore, rowAfter);
            Assert.Equal(outside.Layer, after.Layer);

            // A piece keeps what it read at construction: the scope's reach is the items built inside it.
            Assert.Equal(Layer.Arms, inside.Layer);

            Assert.Throws<ArgumentException>(() => TestTileRows.Seed(row, row));
        }
        finally
        {
            outside.Delete();
            inside?.Delete();
            after?.Delete();
        }
    }
}
