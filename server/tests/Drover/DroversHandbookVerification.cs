// DroversHandbookVerification.cs
//
// cc-P795: the Drover's Handbook (server/customizations/Drover/). Notes in shard-migration notes/cc-P795-drovers-handbook.md.
//
// Facts:
//   1. The book: stock art, its own type and IDs, blessed, starts with Corral, an Animal Trainer sells it, and it saves and loads.
//   2. Corral casts from the book on Herding for stamina, needs no crook and no mana, and refuses without a book.
//   3. Corral's number: Herding / 8, 25 at 200, only that creature, only that tamer, gone after 60 seconds.
//   4. The real taming gate reads it, and the real taming roll uses it up.
//   5. Pets and paragons cannot be corralled, and pay nothing.
//   6. The gain window follows the creature: a warhorse trains Herding past the crook's window, a rabbit does not.
//   7. Calm: the seconds, for stamina, stops the fight, ends, and the wait before the same animal again.
//   8. Herd: radius and count, nearest first, wild only, inside the engine's walk, one payment, and each animal walks.
//   9. Lure: 15 tiles with no line of sight, a walk the engine keeps, the corner of the square refused for nothing.
//  10. Scrolls: an Animal Trainer sells them, they fill the book, and a full book saves and loads.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.SkillHandlers;
using Server.Spells;
using Server.Spells.Drover;
using Server.Targeting;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class DroversHandbookVerification
{
    // Its own spot: shared spots can hold another fact's leftovers (cc-P793).
    private static readonly Point3D Here = new(1802, 1183, 0);

    private readonly ITestOutputHelper _out;

    public DroversHandbookVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureSkillChecks();
        DroverSettings.Configure();
    }

    private static PlayerMobile Drover(double herding, bool online = false)
    {
        var pm = new PlayerMobile { Player = true, Name = "Drover", Body = 400, Hits = 50, Stam = 50 };
        pm.RawDex = 100;
        pm.Stam = 100;
        pm.Skills.Herding.Cap = 200.0;
        pm.Skills.Herding.Base = herding;
        pm.MoveToWorld(Here, Map.Ilshenar);
        pm.AddItem(new Backpack());

        if (online)
        {
            var ns = PacketTestUtilities.CreateTestNetState();
            pm.NetState = ns;
            ns.Mobile = pm;
        }

        return pm;
    }

    private static T Wild<T>() where T : BaseCreature, new()
    {
        var bc = new T { CantWalk = true }; // the clock's seconds run its AI, and it would wander off
        bc.MoveToWorld(new Point3D(Here.X + 1, Here.Y, Here.Z), Map.Ilshenar);
        return bc;
    }

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    /// <summary>The book's page, cast for real: the cast delay runs, then the target is answered.</summary>
    private static void CastPage(PlayerMobile pm, int id, Mobile target)
    {
        Assert.True(DroversHandbook.Cast(pm, id));
        ShardTestClock.AdvanceSeconds(5);
        Assert.NotNull(pm.Target);
        pm.Target.Invoke(pm, target);
    }

    private static void CastCorral(PlayerMobile pm, Mobile target) => CastPage(pm, DroverSettings.CorralId, target);

    /// <summary>What the target cost, read across the answer alone: the clock's seconds refill stamina in the host.</summary>
    private static int Paid(PlayerMobile pm, int id, Mobile target)
    {
        Assert.True(DroversHandbook.Cast(pm, id));
        ShardTestClock.AdvanceSeconds(5);
        Assert.NotNull(pm.Target);
        var before = pm.Stam;
        pm.Target.Invoke(pm, target);
        return before - pm.Stam;
    }

    private static PlayerMobile Reader(double herding)
    {
        var pm = Drover(herding, true);
        pm.Backpack!.DropItem(new DroversHandbook(DroversHandbook.AllPages));
        return pm;
    }

    private static T WildAt<T>(int dx, int dy) where T : BaseCreature, new()
    {
        var bc = new T { CantWalk = true };
        bc.MoveToWorld(new Point3D(Here.X + dx, Here.Y + dy, Here.Z), Map.Ilshenar);
        return bc;
    }

    private static void Done(PlayerMobile pm, params Mobile[] others)
    {
        pm.NetState = null;
        foreach (var m in others)
        {
            m.Delete();
        }

        pm.Delete();
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheBookIsItsOwnTypeAndSavesAndLoads()
    {
        var book = new DroversHandbook();
        try
        {
            Assert.Equal(0x0FEF, book.ItemID);
            Assert.Equal(SpellbookType.Drover, book.SpellbookType);
            Assert.Equal(LootType.Blessed, book.LootType);
            Assert.Equal(SpellbookType.Drover, Spellbook.GetTypeForSpell(693));
            Assert.Equal(SpellbookType.Drover, Spellbook.GetTypeForSpell(696));
            Assert.Equal(SpellbookType.Mystic, Spellbook.GetTypeForSpell(692));
            Assert.Equal(SpellbookType.Invalid, Spellbook.GetTypeForSpell(697));
            Assert.True(book.HasSpell(DroverSettings.CorralId));
            Assert.False(book.HasSpell(DroverSettings.CorralId + 1));
            Assert.Equal(typeof(CorralSpell), SpellRegistry.Types[DroverSettings.CorralId]);

            var copy = RoundTrip(book);
            Assert.True(copy.HasSpell(DroverSettings.CorralId));
            Assert.False(copy.HasSpell(DroverSettings.CorralId + 1));
            Assert.Equal(1, copy.SpellCount);
        }
        finally
        {
            book.Delete();
        }
    }

    [Fact]
    public void AnAnimalTrainerSellsIt()
    {
        var row = new SBAnimalTrainer().BuyInfo.Single(i => i.Type == typeof(DroversHandbook));
        Assert.Equal(DroverSettings.BookPrice, row.Price);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void CorralCastsFromTheBookOnHerdingForStamina()
    {
        ShardTestClock.Arm();
        var pm = Drover(200.0, true);
        var book = new DroversHandbook();
        pm.Backpack!.DropItem(book);
        var wolf = Wild<GreyWolf>();
        try
        {
            pm.Mana = 0;
            var stamina = pm.Stam;

            CastCorral(pm, wolf);

            Assert.Equal(stamina - DroverSettings.CorralStamina, pm.Stam);
            Assert.Equal(25.0, DroverCorral.Bonus(pm, wolf), 6);
            Assert.Null(pm.Spell);
        }
        finally
        {
            pm.NetState = null;
            wolf.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void WithoutTheBookOrThePageThereIsNoCast()
    {
        ShardTestClock.Arm();
        var pm = Drover(200.0, true);
        try
        {
            Assert.False(DroversHandbook.Cast(pm, DroverSettings.CorralId));

            pm.Backpack!.DropItem(new DroversHandbook(0));
            Assert.False(DroversHandbook.Cast(pm, DroverSettings.CorralId));
            Assert.Null(pm.Spell);
        }
        finally
        {
            pm.NetState = null;
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Theory]
    [InlineData(200.0, 25.0)]
    [InlineData(80.0, 10.0)]
    public void TheNumberIsHerdingOverEight(double herding, double amount)
    {
        var pm = Drover(herding);
        try
        {
            Assert.Equal(amount, DroverCorral.AmountFor(pm), 6);
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void ItIsForThatCreatureThatTamerAndTheMinute()
    {
        var pm = Drover(200.0);
        var other = Drover(200.0);
        var wolf = Wild<GreyWolf>();
        var bear = Wild<BlackBear>();
        try
        {
            DroverCorral.Set(pm, wolf, 25.0, Core.Now.AddSeconds(60));
            Assert.Equal(25.0, DroverCorral.Bonus(pm, wolf), 6);
            Assert.Equal(0.0, DroverCorral.Bonus(pm, bear), 6);
            Assert.Equal(0.0, DroverCorral.Bonus(other, wolf), 6);

            // The gate does not use it up, and a second corral replaces the first.
            Assert.Equal(25.0, DroverCorral.Bonus(pm, wolf), 6);
            DroverCorral.Set(pm, bear, 12.0, Core.Now.AddSeconds(60));
            Assert.Equal(0.0, DroverCorral.Bonus(pm, wolf), 6);
            Assert.Equal(12.0, DroverCorral.Bonus(pm, bear), 6);

            // The roll uses it up.
            Assert.Equal(12.0, DroverCorral.Spend(pm, bear), 6);
            Assert.Equal(0.0, DroverCorral.Spend(pm, bear), 6);

            // Past the minute the gate is closed; a roll already under way has its grace, and no more.
            DroverCorral.Set(pm, wolf, 25.0, Core.Now.AddSeconds(-1));
            Assert.Equal(0.0, DroverCorral.Bonus(pm, wolf), 6);
            Assert.Equal(25.0, DroverCorral.Spend(pm, wolf), 6);
            DroverCorral.Set(pm, wolf, 25.0, Core.Now.AddSeconds(-1 - DroverSettings.CorralAttemptGraceSeconds));
            Assert.Equal(0.0, DroverCorral.Spend(pm, wolf), 6);
        }
        finally
        {
            wolf.Delete();
            bear.Delete();
            other.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    private static HashSet<Mobile> BeingTamed =>
        (HashSet<Mobile>)typeof(AnimalTaming).GetField("m_BeingTamed", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Fact]
    public void TheTamingGateOpensWithACorralAndTheRollUsesItUp()
    {
        ShardTestClock.Arm();
        var pm = Drover(200.0, true);
        pm.Skills.AnimalTaming.Cap = 200.0;
        pm.Skills.AnimalTaming.Base = 50.0;
        var wolf = Wild<WhiteWolf>();
        wolf.Location = new Point3D(Here.X + 1, Here.Y, Here.Z);
        try
        {
            Assert.True(wolf.MinTameSkill > 50.0);

            AnimalTaming.OnUse(pm);
            pm.Target.Invoke(pm, wolf);
            Assert.DoesNotContain(wolf, BeingTamed); // no chance
            ShardTestClock.AdvanceSeconds(15);

            DroverCorral.Set(pm, wolf, 25.0, Core.Now.AddSeconds(600));
            AnimalTaming.OnUse(pm);
            pm.Target.Invoke(pm, wolf);
            Assert.Contains(wolf, BeingTamed); // the attempt has begun
            Assert.Equal(25.0, DroverCorral.Bonus(pm, wolf), 6);

            ShardTestClock.AdvanceSeconds(60);
            Assert.DoesNotContain(wolf, BeingTamed);
            Assert.Equal(0.0, DroverCorral.Bonus(pm, wolf), 6); // used, whatever the roll said
        }
        finally
        {
            BeingTamed.Remove(wolf);
            pm.NetState = null;
            wolf.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void APetCannotBeCorralledAndCostsNothing()
    {
        ShardTestClock.Arm();
        var pm = Drover(200.0, true);
        var owner = Drover(0.0);
        pm.Backpack!.DropItem(new DroversHandbook());
        var wolf = Wild<GreyWolf>();
        wolf.Controlled = true;
        wolf.ControlMaster = owner;
        try
        {
            Assert.False(DroverSpell.IsWildAnimal(wolf));
            var stamina = pm.Stam;

            CastCorral(pm, wolf);

            Assert.Equal(stamina, pm.Stam);
            Assert.Equal(0.0, DroverCorral.Bonus(pm, wolf), 6);
            Assert.Null(pm.Spell);
        }
        finally
        {
            pm.NetState = null;
            wolf.Delete();
            owner.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void AParagonIsNotHerdable()
    {
        var wolf = Wild<GreyWolf>();
        try
        {
            Assert.True(DroverSpell.IsWildAnimal(wolf));
            wolf.IsParagon = true;
            Assert.False(DroverSpell.IsWildAnimal(wolf));
        }
        finally
        {
            wolf.Delete();
        }
    }

    // ---------------------------------------------------------------- 6

    private double GainsOn(BaseCreature bc, double herding, int tries = 4000)
    {
        var pm = Drover(herding);
        try
        {
            var spell = new CorralSpell(pm);
            for (var i = 0; i < tries; i++)
            {
                spell.Persuade(bc);
            }

            _out.WriteLine($"{bc.GetType().Name} MTS {bc.MinTameSkill}: Herding {herding} to {pm.Skills.Herding.Base}");
            return pm.Skills.Herding.Base - herding;
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void TheGainWindowFollowsTheCreatureToTheCap()
    {
        var horse = Wild<DreadWarhorse>();
        var rabbit = Wild<Rabbit>();
        try
        {
            Assert.Equal(200.0, horse.MinTameSkill + DroverSettings.GainTop, 6);

            Assert.True(GainsOn(horse, 150.0) > 0.0); // past the crook's window, 138 to 147
            Assert.True(GainsOn(horse, 199.0) > 0.0);
            Assert.Equal(0.0, GainsOn(horse, 200.0), 6);
            Assert.True(GainsOn(rabbit, 70.0) > 0.0);
            Assert.Equal(0.0, GainsOn(rabbit, 100.0), 6); // a rabbit trains no one to 200
        }
        finally
        {
            horse.Delete();
            rabbit.Delete();
        }
    }

    // ---------------------------------------------------------------- 7

    [Theory]
    [InlineData(200.0, 13.0)]
    [InlineData(100.0, 8.0)]
    public void CalmLastsThreePlusHerdingOverTwenty(double herding, double seconds)
    {
        var pm = Drover(herding);
        try
        {
            Assert.Equal(seconds, CalmSpell.DurationFor(pm).TotalSeconds, 6);
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void CalmStopsTheFightForItsTimeThenTheAnimalWaits()
    {
        ShardTestClock.Arm();
        var pm = Reader(200.0);
        var bystander = Drover(0.0);
        var wolf = Wild<GreyWolf>();
        try
        {
            Assert.True(DroversHandbook.Cast(pm, DroverSettings.CalmId));
            ShardTestClock.AdvanceSeconds(5);

            // Set here: the clock's seconds run its AI, which drops a fight it set itself.
            wolf.Combatant = bystander;
            wolf.Warmode = true;
            Assert.Equal(bystander, wolf.Combatant);
            var before = pm.Stam;
            pm.Target.Invoke(pm, wolf);

            Assert.Equal(DroverSettings.CalmStamina, before - pm.Stam);
            Assert.True(wolf.BardPacified);
            Assert.Null(wolf.Combatant); // it stops fighting everyone, not only the drover
            Assert.False(wolf.Warmode);
            Assert.Equal(13.0, (wolf.BardEndTime - Core.Now).TotalSeconds, 6);
            Assert.Null(pm.Spell);

            // Calm again at once: refused, and free.
            Assert.Equal(0, Paid(pm, DroverSettings.CalmId, wolf));
            Assert.Null(pm.Spell);

            // The calm ends by itself, and the wait outlasts it.
            ShardTestClock.AdvanceSeconds(13);
            wolf.AIObject.DoBardPacified();
            Assert.False(wolf.BardPacified);
            Assert.True(CalmSpell.Waiting(wolf));
            Assert.Equal(0, Paid(pm, DroverSettings.CalmId, wolf));
            Assert.False(wolf.BardPacified);

            // Thirty seconds after the calm, it can be calmed again.
            ShardTestClock.AdvanceSeconds(DroverSettings.CalmCooldownSeconds);
            Assert.False(CalmSpell.Waiting(wolf));
            CastPage(pm, DroverSettings.CalmId, wolf);
            Assert.True(wolf.BardPacified);
        }
        finally
        {
            Done(pm, bystander, wolf);
        }
    }

    [Fact]
    public void ACalmThatFailsLeavesNoWait()
    {
        ShardTestClock.Arm();
        var pm = Reader(0.0);
        var horse = Wild<DreadWarhorse>();
        try
        {
            Assert.Equal(DroverSettings.CalmStamina, Paid(pm, DroverSettings.CalmId, horse)); // the attempt is paid for
            Assert.False(horse.BardPacified);
            Assert.False(CalmSpell.Waiting(horse));
        }
        finally
        {
            Done(pm, horse);
        }
    }

    // ---------------------------------------------------------------- 8

    [Theory]
    [InlineData(200.0, 8, 10)]
    [InlineData(100.0, 4, 5)]
    [InlineData(0.0, 1, 1)]
    public void HerdGrowsWithHerding(double herding, int radius, int count)
    {
        var pm = Drover(herding);
        try
        {
            Assert.Equal(radius, HerdSpell.RadiusFor(pm));
            Assert.Equal(count, HerdSpell.CountFor(pm));
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void TheFlockIsTheNearestWildAnimalsInReachOfTheSpot()
    {
        var pm = Drover(200.0);
        var owner = Drover(0.0);
        var spot = new Point2D(Here.X, Here.Y);
        var mobs = new List<BaseCreature>();
        try
        {
            var lead = WildAt<GreyWolf>(2, 0);
            mobs.Add(lead);

            var pet = WildAt<GreyWolf>(3, 0);
            pet.Controlled = true;
            pet.ControlMaster = owner;
            mobs.Add(pet);

            var monster = WildAt<Zombie>(3, 1);
            mobs.Add(monster);

            var edge = WildAt<GreyWolf>(10, 0); // 8 from the lead: in
            mobs.Add(edge);

            var past = WildAt<GreyWolf>(11, 0); // 9 from the lead: out
            mobs.Add(past);

            var flock = HerdSpell.Flock(pm, lead, spot);
            Assert.Equal(lead, flock[0]);
            Assert.Contains(edge, flock);
            Assert.DoesNotContain(pet, flock);
            Assert.DoesNotContain(monster, flock);
            Assert.DoesNotContain(past, flock);

            // The count cap keeps the nearest.
            for (var i = 0; i < 12; i++)
            {
                mobs.Add(WildAt<GreyWolf>(2, 1 + i % 3));
            }

            flock = HerdSpell.Flock(pm, lead, spot);
            Assert.Equal(10, flock.Count);
            Assert.DoesNotContain(edge, flock);

            // An animal the engine would not walk to the spot is left out: 2 + 14 tiles.
            var far = HerdSpell.Flock(pm, lead, new Point2D(Here.X - 14, Here.Y));
            Assert.DoesNotContain(lead, far);
        }
        finally
        {
            foreach (var m in mobs)
            {
                m.Delete();
            }

            owner.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void HerdPaysOnceAtTheSpotAndEachAnimalWalks()
    {
        ShardTestClock.Arm();
        var pm = Reader(200.0);
        var a = WildAt<GreyWolf>(1, 0);
        var b = WildAt<GreyWolf>(1, 1);
        var c = WildAt<GreyWolf>(2, 1);
        try
        {
            // The animal is chosen: nothing paid, still casting, and the spot is asked for.
            Assert.Equal(0, Paid(pm, DroverSettings.HerdId, a));
            var stamina = pm.Stam;
            Assert.NotNull(pm.Target);
            Assert.NotNull(pm.Spell);

            pm.Target.Invoke(pm, new LandTarget(new Point3D(Here.X - 5, Here.Y, Here.Z), Map.Ilshenar));

            Assert.Equal(stamina - DroverSettings.HerdStamina, pm.Stam);
            Assert.Null(pm.Spell);

            foreach (var bc in new[] { a, b, c })
            {
                Assert.NotNull(bc.TargetLocation);
                Assert.Equal(Here.X - 5, bc.TargetLocation.X);
                Assert.Equal(Here.Y, bc.TargetLocation.Y);
                Assert.True(bc.AIObject.CheckHerding()); // the engine keeps the walk
            }
        }
        finally
        {
            Done(pm, a, b, c);
        }
    }

    [Fact]
    public void CancellingHerdsSpotCostsNothing()
    {
        ShardTestClock.Arm();
        var pm = Reader(200.0);
        var a = WildAt<GreyWolf>(1, 0);
        try
        {
            Assert.Equal(0, Paid(pm, DroverSettings.HerdId, a));
            var stamina = pm.Stam;
            pm.Target.Cancel(pm, TargetCancelType.Canceled);

            Assert.Equal(stamina, pm.Stam);
            Assert.Null(pm.Spell);
            Assert.Null(a.TargetLocation);
        }
        finally
        {
            Done(pm, a);
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void LureReachesFifteenTilesWithNoLineOfSight()
    {
        ShardTestClock.Arm();
        var pm = Reader(200.0);
        var wolf = WildAt<GreyWolf>(15, 0);
        try
        {
            Assert.True(DroversHandbook.Cast(pm, DroverSettings.LureId));
            ShardTestClock.AdvanceSeconds(5);

            var stamina = pm.Stam;
            var target = pm.Target;
            Assert.Equal(15, target.Range);
            Assert.False(target.CheckLOS);
            target.Invoke(pm, wolf);

            Assert.Equal(stamina - DroverSettings.LureStamina, pm.Stam);
            Assert.NotNull(wolf.TargetLocation);
            Assert.True(pm.GetDistanceToSqrt(wolf.TargetLocation) < 2.0); // beside the drover, or the drover's tile if all are blocked
            Assert.True(wolf.AIObject.CheckHerding()); // within the engine's walk
        }
        finally
        {
            Done(pm, wolf);
        }
    }

    [Fact]
    public void LureRefusesTheCornerOfTheSquareForNothing()
    {
        ShardTestClock.Arm();
        var pm = Reader(200.0);
        var wolf = WildAt<GreyWolf>(14, 14); // inside the target's square, 19.8 tiles as the engine walks
        try
        {
            Assert.Equal(0, Paid(pm, DroverSettings.LureId, wolf));
            Assert.Null(wolf.TargetLocation);
            Assert.Null(pm.Spell);
        }
        finally
        {
            Done(pm, wolf);
        }
    }

    // ---------------------------------------------------------------- 10

    [Theory]
    [InlineData(typeof(CalmScroll), 500)]
    [InlineData(typeof(HerdScroll), 1000)]
    [InlineData(typeof(LureScroll), 1500)]
    public void AnAnimalTrainerSellsTheScrolls(Type type, int price)
    {
        var row = new SBAnimalTrainer().BuyInfo.Single(i => i.Type == type);
        Assert.Equal(price, row.Price);

        var made = (Item)row.GetEntity();
        try
        {
            Assert.IsAssignableFrom<SpellScroll>(made);
        }
        finally
        {
            made.Delete();
        }
    }

    [Fact]
    public void TheVendorsBookIsMadeFromItsRow()
    {
        var made = (Item)new SBAnimalTrainer().BuyInfo.Single(i => i.Type == typeof(DroversHandbook)).GetEntity();
        try
        {
            Assert.True(((DroversHandbook)made).HasSpell(DroverSettings.CorralId));
        }
        finally
        {
            made.Delete();
        }
    }

    [Fact]
    public void ScrollsFillTheBookAndAFullBookSavesAndLoads()
    {
        var pm = Drover(100.0);
        var book = new DroversHandbook();
        var scrolls = new SpellScroll[] { new CalmScroll(), new HerdScroll(), new LureScroll() };
        try
        {
            pm.Backpack!.DropItem(book);

            foreach (var scroll in scrolls)
            {
                pm.Backpack.DropItem(scroll);
                Assert.True(book.OnDragDrop(pm, scroll));
            }

            Assert.All(scrolls, s => Assert.True(s.Deleted));
            Assert.Equal(4, book.SpellCount);

            var copy = RoundTrip(book);
            Assert.Equal(4, copy.SpellCount);

            foreach (var id in new[] { DroverSettings.CorralId, DroverSettings.CalmId, DroverSettings.HerdId, DroverSettings.LureId })
            {
                Assert.True(copy.HasSpell(id));
            }

            // Another school's scroll does not go in.
            var magery = new ClumsyScroll();
            pm.Backpack.DropItem(magery);
            Assert.False(book.OnDragDrop(pm, magery));
            magery.Delete();
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void AScrollSavesAndLoads()
    {
        var scroll = new CalmScroll(3);
        try
        {
            var copy = RoundTrip(scroll);
            Assert.Equal(DroverSettings.CalmId, copy.SpellID);
            Assert.Equal(3, copy.Amount);
            Assert.Equal(DroverSettings.ScrollHue, copy.Hue);
        }
        finally
        {
            scroll.Delete();
        }
    }
}
