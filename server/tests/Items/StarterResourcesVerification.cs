// StarterResourcesVerification.cs
//
// cc-P36 Part D: the New Haven dungeon's four starter resources (server/customizations/ClusterFStarterResources.cs,
// ResourceInfo.cs, Engines/Harvest/BurrowsRegionHarvest.cs, Engines/Craft/BurrowhideTailoringRegistration.cs). Chase's
// decisions 2026-10-01 items 7, 8, 9 and 12. Notes: shard-migration notes/cc-P36-dungeon-build-1.md, Part D.
//
// Facts:
//   D1. Values append at the top of their ranges (Dawnstone 18, BurrowhideLeather 105, Rootwood 316) and every existing
//       value is unchanged; each new value classifies in its range and has its info (name, hue, attributes).
//   D2. F3: the eight extended woods (308-315) now classify as wood, so their logs and boards carry their hue and name;
//       before cc-P36 the wood range stopped at Frostwood and they had neither. No other resource changes type.
//   D3. Dawnstone is never post-Valorite: no gain ceiling, its own 45, and Rootwood stays out of the wood gain chain.
//   D4. Region-only harvest: in the Burrows a 45 miner digs Dawnstone and a 44.9 one never does; the same for Rootwood;
//       outside, the bank's own vein is used, unchanged.
//   D5. Each new item round-trips through save and load.
//   D6. Each crafts at 45 and not at 44.9 in every craft that takes it; Rootwood and Burrowhide are appended last.
//   D7. The bonuses land: Dawnstone armour phys +3, durability +25, lower requirements +10; weapon durability +50, lower
//       requirements +25; Burrowhide phys +2, luck +20; Rootwood and Gloomsilk none.
//   D8. No new resource is in a BOD table, the smith commission picks, any work order, or a random resource pick.
//   D9. F4: a Hunter's Satchel turns Burrowhide hides into Burrowhide leather, and keeps an unknown hide as hides.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Engines.BulkOrders;
using Server.Engines.Craft;
using Server.Engines.Harvest;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Server.Regions;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class StarterResourcesVerification
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

    private readonly ITestOutputHelper _out;

    public StarterResourcesVerification(ITestOutputHelper output) => _out = output;

    private static readonly CraftResource[] New = { CraftResource.Dawnstone, CraftResource.BurrowhideLeather, CraftResource.Rootwood };

    private static readonly CraftResource[] ExtendedWoods =
    {
        CraftResource.Ironwood, CraftResource.Ghostwood, CraftResource.Emberbark, CraftResource.Frostbark,
        CraftResource.Shadowbark, CraftResource.Runewood, CraftResource.Voidwood, CraftResource.Starwood
    };

    // ---------------------------------------------------------------- D1

    [Fact]
    public void ValuesAppendAndEveryOldValueIsUnchanged()
    {
        Assert.Equal(18, (int)CraftResource.Dawnstone);
        Assert.Equal(105, (int)CraftResource.BurrowhideLeather);
        Assert.Equal(316, (int)CraftResource.Rootwood);

        // Every value that existed before cc-P36, by name and number.
        var before = new (string Name, int Value)[]
        {
            ("None", 0), ("Iron", 1), ("DullCopper", 2), ("ShadowIron", 3), ("Copper", 4), ("Bronze", 5), ("Gold", 6),
            ("Agapite", 7), ("Verite", 8), ("Valorite", 9), ("Platinum", 10), ("Toxic", 11), ("Blaze", 12), ("Frost", 13),
            ("Obsidian", 14), ("Mythril", 15), ("Adamantium", 16), ("Celestial", 17),
            ("RegularLeather", 101), ("SpinedLeather", 102), ("HornedLeather", 103), ("BarbedLeather", 104),
            ("RedScales", 201), ("YellowScales", 202), ("BlackScales", 203), ("GreenScales", 204), ("WhiteScales", 205),
            ("BlueScales", 206),
            ("RegularWood", 301), ("OakWood", 302), ("AshWood", 303), ("YewWood", 304), ("Heartwood", 305),
            ("Bloodwood", 306), ("Frostwood", 307), ("Ironwood", 308), ("Ghostwood", 309), ("Emberbark", 310),
            ("Frostbark", 311), ("Shadowbark", 312), ("Runewood", 313), ("Voidwood", 314), ("Starwood", 315)
        };

        foreach (var (name, value) in before)
        {
            Assert.Equal(value, (int)Enum.Parse<CraftResource>(name));
        }

        Assert.Equal(before.Length + New.Length, Enum.GetValues<CraftResource>().Length);

        var expected = new (CraftResource Res, CraftResourceType Type, string Name, int Index)[]
        {
            (CraftResource.Dawnstone, CraftResourceType.Metal, "Dawnstone", 17),
            (CraftResource.BurrowhideLeather, CraftResourceType.Leather, "Burrowhide", 4),
            (CraftResource.Rootwood, CraftResourceType.Wood, "Rootwood", 15)
        };

        foreach (var (res, type, name, index) in expected)
        {
            var info = CraftResources.GetInfo(res);
            _out.WriteLine($"{res}: {CraftResources.GetType(res)}, index {CraftResources.GetIndex(res)}, hue 0x{CraftResources.GetHue(res):X4}, \"{CraftResources.GetName(res)}\"");
            Assert.Equal(type, CraftResources.GetType(res));
            Assert.Equal(index, CraftResources.GetIndex(res));
            Assert.NotNull(info);
            Assert.Equal(res, info.Resource);
            Assert.Equal(name, CraftResources.GetName(res));
            Assert.NotEqual(0, CraftResources.GetHue(res));
        }

        Assert.Equal(CraftResource.Dawnstone, CraftResources.GetFromType(typeof(DawnstoneOre)));
        Assert.Equal(CraftResource.Dawnstone, CraftResources.GetFromType(typeof(DawnstoneIngot)));
        Assert.Equal(CraftResource.Dawnstone, CraftResources.GetFromType(typeof(DawnstoneGranite)));
        Assert.Equal(CraftResource.BurrowhideLeather, CraftResources.GetFromType(typeof(BurrowhideLeather)));
        Assert.Equal(CraftResource.BurrowhideLeather, CraftResources.GetFromType(typeof(BurrowhideHides)));
        Assert.Equal(CraftResource.Rootwood, CraftResources.GetFromType(typeof(RootwoodLog)));
        Assert.Equal(CraftResource.Rootwood, CraftResources.GetFromType(typeof(RootwoodBoard)));
    }

    // ---------------------------------------------------------------- D2

    [Fact]
    public void TheExtendedWoodsClassifyAsWoodAndShowTheirLook()
    {
        foreach (var res in ExtendedWoods)
        {
            var info = CraftResources.GetInfo(res);
            var log = (Log)Activator.CreateInstance(info.ResourceTypes[0], 1)!; // (int amount = 1): no parameterless ctor
            var board = (Board)Activator.CreateInstance(info.ResourceTypes[1], 1)!;
            try
            {
                _out.WriteLine($"{res}: type {CraftResources.GetType(res)}, name \"{CraftResources.GetName(res)}\", hue 0x{info.Hue:X4}; log hue 0x{log.Hue:X4}, board hue 0x{board.Hue:X4}");
                Assert.Equal(CraftResourceType.Wood, CraftResources.GetType(res));
                Assert.Equal(res.ToString(), CraftResources.GetName(res));
                Assert.Equal(info.Hue, log.Hue);
                Assert.Equal(info.Hue, board.Hue);
                Assert.NotEqual(0, log.Hue);
            }
            finally
            {
                log.Delete();
                board.Delete();
            }
        }

        // Nothing else moved: every metal, leather and scale keeps its type and index.
        for (var r = CraftResource.Iron; r <= CraftResource.Celestial; r++)
        {
            Assert.Equal(CraftResourceType.Metal, CraftResources.GetType(r));
            Assert.Equal(r - CraftResource.Iron, CraftResources.GetIndex(r));
        }

        for (var r = CraftResource.RegularLeather; r <= CraftResource.BarbedLeather; r++)
        {
            Assert.Equal(CraftResourceType.Leather, CraftResources.GetType(r));
        }

        for (var r = CraftResource.RedScales; r <= CraftResource.BlueScales; r++)
        {
            Assert.Equal(CraftResourceType.Scales, CraftResources.GetType(r));
        }

        Assert.Equal(CraftResourceType.None, CraftResources.GetType((CraftResource)19));
        Assert.Equal(CraftResourceType.None, CraftResources.GetType((CraftResource)106));
        Assert.Equal(CraftResourceType.None, CraftResources.GetType((CraftResource)317));
    }

    // ---------------------------------------------------------------- D3

    [Fact]
    public void DawnstoneIsNeverPostValoriteAndRootwoodIsOutOfTheGainChain()
    {
        Assert.False(ClusterFMetalTiers.IsPostValorite(CraftResource.Dawnstone));
        Assert.All(new[] { CraftResource.Platinum, CraftResource.Celestial }, r => Assert.True(ClusterFMetalTiers.IsPostValorite(r)));
        Assert.False(ClusterFMetalTiers.IsPostValorite(CraftResource.Valorite));

        Assert.Equal(45.0, ClusterFMetalTiers.RequiredSkill(CraftResource.Dawnstone));
        Assert.Equal(0.0, ClusterFMetalTiers.GainCeiling(CraftResource.Dawnstone));
        Assert.Equal(0.0, ClusterFCraftGain.CeilingFor(Smith(), CraftResource.Dawnstone));
        Assert.Equal(0.0, ClusterFCraftGain.CeilingFor(DefTinkering.CraftSystem, CraftResource.Dawnstone));
        Assert.Equal(0.0, ClusterFCraftGain.CeilingFor(DefCarpentry.CraftSystem, CraftResource.Rootwood));

        Assert.Equal(45.0, ClusterFWoodTiers.RequiredSkill(CraftResource.Rootwood));
        Assert.Equal(0.0, ClusterFWoodTiers.GainCeiling(CraftResource.Rootwood));
    }

    // ---------------------------------------------------------------- D4

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

    private static readonly Point3D InBurrows = new(5190, 1190, 0);
    private static readonly Point3D OutOfBurrows = new(1220, 1220, 0);

    private static (Type Got, bool MayTry) Harvest(HarvestSystem system, HarvestDefinition def, SkillName skill, double value,
        Point3D at, HarvestVein vein, double roll)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.Skills[skill].Cap = 200.0;
        pm.Skills[skill].Base = value;
        BuiltInRng.Generator = new ConstantRandom { Value = roll };
        try
        {
            var res = system.MutateResource(pm, null, def, Map.Trammel, at, vein, vein.PrimaryResource, vein.FallbackResource);
            return (res.Types[0], pm.Skills[skill].Base >= res.ReqSkill);
        }
        finally
        {
            BuiltInRng.Reset();
            pm.Delete();
        }
    }

    [Fact]
    public void InTheBurrowsA45MinerDigsDawnstoneAndA44Point9OneNever()
    {
        OldHavenBurrowsRegion.Initialize();
        var mining = (Mining)Activator.CreateInstance(typeof(Mining), true)!;
        ClusterFMiningExtension.ApplyExtendedVeins(mining.OreAndStone);
        var def = mining.OreAndStone;
        var valorite = def.Veins.Single(v => v.PrimaryResource.Types[0] == typeof(ValoriteOre));

        Assert.Equal((45.0, 5.0, 85.0), (Mining.Dawnstone.ReqSkill, Mining.Dawnstone.MinSkill, Mining.Dawnstone.MaxSkill));

        // Inside: whatever the bank's vein, a roll that does not fall back gives Dawnstone at 45, iron at 44.9.
        var at45 = Harvest(mining, def, SkillName.Mining, 45.0, InBurrows, valorite, 0.99);
        Assert.Equal(typeof(DawnstoneOre), at45.Got);
        Assert.True(at45.MayTry);
        foreach (var roll in new[] { 0.0, 0.49, 0.99 })
        {
            Assert.Equal(typeof(IronOre), Harvest(mining, def, SkillName.Mining, 44.9, InBurrows, valorite, roll).Got);
        }

        Assert.Equal(typeof(IronOre), Harvest(mining, def, SkillName.Mining, 45.0, InBurrows, valorite, 0.0).Got); // falls back

        // Outside: the bank's own vein, unchanged (a GM gets Valorite, as PostValoriteHarvestVerification's stock rows).
        Assert.Equal(typeof(ValoriteOre), Harvest(mining, def, SkillName.Mining, 100.0, OutOfBurrows, valorite, 0.99).Got);
        Assert.Equal(typeof(IronOre), Harvest(mining, def, SkillName.Mining, 45.0, OutOfBurrows, valorite, 0.99).Got);
        Assert.NotEqual(typeof(DawnstoneOre), Harvest(mining, def, SkillName.Mining, 100.0, OutOfBurrows, def.Veins[0], 0.99).Got);
    }

    [Fact]
    public void InTheBurrowsA45LumberjackCutsRootwoodAndA44Point9OneNever()
    {
        OldHavenBurrowsRegion.Initialize();
        var lj = (Lumberjacking)Activator.CreateInstance(typeof(Lumberjacking), true)!;
        var def = lj.Definitions[0];
        var oak = def.Veins.Single(v => v.PrimaryResource.Types[0] == typeof(OakLog));

        Assert.Equal((45.0, 5.0, 85.0), (Lumberjacking.Rootwood.ReqSkill, Lumberjacking.Rootwood.MinSkill, Lumberjacking.Rootwood.MaxSkill));

        var at45 = Harvest(lj, def, SkillName.Lumberjacking, 45.0, InBurrows, oak, 0.99);
        Assert.Equal(typeof(RootwoodLog), at45.Got);
        Assert.True(at45.MayTry);
        Assert.Equal(typeof(Log), Harvest(lj, def, SkillName.Lumberjacking, 44.9, InBurrows, oak, 0.99).Got);

        Assert.Equal(typeof(OakLog), Harvest(lj, def, SkillName.Lumberjacking, 65.0, OutOfBurrows, oak, 0.99).Got);
        Assert.Equal(typeof(Log), Harvest(lj, def, SkillName.Lumberjacking, 45.0, OutOfBurrows, oak, 0.99).Got);
    }

    // ---------------------------------------------------------------- D5

    private static T RoundTrip<T>(T original) where T : Item
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    [Fact]
    public void EachNewItemRoundTrips()
    {
        var items = new List<Item>
        {
            new DawnstoneOre(7), new DawnstoneIngot(11), new DawnstoneGranite(), new BurrowhideHides(13), new BurrowhideLeather(17),
            new RootwoodLog(19), new RootwoodBoard(23), new GloomsilkCloth(29),
            new PlatinumGranite(), new ToxicGranite(), new BlazeGranite(), new FrostGranite(), new ObsidianGranite(),
            new MythrilGranite(), new AdamantiumGranite(), new CelestialGranite()
        };

        foreach (var original in items)
        {
            var copy = RoundTrip(original);
            try
            {
                Assert.Equal(original.GetType(), copy.GetType());
                Assert.Equal(original.Amount, copy.Amount);
                Assert.Equal(original.Hue, copy.Hue);
                Assert.NotEqual(0, copy.Hue);
                var res = original switch
                {
                    BaseOre o => (o.Resource, ((BaseOre)(Item)copy).Resource),
                    BaseIngot i => (i.Resource, ((BaseIngot)(Item)copy).Resource),
                    BaseGranite g => (g.Resource, ((BaseGranite)(Item)copy).Resource),
                    BaseHides h => (h.Resource, ((BaseHides)(Item)copy).Resource),
                    BaseLeather l => (l.Resource, ((BaseLeather)(Item)copy).Resource),
                    Log l => (l.Resource, ((Log)(Item)copy).Resource),
                    Board b => (b.Resource, ((Board)(Item)copy).Resource),
                    _ => (CraftResource.None, CraftResource.None)
                };
                _out.WriteLine($"{original.GetType().Name} x{copy.Amount}: hue 0x{copy.Hue:X4}, resource {res.Item2}");
                Assert.Equal(res.Item1, res.Item2);
            }
            finally
            {
                original.Delete();
                copy.Delete();
            }
        }

        Assert.Equal(GloomsilkCloth.GloomsilkHue, new GloomsilkCloth().Hue);
    }

    // ---------------------------------------------------------------- D6

    private static CraftSystem Smith()
    {
        ShardTestHost.EnsureCraftSystems();
        if (DefMasonry.CraftSystem.CraftSubRes.SearchFor(typeof(PlatinumGranite)) == null)
        {
            ClusterFMiningExtension.ExtendMasonrySubResources();
        }

        BurrowhideTailoringRegistration.Register();
        return DefBlacksmithy.CraftSystem;
    }

    private static PlayerMobile Crafter()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(new Point3D(1240, 1240, 0), Map.Trammel);
        return pm;
    }

    [Fact]
    public void EachCraftsAt45AndNotAt44Point9()
    {
        Smith();

        var cases = new (string Craft, CraftSystem System, SkillName Skill, Type Item, Type Material, Func<Item> Stock)[]
        {
            ("Blacksmithy", DefBlacksmithy.CraftSystem, SkillName.Blacksmith, typeof(Dagger), typeof(DawnstoneIngot), () => new DawnstoneIngot(50)),
            ("Tinkering", DefTinkering.CraftSystem, SkillName.Tinkering, typeof(Scissors), typeof(DawnstoneIngot), () => new DawnstoneIngot(50)),
            ("Tailoring", DefTailoring.CraftSystem, SkillName.Tailoring, typeof(LeatherCap), typeof(BurrowhideLeather), () => new BurrowhideLeather(50)),
            ("Carpentry", DefCarpentry.CraftSystem, SkillName.Carpentry, typeof(Stool), typeof(RootwoodBoard), () => new RootwoodBoard(50)),
            ("Fletching", DefBowFletching.CraftSystem, SkillName.Fletching, typeof(Bow), typeof(RootwoodLog), () => new RootwoodLog(50)),
            ("Masonry", DefMasonry.CraftSystem, SkillName.Carpentry, typeof(StoneChair), typeof(DawnstoneGranite), () => new DawnstoneGranite())
        };

        foreach (var (craft, system, skill, itemType, material, stock) in cases)
        {
            var sub = system.CraftSubRes.SearchFor(material);
            Assert.NotNull(sub);
            Assert.Equal(45.0, sub.RequiredSkill);

            var item = system.CraftItems.SearchFor(itemType);
            Assert.NotNull(item);

            foreach (var (value, expected) in new[] { (45.0, true), (44.9, false) })
            {
                var pm = Crafter();
                try
                {
                    pm.Skills[skill].Cap = 200.0;
                    pm.Skills[skill].BaseFixedPoint = (int)Math.Round(value * 10);
                    for (var i = 0; i < 6; i++)
                    {
                        pm.Backpack.DropItem(stock()); // granite does not stack: StoneChair needs four
                    }

                    var hue = 0;
                    var max = 0;
                    TextDefinition message = null;
                    var ok = item.ConsumeRes(pm, material, system, ref hue, ref max, ConsumeType.None, ref message);
                    _out.WriteLine($"{craft} {itemType.Name} in {material.Name} at {value}: {ok} ({message?.Number})");
                    Assert.Equal(expected, ok);
                    if (!expected)
                    {
                        Assert.Equal(sub.Message.Number, message.Number);
                    }
                }
                finally
                {
                    pm.Delete();
                }
            }
        }

        // Appended last, so every saved menu choice keeps its index.
        Assert.Equal(typeof(DawnstoneIngot), DefBlacksmithy.CraftSystem.CraftSubRes.GetAt(DefBlacksmithy.CraftSystem.CraftSubRes.Count - 1).ItemType);
        Assert.Equal(typeof(DawnstoneIngot), DefTinkering.CraftSystem.CraftSubRes.GetAt(DefTinkering.CraftSystem.CraftSubRes.Count - 1).ItemType);
        Assert.Equal(typeof(BurrowhideLeather), DefTailoring.CraftSystem.CraftSubRes.GetAt(DefTailoring.CraftSystem.CraftSubRes.Count - 1).ItemType);
        Assert.Equal(typeof(RootwoodBoard), DefCarpentry.CraftSystem.CraftSubRes.GetAt(DefCarpentry.CraftSystem.CraftSubRes.Count - 1).ItemType);
        Assert.Equal(typeof(RootwoodLog), DefBowFletching.CraftSystem.CraftSubRes.GetAt(DefBowFletching.CraftSystem.CraftSubRes.Count - 1).ItemType);
        Assert.Equal(typeof(DawnstoneGranite), DefMasonry.CraftSystem.CraftSubRes.GetAt(DefMasonry.CraftSystem.CraftSubRes.Count - 1).ItemType);
    }

    // ---------------------------------------------------------------- D7

    [Fact]
    public void TheBonusesLand()
    {
        var ironPlate = new PlateChest();
        var dawnPlate = new PlateChest { Resource = CraftResource.Dawnstone };
        var ironSword = new Longsword();
        var dawnSword = new Longsword { Resource = CraftResource.Dawnstone };
        var plainLeather = new LeatherChest();
        var burrowLeather = new LeatherChest { Resource = CraftResource.BurrowhideLeather };
        try
        {
            Assert.Equal(3, dawnPlate.PhysicalResistance - ironPlate.PhysicalResistance);
            Assert.Equal(25, dawnPlate.GetDurabilityBonus() - ironPlate.GetDurabilityBonus());
            Assert.Equal(10, dawnPlate.GetLowerStatReq() - ironPlate.GetLowerStatReq());
            Assert.Equal(50, dawnSword.GetDurabilityBonus() - ironSword.GetDurabilityBonus());
            Assert.Equal(25, dawnSword.GetLowerStatReq() - ironSword.GetLowerStatReq());
            Assert.Equal(2, burrowLeather.PhysicalResistance - plainLeather.PhysicalResistance);
            Assert.Equal(20, burrowLeather.GetLuckBonus() - plainLeather.GetLuckBonus());
            Assert.Equal(CraftResources.GetHue(CraftResource.Dawnstone), dawnPlate.Hue);

            // Half or less of the lowest stock tier, never more (Dull Copper, Spined).
            var dc = CraftAttributeInfo.DullCopper;
            var d = CraftAttributeInfo.Dawnstone;
            Assert.True(d.ArmorPhysicalResist * 2 <= dc.ArmorPhysicalResist && d.ArmorDurability * 2 <= dc.ArmorDurability);
            Assert.Equal((0, 0, 0, 0), (d.RunicMinAttributes, d.RunicMaxAttributes, d.RunicMinIntensity, d.RunicMaxIntensity));

            var rootwood = CraftAttributeInfo.Rootwood;
            Assert.Equal((0, 0, 0), (rootwood.ArmorPhysicalResist, rootwood.WeaponDurability, rootwood.ArmorLuck));
        }
        finally
        {
            ironPlate.Delete();
            dawnPlate.Delete();
            ironSword.Delete();
            dawnSword.Delete();
            plainLeather.Delete();
            burrowLeather.Delete();
        }
    }

    // ---------------------------------------------------------------- D8

    [Fact]
    public void NoNewResourceIsInABodCommissionWorkOrderOrRandomPick()
    {
        // Bulk orders: material is a BulkMaterialType, which gains nothing, and the new resources map to none.
        var names = Enum.GetNames<BulkMaterialType>();
        Assert.DoesNotContain(names, n => n.Contains("Dawnstone") || n.Contains("Burrowhide") || n.Contains("Rootwood"));
        Assert.All(New, r => Assert.Equal(BulkMaterialType.None, SmallBOD.GetMaterial(r)));

        // Smith commissions: the material pick, at every skill a tenth apart and both ends of its roll, bypass or not.
        var pick = typeof(SmithCommissionSystem).GetMethod("GetMaterialForSkill", Private)!;
        var seen = new HashSet<CraftResource>();
        foreach (var roll in new[] { 0.0, 0.69, 0.99 })
        {
            BuiltInRng.Generator = new ConstantRandom { Value = roll };
            try
            {
                for (var tenth = 0; tenth <= 2000; tenth++)
                {
                    seen.Add((CraftResource)pick.Invoke(null, new object[] { tenth / 10.0, false })!);
                }

                seen.Add((CraftResource)pick.Invoke(null, new object[] { 0.0, true })!);
            }
            finally
            {
                BuiltInRng.Reset();
            }
        }

        _out.WriteLine($"commission materials: {string.Join(", ", seen.OrderBy(r => r))}");
        Assert.DoesNotContain(CraftResource.Dawnstone, seen);

        // Work orders: no requirement in any registered order is a new type.
        ClusterFWorkOrderSystem.Configure();
        var defs = (Dictionary<string, WorkOrderDef>)typeof(ClusterFWorkOrderSystem).GetField("_defs", Private)!.GetValue(null)!;
        var newTypes = new[]
        {
            typeof(DawnstoneOre), typeof(DawnstoneIngot), typeof(DawnstoneGranite), typeof(BurrowhideHides),
            typeof(BurrowhideLeather), typeof(RootwoodLog), typeof(RootwoodBoard), typeof(GloomsilkCloth)
        };
        var required = defs.Values.SelectMany(d => d.Requirements).Select(r => r.ItemType).ToHashSet();
        _out.WriteLine($"work orders: {defs.Count}, distinct requirement types {required.Count}");
        Assert.NotEmpty(defs);
        Assert.DoesNotContain(required, newTypes.Contains);

        // Random resource picks: pinned's only calls draw DullCopper .. Valorite (ColoredAnvil.cs:15, GlovesOfMining.cs:111).
        for (var r = (int)CraftResource.DullCopper; r <= (int)CraftResource.Valorite; r++)
        {
            Assert.DoesNotContain((CraftResource)r, New);
        }
    }

    // ---------------------------------------------------------------- D9

    [Fact]
    public void AHuntersSatchelCutsBurrowhideAsBurrowhide()
    {
        var leather = HuntersSatchel.ConvertHidesToLeather(new BurrowhideHides(9));
        Assert.IsType<BurrowhideLeather>(leather);
        Assert.Equal(9, leather!.Amount);
        leather.Delete();

        foreach (var (hides, expected) in new (BaseHides, Type)[]
                 {
                     (new Hides(3), typeof(Leather)), (new SpinedHides(3), typeof(SpinedLeather)),
                     (new HornedHides(3), typeof(HornedLeather)), (new BarbedHides(3), typeof(BarbedLeather))
                 })
        {
            var cut = HuntersSatchel.ConvertHidesToLeather(hides);
            Assert.IsType(expected, cut);
            cut!.Delete();
            hides.Delete();
        }

        // A hide type the satchel does not know is kept as hides rather than downgraded to plain leather.
        var unknown = new UnknownHides();
        Assert.Null(HuntersSatchel.ConvertHidesToLeather(unknown));
        unknown.Delete();
    }

    // A hide type no table knows, for D9 (as QuiverSetCarrierVerification's test armour). Never saved: deleted at once.
    private sealed class UnknownHides : BaseHides
    {
        public UnknownHides() : base(CraftResource.HornedLeather)
        {
        }
    }
}
