// ArmsLoreMattersVerification.cs
//
// cc-P226, F-26 parts 1 to 4 and 6: customizations ClusterFArmsLore, the five server/patches it is called from
// (BaseWeapon-arms-lore, BaseArmor-arms-lore, BaseShield-arms-lore-wear, ArmsLore-field-maintenance,
// Repair-arms-lore-weaken) and the Arms Lore row and crafted roll in ClusterFSkillGain. Every fact drives a real pinned
// call site: OnCraft, OnHit, the Arms Lore target, a smith's repair at an anvil and forge. Notes: shard-migration
// notes/cc-P226-arms-lore-matters.md.
//
//   A1. An exceptional weapon's damage increase is 35 at Arms Lore 0, 42 at 100, 50 at 200 (pinned 35, 40, 45).
//   A2. Exceptional armor's extra resist points stop at 10: Arms Lore 240 (200 and a +40 bonus) adds 10, not 12.
//   B1. Durability lost over a fixed number of hits, at Arms Lore 0, 100 and 200: about 100%, 75%, 50% for a weapon
//       (wielder), body armor and a shield (wearer).
//   C1. Field maintenance restores 2 + Arms Lore / 10, never past the maximum, only on your own gear, once per 10 s.
//   C2. Its chance to lose 1 maximum is 0 at 200 and about 25% at 100.
//   C3. A smith's repair still restores more: to full, where field maintenance at 200 restores 22.
//   D1. A smith's repair loses maximum durability less often with Arms Lore (31% at 0, 11% at 200, Blacksmithy 100).
//   E1. At Arms Lore 150, inspecting a Valorite exceptional magic weapon gains; a plain iron one does not.
//   E2. At 150, crafting an exceptional Valorite weapon gains, on the new item's tier.

using System;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ArmsLoreMattersVerification
{
    private readonly ITestOutputHelper _out;

    public ArmsLoreMattersVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsureCraftSystems();
    }

    private sealed class ConstantRandom : System.Random
    {
        public double Value;

        protected override double Sample() => Value;
        public override double NextDouble() => Value;
        public override int Next() => (int)(Value * int.MaxValue);
        public override int Next(int maxValue) => (int)(Value * maxValue);
        public override int Next(int minValue, int maxValue) => minValue + (int)(Value * (maxValue - minValue));
        public override long NextInt64(long maxValue) => (long)(Value * maxValue);
    }

    // The shard's skill hook over pinned's handlers (as SkillGainPastStockCeilingVerification installs it), stock rate.
    private sealed class Hook : IDisposable
    {
        private readonly SkillCheckLocationHandler _location = Mobile.SkillCheckLocationHandler;
        private readonly SkillCheckDirectLocationHandler _directLocation = Mobile.SkillCheckDirectLocationHandler;
        private readonly SkillCheckTargetHandler _target = Mobile.SkillCheckTargetHandler;
        private readonly SkillCheckDirectTargetHandler _directTarget = Mobile.SkillCheckDirectTargetHandler;
        private readonly bool _enabled = ClusterFSkillGain.Enabled;
        private readonly int _attempts = ClusterFSkillGain.ChanceAttempts;
        private readonly int _amount = ClusterFSkillGain.AmountMultiplier;

        public Hook()
        {
            Mobile.SkillCheckLocationHandler = SkillCheck.Mobile_SkillCheckLocation;
            Mobile.SkillCheckDirectLocationHandler = SkillCheck.Mobile_SkillCheckDirectLocation;
            Mobile.SkillCheckTargetHandler = SkillCheck.Mobile_SkillCheckTarget;
            Mobile.SkillCheckDirectTargetHandler = SkillCheck.Mobile_SkillCheckDirectTarget;
            ClusterFSkillGain.Initialize();
            ClusterFSkillGain.Enabled = false;
            ClusterFSkillGain.ChanceAttempts = 1;
            ClusterFSkillGain.AmountMultiplier = 1;
        }

        public void Dispose()
        {
            BuiltInRng.Reset();
            Mobile.SkillCheckLocationHandler = _location;
            Mobile.SkillCheckDirectLocationHandler = _directLocation;
            Mobile.SkillCheckTargetHandler = _target;
            Mobile.SkillCheckDirectTargetHandler = _directTarget;
            ClusterFSkillGain.Enabled = _enabled;
            ClusterFSkillGain.ChanceAttempts = _attempts;
            ClusterFSkillGain.AmountMultiplier = _amount;
        }
    }

    private static int _slot;

    private static PlayerMobile Player(double armsLore, SkillLock lockArmsLore = SkillLock.Locked)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Elf }; // no Human +20 racial floor
        pm.AddItem(new Backpack());
        pm.RawStr = pm.RawDex = pm.RawInt = 100;
        pm.Skills.Cap = 58 * 2000;
        pm.MoveToWorld(new Point3D(1500 + _slot++ % 50 * 4, 1500, 0), Map.Trammel);

        var lore = pm.Skills.ArmsLore;
        lore.Cap = 200.0;
        lore.Base = armsLore;
        lore.SetLockNoRelay(lockArmsLore);
        return pm;
    }

    private static void Clean(params IEntity[] things)
    {
        foreach (var thing in things)
        {
            if (thing is Mobile m)
            {
                foreach (var item in m.Items.ToList())
                {
                    item.Delete();
                }
            }

            thing?.Delete();
        }
    }

    // Below Base 100 a skill's Value carries a stat offset; with stats at 0 (after equipping) Value is exactly Base.
    private static void NoStatOffset(Mobile m, double armsLore)
    {
        m.RawStr = m.RawDex = m.RawInt = 0;
        Assert.Equal(armsLore, m.Skills.ArmsLore.Value);
    }

    private static void Seeded(int seed) => BuiltInRng.Generator = new System.Random(seed);

    private static void Held(double roll) => BuiltInRng.Generator = new ConstantRandom { Value = roll };

    // ---------------------------------------------------------------- A1, A2 crafting

    [Theory]
    [InlineData(0.0, 35)]
    [InlineData(100.0, 42)]
    [InlineData(200.0, 50)]
    public void ExceptionalWeaponDamageIncreaseRescaledTo200(double armsLore, int expected)
    {
        var pm = Player(armsLore);
        NoStatOffset(pm, armsLore);
        var katana = new Katana();

        try
        {
            Seeded(1);
            katana.OnCraft(2, false, pm, DefBlacksmithy.CraftSystem, typeof(IronIngot), null, null, 0);
            _out.WriteLine($"Arms Lore {armsLore}: damage increase {katana.Attributes.WeaponDamage}");
            Assert.Equal(WeaponQuality.Exceptional, katana.Quality);
            Assert.Equal(expected, katana.Attributes.WeaponDamage);
        }
        finally
        {
            BuiltInRng.Reset();
            Clean(katana, pm);
        }
    }

    [Fact]
    public void ExceptionalArmorResistBonusIsCappedAtTen()
    {
        var pm = Player(200.0);
        pm.AddSkillMod(new DefaultSkillMod(SkillName.ArmsLore, "P226Worn", true, 40.0));
        var plate = new PlateChest();

        try
        {
            Assert.Equal(240.0, pm.Skills.ArmsLore.Value);
            Seeded(2);
            plate.OnCraft(2, false, pm, DefBlacksmithy.CraftSystem, typeof(IronIngot), null, null, 0);

            var total = plate.PhysicalBonus + plate.FireBonus + plate.ColdBonus + plate.PoisonBonus + plate.EnergyBonus;
            _out.WriteLine($"Arms Lore 240: {total} resist points (15 exceptional + the Arms Lore bonus)");
            Assert.Equal(ArmorQuality.Exceptional, plate.Quality);
            Assert.Equal(15 + 10, total);
        }
        finally
        {
            BuiltInRng.Reset();
            pm.RemoveSkillMod("P226Worn");
            Clean(plate, pm);
        }
    }

    // ---------------------------------------------------------------- B1 durability over a fixed number of hits

    private const int Hits = 6000;

    // Durability the item loses over Hits hits, its hit points topped up after each so nothing breaks or caps.
    private static int WeaponWear(double armsLore, int seed)
    {
        var attacker = Player(armsLore);
        var katana = new Katana { MaxHitPoints = 255, HitPoints = 255 };
        attacker.AddItem(katana); // worn; equip requirements are not the point
        NoStatOffset(attacker, armsLore);
        var slime = new Slime { Blessed = true }; // acid: every hit wears the weapon (BaseWeapon.cs:2064-2069)
        slime.MoveToWorld(new Point3D(attacker.X + 1, attacker.Y, 0), Map.Trammel);

        try
        {
            Seeded(seed);
            var lost = 0;

            for (var i = 0; i < Hits; i++)
            {
                katana.OnHit(attacker, slime);
                lost += 255 - katana.HitPoints;
                katana.HitPoints = 255;
                slime.Hits = slime.HitsMax;
            }

            return lost;
        }
        finally
        {
            BuiltInRng.Reset();
            Clean(slime, attacker);
        }
    }

    private static int ArmorWear<T>(double armsLore, int seed) where T : BaseArmor, new()
    {
        var wearer = Player(armsLore);
        var armor = new T();
        wearer.AddItem(armor);
        NoStatOffset(wearer, armsLore);
        armor.MaxHitPoints = 255;
        armor.HitPoints = 255;
        var mace = new Mace(); // bashing: wear is half the absorbed damage, at least 1 (BaseArmor.cs:743)

        try
        {
            Seeded(seed);
            var lost = 0;

            for (var i = 0; i < Hits; i++)
            {
                armor.OnHit(mace, 40);
                lost += 255 - armor.HitPoints;
                armor.HitPoints = 255;
            }

            return lost;
        }
        finally
        {
            BuiltInRng.Reset();
            Clean(mace, wearer);
        }
    }

    [Theory]
    [InlineData("weapon")]
    [InlineData("armor")]
    [InlineData("shield")]
    public void ArmsLoreSavesAQuarterOfTheWearAt100AndHalfAt200(string kind)
    {
        Func<double, int, int> wear = kind switch
        {
            "weapon" => WeaponWear,
            "armor"  => ArmorWear<PlateChest>,
            _        => ArmorWear<HeaterShield>
        };

        var at0 = wear(0.0, 11);
        var at100 = wear(100.0, 12);
        var at200 = wear(200.0, 13);
        var r100 = at100 / (double)at0;
        var r200 = at200 / (double)at0;
        _out.WriteLine($"{kind}, {Hits} hits: lost {at0} at 0, {at100} at 100 ({r100:P1}), {at200} at 200 ({r200:P1})");

        Assert.True(at0 > 500, $"too few wear events at 0 to measure ({at0})");
        Assert.InRange(r100, 0.70, 0.80);
        Assert.InRange(r200, 0.45, 0.55);
    }

    // ---------------------------------------------------------------- C field maintenance, through the Arms Lore use

    private static void UseArmsLoreOn(Mobile from, Item item)
    {
        Server.SkillHandlers.ArmsLore.OnUse(from);
        Assert.NotNull(from.Target);
        from.Target.Invoke(from, item);
    }

    [Theory]
    [InlineData(100.0, 30, 12)]  // 2 + 100 / 10
    [InlineData(200.0, 30, 22)]  // 2 + 200 / 10
    [InlineData(200.0, 5, 5)]    // never past the maximum
    public void FieldMaintenanceRestoresDurabilityUpToTheMaximum(double armsLore, int missing, int restored)
    {
        using var hook = new Hook();
        ShardTestClock.Arm();
        var pm = Player(armsLore);
        var katana = new Katana { MaxHitPoints = 100 };
        katana.HitPoints = 100 - missing;
        pm.Backpack.DropItem(katana);

        try
        {
            Held(0.99); // the inspect passes (Arms Lore 100 and up is "no challenge"); no maximum lost
            UseArmsLoreOn(pm, katana);
            _out.WriteLine($"Arms Lore {armsLore}, {missing} missing: {katana.HitPoints} / {katana.MaxHitPoints}");
            Assert.Equal(100, katana.MaxHitPoints);
            Assert.Equal(100 - missing + restored, katana.HitPoints);
        }
        finally
        {
            Clean(pm);
        }
    }

    [Fact]
    public void FieldMaintenanceIsOwnGearOnlyAndWaitsTenSeconds()
    {
        using var hook = new Hook();
        ShardTestClock.Arm();
        var pm = Player(100.0);
        var other = Player(0.0);
        var worn = new PlateChest();
        pm.AddItem(worn);
        worn.MaxHitPoints = 100;
        worn.HitPoints = 50;
        var theirs = new Katana { MaxHitPoints = 100, HitPoints = 50 };
        theirs.MoveToWorld(new Point3D(pm.X + 1, pm.Y, 0), Map.Trammel); // on the ground: not the player's

        try
        {
            Held(0.99);
            UseArmsLoreOn(pm, theirs);
            Assert.Equal(50, theirs.HitPoints);

            UseArmsLoreOn(pm, worn);
            Assert.Equal(62, worn.HitPoints);

            UseArmsLoreOn(pm, worn); // at once: refused
            Assert.Equal(62, worn.HitPoints);

            ShardTestClock.Advance(ClusterFArmsLore.MaintenanceDelay + TimeSpan.FromSeconds(1));
            UseArmsLoreOn(pm, worn);
            _out.WriteLine($"worn plate: 50 -> 62, refused at once, {worn.HitPoints} after the wait");
            Assert.Equal(74, worn.HitPoints);
        }
        finally
        {
            Clean(theirs, other, pm);
        }
    }

    private static int MaintenanceMaxLosses(double armsLore, int uses, int seed)
    {
        using var hook = new Hook();
        ShardTestClock.Arm();
        var pm = Player(armsLore);
        var katana = new Katana { MaxHitPoints = 255, HitPoints = 1 };
        pm.Backpack.DropItem(katana);

        try
        {
            Seeded(seed);
            var losses = 0;

            for (var i = 0; i < uses; i++)
            {
                katana.MaxHitPoints = 255;
                katana.HitPoints = 1;
                pm.EndAction(typeof(ClusterFArmsLore)); // the 10 s wait, skipped
                UseArmsLoreOn(pm, katana);
                losses += 255 - katana.MaxHitPoints;
            }

            return losses;
        }
        finally
        {
            Clean(pm);
        }
    }

    [Fact]
    public void FieldMaintenanceNeverCostsMaximumAt200AndAQuarterOfTheTimeAt100()
    {
        const int uses = 2000;
        var at200 = MaintenanceMaxLosses(200.0, uses, 21);
        var at100 = MaintenanceMaxLosses(100.0, uses, 22);
        _out.WriteLine($"{uses} maintenances: maximum lost {at200} times at 200, {at100} at 100 ({at100 / (double)uses:P1})");
        Assert.Equal(0, at200);
        Assert.InRange(at100 / (double)uses, 0.21, 0.29);
    }

    // ---------------------------------------------------------------- C3, D1 smith repairs

    private sealed class Smithy : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly SmithHammer Hammer = new() { UsesRemaining = 30000 };
        private readonly Item _anvil = new Anvil();
        private readonly Item _forge = new Forge();

        public Smithy(double armsLore)
        {
            Pm = Player(armsLore);
            Pm.Skills.Blacksmith.Cap = 200.0;
            Pm.Skills.Blacksmith.Base = 100.0;
            Pm.Skills.Blacksmith.SetLockNoRelay(SkillLock.Locked);
            Pm.Backpack.DropItem(Hammer);
            NoStatOffset(Pm, armsLore);
            _anvil.MoveToWorld(new Point3D(Pm.X + 1, Pm.Y, Pm.Z), Map.Trammel);
            _forge.MoveToWorld(new Point3D(Pm.X - 1, Pm.Y, Pm.Z), Map.Trammel);
        }

        public void Repair(Item item)
        {
            Server.Engines.Craft.Repair.Do(Pm, DefBlacksmithy.CraftSystem, Hammer);
            Assert.NotNull(Pm.Target);
            Pm.Target.Invoke(Pm, item);
        }

        public void Dispose()
        {
            BuiltInRng.Reset();
            Clean(_anvil, _forge, Pm);
        }
    }

    // Times a repair of a katana one point short of its 200 maximum lost maximum durability, out of `repairs`.
    private static int RepairWeakens(double armsLore, int repairs, int seed)
    {
        using var hook = new Hook();
        using var smithy = new Smithy(armsLore);
        var katana = new Katana();
        smithy.Pm.Backpack.DropItem(katana);
        Seeded(seed);
        var weakened = 0;

        for (var i = 0; i < repairs; i++)
        {
            katana.MaxHitPoints = 200;
            katana.HitPoints = 199;
            smithy.Repair(katana);
            weakened += katana.MaxHitPoints < 200 ? 1 : 0;
        }

        return weakened;
    }

    [Fact]
    public void SmithRepairLosesMaximumLessOftenWithArmsLore()
    {
        const int repairs = 3000;
        var at0 = RepairWeakens(0.0, repairs, 31) / (double)repairs;
        var at100 = RepairWeakens(100.0, repairs, 32) / (double)repairs;
        var at200 = RepairWeakens(200.0, repairs, 33) / (double)repairs;
        _out.WriteLine($"Blacksmithy 100, 1 point missing: maximum lost {at0:P1} at Arms Lore 0, {at100:P1} at 100, " +
                       $"{at200:P1} at 200 (pinned's chance 31%)");

        Assert.InRange(at0, 0.27, 0.35);   // 40 + 1 - 100 / 10
        Assert.InRange(at100, 0.17, 0.25); // less 10
        Assert.InRange(at200, 0.07, 0.15); // less 20
    }

    [Fact]
    public void SmithRepairStillRestoresMoreThanFieldMaintenance()
    {
        using var hook = new Hook();
        ShardTestClock.Arm();
        using var smithy = new Smithy(200.0);
        var field = new Katana { MaxHitPoints = 100, HitPoints = 40 };
        var forged = new Katana { MaxHitPoints = 100, HitPoints = 40 };
        smithy.Pm.Backpack.DropItem(field);
        smithy.Pm.Backpack.DropItem(forged);

        Held(0.99); // the repair passes (no weaken: 99 is not below the chance), the inspect passes
        UseArmsLoreOn(smithy.Pm, field);
        smithy.Repair(forged);
        _out.WriteLine($"60 missing at Arms Lore 200: field maintenance {field.HitPoints}, smith repair {forged.HitPoints}");
        Assert.Equal(62, field.HitPoints);
        Assert.Equal(forged.MaxHitPoints, forged.HitPoints);
        Assert.True(forged.HitPoints > field.HitPoints);
    }

    // ---------------------------------------------------------------- E gain past 100 on the item's tier

    private static Katana ValoriteExceptionalMagic()
    {
        var katana = new Katana { Resource = CraftResource.Valorite, Quality = WeaponQuality.Exceptional };
        katana.Attributes.WeaponSpeed = 10;
        return katana;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InspectAt150GainsOnlyOnGearOfAHighEnoughTier(bool highTier)
    {
        using var hook = new Hook();
        ShardTestClock.Arm();
        var pm = Player(150.0, SkillLock.Up);
        var katana = highTier ? ValoriteExceptionalMagic() : new Katana();
        pm.Backpack.DropItem(katana);

        try
        {
            var (min, max) = ClusterFArmsLore.Window(katana);
            Held(0.0); // every gain roll passes
            UseArmsLoreOn(pm, katana);
            _out.WriteLine($"{(highTier ? "Valorite exceptional magic" : "plain iron")} (window {min} to {max}): " +
                           $"Arms Lore 150 -> {pm.Skills.ArmsLore.Base}");

            if (highTier)
            {
                Assert.Equal(149.0, ClusterFArmsLore.Difficulty(katana));
                Assert.True(pm.Skills.ArmsLore.Base > 150.0);
            }
            else
            {
                Assert.Equal(150.0, pm.Skills.ArmsLore.Base);
            }
        }
        finally
        {
            Clean(pm);
        }
    }

    [Fact]
    public void CraftingAnExceptionalValoriteWeaponAt150Gains()
    {
        using var hook = new Hook();
        var pm = Player(150.0, SkillLock.Up);
        var katana = new Katana();

        try
        {
            Held(0.0);
            katana.OnCraft(2, false, pm, DefBlacksmithy.CraftSystem, typeof(ValoriteIngot), null, null, 0);
            _out.WriteLine($"exceptional {katana.Resource} katana (tier {ClusterFArmsLore.Difficulty(katana)}): " +
                           $"Arms Lore 150 -> {pm.Skills.ArmsLore.Base}");
            Assert.Equal(CraftResource.Valorite, katana.Resource);
            Assert.Equal(99.0 + 25.0, ClusterFArmsLore.Difficulty(katana)); // exceptional's 35% is not magic
            Assert.True(pm.Skills.ArmsLore.Base > 150.0);
        }
        finally
        {
            Clean(katana, pm);
        }
    }
}
