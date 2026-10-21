// ExceptionalMaterialVerification.cs
//
// cc-P228, F-29: exceptional items get their material's bonuses x1.5 (half up), every material, OSI's included.
// customizations ClusterFExceptionalMaterial, called from pinned's live reads in BaseArmor and BaseWeapon
// (patches BaseArmor-exceptional-material, BaseWeapon-exceptional-material) and around BaseRanged's OSI wood switch
// (patch BaseRanged-exceptional-wood). Notes: shard-migration notes/cc-P228-exceptional-material-gap.md.
//
// Every expected number is written out from pinned's CraftAttributeInfo (ResourceInfo.cs) and BaseRanged's switch,
// independently of the rule's code.
//
//   X1. Armor (ore, shard ore, leather, scales): a normal piece reads pinned's values exactly; an exceptional one reads
//       the material's resists, durability, luck and lower requirements x1.5. Penalties (red scales' -3 cold) stay.
//   X2. Weapons (ore, shard ore): the same for element damage, durability, luck and lower requirements; the split
//       still totals 100.
//   X3. OSI wood bows: normal matches pinned exactly; exceptional gets the wood's bonuses x1.5 (all six Heartwood
//       rolls too).
//   X4. Shard wood: the F-28 table is not multiplied (Ironwood bow and shield, exceptional, as cc-P227 approved).
//   X5. Cloth and leather clothing: no material values in pinned, so exceptional reads the same as normal.
//   X6. Runic: the runic ranges are not scaled; a runic piece's runic properties are the same normal or exceptional.
//       (No runic bow exists to test: the OSI woods' runic ranges are 0 in pinned, so a runic fletcher's tool rolls
//       nothing. X3's exceptional bows hold that what OnCraft set before the wood switch, the 35 damage, is not scaled.)
//   X7. Caps: a player whose resist sits at the cap stays at the cap with the exceptional piece.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ExceptionalMaterialVerification
{
    private readonly ITestOutputHelper _out;

    public ExceptionalMaterialVerification(ITestOutputHelper output)
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

    // Utility.Random(6) returns pick
    private static void HeldPick(int pick) => BuiltInRng.Generator = new ConstantRandom { Value = (pick + 0.5) / 6.0 };

    private static T Armor<T>(CraftResource resource, bool exceptional) where T : BaseArmor, new() =>
        new() { Resource = resource, Quality = exceptional ? ArmorQuality.Exceptional : ArmorQuality.Regular };

    private static string Read(BaseArmor a) =>
        $"Phys={a.PhysicalResistance} Fire={a.FireResistance} Cold={a.ColdResistance} Poison={a.PoisonResistance} " +
        $"Energy={a.EnergyResistance} Durability={a.GetDurabilityBonus()} Luck={a.GetLuckBonus()} LowerReq={a.GetLowerStatReq()}";

    private static string Read(BaseWeapon w)
    {
        w.GetDamageTypes(null, out var phys, out var fire, out var cold, out var pois, out var nrgy, out var chaos, out var direct);
        Assert.Equal(100, phys + fire + cold + pois + nrgy + chaos + direct);
        return $"Phys={phys} Fire={fire} Cold={cold} Poison={pois} Energy={nrgy} " +
               $"Durability={w.GetDurabilityBonus()} Luck={w.GetLuckBonus()} LowerReq={w.GetLowerStatReq()}";
    }

    // ---------------------------------------------------------------- X1

    // PlateChest base 5/3/2/3/2, LeatherChest 2/4/3/3/3, DragonChest 3/3/3/3/3. Exceptional's own +20 durability is
    // pinned's (BaseArmor.GetDurabilityBonus) and not multiplied.
    [Theory]
    // Valorite: 4 phys, 3 cold, 3 poison, 3 energy, durability 50
    [InlineData("PlateChest", CraftResource.Valorite, false, "Phys=9 Fire=3 Cold=5 Poison=6 Energy=5 Durability=50 Luck=0 LowerReq=0")]
    [InlineData("PlateChest", CraftResource.Valorite, true, "Phys=11 Fire=3 Cold=7 Poison=8 Energy=7 Durability=95 Luck=0 LowerReq=0")]
    // Gold: 1/1/2/0/2, luck 40, lower requirements 30
    [InlineData("PlateChest", CraftResource.Gold, false, "Phys=6 Fire=4 Cold=4 Poison=3 Energy=4 Durability=0 Luck=40 LowerReq=30")]
    [InlineData("PlateChest", CraftResource.Gold, true, "Phys=7 Fire=5 Cold=5 Poison=3 Energy=5 Durability=20 Luck=60 LowerReq=45")]
    // Dull Copper: 6 phys, durability 50, lower requirements 20
    [InlineData("PlateChest", CraftResource.DullCopper, false, "Phys=11 Fire=3 Cold=2 Poison=3 Energy=2 Durability=50 Luck=0 LowerReq=20")]
    [InlineData("PlateChest", CraftResource.DullCopper, true, "Phys=14 Fire=3 Cold=2 Poison=3 Energy=2 Durability=95 Luck=0 LowerReq=30")]
    // Celestial (ours): 6/5/5/5/5, luck 60, durability 100
    [InlineData("PlateChest", CraftResource.Celestial, false, "Phys=11 Fire=8 Cold=7 Poison=8 Energy=7 Durability=100 Luck=60 LowerReq=0")]
    [InlineData("PlateChest", CraftResource.Celestial, true, "Phys=14 Fire=11 Cold=10 Poison=11 Energy=10 Durability=170 Luck=90 LowerReq=0")]
    // Spined: 5 phys, luck 40
    [InlineData("LeatherChest", CraftResource.SpinedLeather, false, "Phys=7 Fire=4 Cold=3 Poison=3 Energy=3 Durability=0 Luck=40 LowerReq=0")]
    [InlineData("LeatherChest", CraftResource.SpinedLeather, true, "Phys=10 Fire=4 Cold=3 Poison=3 Energy=3 Durability=20 Luck=60 LowerReq=0")]
    // Burrowhide (ours): 2 phys, luck 20
    [InlineData("LeatherChest", CraftResource.BurrowhideLeather, false, "Phys=4 Fire=4 Cold=3 Poison=3 Energy=3 Durability=0 Luck=20 LowerReq=0")]
    [InlineData("LeatherChest", CraftResource.BurrowhideLeather, true, "Phys=5 Fire=4 Cold=3 Poison=3 Energy=3 Durability=20 Luck=30 LowerReq=0")]
    // Red scales: fire 10, cold -3 (a penalty: not multiplied)
    [InlineData("DragonChest", CraftResource.RedScales, false, "Phys=3 Fire=13 Cold=0 Poison=3 Energy=3 Durability=0 Luck=0 LowerReq=0")]
    [InlineData("DragonChest", CraftResource.RedScales, true, "Phys=3 Fire=18 Cold=0 Poison=3 Energy=3 Durability=20 Luck=0 LowerReq=0")]
    public void X1_ArmorReadsItsMaterialTimesOneAndAHalfWhenExceptional(string item, CraftResource resource, bool exceptional, string expected)
    {
        BaseArmor armor = item switch
        {
            "PlateChest" => Armor<PlateChest>(resource, exceptional),
            "LeatherChest" => Armor<LeatherChest>(resource, exceptional),
            _ => Armor<DragonChest>(resource, exceptional)
        };

        try
        {
            var got = Read(armor);
            _out.WriteLine($"{(exceptional ? "exceptional" : "normal")} {resource} {item}: {got}");
            Assert.Equal(expected, got);
        }
        finally
        {
            armor.Delete();
        }
    }

    // ---------------------------------------------------------------- X2

    [Theory]
    // Valorite: fire 10, cold 20, poison 10, energy 20 out of physical
    [InlineData(CraftResource.Valorite, false, "Phys=40 Fire=10 Cold=20 Poison=10 Energy=20 Durability=0 Luck=0 LowerReq=0")]
    [InlineData(CraftResource.Valorite, true, "Phys=10 Fire=15 Cold=30 Poison=15 Energy=30 Durability=20 Luck=0 LowerReq=0")]
    // Gold: luck 40, lower requirements 50
    [InlineData(CraftResource.Gold, false, "Phys=100 Fire=0 Cold=0 Poison=0 Energy=0 Durability=0 Luck=40 LowerReq=50")]
    [InlineData(CraftResource.Gold, true, "Phys=100 Fire=0 Cold=0 Poison=0 Energy=0 Durability=20 Luck=60 LowerReq=75")]
    // Dull Copper: durability 100, lower requirements 50
    [InlineData(CraftResource.DullCopper, false, "Phys=100 Fire=0 Cold=0 Poison=0 Energy=0 Durability=100 Luck=0 LowerReq=50")]
    [InlineData(CraftResource.DullCopper, true, "Phys=100 Fire=0 Cold=0 Poison=0 Energy=0 Durability=170 Luck=0 LowerReq=75")]
    // Celestial (ours): 15 of each element, luck 60
    [InlineData(CraftResource.Celestial, false, "Phys=40 Fire=15 Cold=15 Poison=15 Energy=15 Durability=0 Luck=60 LowerReq=0")]
    [InlineData(CraftResource.Celestial, true, "Phys=8 Fire=23 Cold=23 Poison=23 Energy=23 Durability=20 Luck=90 LowerReq=0")]
    // Mythril (ours): energy 50, luck 50
    [InlineData(CraftResource.Mythril, false, "Phys=50 Fire=0 Cold=0 Poison=0 Energy=50 Durability=0 Luck=50 LowerReq=0")]
    [InlineData(CraftResource.Mythril, true, "Phys=25 Fire=0 Cold=0 Poison=0 Energy=75 Durability=20 Luck=75 LowerReq=0")]
    public void X2_WeaponsReadTheirMaterialTimesOneAndAHalfWhenExceptional(CraftResource resource, bool exceptional, string expected)
    {
        var sword = new Broadsword
        {
            Resource = resource, Quality = exceptional ? WeaponQuality.Exceptional : WeaponQuality.Regular
        };

        try
        {
            var got = Read(sword);
            _out.WriteLine($"{(exceptional ? "exceptional" : "normal")} {resource} broadsword: {got}");
            Assert.Equal(expected, got);
        }
        finally
        {
            sword.Delete();
        }
    }

    // ---------------------------------------------------------------- crafted wood items

    private static readonly Dictionary<CraftResource, (Type Log, Type Board)> Lumber = new()
    {
        [CraftResource.OakWood] = (typeof(OakLog), typeof(OakBoard)),
        [CraftResource.AshWood] = (typeof(AshLog), typeof(AshBoard)),
        [CraftResource.YewWood] = (typeof(YewLog), typeof(YewBoard)),
        [CraftResource.Heartwood] = (typeof(HeartwoodLog), typeof(HeartwoodBoard)),
        [CraftResource.Bloodwood] = (typeof(BloodwoodLog), typeof(BloodwoodBoard)),
        [CraftResource.Frostwood] = (typeof(FrostwoodLog), typeof(FrostwoodBoard)),
        [CraftResource.Ironwood] = (typeof(IronwoodLog), typeof(IronwoodBoard))
    };

    private static SortedDictionary<string, int> Snapshot(BaseWeapon w) => new()
    {
        ["Damage"] = w.Attributes.WeaponDamage,
        ["HitChance"] = w.Attributes.AttackChance,
        ["DefendChance"] = w.Attributes.DefendChance,
        ["SwingSpeed"] = w.Attributes.WeaponSpeed,
        ["Luck"] = w.Attributes.Luck,
        ["RegenHits"] = w.Attributes.RegenHits,
        ["RegenStam"] = w.Attributes.RegenStam,
        ["RegenMana"] = w.Attributes.RegenMana,
        ["SpellChanneling"] = w.Attributes.SpellChanneling,
        ["CastSpeed"] = w.Attributes.CastSpeed,
        ["Durability"] = w.WeaponAttributes.DurabilityBonus,
        ["LowerStatReq"] = w.WeaponAttributes.LowerStatReq,
        ["HitLeechHits"] = w.WeaponAttributes.HitLeechHits,
        ["HitLeechMana"] = w.WeaponAttributes.HitLeechMana,
        ["HitLeechStam"] = w.WeaponAttributes.HitLeechStam,
        ["HitLowerAttack"] = w.WeaponAttributes.HitLowerAttack,
        ["HitLowerDefend"] = w.WeaponAttributes.HitLowerDefend,
        ["HitFireArea"] = w.WeaponAttributes.HitFireArea,
        ["ResistPhysical"] = w.WeaponAttributes.ResistPhysicalBonus,
        ["SelfRepair"] = w.WeaponAttributes.SelfRepair,
        ["Physical"] = w.AosElementDamages.Physical,
        ["Fire"] = w.AosElementDamages.Fire,
        ["Cold"] = w.AosElementDamages.Cold,
        ["Poison"] = w.AosElementDamages.Poison,
        ["Energy"] = w.AosElementDamages.Energy
    };

    private static SortedDictionary<string, int> Snapshot(BaseArmor a) => new()
    {
        ["DefendChance"] = a.Attributes.DefendChance,
        ["Luck"] = a.Attributes.Luck,
        ["RegenHits"] = a.Attributes.RegenHits,
        ["RegenMana"] = a.Attributes.RegenMana,
        ["ReflectPhysical"] = a.Attributes.ReflectPhysical,
        ["BonusHits"] = a.Attributes.BonusHits,
        ["NightSight"] = a.Attributes.NightSight,
        ["Durability"] = a.ArmorAttributes.DurabilityBonus,
        ["LowerStatReq"] = a.ArmorAttributes.LowerStatReq,
        ["MageArmor"] = a.ArmorAttributes.MageArmor,
        ["SelfRepair"] = a.ArmorAttributes.SelfRepair,
        ["PhysicalResist"] = a.PhysicalBonus,
        ["FireResist"] = a.FireBonus,
        ["ColdResist"] = a.ColdBonus,
        ["PoisonResist"] = a.PoisonBonus,
        ["EnergyResist"] = a.EnergyBonus
    };

    private static string Show(SortedDictionary<string, int> d) =>
        string.Join(" ", d.Where(p => p.Value != 0).Select(p => $"{p.Key}={p.Value}"));

    private static int _slot;

    private static PlayerMobile Crafter()
    {
        // Arms Lore 0: pinned's exceptional damage increase is exactly 35 (ArmsLoreMattersVerification A1)
        var pm = new PlayerMobile { Player = true, Race = Race.Elf }; // not Human: Jack of All Trades counts Arms Lore as 20
        pm.AddItem(new Backpack());
        pm.RawStr = pm.RawDex = pm.RawInt = 100;
        pm.MoveToWorld(new Point3D(1700 + _slot++ % 50 * 4, 1700, 0), Map.Trammel);
        pm.Skills.ArmsLore.Base = 0;
        pm.Skills.ArmsLore.SetLockNoRelay(SkillLock.Locked);
        return pm;
    }

    // As the craft menus do: Fletching works logs into bows, Carpentry boards into shields. pick holds every random roll to the same value (Heartwood's Utility.Random(6) returns pick).
    private static T Craft<T>(PlayerMobile pm, CraftResource wood, bool exceptional, int pick = 0) where T : Item, new()
    {
        var item = new T();
        var bow = item is BaseRanged;
        var typeRes = bow ? Lumber[wood].Log : Lumber[wood].Board;
        CraftSystem system = bow ? DefBowFletching.CraftSystem : DefCarpentry.CraftSystem;

        HeldPick(pick);
        try
        {
            ((ICraftable)item).OnCraft(exceptional ? 2 : 1, false, pm, system, typeRes, null, null, 0);
        }
        finally
        {
            BuiltInRng.Reset();
        }

        return item;
    }

    // ---------------------------------------------------------------- X3

    [Theory]
    // pinned BaseRanged.cs:215-258, any quality; exceptional also has pinned's own 35 damage increase (Arms Lore 0)
    [InlineData(CraftResource.OakWood, false, "Damage=5 Luck=40")]
    [InlineData(CraftResource.OakWood, true, "Damage=43 Luck=60")]
    [InlineData(CraftResource.AshWood, false, "LowerStatReq=20 SwingSpeed=10")]
    [InlineData(CraftResource.AshWood, true, "Damage=35 LowerStatReq=30 SwingSpeed=15")]
    [InlineData(CraftResource.YewWood, false, "Damage=10 HitChance=5")]
    [InlineData(CraftResource.YewWood, true, "Damage=50 HitChance=8")]
    [InlineData(CraftResource.Bloodwood, false, "HitLeechHits=16 RegenHits=2")]
    [InlineData(CraftResource.Bloodwood, true, "Damage=35 HitLeechHits=24 RegenHits=3")]
    // Frostwood's 40 cold is its element share: x1.5 is 60, and physical keeps the rest (the split totals 100)
    [InlineData(CraftResource.Frostwood, false, "Cold=40 Damage=12 Physical=60")]
    [InlineData(CraftResource.Frostwood, true, "Cold=60 Damage=53 Physical=40")]
    public void X3_OsiWoodBowsGetTheirWoodTimesOneAndAHalfWhenExceptional(CraftResource wood, bool exceptional, string expected)
    {
        var pm = Crafter();
        var bow = Craft<Bow>(pm, wood, exceptional);
        try
        {
            var got = Show(Snapshot(bow));
            _out.WriteLine($"{(exceptional ? "exceptional" : "normal")} {wood} bow: {got}");
            Assert.Equal(expected, got);
        }
        finally
        {
            bow.Delete();
            pm.Delete();
        }
    }

    [Theory]
    // pinned BaseRanged.cs:260-295, one Utility.Random(6): luck 40, durability 50, LSR 20, speed 10, hit chance 5, leech 10
    [InlineData(0, "Luck=40", "Damage=35 Luck=60")]
    [InlineData(1, "Durability=50", "Damage=35 Durability=75")]
    [InlineData(2, "LowerStatReq=20", "Damage=35 LowerStatReq=30")]
    [InlineData(3, "SwingSpeed=10", "Damage=35 SwingSpeed=15")]
    [InlineData(4, "HitChance=5", "Damage=35 HitChance=8")]
    [InlineData(5, "HitLeechHits=10", "Damage=35 HitLeechHits=15")]
    public void X3_HeartwoodBowsEveryRoll(int pick, string normal, string exceptional)
    {
        var pm = Crafter();
        var a = Craft<Bow>(pm, CraftResource.Heartwood, false, pick);
        var b = Craft<Bow>(pm, CraftResource.Heartwood, true, pick);
        try
        {
            _out.WriteLine($"roll {pick}: normal {Show(Snapshot(a))}, exceptional {Show(Snapshot(b))}");
            Assert.Equal(normal, Show(Snapshot(a)));
            Assert.Equal(exceptional, Show(Snapshot(b)));
        }
        finally
        {
            a.Delete();
            b.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- X4

    [Fact]
    public void X4_ShardWoodTableIsNotMultiplied()
    {
        var pm = Crafter();
        var bow = Craft<Bow>(pm, CraftResource.Ironwood, true);
        var shield = Craft<WoodenShield>(pm, CraftResource.Ironwood, true);
        var plainShield = Craft<WoodenShield>(pm, CraftResource.Ironwood, false);
        try
        {
            // F-28 Ironwood, exceptional = normal + adds, as cc-P227 (ExtendedWoodBonusesVerification W1, W2)
            _out.WriteLine($"Ironwood bow {Show(Snapshot(bow))}; shield {Show(Snapshot(shield))}");
            Assert.Equal("Damage=49 Durability=50 LowerStatReq=20", Show(Snapshot(bow)));
            Assert.Equal("Durability=50 LowerStatReq=20 PhysicalResist=3", Show(Snapshot(shield)));

            // and the shard woods have no CraftAttributeInfo values, so the live read adds nothing either
            Assert.Equal(plainShield.PhysicalResistance + 3, shield.PhysicalResistance);
            Assert.Equal(plainShield.FireResistance, shield.FireResistance);
            Assert.Equal(0, shield.GetLuckBonus());
            Assert.Equal(0, bow.GetLuckBonus());
        }
        finally
        {
            bow.Delete();
            shield.Delete();
            plainShield.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- X5

    [Theory]
    [InlineData(CraftResource.None)]
    [InlineData(CraftResource.SpinedLeather)]
    [InlineData(CraftResource.BarbedLeather)]
    public void X5_ClothingHasNoMaterialShareToScale(CraftResource resource)
    {
        // cloth is not a CraftResource in pinned, and BaseClothing reads no CraftAttributeInfo (leather shoes included)
        Item[] pair =
        [
            new Robe { Resource = resource, Quality = ClothingQuality.Regular },
            new Robe { Resource = resource, Quality = ClothingQuality.Exceptional },
            new Boots { Resource = resource, Quality = ClothingQuality.Regular },
            new Boots { Resource = resource, Quality = ClothingQuality.Exceptional }
        ];

        try
        {
            static string R(Item i) =>
                $"{i.PhysicalResistance}/{i.FireResistance}/{i.ColdResistance}/{i.PoisonResistance}/{i.EnergyResistance}";

            for (var i = 0; i < pair.Length; i += 2)
            {
                _out.WriteLine($"{resource} {pair[i].GetType().Name}: normal {R(pair[i])}, exceptional {R(pair[i + 1])}");
                Assert.Equal(R(pair[i]), R(pair[i + 1]));
            }
        }
        finally
        {
            foreach (var i in pair)
            {
                i.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- X6

    [Fact]
    public void X6_RunicRangesAreNotScaled()
    {
        foreach (var resource in Enum.GetValues<CraftResource>())
        {
            var pinned = CraftResources.GetInfo(resource)?.AttributeInfo;
            if (pinned == null)
            {
                continue;
            }

            // a normal item reads pinned's own object
            Assert.Same(pinned, ClusterFExceptionalMaterial.Attrs(resource, false));

            var ex = ClusterFExceptionalMaterial.Attrs(resource, true);
            Assert.Equal(
                (pinned.RunicMinAttributes, pinned.RunicMaxAttributes, pinned.RunicMinIntensity, pinned.RunicMaxIntensity),
                (ex.RunicMinAttributes, ex.RunicMaxAttributes, ex.RunicMinIntensity, ex.RunicMaxIntensity)
            );
        }
    }

    [Fact]
    public void X6_RunicArmorPropertiesAreTheSameNormalOrExceptional()
    {
        var pm = Crafter();
        BaseArmor Make(bool exceptional)
        {
            var chest = new PlateChest();
            var hammer = new RunicHammer(CraftResource.Valorite);
            BuiltInRng.Generator = new ConstantRandom { Value = 0.7 };
            try
            {
                chest.OnCraft(exceptional ? 2 : 1, false, pm, DefBlacksmithy.CraftSystem, typeof(ValoriteIngot), hammer, null, 0);
            }
            finally
            {
                BuiltInRng.Reset();
                hammer.Delete();
            }

            return chest;
        }

        var normal = Make(false);
        var exceptional = Make(true);
        try
        {
            // the exceptional piece's own resist points are pinned's (DistributeBonuses) and land in the resist bonuses;
            // everything the runic hammer rolled sits in Attributes and ArmorAttributes
            static string Runic(BaseArmor a) => Show(
                new SortedDictionary<string, int>(Snapshot(a).Where(p => !p.Key.EndsWith("Resist")).ToDictionary())
            );

            _out.WriteLine($"runic Valorite plate: normal {Runic(normal)}; exceptional {Runic(exceptional)}");
            Assert.NotEqual("", Runic(normal));
            Assert.Equal(Runic(normal), Runic(exceptional));
        }
        finally
        {
            normal.Delete();
            exceptional.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- X7

    [Fact]
    public void X7_AResistAtTheCapStaysAtTheCap()
    {
        var pm = Crafter();
        var cap = pm.GetMaxResistance(ResistanceType.Fire);

        // a red scale chest's fire: 3 base + 10 red; the bonus brings a normal piece to the cap exactly
        var chest = new DragonChest { Resource = CraftResource.RedScales };
        chest.FireBonus = cap - 13;
        try
        {
            pm.AddItem(chest);
            pm.UpdateResistances();
            _out.WriteLine($"normal: chest fire {chest.FireResistance}, player fire {pm.FireResistance}, cap {cap}");
            Assert.Equal(cap, chest.FireResistance);
            Assert.Equal(cap, pm.FireResistance);

            chest.Quality = ArmorQuality.Exceptional;
            pm.UpdateResistances();
            _out.WriteLine($"exceptional: chest fire {chest.FireResistance}, player fire {pm.FireResistance}, cap {cap}");
            Assert.Equal(cap + 5, chest.FireResistance);
            Assert.Equal(cap, pm.FireResistance);
        }
        finally
        {
            chest.Delete();
            pm.Delete();
        }
    }
}
