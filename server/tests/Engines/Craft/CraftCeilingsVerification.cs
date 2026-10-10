// CraftCeilingsVerification.cs
//
// cc-P67 (cc-P54 batches K and N, Chase's decisions 10 and 11 of 2026-10-05, Part C of 2026-10-05 15:12, Part E of
// 2026-10-06). The rule is ClusterFCraftGain (one call site, CraftItem-shard-hooks.patch); the tables ClusterFMetalTiers
// and ClusterFWoodTiers; the caps ClusterFSkillClamps. Notes: shard-migration notes/cc-P67-craft-ceilings.md.
//
// Facts:
//   K1. Each craft's chain from its stock top to 200: every material's requirement in the craft menu is its table's, every
//       ceiling is the next material's requirement (the last, the 200 cap), and every skill from the stock top to 199.9
//       has a material that still teaches. Blacksmithy and Tinkering on the metals, Carpentry and Fletching on the woods.
//   K2. The consume check: only a recipe that consumes the picked material gets a ceiling. Logs to boards and kindling
//       (stock windows 0-0) never teach; a recipe whose resources do not include the material list's base type gets none.
//   K3. The craft's gain roll in each craft: past the item's maximum, (item's minimum, ceiling); a real gain below the
//       ceiling and none at it; below the item's maximum and in a stock material, the item's own window.
//   K4. Harder items teach better (Part C 2): the gain chance at 150 and 160 in Frost for a dagger, a plate tunic and a
//       kabuto, before (cc-P61's slid window) and after; after, the kabuto is the best trainer and the dagger the worst.
//   K5. Worn bonuses (Part E): a +10 smith in Mythril gains at Base 180 and stops at Base 187.5; with no bonus, the same
//       Base; a +12.5 smith whose band top lands on the cap (187.5 + 12.5 = 200) is not counted twice.
//   K6. Orders (Part C 1): with "Orders that still teach me" on, the post-Valorite metal of a small and a large Society
//       order is only one that still teaches the smith; off, as before.
//   K7. The caps: trap damage level at most 12 (power uncapped); axe bonus at most 30%; potion strength's Alchemy share at
//       most 30; deep-water finds count Fishing at most 120. Below OSI's tops, as pinned; creatures as pinned.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Engines.Craft;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CraftCeilingsVerification
{
    private const double Cap = 200.0;
    private readonly ITestOutputHelper _out;

    public CraftCeilingsVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureCraftSystems();
        ShardTestHost.EnsureSkillChecks();
        TwoHundredCapVerification.Blacksmithy();
        ClusterFMiningExtension.ExtendTinkeringSubResources();
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

    // Pinned's handlers wrapped by ours, as on the server; ClusterF's rate off (pinned's 0.1 a gain), rolls held.
    private sealed class Hook : IDisposable
    {
        private readonly SkillCheckLocationHandler _location = Mobile.SkillCheckLocationHandler;
        private readonly SkillCheckDirectLocationHandler _directLocation = Mobile.SkillCheckDirectLocationHandler;
        private readonly SkillCheckTargetHandler _target = Mobile.SkillCheckTargetHandler;
        private readonly SkillCheckDirectTargetHandler _directTarget = Mobile.SkillCheckDirectTargetHandler;
        private readonly bool _enabled = ClusterFSkillGain.Enabled;
        private readonly int _attempts = ClusterFSkillGain.ChanceAttempts;
        private readonly int _amount = ClusterFSkillGain.AmountMultiplier;

        public Hook(double roll = 0.0)
        {
            Mobile.SkillCheckLocationHandler = SkillCheck.Mobile_SkillCheckLocation;
            Mobile.SkillCheckDirectLocationHandler = SkillCheck.Mobile_SkillCheckDirectLocation;
            Mobile.SkillCheckTargetHandler = SkillCheck.Mobile_SkillCheckTarget;
            Mobile.SkillCheckDirectTargetHandler = SkillCheck.Mobile_SkillCheckDirectTarget;
            ClusterFSkillGain.Initialize();
            ClusterFSkillGain.Enabled = false;
            ClusterFSkillGain.ChanceAttempts = 1;
            ClusterFSkillGain.AmountMultiplier = 1;
            BuiltInRng.Generator = new ConstantRandom { Value = roll };
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

    private static readonly Point3D Spot = new(1460, 1760, 0);

    private static PlayerMobile Player()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.Skills.Cap = 58 * 2000;
        foreach (var name in new[] { SkillName.Blacksmith, SkillName.Tinkering, SkillName.Carpentry, SkillName.Fletching })
        {
            pm.Skills[name].Cap = Cap;
        }

        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    // Skill.Base truncates (value x 10) to an int, so 187.4 would land on 187.3; set the fixed point exactly.
    private static void SetBase(Mobile m, SkillName name, double value) =>
        m.Skills[name].BaseFixedPoint = (int)Math.Round(value * 10.0);

    private static CraftSystem CraftOf(string name) => name switch
    {
        "Blacksmithy" => DefBlacksmithy.CraftSystem,
        "Tinkering"   => DefTinkering.CraftSystem,
        "Carpentry"   => DefCarpentry.CraftSystem,
        "Fletching"   => DefBowFletching.CraftSystem,
        _             => throw new ArgumentException(name)
    };

    private static readonly CraftResource[] MetalsAbove =
    {
        CraftResource.Platinum, CraftResource.Toxic, CraftResource.Blaze, CraftResource.Frost,
        CraftResource.Obsidian, CraftResource.Mythril, CraftResource.Adamantium, CraftResource.Celestial
    };

    private static readonly CraftResource[] WoodsAbove =
    {
        CraftResource.Ironwood, CraftResource.Ghostwood, CraftResource.Emberbark, CraftResource.Frostbark,
        CraftResource.Shadowbark, CraftResource.Runewood, CraftResource.Voidwood, CraftResource.Starwood
    };

    private static double Required(string craft, CraftResource r) =>
        craft is "Blacksmithy" or "Tinkering" ? ClusterFMetalTiers.RequiredSkill(r) : ClusterFWoodTiers.RequiredSkill(r);

    private static CraftSkill MainOf(CraftSystem system, CraftItem item) =>
        item.Skills.First(s => s.SkillToMake == system.MainSkill);

    private static CraftItem Item(CraftSystem system, Type type) => system.CraftItems.SearchFor(type);

    /// <summary>The main-skill window the craft's gain roll hands Mobile.CheckSkill, read off the handler.</summary>
    private static (double Min, double Max) GainWindow(Mobile m, CraftSystem system, Type itemType, Type material)
    {
        var windows = new List<(double, double)>();
        var handler = Mobile.SkillCheckLocationHandler;
        Mobile.SkillCheckLocationHandler = (_, s, min, max) =>
        {
            if (s == system.MainSkill)
            {
                windows.Add((min, max));
            }

            return true;
        };

        try
        {
            Item(system, itemType).GetSuccessChance(m, material, system, true, out _);
        }
        finally
        {
            Mobile.SkillCheckLocationHandler = handler;
        }

        Assert.Single(windows);
        return windows[0];
    }

    private static bool CanGain(double value, (double Min, double Max) w) => value >= w.Min && value < w.Max;

    // ---------------------------------------------------------------- K1

    public static readonly TheoryData<string, double> Crafts = new()
    {
        // craft, its stock top (the highest maximum of any item it makes)
        { "Blacksmithy", 140.0 },
        { "Tinkering", 130.0 },
        { "Carpentry", 140.3 },
        { "Fletching", 130.0 }
    };

    [Theory]
    [MemberData(nameof(Crafts))]
    public void K1_EachCraftsChainReachesTheCapWithNoGap(string craft, double stockTop)
    {
        var system = CraftOf(craft);
        var materials = craft is "Blacksmithy" or "Tinkering" ? MetalsAbove : WoodsAbove;
        var top = system.CraftItems
            .Select(i => i.Skills.FirstOrDefault(s => s.SkillToMake == system.MainSkill))
            .Where(s => s != null)
            .Max(s => s.MaxSkill);

        _out.WriteLine($"{craft}: stock items teach to {top}");
        _out.WriteLine("material | needs (menu) | teaches to | next needs");
        for (var i = 0; i < materials.Length; i++)
        {
            // The menu's entry for the material (GetFromType: CraftResources.GetInfo knows no extended wood, see the notes).
            var menuReq = system.CraftSubRes.Single(r => CraftResources.GetFromType(r.ItemType) == materials[i]).RequiredSkill;
            var ceiling = ClusterFCraftGain.CeilingFor(system, materials[i]);
            var next = i + 1 < materials.Length ? Required(craft, materials[i + 1]) : Cap;
            _out.WriteLine($"{materials[i]} | {menuReq} | {ceiling} | {(i + 1 < materials.Length ? next : "(cap " + Cap + ")")}");

            Assert.Equal(Required(craft, materials[i]), menuReq);
            Assert.Equal(next, ceiling);
        }

        Assert.Equal(stockTop, top, 6);
        Assert.True(top >= Required(craft, materials[0]), $"{craft}: stock stops at {top}, below the first material");

        // No gap: every skill from the stock top to 199.9 has a material it can work that still teaches.
        for (var s = Math.Round(top, 1); s < Cap; s = Math.Round(s + 0.1, 1))
        {
            var skill = s;
            Assert.True(
                materials.Any(r => Required(craft, r) <= skill && skill < ClusterFCraftGain.CeilingFor(system, r)),
                $"{craft}: no material teaches at {skill}"
            );
        }
    }

    // ---------------------------------------------------------------- K2

    [Theory]
    [InlineData("Blacksmithy")]
    [InlineData("Tinkering")]
    [InlineData("Carpentry")]
    [InlineData("Fletching")]
    public void K2_OnlyRecipesThatConsumeTheMaterialGetACeiling(string craft)
    {
        var system = CraftOf(craft);
        var material = craft is "Blacksmithy" or "Tinkering" ? typeof(FrostIngot) :
            craft == "Carpentry" ? typeof(VoidwoodBoard) : typeof(VoidwoodLog);
        var baseType = system.CraftSubRes.ResType;

        var consuming = new List<string>();
        var other = new List<string>();
        var cuts = new List<string>();
        foreach (var item in system.CraftItems)
        {
            var main = item.Skills.FirstOrDefault(s => s.SkillToMake == system.MainSkill);
            if (main == null)
            {
                continue;
            }

            var ceiling = ClusterFCraftGain.CeilingFor(system, item, material);
            var name = $"{item.ItemType.Name} {main.MinSkill}-{main.MaxSkill} [{string.Join(", ", item.Resources.Select(r => r.ItemType.Name))}]";
            if (item.UseSubRes2)
            {
                Assert.Equal(0.0, ceiling);
                other.Add(name + " (scales)");
            }
            else if (!item.Resources.Any(r => r.ItemType == baseType))
            {
                Assert.Equal(0.0, ceiling);
                other.Add(name);
            }
            else if (main.MaxSkill <= main.MinSkill)
            {
                Assert.Equal(0.0, ceiling);
                cuts.Add(name);
            }
            else
            {
                Assert.True(ceiling > 0.0, $"{name} consumes {baseType.Name} but has no ceiling");
                consuming.Add(name);
            }
        }

        _out.WriteLine($"{craft}: material list's base type {baseType.Name}; {consuming.Count} recipes consume it and teach past their maximum in a shard material");
        _out.WriteLine($"{craft}: material cuts (never teach): {(cuts.Count == 0 ? "none" : string.Join("; ", cuts))}");
        _out.WriteLine($"{craft}: {other.Count} recipes do not consume it (stock window always): {string.Join("; ", other)}");

        if (craft == "Carpentry")
        {
            // Logs to boards is a recipe only in AOS and SE (DefCarpentry.cs:94-97); here an axe cuts them (Log.cs:84), which
            // rolls no skill at all.
            Assert.Null(system.CraftItems.SearchFor(typeof(Board)));
        }

        if (craft == "Fletching")
        {
            Assert.Contains(cuts, c => c.StartsWith("Kindling ", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void K2_CuttingShardLogsIntoKindlingTeachesNothing()
    {
        var pm = Player();
        try
        {
            var fletching = DefBowFletching.CraftSystem;
            SetBase(pm, SkillName.Fletching, 190.0);
            var w = GainWindow(pm, fletching, typeof(Kindling), typeof(VoidwoodLog));
            _out.WriteLine($"Kindling in Voidwood at 190: window {w.Min}-{w.Max}");
            Assert.Equal((0.0, 0.0), w);

            using (new Hook())
            {
                Item(fletching, typeof(Kindling)).GetSuccessChance(pm, typeof(VoidwoodLog), fletching, true, out _);
                Assert.Equal(1900, pm.Skills.Fletching.BaseFixedPoint);

                // The same fletcher on a bow in the same wood gains.
                Item(fletching, typeof(Bow)).GetSuccessChance(pm, typeof(VoidwoodLog), fletching, true, out _);
                Assert.True(pm.Skills.Fletching.BaseFixedPoint > 1900);
            }
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- K3

    public static readonly TheoryData<string, double, string, string, double, double, bool> Windows = new()
    {
        // craft, skill, item, material, window min, window max, can gain
        { "Carpentry", 150.0, nameof(FootStool), nameof(FrostbarkBoard), 11.0, 155.0, true },
        { "Carpentry", 150.0, nameof(Tetsubo), nameof(FrostbarkBoard), 80.0, 155.0, true },
        { "Carpentry", 155.0, nameof(Tetsubo), nameof(FrostbarkBoard), 80.0, 155.0, false },
        { "Carpentry", 150.0, nameof(Tetsubo), nameof(FrostwoodBoard), 80.0, 140.3, false },
        { "Carpentry", 120.0, nameof(Tetsubo), nameof(GhostwoodBoard), 80.0, 140.3, true },
        { "Fletching", 150.0, nameof(Bow), nameof(FrostbarkLog), 30.0, 155.0, true },
        { "Fletching", 150.0, nameof(Yumi), nameof(FrostbarkLog), 90.0, 155.0, true },
        { "Fletching", 190.0, nameof(Yumi), nameof(VoidwoodLog), 90.0, 200.0, true },
        { "Fletching", 200.0, nameof(Yumi), nameof(StarwoodLog), 90.0, 200.0, false },
        { "Fletching", 140.0, nameof(Yumi), nameof(Log), 90.0, 130.0, false },
        { "Tinkering", 150.0, nameof(WindChimes), nameof(FrostIngot), 80.0, 162.5, true },
        { "Tinkering", 150.0, nameof(Scissors), nameof(FrostIngot), 5.0, 162.5, true },
        { "Tinkering", 162.5, nameof(WindChimes), nameof(FrostIngot), 80.0, 162.5, false },
        { "Tinkering", 140.0, nameof(WindChimes), nameof(ValoriteIngot), 80.0, 130.0, false },
        { "Blacksmithy", 150.0, nameof(PlateChest), nameof(FrostIngot), 75.0, 162.5, true },
        { "Blacksmithy", 115.0, nameof(PlateChest), nameof(PlatinumIngot), 75.0, 125.0, true }
    };

    private static Type ItemType(string name) =>
        typeof(PlateChest).Assembly.GetType($"Server.Items.{name}") ?? throw new ArgumentException(name);

    [Theory]
    [MemberData(nameof(Windows))]
    public void K3_TheCraftsGainRollUsesTheBandFromTheItemsMinimumToTheCeiling(
        string craft, double skill, string itemName, string materialName, double min, double max, bool canGain
    )
    {
        var system = CraftOf(craft);
        var pm = Player();
        try
        {
            SetBase(pm, system.MainSkill, skill);
            var itemType = ItemType(itemName);
            var material = ItemType(materialName);
            var own = MainOf(system, Item(system, itemType));
            var w = GainWindow(pm, system, itemType, material);
            _out.WriteLine($"{craft} {skill} {itemName} in {materialName}: item {own.MinSkill}-{own.MaxSkill}, gain window {w.Min}-{w.Max}, can gain {CanGain(skill, w)}");

            Assert.Equal(min, w.Min, 6);
            Assert.Equal(max, w.Max, 6);
            Assert.Equal(canGain, CanGain(skill, w));

            // A real gain through pinned's SkillCheck, every roll passing: only where the window can gain.
            using (new Hook())
            {
                Item(system, itemType).GetSuccessChance(pm, material, system, true, out _);
            }

            _out.WriteLine($"  after one craft: {pm.Skills[system.MainSkill].Base}");
            Assert.Equal(canGain, pm.Skills[system.MainSkill].Base > skill);
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- K4

    // Pinned's gain chance for one craft (SkillCheck.cs:122-140), for a smith whose skill total is at the total cap (the
    // first term 0) and the skill's GainFactor 1.0: a check passes with chance c, and then gains with
    // ((200 - base) / 200 / 2 + (1 - c) * 0.5) / 2; a failed check gains with (200 - base) / 200 / 2 / 2.
    private static double GainPerCraft(double b, double c)
    {
        var a = (Cap - b) / Cap / 2.0;
        return c * Math.Max(0.01, (a + (1.0 - c) * 0.5) / 2.0) + (1.0 - c) * Math.Max(0.01, a / 2.0);
    }

    [Fact]
    public void K4_HarderItemsTeachBetterInTheBand()
    {
        var smith = DefBlacksmithy.CraftSystem;
        var ceiling = ClusterFMetalTiers.GainCeiling(CraftResource.Frost);
        var pm = Player();
        try
        {
            _out.WriteLine("Frost (ceiling 162.5), a smith at the total skill cap: roll chance c and gain chance per craft");
            _out.WriteLine("item (own range) | skill | before: window, c, gain | after: window, c, gain");
            foreach (var skill in new[] { 150.0, 160.0 })
            {
                pm.Skills.Blacksmith.Base = skill;
                var after = new List<double>();
                foreach (var type in new[] { typeof(Dagger), typeof(PlateChest), typeof(PlateBattleKabuto) })
                {
                    var own = MainOf(smith, Item(smith, type));
                    var before = (Min: ceiling - (own.MaxSkill - own.MinSkill), Max: ceiling);
                    var cBefore = (skill - before.Min) / (before.Max - before.Min);
                    var w = GainWindow(pm, smith, type, typeof(FrostIngot));
                    var c = (skill - w.Min) / (w.Max - w.Min);
                    var gain = GainPerCraft(skill, c);
                    after.Add(gain);
                    _out.WriteLine(
                        $"{type.Name} ({own.MinSkill}-{own.MaxSkill}) | {skill} | {before.Min}-{before.Max}, {cBefore:F3}, {GainPerCraft(skill, cBefore):F4} | " +
                        $"{w.Min}-{w.Max}, {c:F3}, {gain:F4}"
                    );

                    Assert.Equal(own.MinSkill, w.Min, 6);
                    Assert.Equal(ceiling, w.Max, 6);
                }

                // Dagger < plate tunic < kabuto.
                Assert.True(after[0] < after[1] && after[1] < after[2], $"at {skill}: {string.Join(", ", after)}");
            }
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- K5

    [Theory]
    [InlineData(0.0, 180.0, true)]
    [InlineData(0.0, 187.4, true)]
    [InlineData(0.0, 187.5, false)]
    [InlineData(10.0, 180.0, true)]
    [InlineData(10.0, 187.4, true)]
    [InlineData(10.0, 187.5, false)]
    [InlineData(12.5, 187.4, true)]
    [InlineData(12.5, 187.5, false)]
    public void K5_MaterialCeilingsCompareBaseSkillWhateverIsWorn(double worn, double baseSkill, bool gains)
    {
        var smith = DefBlacksmithy.CraftSystem;
        var pm = Player();
        DefaultSkillMod mod = null;
        try
        {
            SetBase(pm, SkillName.Blacksmith, baseSkill);
            var before = pm.Skills.Blacksmith.BaseFixedPoint;
            if (worn > 0.0)
            {
                mod = new DefaultSkillMod(SkillName.Blacksmith, "P67Worn", true, worn) { ObeyCap = true };
                pm.AddSkillMod(mod);
            }

            var w = GainWindow(pm, smith, typeof(PlateChest), typeof(MythrilIngot));
            _out.WriteLine($"Mythril plate, Base {baseSkill} + {worn} worn = {pm.Skills.Blacksmith.Value}: window {w.Min}-{w.Max}");
            Assert.Equal(ClusterFMetalTiers.Adamantium + worn, w.Max, 6); // Mythril's ceiling, plus what is worn

            // "Orders that still teach me" says the same, before the craft.
            Assert.Equal(gains, ClusterFSmithTeaching.Teaches(pm, typeof(PlateChest), ClusterFMetalTiers.GainCeiling(CraftResource.Mythril)));

            using (new Hook())
            {
                Item(smith, typeof(PlateChest)).GetSuccessChance(pm, typeof(MythrilIngot), smith, true, out _);
            }

            _out.WriteLine($"  after one craft: Base {pm.Skills.Blacksmith.Base}");
            Assert.Equal(gains, pm.Skills.Blacksmith.BaseFixedPoint > before);
            Assert.True(pm.Skills.Blacksmith.Base <= ClusterFMetalTiers.Adamantium, "Base passed Mythril's ceiling");
        }
        finally
        {
            if (mod != null)
            {
                pm.RemoveSkillMod(mod);
            }

            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- K6

    [Fact]
    public void K6_TheTeachingMetalIsTheOneThatStillTeaches()
    {
        var pm = Player();
        try
        {
            var chances = SmallSmithBOD.m_PostValMaterialChances;
            var lines = new List<string>();
            for (var s = 100.0; s <= Cap; s = Math.Round(s + 0.5, 1))
            {
                foreach (var skill in new[] { s, Math.Round(s - 0.1, 1) })
                {
                    SetBase(pm, SkillName.Blacksmith, skill);
                    var metal = ClusterFSmithTeaching.TeachingMetal(pm, chances);
                    var expected = Enumerable.Range(0, 8).Select(i => BulkMaterialType.Platinum + i)
                        .Where(m => ClusterFMetalTiers.PostValoriteRequiredSkill(m) <= skill && skill < ClusterFMetalTiers.GainCeiling(m))
                        .DefaultIfEmpty(BulkMaterialType.None).Single();
                    Assert.Equal(expected, metal);
                    if (skill % 12.5 == 0.0 || skill == 112.4 || skill == 199.9)
                    {
                        lines.Add($"{skill}: {metal}");
                    }
                }
            }

            _out.WriteLine(string.Join("; ", lines.Distinct()));
            pm.Skills.Blacksmith.Base = 150.0;
            Assert.Equal(BulkMaterialType.Frost, ClusterFSmithTeaching.TeachingMetal(pm, chances));
            SetBase(pm, SkillName.Blacksmith, 162.4);
            Assert.Equal(BulkMaterialType.Frost, ClusterFSmithTeaching.TeachingMetal(pm, chances));
            pm.Skills.Blacksmith.Base = 162.5;
            Assert.Equal(BulkMaterialType.Obsidian, ClusterFSmithTeaching.TeachingMetal(pm, chances));
            SetBase(pm, SkillName.Blacksmith, 112.4);
            Assert.Equal(BulkMaterialType.None, ClusterFSmithTeaching.TeachingMetal(pm, chances));
            pm.Skills.Blacksmith.Base = Cap;
            Assert.Equal(BulkMaterialType.None, ClusterFSmithTeaching.TeachingMetal(pm, chances));
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void K6_SocietyOrdersAtOneFiftyComeOnlyInFrostWhenTheSettingIsOn()
    {
        ShardTestHost.EnsureAccounts();
        var account = new Account($"p67k{Guid.NewGuid():N}"[..16], "p67-test-only");
        var pm = Player();
        pm.Skills.Blacksmith.Base = 150.0;
        account[0] = pm;
        ClusterFAccountPersistence.GetOrCreate(account).GetOrCreateGuildData(pm.Serial).JoinedGuilds.Add("smithing");
        try
        {
            static bool PostValorite(BulkMaterialType m) => m >= BulkMaterialType.Platinum && m <= BulkMaterialType.Celestial;

            Dictionary<BulkMaterialType, int> Small(bool on)
            {
                var counts = new Dictionary<BulkMaterialType, int>();
                for (var i = 0; i < 400; i++)
                {
                    var bod = SmallSmithBOD.CreateRandomFor(pm, on);
                    Assert.NotNull(bod);
                    if (PostValorite(bod.Material))
                    {
                        counts[bod.Material] = counts.GetValueOrDefault(bod.Material) + 1;
                        if (on)
                        {
                            Assert.True(ClusterFSmithTeaching.Teaches(pm, bod.Type, ClusterFMetalTiers.GainCeiling(bod.Material)), bod.Type.Name);
                        }
                    }

                    bod.Delete();
                }

                return counts;
            }

            string Show(Dictionary<BulkMaterialType, int> c) =>
                c.Count == 0 ? "none" : string.Join(", ", c.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} x{kv.Value}"));

            var on = Small(true);
            var off = Small(false);
            _out.WriteLine($"at 150, 400 small orders each: setting on, post-Valorite {Show(on)}; off, {Show(off)}");
            Assert.True(on.Count == 1 && on.ContainsKey(BulkMaterialType.Frost), Show(on));
            Assert.True(on[BulkMaterialType.Frost] > 40, Show(on));
            Assert.Contains(off.Keys, m => m != BulkMaterialType.Frost);

            var large = new Dictionary<BulkMaterialType, int>();
            var sets = new Dictionary<string, int>();
            for (var i = 0; i < 400; i++)
            {
                var bod = LargeSmithBOD.CreateRandomFor(pm);
                Assert.NotNull(bod);
                var set = bod.Entries[0].Details.Type.Name;
                sets[set] = sets.GetValueOrDefault(set) + 1;
                if (PostValorite(bod.Material))
                {
                    large[bod.Material] = large.GetValueOrDefault(bod.Material) + 1;
                }

                bod.Delete();
            }

            _out.WriteLine($"at 150, 400 large orders, setting on: post-Valorite {Show(large)}; first piece {string.Join(", ", sets.Select(kv => $"{kv.Key} x{kv.Value}"))}");
            Assert.True(large.Count == 1 && large.ContainsKey(BulkMaterialType.Frost), Show(large));
        }
        finally
        {
            ClusterFAccountPersistence.Get(account)?.ClearGuildData();
            pm.Delete();
            Accounts.Remove(account);
        }
    }

    // ---------------------------------------------------------------- K7

    private static string BuildTreeFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Projects", "UOContent")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, $"no Projects/UOContent above {AppContext.BaseDirectory}");
        return File.ReadAllText(Path.Combine(dir!.FullName, "Projects", "UOContent", relative));
    }

    [Theory]
    [InlineData(100.0, 10, 90)]
    [InlineData(120.0, 12, 108)]
    [InlineData(130.0, 12, 117)]
    [InlineData(200.0, 12, 180)]
    public void K7_ATrapsDamageStopsAtOsisTopAndItsPowerDoesNot(double tinkering, int level, int power)
    {
        var tinker = DefTinkering.CraftSystem;
        var pm = Player();
        var box = new MetalBox { KeyValue = 0x1234, Locked = false };
        try
        {
            pm.Skills.Tinkering.Base = tinkering;
            box.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);

            var craft = new ExplosionTrapCraft(pm, Item(tinker, typeof(ExplosionTrapCraft)), tinker, null, new TinkerTools(), 1);
            typeof(TrapCraft).GetProperty(nameof(TrapCraft.Container))!.SetValue(craft, box);
            craft.CompleteCraft(out var message);

            _out.WriteLine($"Tinkering {tinkering}: trap level {box.TrapLevel}, power {box.TrapPower} (message {message})");
            Assert.Equal(1005639, message);
            Assert.Equal(level, box.TrapLevel);
            Assert.Equal(power, box.TrapPower);
        }
        finally
        {
            box.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void K7_AxeBonusPotionStrengthAndDeepWaterFindsStopAtOsisTops()
    {
        Assert.Contains(
            "? GetBonus(ClusterFSkillClamps.AxeLumberjackingSkill(attacker), 0.200, 100.0, 10.00)",
            BuildTreeFile("Items/Weapons/BaseWeapon.cs")
        );
        Assert.Contains(
            "var value = ClusterFSkillClamps.FishingFindSkill(from, skillValue, entry.m_MaxSkill > entry.m_MinSkill);",
            BuildTreeFile("Engines/Harvest/Fishing.cs")
        );

        var pm = Player();
        var mongbat = new Mongbat();
        try
        {
            foreach (var name in new[] { SkillName.Lumberjacking, SkillName.Alchemy, SkillName.Fishing })
            {
                pm.Skills[name].Cap = Cap;
            }

            // The axe: GetBonus(skill, 0.2, 100, 10) = skill x 0.2% + 10% from 100 (BaseWeapon.GetBonus).
            static double Axe(double skill) => skill * 0.200 / 100.0 + (skill >= 100.0 ? 0.10 : 0.0);
            foreach (var (skill, counted) in new[] { (90.0, 90.0), (100.0, 100.0), (148.0, 100.0), (200.0, 100.0) })
            {
                pm.Skills.Lumberjacking.Base = skill;
                Assert.Equal(counted, ClusterFSkillClamps.AxeLumberjackingSkill(pm));
                _out.WriteLine($"Lumberjacking {skill}: axe bonus {Axe(ClusterFSkillClamps.AxeLumberjackingSkill(pm)):P1} (pinned {Axe(skill):P1})");
            }

            // Potion strength, through pinned's own EnhancePotions (no item bonus worn).
            foreach (var (skill, share) in new[] { (60.0, 18), (100.0, 30), (150.0, 30), (200.0, 30) })
            {
                pm.Skills.Alchemy.Base = skill;
                _out.WriteLine($"Alchemy {skill}: EnhancePotions {BasePotion.EnhancePotions(pm)} (pinned {(int)(skill * 10 / 33)})");
                Assert.Equal(share, BasePotion.EnhancePotions(pm));
            }

            // Deep-water finds, (value - 80) / 4000; the falling entries read the whole skill.
            foreach (var (skill, counted) in new[] { (100.0, 100.0), (120.0, 120.0), (200.0, 120.0) })
            {
                Assert.Equal(counted, ClusterFSkillClamps.FishingFindSkill(pm, skill, true));
                Assert.Equal(skill, ClusterFSkillClamps.FishingFindSkill(pm, skill, false));
                _out.WriteLine($"Fishing {skill}: each deep-water find {(counted - 80.0) / 4000.0:P2} (pinned {(skill - 80.0) / 4000.0:P2})");
            }

            // Creatures as pinned.
            mongbat.Skills.Lumberjacking.Cap = Cap;
            mongbat.Skills.Lumberjacking.Base = 150.0;
            Assert.Equal(150.0, ClusterFSkillClamps.AxeLumberjackingSkill(mongbat));
            Assert.Equal(150.0, ClusterFSkillClamps.FishingFindSkill(mongbat, 150.0, true));
        }
        finally
        {
            mongbat.Delete();
            pm.Delete();
        }
    }
}
