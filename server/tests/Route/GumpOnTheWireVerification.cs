// A shard test can put a gump on a NetState and read it back. See shard-migration/notes/gate-integrity.md part B.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh.
//
// Every gump is deflate-packed on compile (DynamicGump.Compile -> OutgoingGumpPackets.WritePacked ->
// Deflate.Standard, a LibDeflateBinding over the native libdeflate). Until the builder stage installed the runtime
// stage's native libraries (docker/uo/Dockerfile), the first gump a test sent threw in LibDeflateBinding's
// constructor, threw again in its finalizer, and killed the test host, so no shard test could assert on a gump. This
// fact is the gate for that: if the builder loses libdeflate again, the host dies here and build.sh goes red.
//
// The layout and strings are inflated with the managed ZLibStream, not the native binding that packed them, so a
// pass means the bytes are a zlib stream any client can read, not only that libdeflate agrees with itself.

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GumpOnTheWireVerification
{
    private const string Text = "Shattered Legacy gate: a gump reached the wire";

    private readonly ITestOutputHelper _out;

    public GumpOnTheWireVerification(ITestOutputHelper output) => _out = output;

    private class WireProbeGump : DynamicGump
    {
        public WireProbeGump() : base(50, 60)
        {
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(10, 10, 265, 140, 0x242C);
            builder.AddHtml(30, 30, 150, 75, Text);
        }
    }

    private static byte[] Inflate(ReadOnlySpan<byte> packed, int expectedLength)
    {
        using var input = new MemoryStream(packed.ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        Assert.Equal(expectedLength, (int)output.Length);
        return output.ToArray();
    }

    [Fact]
    public void AGumpIsCompiledSentAndReadBackOffATestNetState()
    {
        var pm = new PlayerMobile { Player = true };
        pm.MoveToWorld(new Point3D(1000, 1000, 0), Map.Trammel);

        using var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;

        try
        {
            var gump = new WireProbeGump();
            var before = ns.SendBuffer.GetReadSpan().Length;

            pm.SendGump(gump);

            Assert.True(pm.HasGump<WireProbeGump>());

            var wire = ns.SendBuffer.GetReadSpan()[before..];
            Assert.True(wire.Length > 27, $"nothing usable on the wire: {wire.Length} bytes");
            Assert.Equal(0xDD, wire[0]);

            var length = BinaryPrimitives.ReadUInt16BigEndian(wire[1..]);
            Assert.Equal(wire.Length, length);
            Assert.Equal((uint)gump.Serial, BinaryPrimitives.ReadUInt32BigEndian(wire[3..]));
            Assert.Equal((uint)gump.TypeID, BinaryPrimitives.ReadUInt32BigEndian(wire[7..]));
            Assert.Equal(50u, BinaryPrimitives.ReadUInt32BigEndian(wire[11..]));
            Assert.Equal(60u, BinaryPrimitives.ReadUInt32BigEndian(wire[15..]));

            // Layout: (4 + packed length), unpacked length, packed bytes.
            var pos = 19;
            var layoutPacked = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]) - 4;
            var layoutLength = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[(pos + 4)..]);
            pos += 8;
            var layout = Encoding.ASCII.GetString(Inflate(wire.Slice(pos, layoutPacked), layoutLength));
            pos += layoutPacked;

            // Strings: count, then the same packed shape; each entry is a u16 length and UTF-16BE text.
            var stringCount = BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]);
            pos += 4;
            var stringsPacked = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]) - 4;
            var stringsLength = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[(pos + 4)..]);
            pos += 8;
            var strings = Inflate(wire.Slice(pos, stringsPacked), stringsLength);
            pos += stringsPacked;

            var firstLength = BinaryPrimitives.ReadUInt16BigEndian(strings);
            var first = Encoding.BigEndianUnicode.GetString(strings, 2, firstLength * 2);

            _out.WriteLine($"0xDD, {length} bytes; layout {layoutPacked} packed -> {layoutLength}: {layout}");
            _out.WriteLine($"strings: {stringCount}, {stringsPacked} packed -> {stringsLength}; first: {first}");

            Assert.Equal(length, pos);
            Assert.Contains("{ resizepic 10 10 9260 265 140 }", layout);
            Assert.Contains("{ htmlgump 30 30 150 75 0 0 0 }", layout);
            Assert.Equal(1u, stringCount);
            Assert.Equal(Text, first);
        }
        finally
        {
            pm.NetState = null;
            pm.Delete();
        }
    }
}
