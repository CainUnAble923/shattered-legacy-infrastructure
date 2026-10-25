// DeathHooksVerification.cs
//
// cc-P455 (cc-P429 W1; cc-P263 notes; cc-P404 #1). Pinned deletes a dead non-bonded creature inside Mobile.OnDeath (Mobile.cs:4899-4902)
// before BaseCreature raises CreatureDeathEvent, and the delete clears a pet's master. Handlers that read the victim's
// Deleted or Controlled there saw every creature deleted and every pet wild. These facts kill through the game's own
// path (Mobile.Damage to zero, or Mobile.Kill), never by calling a handler. Notes in shard-migration
// notes/cc-P455-death-hooks.md.
//
// Facts:
//   1. A boss killed by a player gives that player the first-kill loot box (ClusterFLootBoxes, cc-P79).
//   2. A high-end creature's corpse gets the rare Self Repair roll (ClusterFRareSelfRepair, cc-P32), forced to hit.
//   3. A killed non-bonded pet pays no imbue kill effect (Butcher's Eye); a wild creature killed the same way does.
// The bestiary's pet, summon and provoked kills are LeagueBestiaryVerification fact 7.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Network;
using Server.Random;
using Server.Tests.Network;
using Xunit;

using A = Server.ClusterFAchievementSystem;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class DeathHooksVerification : IDisposable
{
    private static readonly Point3D Spot = new(1640, 1760, 0);

    private readonly System.Collections.Generic.List<IDisposable> _players = [];
    private readonly System.Collections.Generic.List<IEntity> _things = [];

    public DeathHooksVerification()
    {
        ShardTestHost.EnsureAccounts();
        if (Mobile.CreateCorpseHandler == null)
        {
            Corpse.Initialize();
        }

        if (A.Definitions.Count == 0)
        {
            A.Configure();
        }

        LootTable.Current = LootTable.Defaults;
        ClusterFLootBoxes.LoopProvider = _ => 1;
    }

    public void Dispose()
    {
        ClusterFFanfare.FlushAll();

        foreach (var p in _players)
        {
            p.Dispose();
        }

        foreach (var e in _things)
        {
            e.Delete();
        }
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p455{Guid.NewGuid():N}"[..16], "p455-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(Point3D at)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = $"P455 {Guid.NewGuid():N}"[..12] };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(at, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            A.ResetForAccount(Account.Username);
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private Player NewPlayer(Point3D at)
    {
        var p = new Player(at);
        _players.Add(p);
        return p;
    }

    private T Placed<T>(T m, Point3D at) where T : Mobile
    {
        _things.Add(m);
        m.MoveToWorld(at, Map.Trammel);
        return m;
    }

    // Every roll hits: Next(n) is 0, NextDouble is 0 (as RareSelfRepairVerification forces it).
    private sealed class AlwaysZero : System.Random
    {
        public override int Next() => 0;
        public override int Next(int maxValue) => 0;
        public override int Next(int minValue, int maxValue) => minValue;
        public override double NextDouble() => 0.0;
        protected override double Sample() => 0.0;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ABossKilledByAPlayerGivesTheFirstKillLootBox()
    {
        var at = new Point3D(Spot.X, Spot.Y, Spot.Z);
        var p = NewPlayer(at);
        var boss = Placed(new Barracoon(), at);
        Assert.True(ClusterFLootBoxes.IsBoss(boss));

        boss.Damage(boss.Hits + 50, p.Pm);

        Assert.False(boss.Alive);
        Assert.Same(p.Pm, boss.LastKiller);
        Assert.True(ClusterFLootBoxes.RecordOf(p.Pm)?.CountOf("boss:Barracoon") == 1, "no first-kill box for the boss");
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AHighEndCreaturesCorpseGetsTheSelfRepairRoll()
    {
        var at = new Point3D(Spot.X + 10, Spot.Y, Spot.Z);
        var paragon = Placed(new Chicken(), at);
        paragon.IsParagon = true;
        Assert.True(ClusterFRareSelfRepair.IsHighEndSource(paragon));

        var plate = new PlateChest();
        plate.Attributes.Luck = 50; // magic, so eligible
        if (paragon.Backpack == null)
        {
            paragon.AddItem(new Backpack());
        }

        paragon.Backpack.DropItem(plate);

        // An ordinary creature, the same way: no roll.
        var plain = Placed(new Chicken(), at);
        var plainPlate = new PlateChest();
        plainPlate.Attributes.Luck = 50;
        if (plain.Backpack == null)
        {
            plain.AddItem(new Backpack());
        }

        plain.Backpack.DropItem(plainPlate);

        var original = BuiltInRng.Generator;
        BuiltInRng.Generator = new AlwaysZero();
        try
        {
            paragon.Kill();
            plain.Kill();
        }
        finally
        {
            BuiltInRng.Generator = original;
        }

        var corpse = Assert.IsAssignableFrom<Corpse>(paragon.Corpse);
        _things.Add(corpse);
        _things.Add(plain.Corpse);
        Assert.Same(corpse, plate.Parent);
        Assert.True(plate.ArmorAttributes.SelfRepair == 1, $"the paragon's corpse plate has Self Repair {plate.ArmorAttributes.SelfRepair}");
        Assert.Equal(0, plainPlate.ArmorAttributes.SelfRepair);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AKilledNonBondedPetPaysNoImbueKillEffect()
    {
        var at = new Point3D(Spot.X + 20, Spot.Y, Spot.Z);
        var killer = NewPlayer(at);
        var owner = NewPlayer(at);

        var ring = new GoldRing { Layer = Layer.Ring };
        ImbueCustomStore.Set(ring, "Butcher's Eye", 20);
        killer.Pm.AddItem(ring);
        _things.Add(ring);
        Assert.True(ClusterFImbueEffects.Worn(killer.Pm, "Butcher's Eye") > 0);

        Cat WithClaws(Cat cat)
        {
            if (cat.Backpack == null)
            {
                cat.AddItem(new Backpack());
            }

            cat.Backpack.DropItem(new DaemonClaw(10));
            return cat;
        }

        int ClawsOn(BaseCreature bc) =>
            Assert.IsAssignableFrom<Corpse>(bc.Corpse).Items.OfType<DaemonClaw>().Sum(c => c.Amount);

        // A wild cat: Butcher's Eye grows its claws.
        var wild = WithClaws(Placed(new Cat(), at));
        wild.Damage(wild.Hits + 50, killer.Pm);
        Assert.False(wild.Alive);
        _things.Add(wild.Corpse);
        Assert.True(ClawsOn(wild) > 10, "Butcher's Eye did nothing on a wild kill");

        // Someone's pet, not bonded: nothing.
        var pet = WithClaws(Placed(new Cat(), at));
        Assert.True(pet.SetControlMaster(owner.Pm));
        Assert.False(pet.IsBonded);
        pet.Damage(pet.Hits + 50, killer.Pm);
        Assert.False(pet.Alive);
        _things.Add(pet.Corpse);
        Assert.True(ClawsOn(pet) == 10, $"a killed pet paid Butcher's Eye: {ClawsOn(pet)} claws");
    }
}
