// TrashBagCorpseClaimVerification.cs
//
// cc-P415 (Chase 2026-10-22: "corpse claim ... should be a menu option on trash bag too, so players can clear corpses to
// help server resources"). The trash bag's "Clear a corpse" and "Clear corpses nearby" (customizations TrashBag.cs) run
// ClusterFCustodianSystem's corpse claim (ClearCorpse, ClearCorpsesNearby) for any player who owns a bag, member or not.
// Notes in shard-migration notes/cc-P415-trash-bag-corpse-claim.md.
//
// Facts:
//   1. The two entries show only for the bag's owner with the bag in their backpack: not in the bank, not for a bag
//      someone else owns. Their numbers are our clilocs (ShardClilocs).
//   2. A monster corpse clears and pays by the existing rule (ComputePoints: the creature's base for its killer plus each
//      item inside at its Clean Up value), for a player who is not a Custodian. A corpse that still holds items asks
//      first and is untouched until confirmed; then the corpse and everything inside are deleted.
//   3. A player's corpse is refused, by either entry.
//   4. A corpse someone else still has looting rights to is refused: another player's fresh kill, and, on a shared kill,
//      a corpse holding an item dealt to the other attacker (SE instanced loot). Once the rights lapse it clears. [cleanup
//      follows the same rule (CleanupOne).
//   5. "Clear corpses nearby" clears every corpse the player may clear within the [cleanupall reach and nothing else: not
//      a corpse out of reach, a player's corpse, another's fresh kill, or ground litter.
//   6. Server resources: a typical hunt's sweep (orcs, skeletons, an ettin, a lich, a dragon, all the player's kills) asks
//      first, then removes every corpse and every item inside for real: none is left in the world, in any container.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.CleanUpBritannia;
using Server.Engines.Points;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TrashBagCorpseClaimVerification
{
    private static readonly Point3D Spot = new(1520, 1650, 0);

    private readonly ITestOutputHelper _out;

    public TrashBagCorpseClaimVerification(ITestOutputHelper output)
    {
        _out = output;
        CleanUpCraftHost.EnsureCraftSystems(); // a corpse's contents pay by the craft tables
        CleanUpCraftHost.EnsureAccounts();     // and points live on the character
    }

    // A player who is not a Custodian, with an account (points live on the character) and a trash bag of their own.
    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p415{Guid.NewGuid():N}"[..16], "p415-test-only");
        public readonly PlayerMobile Pm;
        public readonly TrashBag Bag;

        public Player(string name)
        {
            Pm = new PlayerMobile { Player = true, Name = name, Body = 0x190 }; // a body, so its corpse has one
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);
            Bag = TrashBag.IssueTo(Pm);
        }

        public double Points => CleanUpBritanniaData.Instance.GetPoints(Pm);

        public void Dispose()
        {
            foreach (var bag in TrashBag.OwnedBy(Pm))
            {
                bag.Delete();
            }

            CleanUpBritanniaData.Instance.RemoveEntry(Pm);
            Pm.Delete();
        }
    }

    private static Point3D At(int dx) => new(Spot.X + dx, Spot.Y, Spot.Z);

    // Kills the creature with the damage on record from the given players, and returns its corpse.
    private static Corpse KillBy(BaseCreature bc, int dx, params Mobile[] damagers)
    {
        bc.MoveToWorld(At(dx), Map.Trammel);
        foreach (var m in damagers)
        {
            bc.RegisterDamage(Math.Max(1, bc.HitsMax / Math.Max(1, damagers.Length)), m);
        }

        bc.Kill();
        return Assert.IsAssignableFrom<Corpse>(bc.Corpse);
    }

    // A monster corpse no one killed, its looting rights long lapsed.
    private static Corpse OldCorpse(List<Mobile> mobs, int dx)
    {
        var rat = new Rat();
        mobs.Add(rat);
        var corpse = new Corpse(rat, []);
        corpse.MoveToWorld(At(dx), Map.Trammel);
        corpse.TimeOfDeath = Core.Now - TimeSpan.FromMinutes(5);
        return corpse;
    }

    private static List<Item> Contents(Corpse corpse)
    {
        var items = new List<Item>();
        foreach (var item in corpse.FindItemsByType<Item>())
        {
            items.Add(item);
        }

        return items;
    }

    private static ContextMenuEntry[] Entries(Item item, Mobile from)
    {
        var list = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            item.GetContextMenuEntries(from, ref list);
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

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheEntriesShowOnlyForTheOwnerWithTheBagInTheirPack()
    {
        using var a = new Player("p415 owner");
        using var b = new Player("p415 other");

        string Show(ContextMenuEntry[] e) => string.Join(", ", e.Select(x => $"{x.GetType().Name} {x.Number}"));

        var mine = Entries(a.Bag, a.Pm);
        _out.WriteLine($"owner, in pack: {Show(mine)}");
        Assert.Equal(ShardClilocs.ClearACorpse, Assert.Single(mine, e => e is TrashBag.ClearCorpseEntry).Number);
        Assert.Equal(ShardClilocs.ClearCorpsesNearby, Assert.Single(mine, e => e is TrashBag.ClearCorpsesNearbyEntry).Number);
        Assert.InRange(ShardClilocs.ClearACorpse, ShardClilocs.BlockFirst, ShardClilocs.BlockLast);
        Assert.InRange(ShardClilocs.ClearCorpsesNearby, ShardClilocs.BlockFirst, ShardClilocs.BlockLast);

        // Someone else's bag in this player's pack: Dump only (it refuses them), no corpse claim.
        a.Pm.Backpack.DropItem(b.Bag);
        var theirs = Entries(b.Bag, a.Pm);
        _out.WriteLine($"another's bag, in pack: {Show(theirs)}");
        Assert.DoesNotContain(theirs, e => e is TrashBag.ClearCorpseEntry or TrashBag.ClearCorpsesNearbyEntry);

        // The owner's bag in the bank.
        a.Pm.BankBox.DropItem(a.Bag);
        var banked = Entries(a.Bag, a.Pm);
        _out.WriteLine($"owner, in bank: {Show(banked)}");
        Assert.DoesNotContain(banked, e => e is TrashBag.ClearCorpseEntry or TrashBag.ClearCorpsesNearbyEntry);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AMonsterCorpseClearsAndPaysByTheExistingRuleWithItsContents()
    {
        using var a = new Player("p415 clearer");
        var orc = new Orc();
        try
        {
            Assert.False(ClusterFGuildSystem.IsJoined(a.Pm, "custodians"));

            var corpse = KillBy(orc, 2, a.Pm);
            corpse.DropItem(new Gold(300));          // 3
            corpse.DropItem(new ShadowIronIngot(4)); // 3
            var contents = Contents(corpse);
            var expected = ClusterFCustodianSystem.ComputePoints(corpse, a.Pm);
            var base_ = ClusterFCorpseValue.BaseFor(corpse, a.Pm);
            _out.WriteLine($"orc corpse: {contents.Count} items, base {base_}, pays {expected}");
            Assert.Equal(orc.Fame * ClusterFCorpseValue.PointsPerFame, base_, 9);
            Assert.True(expected >= base_ + 6.0 - 1e-9);

            // Items inside: it asks first and touches nothing.
            var before = a.Points;
            Assert.False(ClusterFCustodianSystem.ClearCorpse(a.Pm, corpse));
            Assert.False(corpse.Deleted);
            Assert.Equal(before, a.Points, 9);

            // Confirmed: the corpse and every item inside are gone for good, paid by the existing rule.
            Assert.True(ClusterFCustodianSystem.ClearCorpse(a.Pm, corpse, confirmed: true));
            Assert.True(corpse.Deleted);
            Assert.All(contents, i => Assert.True(i.Deleted));
            Assert.All(contents, i => Assert.Null(World.FindItem(i.Serial)));
            Assert.Equal(before + expected, a.Points, 9);

            // An empty corpse needs no confirmation.
            var mobs = new List<Mobile>();
            var empty = OldCorpse(mobs, 3);
            Assert.True(ClusterFCustodianSystem.ClearCorpse(a.Pm, empty));
            Assert.True(empty.Deleted);
            mobs.ForEach(m => m.Delete());
        }
        finally
        {
            orc.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void APlayersCorpseIsRefused()
    {
        using var a = new Player("p415 clearer");
        using var dead = new Player("p415 dead");
        var corpse = new Corpse(dead.Pm, []);
        try
        {
            corpse.DropItem(new Dagger());
            corpse.MoveToWorld(At(1), Map.Trammel);

            Assert.False(ClusterFCustodianSystem.ClearCorpse(a.Pm, corpse, confirmed: true));
            Assert.Equal(0, ClusterFCustodianSystem.ClearCorpsesNearby(a.Pm, confirmed: true));
            Assert.False(corpse.Deleted);
            Assert.False(ClusterFCustodianSystem.CanClear(corpse, dead.Pm)); // not even its owner's to throw away
        }
        finally
        {
            corpse.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ACorpseWithSomeoneElsesLiveLootingRightsIsRefusedUntilTheyLapse()
    {
        using var a = new Player("p415 clearer");
        using var b = new Player("p415 killer");
        var orc = new Orc();
        var shared = new Orc();
        try
        {
            // B's fresh kill.
            var corpse = KillBy(orc, 2, b.Pm);
            Assert.False(ClusterFCustodianSystem.CanClear(corpse, a.Pm));
            Assert.True(ClusterFCustodianSystem.CanClear(corpse, b.Pm));
            Assert.False(ClusterFCustodianSystem.ClearCorpse(a.Pm, corpse, confirmed: true));
            Assert.False(ClusterFCustodianSystem.CleanupOne(a.Pm, corpse)); // [cleanup, the same rule
            Assert.False(corpse.Deleted);

            // The rights lapse (Corpse.MonsterLootRightSacrifice, 2 minutes; instanced loot, 3).
            corpse.TimeOfDeath = Core.Now - TimeSpan.FromMinutes(4);
            Assert.True(ClusterFCustodianSystem.ClearCorpse(a.Pm, corpse, confirmed: true));
            Assert.True(corpse.Deleted);

            // A shared kill: A has the right to loot, but one item was dealt to B (SE instanced loot).
            shared.PackItem(new Dagger());
            shared.PackItem(new Dagger());
            var both = KillBy(shared, 3, a.Pm, b.Pm);
            var hidden = both.Items.Count(i => !both.IsChildVisibleTo(a.Pm, i));
            _out.WriteLine($"shared kill: instanced {both.InstancedCorpse}, {both.Items.Count} items, {hidden} dealt to B");
            Assert.False(both.IsCriminalAction(a.Pm));
            Assert.True(both.InstancedCorpse);
            Assert.True(hidden > 0);
            Assert.False(ClusterFCustodianSystem.CanClear(both, a.Pm));
            Assert.False(ClusterFCustodianSystem.ClearCorpse(a.Pm, both, confirmed: true));
            Assert.False(both.Deleted);

            both.TimeOfDeath = Core.Now - TimeSpan.FromMinutes(4);
            Assert.True(ClusterFCustodianSystem.CanClear(both, a.Pm));
            Assert.True(ClusterFCustodianSystem.ClearCorpse(a.Pm, both, confirmed: true));
            Assert.True(both.Deleted);
        }
        finally
        {
            orc.Delete();
            shared.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void NearbyClearsEveryCorpseInReachThePlayerMayClearAndNothingElse()
    {
        using var a = new Player("p415 sweeper");
        using var b = new Player("p415 killer");
        using var dead = new Player("p415 dead");
        var mobs = new List<Mobile>();
        var made = new List<Item>();
        try
        {
            var own = new Orc();
            mobs.Add(own);
            var ownKill = KillBy(own, 2, a.Pm);
            var old = OldCorpse(mobs, 5);
            old.DropItem(new Dagger());
            var edge = OldCorpse(mobs, ClusterFCustodianSystem.CleanupAllRadius - 1);

            var far = OldCorpse(mobs, ClusterFCustodianSystem.CleanupAllRadius + 5);
            var playerCorpse = new Corpse(dead.Pm, []);
            playerCorpse.MoveToWorld(At(1), Map.Trammel);
            var theirs = new Orc();
            mobs.Add(theirs);
            var theirKill = KillBy(theirs, 3, b.Pm);
            var litter = new Dagger();
            litter.MoveToWorld(At(2), Map.Trammel);
            made.AddRange([ownKill, old, edge, far, playerCorpse, theirKill, litter]);
            Assert.True(ClusterFCustodianSystem.IsEligible(litter)); // [cleanupall would take it; this does not

            var inside = Contents(old).Concat(Contents(ownKill)).ToList();
            var expected = ClusterFCustodianSystem.ComputePoints(ownKill, a.Pm) +
                           ClusterFCustodianSystem.ComputePoints(old, a.Pm) +
                           ClusterFCustodianSystem.ComputePoints(edge, a.Pm);

            // Some still hold items: it asks first and clears nothing.
            Assert.Equal(0, ClusterFCustodianSystem.ClearCorpsesNearby(a.Pm));
            Assert.False(old.Deleted);

            var before = a.Points;
            Assert.Equal(3, ClusterFCustodianSystem.ClearCorpsesNearby(a.Pm, confirmed: true));

            Assert.True(ownKill.Deleted);
            Assert.True(old.Deleted);
            Assert.True(edge.Deleted);
            Assert.All(inside, i => Assert.True(i.Deleted));
            Assert.Equal(before + expected, a.Points, 9);

            Assert.False(far.Deleted);
            Assert.False(playerCorpse.Deleted);
            Assert.False(theirKill.Deleted);
            Assert.False(litter.Deleted);

            // The [cleanupall cooldown: a second sweep at once clears nothing.
            var again = OldCorpse(mobs, 4);
            made.Add(again);
            Assert.Equal(0, ClusterFCustodianSystem.ClearCorpsesNearby(a.Pm, confirmed: true));
            Assert.False(again.Deleted);
        }
        finally
        {
            made.ForEach(i => i.Delete());
            mobs.ForEach(m => m.Delete());
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void ATypicalHuntsSweepRemovesEveryCorpseAndItemForReal()
    {
        using var a = new Player("p415 hunter");
        var mobs = new List<Mobile>();
        var corpses = new List<Corpse>();
        try
        {
            BaseCreature[] hunt = [new Orc(), new Orc(), new Skeleton(), new Skeleton(), new Ettin(), new Lich(), new Dragon()];
            var dx = -4;
            foreach (var bc in hunt)
            {
                mobs.Add(bc);
                corpses.Add(KillBy(bc, dx++, a.Pm));
            }

            var gone = new List<Item>();
            foreach (var corpse in corpses)
            {
                var inside = Contents(corpse);
                _out.WriteLine($"{corpse.Owner?.GetType().Name}: {inside.Count} item(s) inside");
                gone.Add(corpse);
                gone.AddRange(inside);
            }

            _out.WriteLine($"sweep: {corpses.Count} corpses, {gone.Count - corpses.Count} items inside, {gone.Count} items in all");

            if (gone.Count > corpses.Count)
            {
                Assert.Equal(0, ClusterFCustodianSystem.ClearCorpsesNearby(a.Pm)); // asks first
                Assert.All(corpses, c => Assert.False(c.Deleted));
            }

            Assert.Equal(corpses.Count, ClusterFCustodianSystem.ClearCorpsesNearby(a.Pm, confirmed: true));
            Assert.All(gone, i => Assert.True(i.Deleted));
            Assert.All(gone, i => Assert.Null(World.FindItem(i.Serial)));
        }
        finally
        {
            corpses.ForEach(c => c.Delete());
            mobs.ForEach(m => m.Delete());
        }
    }
}
