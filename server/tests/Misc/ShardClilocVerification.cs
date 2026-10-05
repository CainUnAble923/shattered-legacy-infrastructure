// ShardClilocVerification.cs
//
// cc-P57 Part D (Chase 2026-10-05): our own client clilocs, 1,900,000 and up (ShardClilocs). The player package adds the
// text to the client's Cliloc.enu; the server sends the numbers. Notes: shard-migration notes/cc-P57-batch-6.md, Part D.
//
// Facts:
//   D1. Each of the three entries sends its new number on the wire: the pack mule's breed entry 1900000 "Breed", the ore
//       satchel's smelt entry 1900001 "Smelt Ore", the Hammer of Hephaestus entry 1900002 "Metal Familiarity". The menu
//       is built from the object's real entries and sent to a 7.0 client's NetState; the bytes read back are the newer
//       context-menu packet (0xBF, sub 0x14, command 2), whose entries carry the number as a 32-bit value (pinned
//       ContextMenuSystem.cs:149-194), so the number reaches the client whole.
//   D2. The numbers are in our block and distinct.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ShardClilocVerification
{
    private readonly ITestOutputHelper _out;

    public ShardClilocVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
    }

    private static readonly Point3D Spot = new(1490, 1780, 0);

    private static ContextMenuEntry[] Entries(IEntity target, Mobile from)
    {
        var list = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            switch (target)
            {
                case Item item:
                    item.GetContextMenuEntries(from, ref list);
                    break;
                case Mobile mobile:
                    mobile.GetContextMenuEntries(from, ref list);
                    break;
            }

            var result = new ContextMenuEntry[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                result[i] = list[i];
            }

            return result;
        }
        finally
        {
            list.Dispose();
        }
    }

    // Sends the target's menu to a 7.0 client and returns the numbers in the packet that arrives.
    private int[] NumbersOnTheWire(PlayerMobile pm, IEntity target)
    {
        var ns = PacketTestUtilities.CreateTestNetState(out var client);
        ns.Version = new ClientVersion(7, 0, 100, 0);
        pm.NetState = ns;
        ns.Mobile = pm;
        try
        {
            Assert.True(ns.NewHaven);
            var menu = new ContextMenu(pm, target, Entries(target, pm));
            Assert.True(menu.RequiresNewPacket);
            ns.SendDisplayContextMenu(menu);

            // Read what arrives (anything sent before the menu is skipped) until the context-menu packet is whole.
            var bytes = new List<byte>();
            var buffer = new byte[4096];
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            var at = -1;
            while (deadline.ElapsedMilliseconds < 5000)
            {
                NetState.Slice();
                if (client.Poll(1000, SelectMode.SelectRead))
                {
                    var read = client.Receive(buffer);
                    bytes.AddRange(buffer.Take(read));
                }

                at = Enumerable.Range(0, Math.Max(0, bytes.Count - 4))
                    .FirstOrDefault(i => bytes[i] == 0xBF && bytes[i + 3] == 0x00 && bytes[i + 4] == 0x14, -1);
                if (at >= 0 && bytes.Count >= at + (bytes[at + 1] << 8 | bytes[at + 2]))
                {
                    break;
                }
            }

            Assert.True(at >= 0, $"no context-menu packet in {bytes.Count} bytes");
            var p = bytes.Skip(at).ToArray();
            Assert.Equal(2, p[5] << 8 | p[6]); // the newer command: 32-bit numbers
            var count = p[11];
            var numbers = new int[count];
            for (var i = 0; i < count; i++)
            {
                var o = 12 + i * 8;
                numbers[i] = p[o] << 24 | p[o + 1] << 16 | p[o + 2] << 8 | p[o + 3];
            }

            _out.WriteLine($"{target.GetType().Name}: [{string.Join(", ", numbers)}]");
            return numbers;
        }
        finally
        {
            pm.NetState = null;
            ns.Mobile = null;
            ns.Dispose();
            client.Close();
        }
    }

    private static PlayerMobile Player()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    // ---------------------------------------------------------------- D1

    [Fact]
    public void ThePackMuleSendsBreed()
    {
        var pm = Player();
        var mule = new PackMule();
        try
        {
            mule.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
            mule.SetControlMaster(pm);
            var numbers = NumbersOnTheWire(pm, mule);
            Assert.Contains(1_900_000, numbers);
            Assert.DoesNotContain(3_006_132, numbers); // "Use"
        }
        finally
        {
            mule.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void TheOreSatchelSendsSmeltOre()
    {
        var pm = Player();
        var satchel = new ReinforcedOreSatchel();
        try
        {
            pm.Backpack.DropItem(satchel);
            var numbers = NumbersOnTheWire(pm, satchel);
            Assert.Contains(1_900_001, numbers);
            Assert.DoesNotContain(3_006_143, numbers); // "Smelt"
        }
        finally
        {
            satchel.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void TheHammerSendsMetalFamiliarity()
    {
        var pm = Player();
        var hammer = new HammerOfHephaestus();
        try
        {
            pm.Backpack.DropItem(hammer);
            var numbers = NumbersOnTheWire(pm, hammer);
            Assert.Contains(1_900_002, numbers);
            Assert.DoesNotContain(1_112_530, numbers); // "Knowledge"
        }
        finally
        {
            hammer.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- D2

    [Fact]
    public void TheNumbersAreInOurBlock()
    {
        var ours = new[] { ShardClilocs.Breed, ShardClilocs.SmeltOre, ShardClilocs.MetalFamiliarity };
        Assert.Equal(new[] { 1_900_000, 1_900_001, 1_900_002 }, ours);
        Assert.All(ours, n => Assert.InRange(n, ShardClilocs.BlockFirst, ShardClilocs.BlockLast));
        Assert.Equal(ours.Length, ours.Distinct().Count());
    }
}
