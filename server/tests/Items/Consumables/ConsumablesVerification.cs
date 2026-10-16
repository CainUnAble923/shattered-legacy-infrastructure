// cc-P128: the consumables port (ServUO Items/Consumables -> customizations/Items/Consumables/), its four hook patches
// (BasePotion-sa-potion-effects, AOS-grapes-of-wrath, BaseCreature-tasty-treat-damage, PlayerMobile-urali-mana,
// Spell-urali-trance) and the regen line in our RegenRates.cs.
//
// Facts:
//   1. The nine SA/TOL potion effects are appended after pinned's last value, and each potion names its own label.
//   2. The fifteen begging rewards are the stock foods, drinks and shirt with "Acquired by begging" on them.
//   3. A fruit bowl fills 20 and gives +5 to every stat for two minutes.
//   4. The grapes of wrath: +35 weapon and +15 spell damage through AosAttributes for 20 seconds, then a two-minute
//      cooldown; a second bunch is refused meanwhile. The enchanted apple waits 30 seconds plus the curses removed.
//   5. A Thanksgiving platter hands out its pieces one double-click at a time and goes when the last is taken; the pie
//      changes its art after the first slice.
//   6. A tasty treat feeds only your own pet: stats by Bonus, loyalty to the maximum, refused until Duration + CoolDown
//      has passed; the irresistibly tasty treat adds a tenth to the pet's melee damage.
//   7. The Eodon potions: Sakkhra and Barako resistances (Barako's cold mod, ServUO's bug fixed), Barrab's regen bonus
//      for ten of its twenty minutes, Urali's max mana and mana ticks, a second of the same kind refused, all gone on death.
//   8. A shatter potion destroys a fifth of twenty or more potions and nothing of fewer; fear essence paralyzes those
//      who fail to resist and delays the thrower a minute.
//   9. An elixir of rebirth revives a dead bonded pet; poppies dust identifies a seed and merges charges; the skin
//      tincture applies a clipped hue; a nutcracker unwraps into its chocolate.
//  10. The chocolate, the nutcracker and the dust survive a serialization round trip.
//  11. The five pinned files carry their patched lines in the build tree.
//
// Host notes: timers through ShardTestClock (Arm before anything that schedules, Advance to fire); every fact deletes
// what it made.

using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Engines.Plants;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ConsumablesVerification
{
    private readonly ITestOutputHelper _out;

    public ConsumablesVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsurePoisons();
    }

    private static readonly Point3D Spot = new(1400, 1720, 0);

    private sealed class Player : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly List<Mobile> Others = new();

        public Player(Map map = null)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human, RawStr = 100, RawDex = 100, RawInt = 100 };
            Pm.AddItem(new Backpack());
            Pm.MoveToWorld(Spot, map ?? Map.Trammel);
            Pm.Hits = Pm.HitsMax;
            Pm.Mana = Pm.ManaMax;
        }

        public T Pack<T>(T item) where T : Item
        {
            Pm.Backpack.DropItem(item);
            return item;
        }

        public BaseCreature Pet(int rawStr = 100)
        {
            var bc = new Skeleton { RawStr = rawStr, RawDex = 50, RawInt = 50 };
            bc.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, 0), Pm.Map);
            bc.Controlled = true;
            bc.ControlMaster = Pm;
            Others.Add(bc);
            return bc;
        }

        public BaseCreature Other(Map map = null)
        {
            var bc = new Skeleton();
            bc.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, 0), map ?? Pm.Map);
            Others.Add(bc);
            return bc;
        }

        public void Dispose()
        {
            foreach (var item in new List<Item>(Pm.Backpack.Items))
            {
                item.Delete();
            }

            foreach (var m in Others)
            {
                m.Corpse?.Delete();
                m.Delete();
            }

            EodonianPotion.OnPlayerDeath(Pm);
            Pm.Delete();
        }
    }

    [Fact]
    public void TheSaPotionEffectsAreAppendedAfterPinnedsLast()
    {
        Assert.Equal((int)PotionEffect.FlintsPungentBrew + 1, (int)PotionEffect.ExplodingTarPotion);
        Assert.Equal((int)PotionEffect.ExplodingTarPotion + 1, (int)PotionEffect.Barrab);
        Assert.Equal((int)PotionEffect.Barrab + 5, (int)PotionEffect.Sakkhra);
        Assert.Equal((int)PotionEffect.Sakkhra + 1, (int)PotionEffect.Shatter);
        Assert.Equal((int)PotionEffect.Shatter + 1, (int)PotionEffect.FearEssence);

        var fear = new FearEssence();
        var shatter = new ShatterPotion();
        var tar = new ExplodingTarPotion();
        var urali = new UraliTranceTonic();

        try
        {
            Assert.Equal(PotionEffect.FearEssence, fear.PotionEffect);
            Assert.Equal(1115744, fear.LabelNumber);
            Assert.Equal(1115759, shatter.LabelNumber);
            Assert.Equal(1095147, tar.LabelNumber);
            Assert.Equal(20, tar.Radius);
            Assert.Equal(1156734, urali.LabelNumber);
            Assert.False(fear.RequireFreeHand);
            Assert.True(fear.IsThrowablePotion);
        }
        finally
        {
            fear.Delete();
            shatter.Delete();
            tar.Delete();
            urali.Delete();
        }
    }

    [Fact]
    public void TheBeggingRewardsAreTheStockItemsMarkedAsBegged()
    {
        var items = new Item[]
        {
            new BegCheeseWedge(), new BegCookies(), new BegDates(), new BegFishSteak(), new BegFrenchBread(), new BegPizza(),
            new BegSake(), new BegShirt(), new BegStew(), new BegTurnip(), new BegWine(), new BegFlowerGarland(),
            new BegLiquorPitcher(), new BegWaterPitcher(), new BegWinePitcher()
        };

        try
        {
            Assert.Equal(15, items.Length);
            Assert.IsAssignableFrom<Cookies>(items[1]);
            Assert.IsAssignableFrom<CheesePizza>(items[5]);
            Assert.IsAssignableFrom<Shirt>(items[7]);
            Assert.IsAssignableFrom<WoodenBowlOfStew>(items[8]);
            Assert.Equal(9442, items[6].ItemID);
            Assert.Equal(BeverageType.Wine, ((BeverageBottle)items[10]).Content);
            Assert.Equal(BeverageType.Liquor, ((Pitcher)items[12]).Content);
            Assert.Contains(items[14].ItemID, new[] { 8091, 8092 });
            Assert.Contains(items[11].ItemID, new[] { 8965, 8966 });
        }
        finally
        {
            foreach (var item in items)
            {
                item.Delete();
            }
        }
    }

    [Fact]
    public void AFruitBowlFillsTwentyAndRaisesEveryStat()
    {
        using var p = new Player();
        var bowl = p.Pack(new FruitBowl());
        p.Pm.Hunger = 0;

        Assert.Equal(20, bowl.FillFactor);
        Assert.True(bowl.Eat(p.Pm));

        Assert.Equal(20, p.Pm.Hunger);
        Assert.Equal(105, p.Pm.Str);
        Assert.Equal(105, p.Pm.Dex);
        Assert.Equal(105, p.Pm.Int);
        Assert.True(bowl.Deleted);

        ShardTestClock.Advance(TimeSpan.FromSeconds(121));
        p.Pm.CheckStatTimers();
        Assert.Equal(100, p.Pm.Str);
    }

    [Fact]
    public void TheGrapesOfWrathBoostDamageThenCoolDown()
    {
        using var p = new Player();
        var grapes = p.Pack(new GrapesOfWrath());
        var more = p.Pack(new GrapesOfWrath());

        Assert.Equal(0, AosAttributes.GetValue(p.Pm, AosAttribute.WeaponDamage));
        Assert.True(grapes.Eat(p.Pm));
        Assert.True(grapes.Deleted);

        Assert.True(BaseMagicalFood.IsUnderInfluence(p.Pm, MagicalFood.GrapesOfWrath));
        Assert.Equal(35, AosAttributes.GetValue(p.Pm, AosAttribute.WeaponDamage));
        Assert.Equal(15, AosAttributes.GetValue(p.Pm, AosAttribute.SpellDamage));

        Assert.False(more.Eat(p.Pm)); // still under the influence
        Assert.False(more.Deleted);

        ShardTestClock.Advance(TimeSpan.FromSeconds(21));
        Assert.False(BaseMagicalFood.IsUnderInfluence(p.Pm, MagicalFood.GrapesOfWrath));
        Assert.True(BaseMagicalFood.CoolingDown(p.Pm, MagicalFood.GrapesOfWrath));
        Assert.Equal(0, AosAttributes.GetValue(p.Pm, AosAttribute.WeaponDamage));
        Assert.False(more.Eat(p.Pm)); // cooling down

        ShardTestClock.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));
        Assert.False(BaseMagicalFood.CoolingDown(p.Pm, MagicalFood.GrapesOfWrath));
        Assert.True(more.Eat(p.Pm));
        Assert.True(more.Deleted);
    }

    [Fact]
    public void AnEnchantedAppleWaitsThirtySecondsBetweenBites()
    {
        using var p = new Player();
        var apple = p.Pack(new EnchantedApple());
        var second = p.Pack(new EnchantedApple());

        Assert.True(apple.Eat(p.Pm));
        Assert.True(apple.Deleted);
        ShardTestClock.Advance(TimeSpan.FromMilliseconds(100)); // the zero-length influence ends on the next tick
        _out.WriteLine($"after a bite: influence={BaseMagicalFood.IsUnderInfluence(p.Pm, MagicalFood.EnchantedApple)} cooling={BaseMagicalFood.CoolingDown(p.Pm, MagicalFood.EnchantedApple)}");
        Assert.False(BaseMagicalFood.IsUnderInfluence(p.Pm, MagicalFood.EnchantedApple));
        Assert.True(BaseMagicalFood.CoolingDown(p.Pm, MagicalFood.EnchantedApple));
        Assert.False(second.Eat(p.Pm));

        ShardTestClock.Advance(TimeSpan.FromSeconds(31)); // no curses removed: 30 + 0
        _out.WriteLine($"after 31 s: influence={BaseMagicalFood.IsUnderInfluence(p.Pm, MagicalFood.EnchantedApple)} cooling={BaseMagicalFood.CoolingDown(p.Pm, MagicalFood.EnchantedApple)}");
        Assert.False(BaseMagicalFood.IsUnderInfluence(p.Pm, MagicalFood.EnchantedApple));
        Assert.False(BaseMagicalFood.CoolingDown(p.Pm, MagicalFood.EnchantedApple));
        Assert.True(second.Eat(p.Pm));
    }

    [Fact]
    public void AThanksgivingPlatterHandsOutItsPiecesThenGoes()
    {
        using var p = new Player();
        var platter = p.Pack(new TurkeyPlatter());
        var pie = p.Pack(new SweetPotatoPie());

        Assert.Equal(8, platter.PiecesLeft);
        Assert.Equal(8, platter.FillFactor);
        Assert.Equal(19469, pie.ItemID);

        for (var i = 0; i < 8; i++)
        {
            platter.OnDoubleClick(p.Pm);
        }

        Assert.True(platter.Deleted);

        var pieces = 0;
        foreach (var food in p.Pm.Backpack.FindItemsByType<Food>())
        {
            if (food is TurkeyDinner or RoastDuck or RoastTurkey or RoastChicken or TurkeyLeg or GibletGravey)
            {
                pieces += food.Amount; // the pieces are stackable foods, so same kinds merge
            }
        }

        Assert.Equal(8, pieces);

        pie.OnDoubleClick(p.Pm);
        Assert.Equal(5, pie.PiecesLeft);
        Assert.Equal(19458, pie.ItemID);
        Assert.NotNull(p.Pm.Backpack.FindItemByType<SliceOfPie>());
    }

    [Fact]
    public void ATastyTreatFeedsOnlyYourOwnPet()
    {
        using var p = new Player();
        var pet = p.Pet();
        var stray = p.Other();
        var treat = p.Pack(new TastyTreat());
        var second = p.Pack(new TastyTreat());
        var big = p.Pack(new IrresistiblyTastyTreat());

        pet.Loyalty = 10;

        treat.TryFeed(p.Pm, stray);
        Assert.False(treat.Deleted); // not yours

        treat.TryFeed(p.Pm, pet);
        Assert.True(treat.Deleted);
        Assert.Equal(105, pet.Str); // 100 + 5%
        Assert.Equal(BaseCreature.MaxLoyalty, pet.Loyalty);
        Assert.True(TastyTreat.UnderInfluence(pet));
        Assert.Equal(0, TastyTreat.DamageBonusOf(pet));

        second.TryFeed(p.Pm, pet);
        Assert.False(second.Deleted); // still enjoying the last one

        ShardTestClock.Advance(TimeSpan.FromMinutes(22) + TimeSpan.FromSeconds(1)); // 20 on, 2 cooling
        Assert.False(TastyTreat.UnderInfluence(pet));

        big.TryFeed(p.Pm, pet);
        Assert.True(big.Deleted);
        Assert.Equal(115, pet.Str);
        Assert.Equal(10, TastyTreat.DamageBonusOf(pet));

        var damage = 100;
        pet.AlterMeleeDamageTo(p.Pm, ref damage);
        Assert.Equal(110, damage);

        ShardTestClock.Advance(TimeSpan.FromMinutes(140) + TimeSpan.FromSeconds(1)); // 20 on, 120 cooling
        Assert.Equal(0, TastyTreat.DamageBonusOf(pet));
        damage = 100;
        pet.AlterMeleeDamageTo(p.Pm, ref damage);
        Assert.Equal(100, damage);
    }

    [Fact]
    public void TheEodonPotionsApplyRefuseASecondAndWearOff()
    {
        // The host has no RegenRates handler, so pinned's default mana regen would run every tick; hold it still so the
        // trance tonic's own +10 a second can be counted.
        var regen = Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler = _ => TimeSpan.FromHours(1);

        try
        {
            EodonPotionsBody();
        }
        finally
        {
            Mobile.ManaRegenRateHandler = regen;
        }
    }

    private void EodonPotionsBody()
    {
        using var p = new Player();
        var sakkhra = p.Pack(new SakkhraProphylaxisPotion(2));
        var barako = p.Pack(new BarakoDraftOfMight());
        var barrab = p.Pack(new BarrabHemolymphConcentrate());
        var urali = p.Pack(new UraliTranceTonic());

        var poison = p.Pm.PoisonResistance;
        var energy = p.Pm.EnergyResistance;
        var physical = p.Pm.PhysicalResistance;
        var cold = p.Pm.ColdResistance;
        var manaMax = p.Pm.ManaMax;

        sakkhra.Drink(p.Pm);
        Assert.Equal(1, sakkhra.Amount);
        Assert.Equal(poison + 10, p.Pm.PoisonResistance);
        Assert.Equal(energy + 5, p.Pm.EnergyResistance);
        Assert.True(EodonianPotion.IsUnderEffects(p.Pm, PotionEffect.Sakkhra));

        sakkhra.Drink(p.Pm); // You are already under a similar effect.
        Assert.Equal(1, sakkhra.Amount);

        barako.Drink(p.Pm);
        Assert.True(barako.Deleted);
        Assert.Equal(physical + 10, p.Pm.PhysicalResistance);
        Assert.Equal(cold + 5, p.Pm.ColdResistance);

        barrab.Drink(p.Pm);
        Assert.Equal(100, BarrabHemolymphConcentrate.HPRegenBonus(p.Pm));

        urali.Drink(p.Pm);
        Assert.Equal(manaMax + 10, p.Pm.ManaMax);
        Assert.Equal(10, UraliTranceTonic.GetManaBuff(p.Pm));
        p.Pm.Mana = 0;
        Assert.Equal(0, p.Pm.Mana);
        ShardTestClock.Advance(TimeSpan.FromSeconds(1));
        _out.WriteLine($"after 1 s of the trance: {p.Pm.Mana} of {p.Pm.ManaMax}");
        Assert.Equal(10, p.Pm.Mana);
        ShardTestClock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(20, p.Pm.Mana);

        ShardTestClock.Advance(TimeSpan.FromMinutes(10)); // the ten-minute effects are over, the contexts (20) are not
        Assert.Equal(poison, p.Pm.PoisonResistance);
        Assert.Equal(physical, p.Pm.PhysicalResistance);
        Assert.Equal(cold, p.Pm.ColdResistance);
        Assert.Equal(0, BarrabHemolymphConcentrate.HPRegenBonus(p.Pm));
        Assert.True(EodonianPotion.IsUnderEffects(p.Pm, PotionEffect.Barrab));
        Assert.Equal(manaMax, p.Pm.ManaMax);

        ShardTestClock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(2));
        Assert.False(EodonianPotion.IsUnderEffects(p.Pm, PotionEffect.Barrab));
        Assert.False(EodonianPotion.IsUnderEffects(p.Pm, PotionEffect.Sakkhra));

        sakkhra.Drink(p.Pm);
        Assert.True(EodonianPotion.IsUnderEffects(p.Pm, PotionEffect.Sakkhra));
        EodonianPotion.OnPlayerDeath(p.Pm);
        Assert.False(EodonianPotion.IsUnderEffects(p.Pm, PotionEffect.Sakkhra));
    }

    [Fact]
    public void AShatterPotionBreaksAFifthOfTwentyOrMore()
    {
        using var p = new Player(Map.Felucca);
        var victim = p.Other();
        victim.AddItem(new Backpack());
        var stack = new LesserHealPotion { Amount = 25 };
        victim.Backpack.DropItem(stack);

        var potion = p.Pack(new ShatterPotion());
        potion.Explode(p.Pm, victim, victim.Map);

        Assert.True(potion.Deleted);
        Assert.Equal(20, stack.Amount); // a fifth of 25

        var again = p.Pack(new ShatterPotion());
        again.Explode(p.Pm, victim, victim.Map);
        Assert.Equal(16, stack.Amount); // a fifth of exactly 20

        var few = p.Pack(new ShatterPotion());
        few.Explode(p.Pm, victim, victim.Map);
        Assert.True(few.Deleted);
        Assert.Equal(16, stack.Amount); // fewer than 20: nothing happens
    }

    [Fact]
    public void FearEssenceParalyzesThoseWhoFailToResist()
    {
        using var p = new Player(Map.Felucca);
        var victim = p.Other();
        victim.Skills[SkillName.MagicResist].Base = 137.5; // (4 * 137.5 + 150) / 700 = 1: never resisted

        var essence = p.Pack(new FearEssence());
        Assert.Equal(0, FearEssence.GetDelay(p.Pm));

        FearEssence.AddDelay(p.Pm);
        Assert.Equal(60, FearEssence.GetDelay(p.Pm));

        essence.Explode(p.Pm, victim.Location, victim.Map);
        Assert.True(essence.Deleted);
        Assert.True(victim.Paralyzed);

        ShardTestClock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(victim.Paralyzed);

        ShardTestClock.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal(0, FearEssence.GetDelay(p.Pm));
    }

    [Fact]
    public void AnElixirRevivesADeadBondedPet()
    {
        using var p = new Player();
        var pet = p.Pet();
        pet.IsBonded = true;
        var elixir = p.Pack(new ElixirOfRebirth());

        Assert.False(elixir.TryResurrect(p.Pm, pet)); // alive
        Assert.False(elixir.Deleted);

        pet.Kill();
        Assert.True(pet.IsDeadBondedPet);
        pet.Corpse?.Delete();

        Assert.True(elixir.TryResurrect(p.Pm, pet));
        Assert.True(elixir.Deleted);
        Assert.False(pet.IsDeadPet);
        Assert.True(pet.Alive);
    }

    [Fact]
    public void PoppiesDustIdentifiesASeedAndMergesCharges()
    {
        using var p = new Player();
        var dust = p.Pack(new PoppiesDust());
        var other = p.Pack(new PoppiesDust(3));
        var seed = p.Pack(new Seed(PlantType.CampionFlowers, PlantHue.Red));

        Assert.Equal(8, dust.UsesRemaining);
        Assert.False(seed.ShowType);

        dust.Use(p.Pm, seed);
        Assert.True(seed.ShowType);
        Assert.Equal(7, dust.UsesRemaining);

        dust.Use(p.Pm, seed); // already identified: nothing spent
        Assert.Equal(7, dust.UsesRemaining);

        other.Use(p.Pm, dust);
        Assert.True(other.Deleted);
        Assert.Equal(10, dust.UsesRemaining);
    }

    [Fact]
    public void TheSkinTinctureAppliesAClippedHueOnce()
    {
        using var p = new Player();
        var tincture = p.Pack(new SkinTingeingTincture());

        Assert.Equal(57, SkinTingeingTincture.GetHueList(p.Pm).Length);
        Assert.False(tincture.Apply(p.Pm, 0));
        Assert.False(tincture.Deleted);

        Assert.True(tincture.Apply(p.Pm, 1010));
        Assert.Equal(Race.Human.ClipSkinHue(1010) | 0x8000, p.Pm.Hue);
        Assert.True(tincture.Deleted);
    }

    [Fact]
    public void ANutcrackerUnwrapsIntoItsChocolate()
    {
        using var p = new Player();
        var dark = p.Pack(new ChocolateNutcracker());
        var white = p.Pack(new ChocolateNutcracker(ChocolateNutcracker.ChocolateType.White));

        Assert.True(dark.Wrapped);
        Assert.Equal(39952, dark.ItemID);
        Assert.Equal(0, dark.Hue);
        Assert.Equal(1156387, dark.LabelNumber);
        Assert.Equal(1156389, white.LabelNumber);

        dark.OnDoubleClick(p.Pm);
        ShardTestClock.Advance(TimeSpan.FromSeconds(2));

        Assert.False(dark.Wrapped);
        Assert.Equal(39954, dark.ItemID);
        Assert.Equal((int)ChocolateNutcracker.ChocolateType.Dark, dark.Hue);
        Assert.Equal(1156390, dark.LabelNumber);
        Assert.True(white.Wrapped);
    }

    [Fact]
    public void TheConsumablesSurviveARoundTrip()
    {
        ShardTestClock.Arm();
        var chocolate = new ValentineChocolate();
        var nutcracker = new ChocolateNutcracker(ChocolateNutcracker.ChocolateType.White) { Wrapped = false };
        var dust = new PoppiesDust(5);

        try
        {
            var chocolateCopy = RoundTrip(chocolate, s => new ValentineChocolate(s));
            Assert.Equal(chocolate.LabelNumber, chocolateCopy.LabelNumber);
            Assert.Equal(chocolate.Label, chocolateCopy.Label);
            Assert.Equal(chocolate.Hue, chocolateCopy.Hue);

            var nutcrackerCopy = RoundTrip(nutcracker, s => new ChocolateNutcracker(s));
            Assert.Equal(ChocolateNutcracker.ChocolateType.White, nutcrackerCopy.Type);
            Assert.False(nutcrackerCopy.Wrapped);
            Assert.Equal(1156392, nutcrackerCopy.LabelNumber);

            var dustCopy = RoundTrip(dust, s => new PoppiesDust(s));
            Assert.Equal(5, dustCopy.UsesRemaining);

            chocolateCopy.Delete();
            nutcrackerCopy.Delete();
            dustCopy.Delete();
        }
        finally
        {
            chocolate.Delete();
            nutcracker.Delete();
            dust.Delete();
        }
    }

    internal static T RoundTrip<T>(T original, Func<Serial, T> make) where T : Item
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var serial = original.Serial;
        original.Delete();

        var copy = make(serial);
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    [Fact]
    public void TheFivePinnedFilesCarryTheirPatchedLinesInTheBuildTree()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Projects", "UOContent")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, $"no Projects/UOContent above {AppContext.BaseDirectory}");
        var root = Path.Combine(dir.FullName, "Projects", "UOContent");

        var expected = new (string File, string Line)[]
        {
            ("Items/Skill Items/Magical/Potions/BasePotion.cs", "FearEssence"),
            ("Misc/AOS.cs", "BaseMagicalFood.IsUnderInfluence(m, MagicalFood.GrapesOfWrath)"),
            ("Mobiles/BaseCreature.cs", "TastyTreat.DamageBonusOf(this)"),
            ("Mobiles/PlayerMobile.cs", "UraliTranceTonic.GetManaBuff(this)"),
            ("Spells/Base/Spell.cs", "EodonianPotion.IsUnderEffects(Caster, PotionEffect.Urali)")
        };

        foreach (var (file, line) in expected)
        {
            var text = File.ReadAllText(Path.Combine(root, file));
            Assert.True(text.Contains(line, StringComparison.Ordinal), $"{file} lacks '{line}': its cc-P128 patch did not apply");
        }
    }
}
