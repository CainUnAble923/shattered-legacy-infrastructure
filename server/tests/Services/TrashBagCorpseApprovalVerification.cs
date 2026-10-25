// TrashBagCorpseApprovalVerification.cs
//
// cc-P611 (P547 #1-#3, P556 A1). The trash bag's corpse claim (TrashBag.cs, ClusterFCustodianSystem.ClearCorpse and
// ClearCorpsesNearby), driven the way a player drives it: the menu entry's click, the target, and the warning's answer.
// Notes in shard-migration notes/cc-P611-fix-drafts-from-p556.md.
//
// Facts:
//   1. A bonded pet's corpse is never cleared, empty or holding items, fresh or with its rights long lapsed, by its
//      owner or anyone else, by either entry or by [cleanupall's rule (IsEligible). The ghost pet is untouched.
//   2. "Clear corpses nearby" clears only the corpses its warning counted: one that dies while the warning is open
//      stays. Accepted after the player walks away or changes facet, it clears nothing there or here.
//   3. Both warnings check the bag again on acceptance: banked or deleted, nothing is cleared or paid. Cancel clears
//      and pays nothing and starts no cooldown. An answer sent twice pays once.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.CleanUpBritannia;
using Server.Engines.Points;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TrashBagCorpseApprovalVerification
{
    private static readonly Point3D Spot = new(1540, 1680, 0);

    private readonly ITestOutputHelper _out;

    public TrashBagCorpseApprovalVerification(ITestOutputHelper output)
    {
        _out = output;
        CleanUpCraftHost.EnsureCraftSystems();
        CleanUpCraftHost.EnsureAccounts();
    }

    // A player with a trash bag and a connection, so the warning gump is really sent and can be answered.
    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p611{Guid.NewGuid():N}"[..16], "p611-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly TrashBag Bag;

        public Player(string name)
        {
            Pm = new PlayerMobile { Player = true, Name = name, Body = 0x190 };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
            Bag = TrashBag.IssueTo(Pm);
        }

        public double Points => CleanUpBritanniaData.Instance.GetPoints(Pm);

        public void Click<T>() where T : ContextMenuEntry
        {
            var list = PooledRefList<ContextMenuEntry>.Create();
            try
            {
                Bag.GetContextMenuEntries(Pm, ref list);
                ContextMenuEntry entry = null;
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is T)
                    {
                        entry = list[i];
                    }
                }

                Assert.NotNull(entry);
                entry.OnClick(Pm, Bag);
            }
            finally
            {
                list.Dispose();
            }
        }

        public WarningGump Warning => Pm.FindGump<WarningGump>();

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

    private static readonly System.Reflection.MethodInfo GumpRemove = typeof(GumpSystem).GetMethod("Remove",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
        [typeof(NetState), typeof(BaseGump)], null);

    // The client's answer: the gump closes, then its response runs (button 1 is OK on a WarningGump).
    private static void Answer(Player p, BaseGump gump, bool ok)
    {
        Assert.NotNull(gump);
        Assert.NotNull(GumpRemove);
        GumpRemove.Invoke(null, [p.Ns, gump]);
        gump.OnResponse(p.Ns, new RelayInfo(ok ? 1 : 0, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
    }

    private static Point3D At(Point3D from, int dx) => new(from.X + dx, from.Y, from.Z);

    // A monster corpse no one killed, its looting rights long lapsed, holding some ingots.
    private static Corpse OldCorpse(List<IEntity> made, Map map, Point3D at)
    {
        var rat = new Rat();
        made.Add(rat);
        var corpse = new Corpse(rat, []);
        made.Add(corpse);
        corpse.MoveToWorld(at, map);
        corpse.TimeOfDeath = Core.Now - TimeSpan.FromMinutes(5);
        corpse.DropItem(new ShadowIronIngot(4)); // pays Clean Up points
        return corpse;
    }

    private static void Cleanup(List<IEntity> made)
    {
        foreach (var e in made)
        {
            e.Delete();
        }
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ABondedPetsCorpseIsNeverCleared()
    {
        using var owner = new Player("p611 owner");
        using var other = new Player("p611 other");
        var made = new List<IEntity>();
        try
        {
            var pet = new Horse();
            made.Add(pet);
            pet.MoveToWorld(At(Spot, 2), Map.Trammel);
            pet.Controlled = true;
            pet.ControlMaster = owner.Pm;
            pet.IsBonded = true;
            pet.Kill();
            Assert.True(pet.IsDeadBondedPet);
            var empty = Assert.IsAssignableFrom<Corpse>(pet.Corpse);
            made.Add(empty);

            var packPet = new Horse();
            made.Add(packPet);
            packPet.MoveToWorld(At(Spot, 3), Map.Trammel);
            packPet.Controlled = true;
            packPet.ControlMaster = owner.Pm;
            packPet.IsBonded = true;
            packPet.Kill();
            var filled = Assert.IsAssignableFrom<Corpse>(packPet.Corpse);
            made.Add(filled);
            var saddlebag = new Bag();
            saddlebag.DropItem(new Gold(250));
            filled.DropItem(saddlebag);
            filled.DropItem(new Dagger());
            filled.TimeOfDeath = Core.Now - TimeSpan.FromMinutes(30); // every looting right long lapsed

            var refused = new List<string>();
            foreach (var corpse in new[] { empty, filled })
            {
                var label = corpse == empty ? "empty" : "filled";
                if (ClusterFCustodianSystem.IsEligible(corpse))
                {
                    refused.Add($"{label}: eligible for [cleanupall");
                }

                foreach (var p in new[] { owner, other })
                {
                    p.Click<TrashBag.ClearCorpseEntry>();
                    p.Pm.Target?.Invoke(p.Pm, corpse);
                    if (p.Warning is { } warning)
                    {
                        Answer(p, warning, true);
                    }

                    if (corpse.Deleted)
                    {
                        refused.Add($"{label}: cleared by {p.Pm.Name} (single)");
                        break;
                    }
                }
            }

            foreach (var p in new[] { owner, other })
            {
                p.Click<TrashBag.ClearCorpsesNearbyEntry>();
                if (p.Warning is { } warning)
                {
                    Answer(p, warning, true);
                }
            }

            if (empty.Deleted || filled.Deleted)
            {
                refused.Add("cleared by nearby");
            }

            _out.WriteLine(refused.Count == 0 ? "both bonded corpses kept" : string.Join("; ", refused));
            Assert.Empty(refused);
            Assert.False(saddlebag.Deleted);
            Assert.True(pet.IsDeadBondedPet);
            Assert.False(pet.Deleted);
        }
        finally
        {
            Cleanup(made);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void NearbyClearsOnlyTheCorpsesItsWarningCounted()
    {
        using var a = new Player("p611 sweeper");
        var made = new List<IEntity>();
        try
        {
            var first = OldCorpse(made, Map.Trammel, At(Spot, 2));
            var second = OldCorpse(made, Map.Trammel, At(Spot, 4));
            var expected = ClusterFCustodianSystem.ComputePoints(first, a.Pm) +
                           ClusterFCustodianSystem.ComputePoints(second, a.Pm);

            a.Click<TrashBag.ClearCorpsesNearbyEntry>();
            var warning = a.Warning;
            Assert.NotNull(warning);

            // A new kill lands in reach while the warning is open: it was not in the warning.
            var late = OldCorpse(made, Map.Trammel, At(Spot, 3));

            var before = a.Points;
            Answer(a, warning, true);
            _out.WriteLine($"first {first.Deleted}, second {second.Deleted}, late {late.Deleted}; +{a.Points - before}");
            Assert.True(first.Deleted);
            Assert.True(second.Deleted);
            Assert.False(late.Deleted);
            Assert.True(expected > 0);
            Assert.Equal(before + expected, a.Points, 9);

            // The same answer sent again pays nothing more.
            var paid = a.Points;
            warning.OnResponse(a.Ns, new RelayInfo(1, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
                ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
            Assert.Equal(paid, a.Points, 9);
            Assert.False(late.Deleted);
        }
        finally
        {
            Cleanup(made);
        }
    }

    [Fact]
    public void ANearbyWarningAcceptedSomewhereElseClearsNothing()
    {
        var made = new List<IEntity>();
        try
        {
            // Walked to another hunt.
            using (var a = new Player("p611 walker"))
            {
                var here = OldCorpse(made, Map.Trammel, At(Spot, 2));
                a.Click<TrashBag.ClearCorpsesNearbyEntry>();
                var warning = a.Warning;
                Assert.NotNull(warning);

                var away = new Point3D(Spot.X + 40, Spot.Y, Spot.Z);
                a.Pm.MoveToWorld(away, Map.Trammel);
                var there = OldCorpse(made, Map.Trammel, At(away, 2));
                var before = a.Points;
                Answer(a, warning, true);
                _out.WriteLine($"walked: here {here.Deleted}, there {there.Deleted}");
                Assert.False(here.Deleted);
                Assert.False(there.Deleted);
                Assert.Equal(before, a.Points, 9);
            }

            // Recalled to the same spot on another facet.
            using (var b = new Player("p611 recaller"))
            {
                var here = OldCorpse(made, Map.Trammel, At(Spot, 2));
                b.Click<TrashBag.ClearCorpsesNearbyEntry>();
                var warning = b.Warning;
                Assert.NotNull(warning);

                b.Pm.MoveToWorld(Spot, Map.Felucca);
                var there = OldCorpse(made, Map.Felucca, At(Spot, 2));
                var before = b.Points;
                Answer(b, warning, true);
                _out.WriteLine($"facet: here {here.Deleted}, there {there.Deleted}");
                Assert.False(here.Deleted);
                Assert.False(there.Deleted);
                Assert.Equal(before, b.Points, 9);
            }
        }
        finally
        {
            Cleanup(made);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void BothWarningsCheckTheBagAgainAndCancelCostsNothing()
    {
        var made = new List<IEntity>();
        try
        {
            var outcomes = new List<string>();

            // Nearby, the bag banked while the warning is open.
            using (var a = new Player("p611 banker"))
            {
                var corpse = OldCorpse(made, Map.Trammel, At(Spot, 2));
                a.Click<TrashBag.ClearCorpsesNearbyEntry>();
                var warning = a.Warning;
                a.Pm.BankBox.DropItem(a.Bag);
                Answer(a, warning, true);
                if (corpse.Deleted || a.Points > 0)
                {
                    outcomes.Add("nearby cleared with the bag in the bank");
                }
            }

            // Single, the bag deleted while the warning is open.
            using (var b = new Player("p611 dropper"))
            {
                var corpse = OldCorpse(made, Map.Trammel, At(Spot, 2));
                b.Click<TrashBag.ClearCorpseEntry>();
                b.Pm.Target.Invoke(b.Pm, corpse);
                var warning = b.Warning;
                b.Bag.Delete();
                Answer(b, warning, true);
                if (corpse.Deleted || b.Points > 0)
                {
                    outcomes.Add("single cleared with the bag deleted");
                }
            }

            // Single, the bag handed to another player's pack.
            using (var c = new Player("p611 giver"))
            using (var d = new Player("p611 taker"))
            {
                var corpse = OldCorpse(made, Map.Trammel, At(Spot, 2));
                c.Click<TrashBag.ClearCorpseEntry>();
                c.Pm.Target.Invoke(c.Pm, corpse);
                var warning = c.Warning;
                d.Pm.Backpack.DropItem(c.Bag);
                Answer(c, warning, true);
                if (corpse.Deleted || c.Points > 0)
                {
                    outcomes.Add("single cleared with the bag in someone else's pack");
                }
            }

            // Cancel: nothing cleared or paid, and no cooldown: the next try asks again.
            using (var e = new Player("p611 canceller"))
            {
                var corpse = OldCorpse(made, Map.Trammel, At(Spot, 2));
                e.Click<TrashBag.ClearCorpsesNearbyEntry>();
                Answer(e, e.Warning, false);
                if (corpse.Deleted || e.Points > 0)
                {
                    outcomes.Add("cancel cleared");
                }

                e.Click<TrashBag.ClearCorpsesNearbyEntry>();
                var again = e.Warning;
                if (again == null)
                {
                    outcomes.Add("cancel started the cooldown");
                }
                else
                {
                    Answer(e, again, true);
                    if (!corpse.Deleted)
                    {
                        outcomes.Add("accepted after a cancel did not clear");
                    }
                }
            }

            _out.WriteLine(outcomes.Count == 0 ? "all refused as they should be" : string.Join("; ", outcomes));
            Assert.Empty(outcomes);
        }
        finally
        {
            Cleanup(made);
        }
    }
}
