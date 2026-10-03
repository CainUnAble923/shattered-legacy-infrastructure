// LargeBODItemFillVerification.cs
//
// cc-P42 Part H (Chase, 2026-10-03). A large Smith bulk order deed takes the crafted items directly, each filling its
// entry the way a small deed of that entry fills (pinned SmallBOD.EndCombine's checks and words), as well as completed
// small deeds (pinned's LargeBOD.EndCombine, unchanged). Stock answered "That is not a bulk order." (1045159) to any
// item. Notes: shard-migration notes/cc-P42-defect-batch-3.md, Part H.
//
// Facts (an exceptional Dull Copper large plate deed of 10):
//   1. The deed's Combine button and cursor on an exceptional Dull Copper plate gorget: the gorget is gone and its
//      entry reads 1, with "The item has been combined with the deed." (1045170).
//   2. Refused with pinned's small-deed messages, the item kept: iron (1045168), not exceptional (1045167), a longsword
//      (1045169), an item in the bank (the cursor: 500237; the deed's own check: 1045158), a full entry (1045166).
//   3. A completed small deed still combines into its entry (1045165), and is consumed.
//   4. "Combine this deed with contained items" on a mixed bag fills every entry it can and says how many went in;
//      the refused items stay in the bag.

using System;
using System.Collections.Generic;
using System.Linq;
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
public class LargeBODItemFillVerification
{
    private readonly ITestOutputHelper _out;

    public LargeBODItemFillVerification(ITestOutputHelper output)
    {
        _out = output;
        if (!_startupHooksRun)
        {
            Server.Accounting.Accounts.Configure();
            Server.Misc.WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (Server.Accounting.Security.AccountSecurity.CurrentAlgorithm ==
            Server.Accounting.Security.PasswordProtectionAlgorithm.None)
        {
            Server.Accounting.Security.AccountSecurity.CurrentAlgorithm =
                Server.Accounting.Security.PasswordProtectionAlgorithm.PBKDF2;
        }
    }

    private static bool _startupHooksRun;

    private sealed class Smith : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Smith()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.MoveToWorld(new Point3D(1250, 1250, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            // A connection with no account has a 4 KiB send ring (cc-P30); a player is past login.
            Ns.Account = new Server.Accounting.Account($"p42h{Guid.NewGuid():N}"[..16], "p42-test-only");
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public Container Pack => Pm.Backpack;

        public int Mark => Ns.SendBuffer.GetReadSpan().Length;

        // Every 0xC1 localized message number sent since `from`.
        public List<int> Clilocs(int from)
        {
            var span = Ns.SendBuffer.GetReadSpan();
            var found = new List<int>();
            for (var i = from; i + 18 <= span.Length; i++)
            {
                if (span[i] != 0xC1)
                {
                    continue;
                }

                var len = (span[i + 1] << 8) | span[i + 2];
                if (len < 48 || i + len > span.Length)
                {
                    continue;
                }

                found.Add((span[i + 14] << 24) | (span[i + 15] << 16) | (span[i + 16] << 8) | span[i + 17]);
                i += len - 1;
            }

            return found;
        }

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Server.Accounting.Accounts.Remove((Server.Accounting.Account)Ns.Account);
        }
    }

    private static T Put<T>(Container c, T item) where T : Item
    {
        c.DropItem(item);
        return item;
    }

    private static LargeSmithBOD Deed(Smith s)
    {
        var deed = new LargeSmithBOD(10, true, BulkMaterialType.DullCopper, null);
        deed.Entries = LargeBulkEntry.ConvertEntries(deed, LargeBulkEntry.LargePlate);
        return Put(s.Pack, deed);
    }

    private static LargeBulkEntry EntryFor<T>(LargeBOD deed) => deed.Entries.Single(e => e.Details.Type == typeof(T));

    private static T Made<T>(CraftResource resource, bool exceptional) where T : BaseArmor, new() =>
        new() { Resource = resource, Quality = exceptional ? ArmorQuality.Exceptional : ArmorQuality.Regular };

    private static void Press(BaseGump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    // The deed's own Combine button (reply 2) and the cursor it opens, aimed at `item`: the path a player takes.
    private static void CombineOne(Smith s, LargeBOD deed, Item item)
    {
        Press(new LargeBODGump(deed), s.Ns, 2);
        var target = Assert.IsType<BODTarget>(s.Pm.Target);
        target.Invoke(s.Pm, item);
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ACraftedItemFillsItsEntry()
    {
        using var s = new Smith();
        var deed = Deed(s);
        var gorget = Put(s.Pack, Made<PlateGorget>(CraftResource.DullCopper, true));

        var mark = s.Mark;
        CombineOne(s, deed, gorget);

        Assert.True(gorget.Deleted);
        Assert.Equal(1, EntryFor<PlateGorget>(deed).Amount);
        Assert.Contains(1045170, s.Clilocs(mark));
        Assert.All(deed.Entries.Where(e => e.Details.Type != typeof(PlateGorget)), e => Assert.Equal(0, e.Amount));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheWrongItemsAreRefusedWithPinnedsMessages()
    {
        using var s = new Smith();
        var deed = Deed(s);

        void Refused(Item item, int cliloc)
        {
            var mark = s.Mark;
            CombineOne(s, deed, item);
            var said = s.Clilocs(mark);
            _out.WriteLine($"{item.GetType().Name}: {string.Join(", ", said)}");
            Assert.False(item.Deleted);
            Assert.Contains(cliloc, said);
            Assert.DoesNotContain(1045159, said); // stock's "That is not a bulk order."
        }

        Refused(Put(s.Pack, Made<PlateGorget>(CraftResource.Iron, true)), 1045168);      // not the requested ore
        Refused(Put(s.Pack, Made<PlateGorget>(CraftResource.DullCopper, false)), 1045167); // must be exceptional
        Refused(Put(s.Pack, new Longsword()), 1045169);                                   // not in the request
        // The cursor cannot even reach an item in the bank: the targeting layer refuses it first (500237, "Target can
        // not be seen."). The deed's own check, called directly, says pinned's 1045158.
        Refused(Put(s.Pm.BankBox, Made<PlateGorget>(CraftResource.DullCopper, true)), 500237);

        var mark = s.Mark;
        var outside = Put(s.Pm.BankBox, Made<PlateGorget>(CraftResource.DullCopper, true));
        LargeBODItemFill.Fill(deed, s.Pm, outside);
        Assert.False(outside.Deleted);
        Assert.Contains(1045158, s.Clilocs(mark));

        EntryFor<PlateGorget>(deed).Amount = deed.AmountMax;
        Refused(Put(s.Pack, Made<PlateGorget>(CraftResource.DullCopper, true)), 1045166); // entry full
        Assert.Equal(deed.AmountMax, EntryFor<PlateGorget>(deed).Amount);
        Assert.All(deed.Entries.Where(e => e.Details.Type != typeof(PlateGorget)), e => Assert.Equal(0, e.Amount));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ASmallDeedStillCombines()
    {
        using var s = new Smith();
        var deed = Deed(s);
        var arms = EntryFor<PlateArms>(deed);
        var small = Put(s.Pack, new SmallSmithBOD(10, 10, typeof(PlateArms), arms.Details.Number, arms.Details.Graphic, true,
            BulkMaterialType.DullCopper));

        var mark = s.Mark;
        CombineOne(s, deed, small);

        Assert.True(small.Deleted);
        Assert.Equal(10, arms.Amount);
        Assert.Contains(1045165, s.Clilocs(mark)); // The orders have been combined.
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ContainedItemsFillAMixedBag()
    {
        using var s = new Smith();
        var deed = Deed(s);
        var helm = EntryFor<PlateHelm>(deed);
        var bag = Put(s.Pack, new Bag());

        var gorgets = Enumerable.Range(0, 3).Select(_ => Put(bag, Made<PlateGorget>(CraftResource.DullCopper, true))).ToList();
        var legs = Enumerable.Range(0, 2).Select(_ => Put(bag, Made<PlateLegs>(CraftResource.DullCopper, true))).ToList();
        var iron = Put(bag, Made<PlateGorget>(CraftResource.Iron, true));
        var sword = Put(bag, new Longsword());
        var helmDeed = Put(bag, new SmallSmithBOD(10, 10, typeof(PlateHelm), helm.Details.Number, helm.Details.Graphic, true,
            BulkMaterialType.DullCopper));

        var mark = s.Mark;
        Press(new LargeBODGump(deed), s.Ns, BODCombineContained.ButtonID);
        Assert.IsAssignableFrom<Server.Targeting.Target>(s.Pm.Target).Invoke(s.Pm, bag);

        Assert.Equal(3, EntryFor<PlateGorget>(deed).Amount);
        Assert.Equal(2, EntryFor<PlateLegs>(deed).Amount);
        Assert.Equal(10, helm.Amount);
        Assert.All(gorgets.Cast<Item>().Concat(legs).Append(helmDeed), i => Assert.True(i.Deleted));
        Assert.Same(bag, iron.Parent);
        Assert.Same(bag, sword.Parent);
        Assert.True(s.Ns.SendBuffer.GetReadSpan()[mark..].IndexOf(System.Text.Encoding.ASCII.GetBytes("6 items were combined with the deed.")) >= 0 ||
                    s.Ns.SendBuffer.GetReadSpan()[mark..].IndexOf(System.Text.Encoding.BigEndianUnicode.GetBytes("6 items were combined with the deed.")) >= 0);
    }
}
