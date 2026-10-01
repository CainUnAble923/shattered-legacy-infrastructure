// BankLimitVerification.cs
//
// cc-P22, F-19: the bank box holds 1,000 items (plus a League bonus that is 0 until ranks exist); every other
// container keeps pinned's 125. Notes in shard-migration notes/cc-P22-small-features-1.md.
//
// Facts:
//   1. A bank takes item 1,000 and refuses item 1,001 with OSI's message (cliloc 1080017, sent by pinned
//      Container.SendFullItemsMessage, Server/Items/Container.cs:265-269), through the drag-drop path a player uses.
//   2. A backpack and a house container keep 125.
//   3. A bank saved at the old limit gets the new one at world load, and a character with no bank gets one at login.
//   4. One [TCFill in a fresh bank: the item count, reported (the brief asks for the number).

using System;
using Server;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class BankLimitVerification
{
    private const int ContainerCannotHoldMoreItems = 1080017;

    private readonly ITestOutputHelper _out;

    public BankLimitVerification(ITestOutputHelper output) => _out = output;

    private static PlayerMobile NewPlayer()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(new Point3D(1220, 1220, 0), Map.Trammel);
        return pm;
    }

    private static NetState Online(PlayerMobile pm)
    {
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        return ns;
    }

    private static void Offline(PlayerMobile pm)
    {
        if (pm.NetState != null)
        {
            pm.NetState.Mobile = null;
            pm.NetState = null;
        }
    }

    private static bool SentLocalized(NetState ns, int from, int cliloc)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        for (var i = 0; i + 18 <= span.Length; i++)
        {
            if (span[i] == 0xC1 && System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(span[(i + 14)..]) == cliloc)
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ABankTakesItemOneThousandAndRefusesTheNextWithOsisMessage()
    {
        Assert.Equal(1000, ClusterFBankLimit.BaseItems);
        Assert.Equal(0, ClusterFBankLimit.LeagueRankBonus(null));

        var pm = NewPlayer();
        var ns = Online(pm);

        try
        {
            ClusterFBankLimit.OnLogin(pm);
            var bank = pm.BankBox;
            Assert.Equal(1000, bank.MaxItems);

            for (var i = 0; i < 999; i++)
            {
                bank.DropItem(new Dagger());
            }

            Assert.Equal(999, bank.TotalItems);

            bank.Open();

            var thousandth = new Dagger();
            Assert.True(bank.OnDragDrop(pm, thousandth), "the bank refused item 1,000");
            Assert.Equal(1000, bank.TotalItems);

            var from = ns.SendBuffer.GetReadSpan().Length;
            var extra = new Dagger();
            Assert.False(bank.OnDragDrop(pm, extra), "the bank took item 1,001");
            Assert.True(SentLocalized(ns, from, ContainerCannotHoldMoreItems), "no 1080017 for item 1,001");
            Assert.Equal(1000, bank.TotalItems);
            extra.Delete();
        }
        finally
        {
            Offline(pm);
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ABackpackAndAHouseContainerKeep125()
    {
        Assert.Equal(125, Container.GlobalMaxItems);

        var pm = NewPlayer();
        var chest = new WoodenChest();

        try
        {
            ClusterFBankLimit.OnLogin(pm);
            ClusterFBankLimit.ApplyToLoadedPlayers();

            var pack = pm.Backpack;
            Assert.Equal(125, pack.MaxItems);
            Assert.Equal(125, chest.MaxItems);

            for (var i = 0; i < 125; i++)
            {
                pack.DropItem(new Dagger());
            }

            var extra = new Dagger();
            Assert.False(pack.CheckHold(pm, extra, false, true, 0, 0), "the backpack took item 126");
            extra.Delete();

            // A chest is what a house holds; a locked-down one runs the same limit check (Container.CheckHold, :226-238).
            for (var i = 0; i < 125; i++)
            {
                chest.DropItem(new Dagger());
            }

            var extra2 = new Dagger();
            Assert.False(chest.CheckHold(pm, extra2, false, true, 0, 0), "the chest took item 126");
            extra2.Delete();
        }
        finally
        {
            chest.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ABankAtTheOldLimitGetsTheNewOneAtWorldLoadAndANewBankAtLogin()
    {
        var withBank = NewPlayer();
        var withoutBank = NewPlayer();

        try
        {
            // A bank as an old save holds it: no MaxItems of its own, so pinned's 125.
            var oldBank = withBank.BankBox;
            Assert.Equal(125, oldBank.MaxItems);
            Assert.Null(withoutBank.FindItemOnLayer<BankBox>(Layer.Bank));

            ClusterFBankLimit.ApplyToLoadedPlayers();

            Assert.Equal(1000, oldBank.MaxItems);
            // World load does not create banks for characters that have none.
            Assert.Null(withoutBank.FindItemOnLayer<BankBox>(Layer.Bank));

            ClusterFBankLimit.OnLogin(withoutBank);
            Assert.Equal(1000, Assert.IsType<BankBox>(withoutBank.FindItemOnLayer<BankBox>(Layer.Bank)).MaxItems);
        }
        finally
        {
            withBank.Delete();
            withoutBank.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void OneTCFillInAFreshBankIsCounted()
    {
        var setter = typeof(TestCenter).GetProperty(nameof(TestCenter.Enabled))!.GetSetMethod(true)!;
        var was = TestCenter.Enabled;
        setter.Invoke(null, [true]);

        var pm = NewPlayer();

        try
        {
            ClusterFBankLimit.OnLogin(pm);
            TestCenterKit.RefillBank(pm);

            var bank = pm.BankBox;
            _out.WriteLine(
                $"One [TCFill in a fresh bank: TotalItems {bank.TotalItems}, top-level {bank.Items.Count}, limit {bank.MaxItems}"
            );

            Assert.True(bank.TotalItems > 0);
        }
        finally
        {
            setter.Invoke(null, [was]);
            pm.Delete();
        }
    }
}
