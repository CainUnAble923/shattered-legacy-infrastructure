// PostValoriteDeedMaterialLineVerification.cs
//
// cc-P61 Part C (bug-list D85, Chase 2026-10-05: our own clilocs). A Platinum..Celestial smith deed never said which
// metal it needs: pinned's GetMaterialNumberFor (SmallBODGump, LargeBODGump, SmallBODAcceptGump, LargeBODAcceptGump) maps
// only Dull Copper..Valorite to EA's 1045142+ and returns 0 for the rest, so the gumps and the tooltips (SmallBOD.cs:64,
// LargeBOD.cs:55) had no line. BOD-gumps-post-valorite-material.patch makes the fall-through
// ShardClilocs.PostValoriteMaterial, our 1900003..1900010; the player package ships their text.
// Notes: shard-migration notes/cc-P61-batch-7.md, Part C.
//
// Facts:
//   C1. For each of the eight metals, a small and a large deed: the deed gump and the accept gump sent to a client carry
//       the metal's number, and the tooltip lists it.
//   C2. Every other material is pinned's: Dull Copper..Valorite EA's 1045142..1045149, the leathers 1049348..1049350,
//       None no line.
//   C3. The eight numbers follow 1900002 in order, in our block, one per metal.

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Server;
using Server.Engines.BulkOrders;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PostValoriteDeedMaterialLineVerification
{
    private readonly ITestOutputHelper _out;

    public PostValoriteDeedMaterialLineVerification(ITestOutputHelper output) => _out = output;

    private static readonly (BulkMaterialType Mat, int Number)[] Ours =
    {
        (BulkMaterialType.Platinum, 1_900_003),
        (BulkMaterialType.Toxic, 1_900_004),
        (BulkMaterialType.Blaze, 1_900_005),
        (BulkMaterialType.Frost, 1_900_006),
        (BulkMaterialType.Obsidian, 1_900_007),
        (BulkMaterialType.Mythril, 1_900_008),
        (BulkMaterialType.Adamantium, 1_900_009),
        (BulkMaterialType.Celestial, 1_900_010)
    };

    private sealed class Smith : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Smith()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.MoveToWorld(new Point3D(1240, 1240, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
        }
    }

    // The gump as the client gets it: the 0xDD packet's layout text (as BODCombineContainedVerification reads it).
    private static string Sent(Smith s, BaseGump gump)
    {
        var from = s.Ns.SendBuffer.GetReadSpan().Length;
        s.Pm.SendGump(gump);
        var wire = s.Ns.SendBuffer.GetReadSpan()[from..];
        var start = -1;
        for (var i = 0; i + 7 <= wire.Length; i++)
        {
            if (wire[i] == 0xDD && BinaryPrimitives.ReadUInt32BigEndian(wire[(i + 3)..]) == (uint)gump.Serial)
            {
                start = i;
                break;
            }
        }

        Assert.True(start >= 0, "no gump on the wire");
        wire = wire[start..];
        var pos = 19;
        var packed = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]) - 4;
        var length = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[(pos + 4)..]);
        using var input = new MemoryStream(wire.Slice(pos + 8, packed).ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        Assert.Equal(length, (int)output.Length);
        return Encoding.ASCII.GetString(output.ToArray());
    }

    private static SmallSmithBOD SmallDeed(BulkMaterialType mat) =>
        new(0, 20, typeof(PlateGorget), 1025139, 0x1413, true, mat);

    private static LargeSmithBOD LargeDeed(BulkMaterialType mat)
    {
        var deed = new LargeSmithBOD(20, true, mat, null);
        deed.Entries = LargeBulkEntry.ConvertEntries(deed, LargeBulkEntry.LargePlate);
        return deed;
    }

    // A localized HTML entry carrying the number: "{ xmfhtmlgump x y w h <number> ... }".
    private static bool Shows(string layout, int number) => layout.Contains($" {number} ");

    // ---------------------------------------------------------------- C1

    [Fact]
    public void EveryPostValoriteDeedNamesItsMetalInGumpsAndTooltips()
    {
        foreach (var (mat, number) in Ours)
        {
            using var s = new Smith();
            var small = SmallDeed(mat);
            var large = LargeDeed(mat);
            s.Pm.Backpack.DropItem(small);
            s.Pm.Backpack.DropItem(large);

            var smallGump = Sent(s, new SmallBODGump(small));
            var largeGump = Sent(s, new LargeBODGump(large));
            var smallAccept = Sent(s, new SmallBODAcceptGump(small));
            var largeAccept = Sent(s, new LargeBODAcceptGump(large));
            var smallTip = PropertyListReader.Read(small).Select(e => e.Number).ToArray();
            var largeTip = PropertyListReader.Read(large).Select(e => e.Number).ToArray();

            _out.WriteLine($"{mat}: {number} small gump {Shows(smallGump, number)}, large gump {Shows(largeGump, number)}, " +
                           $"accept {Shows(smallAccept, number)}/{Shows(largeAccept, number)}, tooltips " +
                           $"{smallTip.Contains(number)}/{largeTip.Contains(number)}");

            Assert.True(Shows(smallGump, number), $"{mat} small gump");
            Assert.True(Shows(largeGump, number), $"{mat} large gump");
            Assert.True(Shows(smallAccept, number), $"{mat} small accept gump");
            Assert.True(Shows(largeAccept, number), $"{mat} large accept gump");
            Assert.Contains(number, smallTip);
            Assert.Contains(number, largeTip);

            small.Delete();
            large.Delete();
        }
    }

    // ---------------------------------------------------------------- C2

    [Fact]
    public void EveryOtherMaterialKeepsPinnedsLine()
    {
        for (var m = BulkMaterialType.DullCopper; m <= BulkMaterialType.Valorite; m++)
        {
            var want = 1045142 + (m - BulkMaterialType.DullCopper);
            Assert.Equal(want, SmallBODGump.GetMaterialNumberFor(m));
            Assert.Equal(want, LargeBODGump.GetMaterialNumberFor(m));
            Assert.Equal(want, SmallBODAcceptGump.GetMaterialNumberFor(m));
            Assert.Equal(want, LargeBODAcceptGump.GetMaterialNumberFor(m));
        }

        for (var m = BulkMaterialType.Spined; m <= BulkMaterialType.Barbed; m++)
        {
            Assert.Equal(1049348 + (m - BulkMaterialType.Spined), SmallBODGump.GetMaterialNumberFor(m));
            Assert.Equal(1049348 + (m - BulkMaterialType.Spined), LargeBODGump.GetMaterialNumberFor(m));
        }

        Assert.Equal(0, SmallBODGump.GetMaterialNumberFor(BulkMaterialType.None));
        Assert.Equal(0, LargeBODGump.GetMaterialNumberFor(BulkMaterialType.None));
        Assert.Equal(0, ShardClilocs.PostValoriteMaterial(BulkMaterialType.Valorite));

        // A Valorite deed's tooltip still says EA's line, and an iron one says none.
        var valorite = SmallDeed(BulkMaterialType.Valorite);
        var iron = SmallDeed(BulkMaterialType.None);
        try
        {
            Assert.Contains(1045149, PropertyListReader.Read(valorite).Select(e => e.Number));
            Assert.DoesNotContain(PropertyListReader.Read(iron), e => e.Number is >= 1045142 and <= 1045149 or >= 1_900_003 and <= 1_900_010);
        }
        finally
        {
            valorite.Delete();
            iron.Delete();
        }
    }

    // ---------------------------------------------------------------- C3

    [Fact]
    public void TheEightNumbersFollowTheFirstThreeInOurBlock()
    {
        var numbers = Ours.Select(o => ShardClilocs.PostValoriteMaterial(o.Mat)).ToArray();
        Assert.Equal(Ours.Select(o => o.Number), numbers);
        Assert.Equal(Enumerable.Range(ShardClilocs.MetalFamiliarity + 1, 8), numbers);
        Assert.All(numbers, n => Assert.InRange(n, ShardClilocs.BlockFirst, ShardClilocs.BlockLast));
        Assert.Equal(new[] { ShardClilocs.PlatinumIngots, ShardClilocs.CelestialIngots }, new[] { numbers[0], numbers[7] });
    }
}
