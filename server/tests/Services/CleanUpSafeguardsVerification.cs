// CleanUpSafeguardsVerification.cs
//
// cc-P656 (P476 U2, the corpse and payment safeguards P574 F01 says can proceed without Chase's Civic Contract answers).
// Notes in shard-migration notes/cc-P656-p574-safeguards.md.
//
// Facts:
//   1. A trash barrel pays for what it deletes: a stack split inside it, with one piece thrown back, pays for that one
//      piece (about 0.1 points), not the whole stack it was dropped as (100).
//   2. Two players' stacks that merge in a trash barrel each pay their own share.
//   3. A new trash bag from the Warden keeps what the old bag held, wherever the old one was (the bank, the ground).
//   4. [cleanupall takes only what the sweeper could reach: not an item behind a wall, 20 Z up, loose in someone else's
//      house, or a corpse holding a blessed item or a filled bag. [cleanup refuses the house item too, and "Clear
//      corpses nearby" skips a corpse behind the wall.
//   5. The Clean Up store's confirmation buys nothing from another facet, a floor above, or a deleted trader.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Commands;
using Server.Engines.CleanUpBritannia;
using Server.Engines.Points;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CleanUpSafeguardsVerification
{
    private static readonly Point3D Spot = new(1580, 1640, 0);

    private readonly ITestOutputHelper _out;

    public CleanUpSafeguardsVerification(ITestOutputHelper output)
    {
        _out = output;
        CleanUpCraftHost.EnsureCraftSystems();
        CleanUpCraftHost.EnsureAccounts();
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p656{Guid.NewGuid():N}"[..16], "p656-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(string name, Point3D at, bool custodian = false)
        {
            Pm = new PlayerMobile { Player = true, Name = name, Body = 0x190 };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(at, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            if (custodian)
            {
                ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial).JoinedGuilds.Add("custodians");
            }
        }

        public double Points => CleanUpBritanniaData.Instance.GetPoints(Pm);

        public void Dispose()
        {
            foreach (var bag in TrashBag.OwnedBy(Pm))
            {
                bag.Delete();
            }

            Pm.NetState = null;
            Ns.Mobile = null;
            CleanUpBritanniaData.Instance.RemoveEntry(Pm);
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static Point3D At(int dx, int dy, int dz = 0) => new(Spot.X + dx, Spot.Y + dy, Spot.Z + dz);

    private static T Put<T>(List<IEntity> made, T item, Point3D at) where T : Item
    {
        made.Add(item);
        item.MoveToWorld(at, Map.Trammel);
        return item;
    }

    // A monster corpse no one killed, its looting rights long lapsed.
    private static Corpse OldCorpse(List<IEntity> made, Point3D at, params Item[] contents)
    {
        var rat = new Rat();
        made.Add(rat);
        var corpse = Put(made, new Corpse(rat, []), at);
        corpse.TimeOfDeath = Core.Now - TimeSpan.FromMinutes(5);

        foreach (var item in contents)
        {
            corpse.DropItem(item);
        }

        return corpse;
    }

    private static void Cleanup(List<IEntity> made)
    {
        for (var i = made.Count - 1; i >= 0; i--)
        {
            made[i].Delete();
        }
    }

    private sealed class TestHouse : SmallOldHouse
    {
        private static readonly MultiComponentList _floor = BuildFloor();

        public TestHouse(Mobile owner) : base(owner, 0x64)
        {
        }

        public override MultiComponentList Components => _floor;

        private static MultiComponentList BuildFloor()
        {
            var tiles = new List<MultiTileEntry>();
            for (short x = -3; x <= 3; x++)
            {
                for (short y = -3; y <= 3; y++)
                {
                    tiles.Add(new MultiTileEntry(0x4B8, x, y, 0, (TileFlag)1));
                }
            }

            return new MultiComponentList(tiles);
        }
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ABarrelPaysForTheSplitPieceThrownBackNotTheStackItWasDroppedAs()
    {
        using var p = new Player("Splitter", Spot);
        var barrel = new TrashBarrel();
        barrel.MoveToWorld(At(0, 1), Map.Trammel);

        try
        {
            var ingots = new IronIngot(1000); // 0.1 a piece: 100 points
            p.Pm.Backpack.DropItem(ingots);
            Assert.True(barrel.OnDragDrop(p.Pm, ingots));

            // Lift one: the lifted piece keeps the stack's identity, the 999 left in the barrel are a new item.
            Mobile.LiftItemDupe(ingots, 1);
            var rest = barrel.Items.OfType<IronIngot>().Single(i => i != ingots);
            Assert.Equal(1, ingots.Amount);
            Assert.Equal(999, rest.Amount);

            // Take both back, then throw the one piece back in.
            p.Pm.Backpack.DropItem(rest);
            p.Pm.Backpack.DropItem(ingots);
            var before = p.Points;
            Assert.True(barrel.OnDragDrop(p.Pm, ingots));

            barrel.Empty(501479); // Emptying the trashcan!

            var paid = p.Points - before;
            _out.WriteLine($"paid {paid} for one ingot thrown back");
            Assert.True(ingots.Deleted);
            Assert.False(rest.Deleted);
            Assert.Equal(0.1, paid, 6);
            Assert.Equal(0, CleanUpTrash.PendingCount(barrel));
        }
        finally
        {
            barrel.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void StacksThatMergeInABarrelPayEachDropperTheirShare()
    {
        using var a = new Player("First", Spot);
        using var b = new Player("Second", At(1, 0));
        var barrel = new TrashBarrel();
        barrel.MoveToWorld(At(0, 1), Map.Trammel);

        try
        {
            var aBefore = a.Points;
            var bBefore = b.Points;

            var aIngots = new IronIngot(100); // 10 points
            var bIngots = new IronIngot(50);  // 5 points
            a.Pm.Backpack.DropItem(aIngots);
            b.Pm.Backpack.DropItem(bIngots);

            Assert.True(barrel.OnDragDrop(a.Pm, aIngots));
            Assert.True(barrel.OnDragDrop(b.Pm, bIngots));
            Assert.Equal(150, Assert.Single(barrel.Items).Amount); // the second stack merged into the first

            barrel.Empty(501479);

            _out.WriteLine($"first {a.Points - aBefore}, second {b.Points - bBefore}");
            Assert.Equal(10.0, a.Points - aBefore, 6);
            Assert.Equal(5.0, b.Points - bBefore, 6);
        }
        finally
        {
            barrel.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ANewTrashBagKeepsWhatTheOldOneHeldWhereverItWas()
    {
        using var p = new Player("Bagger", Spot);
        var made = new List<IEntity>();

        try
        {
            // An old bag in the bank, holding an ingot.
            var banked = TrashBag.IssueTo(p.Pm);
            var ingot = new IronIngot(5);
            banked.DropItem(ingot);
            p.Pm.BankBox.DropItem(banked);

            var second = TrashBag.IssueTo(p.Pm);
            Assert.NotNull(second);
            Assert.True(banked.Deleted);
            Assert.False(ingot.Deleted, "the banked bag's ingot was destroyed");
            Assert.True(ingot.IsChildOf(p.Pm.Backpack) || ingot.IsChildOf(p.Pm.BankBox));

            // An old bag on the ground, holding a candle.
            var candle = new Candle();
            second.DropItem(candle);
            second.MoveToWorld(At(3, 3), Map.Trammel);
            made.Add(second);

            var third = TrashBag.IssueTo(p.Pm);
            Assert.NotNull(third);
            Assert.True(second.Deleted);
            Assert.False(candle.Deleted, "the grounded bag's candle was destroyed");
            Assert.True(candle.IsChildOf(p.Pm.Backpack) || candle.IsChildOf(p.Pm.BankBox));

            // Still one bag per character.
            Assert.Same(third, Assert.Single(TrashBag.OwnedBy(p.Pm)));
        }
        finally
        {
            Cleanup(made);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheSweepTakesOnlyWhatTheSweeperCouldReach()
    {
        using var tiles = TestTileRows.Seed(TileRows.StoneWall);
        using var p = new Player("Sweeper", Spot, custodian: true);
        using var other = new Player("Neighbour", At(9, 9));
        var made = new List<IEntity>();

        try
        {
            // A wall to the west, three tiles long.
            for (var dy = -1; dy <= 1; dy++)
            {
                Put(made, new Item(0x0080) { Movable = false }, At(-3, dy));
            }

            var mine = new TestHouse(p.Pm);
            made.Add(mine);
            mine.MoveToWorld(At(-4, -6), Map.Trammel);
            var theirs = new TestHouse(other.Pm);
            made.Add(theirs);
            theirs.MoveToWorld(At(4, -6), Map.Trammel);

            var clear = Put(made, new IronIngot(10), At(2, 0));
            var behindWall = Put(made, new IronIngot(20), At(-5, 0));
            var high = Put(made, new IronIngot(30), At(3, 3, 20));
            var inMyHouse = Put(made, new IronIngot(40), At(-4, -6));
            var inTheirHouse = Put(made, new IronIngot(50), At(4, -6));
            Assert.Same(theirs, BaseHouse.FindHouseAt(inTheirHouse.GetWorldLocation(), Map.Trammel, 16));

            var plainCorpse = OldCorpse(made, At(2, 4), new ShadowIronIngot(4));
            var blessedCorpse = OldCorpse(made, At(0, 4), new Candle { LootType = LootType.Blessed });
            var pouch = new Bag();
            pouch.DropItem(new IronIngot(3));
            var bagCorpse = OldCorpse(made, At(-2, 4), pouch);
            var corpseBehindWall = OldCorpse(made, At(-5, 1), new ShadowIronIngot(2));

            // "Clear corpses nearby" (the trash bag's rule) does not see through the wall.
            var near = ClusterFCustodianSystem.ClearableCorpsesNear(p.Pm);
            _out.WriteLine($"clearable nearby: {near.Count}");
            Assert.DoesNotContain(corpseBehindWall, near);

            // [cleanup on the neighbour's loose ingot: refused.
            Assert.False(ClusterFCustodianSystem.CleanupOne(p.Pm, inTheirHouse));
            Assert.False(inTheirHouse.Deleted);

            // [cleanupall, centred on the sweeper. The test host runs no Configure, so the commands are registered here.
            if (!CommandSystem.Entries.ContainsKey("cleanupall"))
            {
                ClusterFCustodianSystem.Configure();
            }

            Assert.True(CommandSystem.Handle(p.Pm, $"{CommandSystem.Prefix}cleanupall"));
            Assert.NotNull(p.Pm.Target);
            p.Pm.Target.Invoke(p.Pm, p.Pm);

            void Expect(Item item, bool swept, string what)
            {
                _out.WriteLine($"{what}: {(item.Deleted ? "swept" : "left")}");
                Assert.True(item.Deleted == swept, $"{what} was {(item.Deleted ? "swept" : "left")}");
            }

            Expect(clear, true, "the ingot in plain view");
            Expect(inMyHouse, true, "the ingot in the sweeper's own house");
            Expect(plainCorpse, true, "the corpse holding ingots");
            Expect(behindWall, false, "the ingot behind the wall");
            Expect(high, false, "the ingot 20 Z up");
            Expect(inTheirHouse, false, "the ingot in the neighbour's house");
            Expect(blessedCorpse, false, "the corpse holding a blessed candle");
            Expect(bagCorpse, false, "the corpse holding a filled bag");
            Expect(corpseBehindWall, false, "the corpse behind the wall");
        }
        finally
        {
            Cleanup(made);
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheStoreConfirmationBuysNothingFromAnotherFacetAFloorAboveOrADeletedTrader()
    {
        using var p = new Player("Shopper", Spot);
        var warden = new SanitationWarden();
        var cub = CleanUpBritanniaData.Instance;
        var scroll = CleanUpBritanniaRewards.SortedOsi.Single(r => r.Type == typeof(ScrollofAlacrity));
        cub.AwardPoints(p.Pm, 20000, message: false);

        int Bought() => p.Pm.Backpack.Items.OfType<ScrollofAlacrity>().Count();

        // The client's OK: the Confirm button with the first radio ("yes") switched on.
        void Ok() => new ConfirmCleanUpRewardGump(warden, scroll).OnResponse(p.Ns,
            new RelayInfo(2, new[] { 1 }, ReadOnlySpan<ushort>.Empty, ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

        try
        {
            // The Warden two tiles away, on another facet.
            warden.MoveToWorld(At(2, 0), Map.Felucca);
            Ok();
            Assert.Equal(0, Bought());

            // A floor above.
            warden.MoveToWorld(At(2, 0, 40), Map.Trammel);
            Ok();
            Assert.Equal(0, Bought());

            // Beside the shopper: bought.
            warden.MoveToWorld(At(2, 0), Map.Trammel);
            Ok();
            Assert.Equal(1, Bought());

            // Deleted where it stood.
            var points = cub.GetPoints(p.Pm);
            warden.Delete();
            Ok();
            Assert.Equal(1, Bought());
            Assert.Equal(points, cub.GetPoints(p.Pm), 9);
        }
        finally
        {
            warden.Delete();

            foreach (var s in p.Pm.Backpack.Items.OfType<ScrollofAlacrity>().ToList())
            {
                s.Delete();
            }
        }
    }
}
