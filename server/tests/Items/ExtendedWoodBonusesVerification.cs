// ExtendedWoodBonusesVerification.cs
//
// cc-P227, F-28: customizations ClusterFWoodBonuses, called from the end of BaseWeapon.OnCraft and BaseArmor.OnCraft
// (patches BaseWeapon-wood-bonuses, BaseArmor-wood-bonuses), and the shard woods' resource info (bug D88). Every
// crafting fact drives the real pinned OnCraft with the wood's log or board, as Fletching and Carpentry do. Notes:
// shard-migration notes/cc-P227-extended-wood-bonuses.md.
//
//   W1. For each of the 8 woods, a normal bow, staff and club (Tetsubo) gets exactly the weapon row's "Normal" line, and
//       an exceptional one "Normal" plus "Exceptional adds" (and pinned's exceptional 35 damage increase).
//   W2. The same for a wooden shield and the shield row.
//   W3. Oak and Frostwood bows match pinned exactly when normal, and get x1.5 when exceptional (cc-P228, F-29);
//       staves, clubs and shields in an OSI wood, and anything in Rootwood, get nothing.
//   W4. Starwood's random extra is pinned's Heartwood pick: the same outcome for each of the six rolls.
//   W5. No wood item ever has Self Repair, in any wood, quality or Heartwood roll.
//   W6. Resource info returns a name, hue, index and type for every extended wood, and their logs and boards name it.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ExtendedWoodBonusesVerification
{
    private readonly ITestOutputHelper _out;

    public ExtendedWoodBonusesVerification(ITestOutputHelper output)
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

    private static readonly CraftResource[] Extended =
    [
        CraftResource.Ironwood, CraftResource.Ghostwood, CraftResource.Emberbark, CraftResource.Frostbark,
        CraftResource.Shadowbark, CraftResource.Runewood, CraftResource.Voidwood, CraftResource.Starwood
    ];

    private static readonly Dictionary<CraftResource, (Type Log, Type Board, string Name)> Lumber = new()
    {
        [CraftResource.OakWood] = (typeof(OakLog), typeof(OakBoard), "Oak"),
        [CraftResource.Frostwood] = (typeof(FrostwoodLog), typeof(FrostwoodBoard), "Frostwood"),
        [CraftResource.Ironwood] = (typeof(IronwoodLog), typeof(IronwoodBoard), "Ironwood"),
        [CraftResource.Ghostwood] = (typeof(GhostwoodLog), typeof(GhostwoodBoard), "Ghostwood"),
        [CraftResource.Emberbark] = (typeof(EmberbarkLog), typeof(EmberbarkBoard), "Emberbark"),
        [CraftResource.Frostbark] = (typeof(FrostbarkLog), typeof(FrostbarkBoard), "Frostbark"),
        [CraftResource.Shadowbark] = (typeof(ShadowbarkLog), typeof(ShadowbarkBoard), "Shadowbark"),
        [CraftResource.Runewood] = (typeof(RunewoodLog), typeof(RunewoodBoard), "Runewood"),
        [CraftResource.Voidwood] = (typeof(VoidwoodLog), typeof(VoidwoodBoard), "Voidwood"),
        [CraftResource.Starwood] = (typeof(StarwoodLog), typeof(StarwoodBoard), "Starwood"),
        [CraftResource.Rootwood] = (typeof(RootwoodLog), typeof(RootwoodBoard), "Rootwood")
    };

    // F-28's tables, written out here independently of ClusterFWoodBonuses. Exceptional = Normal + Adds.
    private static readonly Dictionary<CraftResource, (string Normal, string Adds)> WeaponTable = new()
    {
        [CraftResource.Ironwood] = ("Durability=50", "Damage=14 LowerStatReq=20"),
        [CraftResource.Ghostwood] = ("HitChance=6", "Damage=15 DefendChance=5"),
        [CraftResource.Emberbark] = ("Physical=50 Fire=50", "Damage=16 HitFireArea=10"),
        [CraftResource.Frostbark] = ("Physical=40 Cold=60", "Damage=17 HitLowerAttack=10"),
        [CraftResource.Shadowbark] = ("SwingSpeed=10", "Damage=18 HitChance=6"),
        [CraftResource.Runewood] = ("Physical=50 Energy=50 RegenMana=2", "Damage=20 HitLeechMana=15"),
        [CraftResource.Voidwood] = ("HitLeechHits=15", "Damage=22 HitLeechStam=15"),
        // the Heartwood extra is held to roll 0 (luck 40) below
        [CraftResource.Starwood] = ("Luck=100", "Damage=25 HitChance=8 Luck=40")
    };

    private static readonly Dictionary<CraftResource, (string Normal, string Adds)> ShieldTable = new()
    {
        [CraftResource.Ironwood] = ("Durability=50", "PhysicalResist=3 LowerStatReq=20"),
        [CraftResource.Ghostwood] = ("DefendChance=4", "EnergyResist=3 DefendChance=3"),
        [CraftResource.Emberbark] = ("FireResist=4", "FireResist=3 ReflectPhysical=5"),
        [CraftResource.Frostbark] = ("ColdResist=4", "ColdResist=3 ReflectPhysical=5"),
        [CraftResource.Shadowbark] = ("DefendChance=5", "PoisonResist=3 DefendChance=3"),
        [CraftResource.Runewood] = ("EnergyResist=4 RegenMana=1", "SpellChanneling=1"),
        [CraftResource.Voidwood] = ("PhysicalResist=1 FireResist=1 ColdResist=1 PoisonResist=1 EnergyResist=1",
            "PhysicalResist=1 FireResist=1 ColdResist=1 PoisonResist=1 EnergyResist=1 RegenHits=2"),
        [CraftResource.Starwood] = ("Luck=100", "PhysicalResist=2 FireResist=2 ColdResist=2 PoisonResist=2 EnergyResist=2 DefendChance=5")
    };

    private static SortedDictionary<string, int> Parse(params string[] specs)
    {
        var d = new SortedDictionary<string, int>();
        foreach (var part in specs.SelectMany(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
        {
            var kv = part.Split('=');
            d[kv[0]] = d.GetValueOrDefault(kv[0]) + int.Parse(kv[1]);
        }

        return d;
    }

    private static string Show(SortedDictionary<string, int> d) =>
        string.Join(" ", d.Where(p => p.Value != 0).Select(p => $"{p.Key}={p.Value}"));

    private static SortedDictionary<string, int> Snapshot(BaseWeapon w) => new()
    {
        ["Damage"] = w.Attributes.WeaponDamage,
        ["HitChance"] = w.Attributes.AttackChance,
        ["DefendChance"] = w.Attributes.DefendChance,
        ["SwingSpeed"] = w.Attributes.WeaponSpeed,
        ["Luck"] = w.Attributes.Luck,
        ["RegenMana"] = w.Attributes.RegenMana,
        ["RegenHits"] = w.Attributes.RegenHits,
        ["Durability"] = w.WeaponAttributes.DurabilityBonus,
        ["LowerStatReq"] = w.WeaponAttributes.LowerStatReq,
        ["HitFireArea"] = w.WeaponAttributes.HitFireArea,
        ["HitLowerAttack"] = w.WeaponAttributes.HitLowerAttack,
        ["HitLeechHits"] = w.WeaponAttributes.HitLeechHits,
        ["HitLeechMana"] = w.WeaponAttributes.HitLeechMana,
        ["HitLeechStam"] = w.WeaponAttributes.HitLeechStam,
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
        ["RegenMana"] = a.Attributes.RegenMana,
        ["RegenHits"] = a.Attributes.RegenHits,
        ["ReflectPhysical"] = a.Attributes.ReflectPhysical,
        ["SpellChanneling"] = a.Attributes.SpellChanneling,
        ["CastSpeed"] = a.Attributes.CastSpeed,
        ["Durability"] = a.ArmorAttributes.DurabilityBonus,
        ["LowerStatReq"] = a.ArmorAttributes.LowerStatReq,
        ["SelfRepair"] = a.ArmorAttributes.SelfRepair,
        ["PhysicalResist"] = a.PhysicalBonus,
        ["FireResist"] = a.FireBonus,
        ["ColdResist"] = a.ColdBonus,
        ["PoisonResist"] = a.PoisonBonus,
        ["EnergyResist"] = a.EnergyBonus
    };

    private static int _slot;

    private static PlayerMobile Crafter()
    {
        // Arms Lore 0: pinned's exceptional damage increase is exactly 35 (ArmsLoreMattersVerification A1)
        var pm = new PlayerMobile { Player = true, Race = Race.Elf };
        pm.AddItem(new Backpack());
        pm.RawStr = pm.RawDex = pm.RawInt = 100;
        pm.MoveToWorld(new Point3D(1600 + _slot++ % 50 * 4, 1600, 0), Map.Trammel);
        pm.Skills.ArmsLore.Base = 0;
        pm.Skills.ArmsLore.SetLockNoRelay(SkillLock.Locked);
        return pm;
    }

    // As the craft menus do: Fletching works logs into bows, Carpentry boards into staves, clubs and shields.
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

    private static string Expected(string normal, string adds, bool exceptional, bool weapon) =>
        Show(exceptional ? Parse(normal, adds, weapon ? "Damage=35" : "") : Parse(normal));

    public static IEnumerable<object[]> WoodsAndQuality() =>
        Extended.SelectMany(w => new[] { new object[] { w, false }, new object[] { w, true } });

    // ---------------------------------------------------------------- W1, W2

    [Theory]
    [MemberData(nameof(WoodsAndQuality))]
    public void W1_WoodWeaponsGetExactlyTheirRow(CraftResource wood, bool exceptional)
    {
        var pm = Crafter();
        var items = new List<Item>();
        try
        {
            var (normal, adds) = WeaponTable[wood];
            var expected = Expected(normal, adds, exceptional, true);

            BaseWeapon[] weapons =
            [
                Craft<Bow>(pm, wood, exceptional), Craft<QuarterStaff>(pm, wood, exceptional),
                Craft<Tetsubo>(pm, wood, exceptional)
            ];
            items.AddRange(weapons);

            foreach (var w in weapons)
            {
                var got = Show(Snapshot(w));
                _out.WriteLine($"{(exceptional ? "exceptional" : "normal")} {wood} {w.GetType().Name}: {got}");
                Assert.Equal(wood, w.Resource);
                Assert.Equal(expected, got);
            }
        }
        finally
        {
            items.ForEach(i => i.Delete());
            pm.Delete();
        }
    }

    [Theory]
    [MemberData(nameof(WoodsAndQuality))]
    public void W2_WoodenShieldsGetExactlyTheirRow(CraftResource wood, bool exceptional)
    {
        var pm = Crafter();
        var shield = Craft<WoodenShield>(pm, wood, exceptional);
        try
        {
            var (normal, adds) = ShieldTable[wood];
            var got = Show(Snapshot(shield));
            _out.WriteLine($"{(exceptional ? "exceptional" : "normal")} {wood} wooden shield: {got}");
            Assert.Equal(wood, shield.Resource);
            Assert.Equal(Expected(normal, adds, exceptional, false), got);
        }
        finally
        {
            shield.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- W3

    [Theory]
    // pinned BaseRanged.cs:221-257: Oak luck 40, damage 5; Frostwood 60 physical / 40 cold, damage 12; any quality.
    // cc-P228 (F-29): an exceptional bow gets them x1.5 (Oak luck 60, damage 8; Frostwood 40 / 60 cold, damage 18).
    [InlineData(CraftResource.OakWood, false, "Damage=5 Luck=40")]
    [InlineData(CraftResource.OakWood, true, "Damage=43 Luck=60")]
    [InlineData(CraftResource.Frostwood, false, "Cold=40 Damage=12 Physical=60")]
    [InlineData(CraftResource.Frostwood, true, "Cold=60 Damage=53 Physical=40")]
    [InlineData(CraftResource.Rootwood, false, "")]
    [InlineData(CraftResource.Rootwood, true, "Damage=35")]
    public void W3_OsiWoodsAndRootwoodKeepTheirRows(CraftResource wood, bool exceptional, string bowExpected)
    {
        var pm = Crafter();
        var items = new List<Item>();
        try
        {
            var bow = Craft<Bow>(pm, wood, exceptional);
            items.Add(bow);
            _out.WriteLine($"{wood} bow: {Show(Snapshot(bow))}");
            Assert.Equal(bowExpected, Show(Snapshot(bow)));

            var plain = exceptional ? "Damage=35" : "";
            foreach (var w in new BaseWeapon[] { Craft<QuarterStaff>(pm, wood, exceptional), Craft<Tetsubo>(pm, wood, exceptional) })
            {
                items.Add(w);
                _out.WriteLine($"{wood} {w.GetType().Name}: {Show(Snapshot(w))}");
                Assert.Equal(plain, Show(Snapshot(w)));
            }

            var shield = Craft<WoodenShield>(pm, wood, exceptional);
            items.Add(shield);
            _out.WriteLine($"{wood} wooden shield: {Show(Snapshot(shield))}");
            Assert.Equal("", Show(Snapshot(shield)));
        }
        finally
        {
            items.ForEach(i => i.Delete());
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- W4

    [Fact]
    public void W4_StarwoodExtraIsPinnedsHeartwoodPick()
    {
        var pinned = typeof(BaseRanged).GetMethod("ApplyHeartwoodBonus", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(pinned);

        for (var pick = 0; pick < 6; pick++)
        {
            var theirs = new Bow();
            var ours = new Bow();
            try
            {
                HeldPick(pick);
                pinned.Invoke(theirs, null);
                HeldPick(pick);
                ClusterFWoodBonuses.HeartwoodExtra(ours);

                _out.WriteLine($"roll {pick}: pinned {Show(Snapshot(theirs))}, ours {Show(Snapshot(ours))}");
                Assert.NotEqual("", Show(Snapshot(theirs)));
                Assert.Equal(Show(Snapshot(theirs)), Show(Snapshot(ours)));
            }
            finally
            {
                BuiltInRng.Reset();
                theirs.Delete();
                ours.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- W5

    [Fact]
    public void W5_NoWoodItemHasSelfRepair()
    {
        var pm = Crafter();
        var count = 0;
        try
        {
            foreach (var wood in Lumber.Keys)
            {
                foreach (var exceptional in new[] { false, true })
                {
                    for (var pick = 0; pick < 6; pick++)
                    {
                        Item[] items =
                        [
                            Craft<Bow>(pm, wood, exceptional, pick), Craft<QuarterStaff>(pm, wood, exceptional, pick),
                            Craft<Tetsubo>(pm, wood, exceptional, pick), Craft<WoodenShield>(pm, wood, exceptional, pick)
                        ];

                        foreach (var item in items)
                        {
                            var selfRepair = item is BaseWeapon w ? w.WeaponAttributes.SelfRepair : ((BaseArmor)item).ArmorAttributes.SelfRepair;
                            Assert.True(selfRepair == 0, $"{wood} {item.GetType().Name} (exceptional {exceptional}, roll {pick}): self repair {selfRepair}");
                            item.Delete();
                            count++;
                        }
                    }
                }
            }

            _out.WriteLine($"{count} wood items crafted, none with Self Repair");
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- W6

    [Fact]
    public void W6_ExtendedWoodsHaveResourceInfo()
    {
        foreach (var wood in Extended.Append(CraftResource.Rootwood))
        {
            var (logType, boardType, name) = Lumber[wood];
            var info = CraftResources.GetInfo(wood);

            _out.WriteLine($"{wood}: type {CraftResources.GetType(wood)}, index {CraftResources.GetIndex(wood)}, " +
                           $"name '{CraftResources.GetName(wood)}', hue 0x{CraftResources.GetHue(wood):X4}");

            Assert.Equal(CraftResourceType.Wood, CraftResources.GetType(wood));
            Assert.NotNull(info);
            Assert.Equal(wood, info.Resource);
            Assert.Equal(wood - CraftResource.RegularWood, CraftResources.GetIndex(wood));
            Assert.Equal(name, CraftResources.GetName(wood));
            Assert.NotEqual(0, CraftResources.GetHue(wood));
            Assert.NotNull(info.AttributeInfo);
            Assert.Equal(wood, CraftResources.GetFromType(logType));
            Assert.Equal(wood, CraftResources.GetFromType(boardType));

            // Log and Board name the wood from GetName when its cliloc is 0 (pinned Log.cs:49-63, Board.cs:55-69)
            foreach (var item in new[] { logType.CreateInstance<Item>(), boardType.CreateInstance<Item>() })
            {
                try
                {
                    var list = PropertyListReader.Read(item);
                    _out.WriteLine($"  {item.GetType().Name}: {list.Describe()}");
                    Assert.Equal(CraftResources.GetHue(wood), item.Hue);
                    Assert.Contains(list, e => e.Argument == name);
                }
                finally
                {
                    item.Delete();
                }
            }
        }
    }
}
