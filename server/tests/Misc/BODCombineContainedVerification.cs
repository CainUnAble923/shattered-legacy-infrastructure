// BODCombineContainedVerification.cs
//
// cc-P22, F-17 (ported): OSI's "Combine this deed with contained items" on small and large bulk order deeds.
// Notes in shard-migration notes/cc-P22-small-features-1.md.
//
// Facts:
//   1. Both pinned gumps carry the button (reply 3, cliloc 1157304) above EXIT, on the wire.
//   2. A small deed fills from a bag through its button and target cursor, and leaves the wrong material behind.
//   3. It stops at its amount and says how many went in.
//   4. A large deed takes its filled small deeds from a bag.
//   5. Nothing outside the targeted container is touched; a container outside the backpack is refused.
//   6. The one-item combine still works.

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
public class BODCombineContainedVerification
{
    private readonly ITestOutputHelper _out;

    public BODCombineContainedVerification(ITestOutputHelper output) => _out = output;

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

        public Container Pack => Pm.Backpack;

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
        }
    }

    private static T Put<T>(Container c, T item) where T : Item
    {
        c.DropItem(item);
        return item;
    }

    private static PlateGorget Gorget(CraftResource resource) => new() { Resource = resource };

    private static SmallSmithBOD GorgetDeed(int cur, int max, BulkMaterialType mat) =>
        new(cur, max, typeof(PlateGorget), 1025139, 0x1413, false, mat);

    private static void Press(BaseGump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    // The button, then the cursor it opens, aimed at `container`: the path a player takes.
    private static void CombineContained(Smith s, BaseBOD deed, BaseGump gump, Container container)
    {
        Press(gump, s.Ns, BODCombineContained.ButtonID);
        var target = Assert.IsAssignableFrom<Server.Targeting.Target>(s.Pm.Target);
        target.Invoke(s.Pm, container);
    }

    // The 0xDD packet for this gump: its id byte followed, three bytes on, by the gump's serial.
    private static string Layout(NetState ns, int from, BaseGump gump)
    {
        var wire = ns.SendBuffer.GetReadSpan()[from..];
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

    // ---------------------------------------------------------------- 1

    [Fact]
    public void BothGumpsCarryTheButtonAboveExit()
    {
        using var s = new Smith();
        var small = Put(s.Pack, GorgetDeed(0, 10, BulkMaterialType.None));
        var large = Put(s.Pack, new LargeSmithBOD());

        var from = s.Ns.SendBuffer.GetReadSpan().Length;
        var smallGump = new SmallBODGump(small);
        s.Pm.SendGump(smallGump);
        var smallLayout = Layout(s.Ns, from, smallGump);

        from = s.Ns.SendBuffer.GetReadSpan().Length;
        var largeGump = new LargeBODGump(large);
        s.Pm.SendGump(largeGump);
        var largeLayout = Layout(s.Ns, from, largeGump);

        _out.WriteLine(smallLayout);
        _out.WriteLine(largeLayout);

        Assert.Contains("{ button 125 216 4005 4007 1 0 3 }", smallLayout);
        Assert.Contains(" 160 216 300 20 1157304 ", smallLayout);
        Assert.Contains("{ button 125 240 4005 4007 1 0 1 }", smallLayout); // EXIT, one row down

        var n = large.Entries.Length * 24;
        Assert.Contains($"{{ button 125 {192 + n} 4005 4007 1 0 3 }}", largeLayout);
        Assert.Contains($" 160 {192 + n} 300 20 1157304 ", largeLayout);
        Assert.Contains($"{{ button 125 {216 + n} 4005 4007 1 0 1 }}", largeLayout);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ASmallDeedFillsFromABagAndLeavesTheWrongMaterialBehind()
    {
        using var s = new Smith();
        var deed = Put(s.Pack, GorgetDeed(0, 10, BulkMaterialType.DullCopper));
        var bag = Put(s.Pack, new Bag());
        var right = Enumerable.Range(0, 3).Select(_ => Put(bag, Gorget(CraftResource.DullCopper))).ToList();
        var iron = Enumerable.Range(0, 2).Select(_ => Put(bag, Gorget(CraftResource.Iron))).ToList();
        var sword = Put(bag, new Longsword());

        CombineContained(s, deed, new SmallBODGump(deed), bag);

        Assert.Equal(3, deed.AmountCur);
        Assert.All(right, i => Assert.True(i.Deleted));
        Assert.All(iron, i => Assert.Same(bag, i.Parent));
        Assert.Same(bag, sword.Parent);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ItStopsAtItsAmountAndSaysHowManyWentIn()
    {
        using var s = new Smith();
        var deed = Put(s.Pack, GorgetDeed(8, 10, BulkMaterialType.None));
        var bag = Put(s.Pack, new Bag());
        var five = Enumerable.Range(0, 5).Select(_ => Put(bag, Gorget(CraftResource.Iron))).ToList();

        Assert.Equal(2, BODCombineContained.Combine(s.Pm, deed, bag));

        Assert.Equal(10, deed.AmountCur);
        Assert.True(deed.Complete);
        Assert.Equal(2, five.Count(i => i.Deleted));
        Assert.Equal(3, five.Count(i => i.Parent == bag));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ALargeDeedTakesItsSmallDeedsFromABag()
    {
        using var s = new Smith();
        var large = Put(s.Pack, new LargeSmithBOD());
        var bag = Put(s.Pack, new Bag());

        var smalls = large.Entries
            .Select(e => Put(bag, new SmallSmithBOD(large.AmountMax, large.AmountMax, e.Details.Type, e.Details.Number,
                e.Details.Graphic, large.RequireExceptional, large.Material)))
            .ToList();
        var unfinished = Put(bag, new SmallSmithBOD(0, large.AmountMax, large.Entries[0].Details.Type,
            large.Entries[0].Details.Number, large.Entries[0].Details.Graphic, large.RequireExceptional, large.Material));
        var gorget = Put(bag, Gorget(CraftResource.Iron));

        CombineContained(s, large, new LargeBODGump(large), bag);

        Assert.True(large.Complete);
        Assert.All(smalls, d => Assert.True(d.Deleted));
        Assert.Same(bag, unfinished.Parent);
        Assert.Same(bag, gorget.Parent);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void NothingOutsideTheTargetedContainerIsTouched()
    {
        using var s = new Smith();
        var deed = Put(s.Pack, GorgetDeed(0, 10, BulkMaterialType.None));
        var bag = Put(s.Pack, new Bag());
        var inner = Put(bag, new Pouch());
        var inBag = Put(bag, Gorget(CraftResource.Iron));
        var inInner = Put(inner, Gorget(CraftResource.Iron));   // one container down: not "in" the targeted one
        var loose = Put(s.Pack, Gorget(CraftResource.Iron));
        var bankBag = Put(s.Pm.BankBox, new Bag());
        var inBank = Put(bankBag, Gorget(CraftResource.Iron));

        Assert.Equal(1, BODCombineContained.Combine(s.Pm, deed, bag));
        Assert.True(inBag.Deleted);
        Assert.Same(inner, inInner.Parent);
        Assert.Same(s.Pack, loose.Parent);

        Assert.Equal(0, BODCombineContained.Combine(s.Pm, deed, bankBag));
        Assert.Same(bankBag, inBank.Parent);
        Assert.Equal(1, deed.AmountCur);

        // The backpack itself may be targeted, as ServUO allows: it takes what is directly in it.
        Assert.Equal(1, BODCombineContained.Combine(s.Pm, deed, s.Pack));
        Assert.True(loose.Deleted);
        Assert.Same(inner, inInner.Parent);
        Assert.Equal(2, deed.AmountCur);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheOneItemCombineStillWorks()
    {
        using var s = new Smith();
        var deed = Put(s.Pack, GorgetDeed(0, 10, BulkMaterialType.None));
        var gorget = Put(s.Pack, Gorget(CraftResource.Iron));

        Press(new SmallBODGump(deed), s.Ns, 2);
        var target = Assert.IsType<BODTarget>(s.Pm.Target);
        target.Invoke(s.Pm, gorget);

        Assert.True(gorget.Deleted);
        Assert.Equal(1, deed.AmountCur);
    }
}
