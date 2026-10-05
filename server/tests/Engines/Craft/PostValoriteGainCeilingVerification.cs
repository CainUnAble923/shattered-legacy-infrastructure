// PostValoriteGainCeilingVerification.cs
//
// cc-P61 Part A (bug-list D83, Chase 2026-10-05): a post-Valorite metal raises the Blacksmithy gain ceiling; success
// chance, failure, material loss and the exceptional chance stay stock. The rule is ClusterFCraftGain; the one call site
// is CraftItem.GetSuccessChance's gain call (CraftItem-shard-hooks.patch). Notes: shard-migration
// notes/cc-P61-batch-7.md, Part A.
//
// Facts:
//   A1. Each post-Valorite metal's gain ceiling is the next metal's requirement (Celestial: the 200 cap), read from
//       ClusterFMetalTiers; iron and the stock metals have none. The chain has no gap: stock items teach to 140, past
//       Platinum's 112.5, and each ceiling is a requirement, so the next metal opens exactly where the last stops.
//   A2. The craft's own gain roll (the window GetSuccessChance hands Mobile.CheckSkill): a smith at 150 crafting Frost
//       plate rolls in (112.5, 162.5), at 162.5 gets no roll; Valorite plate at 140 stays stock (75, 125); below the
//       item's maximum every metal keeps the item's own window.
//   A3. A real gain through pinned's SkillCheck: 150 with Frost plate gains, 162.5 with Frost and 140 with Valorite do not.
//   A4. Success chance is stock: GetSuccessChance equals pinned's formula on the item's own range, with or without the
//       gain roll, for every case of A2.
//   A5. "Orders that still teach me" agrees with the roll: for 17 metals, every Blacksmithy bulk order item and skills
//       from 100 to 200, Teaches(smith, item, metal's ceiling) is exactly "the craft's window can gain". At 150 a Society
//       small order in Frost (the only metal offered at 150 whose ceiling is above it) teaches and is picked from every
//       armor piece, not only the fallback; in any other metal it is the fallback, as before.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PostValoriteGainCeilingVerification
{
    private readonly ITestOutputHelper _out;

    public PostValoriteGainCeilingVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureCraftSystems();
        ShardTestHost.EnsureSkillChecks();
        TwoHundredCapVerification.Blacksmithy();
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

    private static readonly Point3D Spot = new(1440, 1750, 0);

    private static CraftSystem Smith => DefBlacksmithy.CraftSystem;

    private static readonly (CraftResource Res, Type Ingot)[] Metals =
    {
        (CraftResource.Iron, typeof(IronIngot)),
        (CraftResource.DullCopper, typeof(DullCopperIngot)),
        (CraftResource.ShadowIron, typeof(ShadowIronIngot)),
        (CraftResource.Copper, typeof(CopperIngot)),
        (CraftResource.Bronze, typeof(BronzeIngot)),
        (CraftResource.Gold, typeof(GoldIngot)),
        (CraftResource.Agapite, typeof(AgapiteIngot)),
        (CraftResource.Verite, typeof(VeriteIngot)),
        (CraftResource.Valorite, typeof(ValoriteIngot)),
        (CraftResource.Platinum, typeof(PlatinumIngot)),
        (CraftResource.Toxic, typeof(ToxicIngot)),
        (CraftResource.Blaze, typeof(BlazeIngot)),
        (CraftResource.Frost, typeof(FrostIngot)),
        (CraftResource.Obsidian, typeof(ObsidianIngot)),
        (CraftResource.Mythril, typeof(MythrilIngot)),
        (CraftResource.Adamantium, typeof(AdamantiumIngot)),
        (CraftResource.Celestial, typeof(CelestialIngot))
    };

    private static PlayerMobile Player(double skill)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.Skills.Blacksmith.Cap = 200.0;
        pm.Skills.Blacksmith.Base = skill;
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private static CraftItem Item(Type type) => Smith.CraftItems.SearchFor(type);

    private static CraftSkill SmithSkill(CraftItem item) => item.Skills.First(s => s.SkillToMake == SkillName.Blacksmith);

    /// <summary>The Blacksmithy window the craft's gain roll hands Mobile.CheckSkill, read off the handler.</summary>
    private static (double Min, double Max) GainWindow(Mobile m, Type itemType, Type ingot)
    {
        var windows = new List<(double, double)>();
        var handler = Mobile.SkillCheckLocationHandler;
        Mobile.SkillCheckLocationHandler = (from, s, min, max) =>
        {
            if (s == SkillName.Blacksmith)
            {
                windows.Add((min, max));
            }

            return true;
        };

        try
        {
            Item(itemType).GetSuccessChance(m, ingot, Smith, true, out _);
        }
        finally
        {
            Mobile.SkillCheckLocationHandler = handler;
        }

        Assert.Single(windows);
        return windows[0];
    }

    private static bool CanGain(double value, (double Min, double Max) w) => value >= w.Min && value < w.Max;

    // ---------------------------------------------------------------- A1

    [Fact]
    public void EachCeilingIsTheNextMetalsRequirementAndTheChainHasNoGap()
    {
        var post = Metals.Where(m => m.Res >= CraftResource.Platinum && m.Res <= CraftResource.Celestial).ToArray();
        var topStock = Smith.CraftItems
            .Select(i => i.Skills.FirstOrDefault(s => s.SkillToMake == SkillName.Blacksmith))
            .Where(s => s != null)
            .Max(s => s.MaxSkill);

        _out.WriteLine($"stock items teach to {topStock}");
        _out.WriteLine("metal | needs | teaches to | next metal needs");
        for (var i = 0; i < post.Length; i++)
        {
            var req = ClusterFMetalTiers.RequiredSkill(post[i].Res);
            var ceiling = ClusterFMetalTiers.GainCeiling(post[i].Res);
            var next = i + 1 < post.Length ? ClusterFMetalTiers.RequiredSkill(post[i + 1].Res) : ClusterFSkillCaps.DefaultIndividualSkillCap;
            _out.WriteLine($"{post[i].Res} | {req} | {ceiling} | {(i + 1 < post.Length ? next.ToString() : "(cap " + next + ")")}");

            Assert.Equal(next, ceiling);
            Assert.True(ceiling >= req, $"{post[i].Res} teaches below its own requirement");
        }

        Assert.Equal(new[] { 125.0, 137.5, 150.0, 162.5, 175.0, 187.5, 200.0, 200.0 },
            post.Select(m => ClusterFMetalTiers.GainCeiling(m.Res)).ToArray());

        // No gap at the bottom: stock items teach past where Platinum opens.
        Assert.Equal(140.0, topStock);
        Assert.True(topStock >= ClusterFMetalTiers.Platinum);

        // Iron and the stock metals keep the item's own range.
        foreach (var (res, _) in Metals.Where(m => m.Res <= CraftResource.Valorite))
        {
            Assert.Equal(0.0, ClusterFMetalTiers.GainCeiling(res));
        }

        // The bulk order table names the same ceilings.
        Assert.Equal(162.5, ClusterFMetalTiers.GainCeiling(BulkMaterialType.Frost));
        Assert.Equal(0.0, ClusterFMetalTiers.GainCeiling(BulkMaterialType.Valorite));
    }

    // ---------------------------------------------------------------- A2

    public static readonly TheoryData<double, string, string, double, double, bool> Windows = new()
    {
        // skill, item, ingot, window min, window max, can gain
        { 150.0, nameof(PlateChest), nameof(FrostIngot), 112.5, 162.5, true },
        { 162.5, nameof(PlateChest), nameof(FrostIngot), 112.5, 162.5, false },
        { 140.0, nameof(PlateChest), nameof(ValoriteIngot), 75.0, 125.0, false },
        { 140.0, nameof(PlateChest), nameof(BlazeIngot), 100.0, 150.0, true },
        { 187.5, nameof(PlateChest), nameof(AdamantiumIngot), 150.0, 200.0, true },
        { 200.0, nameof(PlateChest), nameof(CelestialIngot), 150.0, 200.0, false },
        // Below the item's own maximum: the item's window, whatever the metal.
        { 115.0, nameof(PlateChest), nameof(PlatinumIngot), 75.0, 125.0, true },
        { 150.0, nameof(Broadsword), nameof(FrostIngot), 112.5, 162.5, true },
        { 30.0, nameof(Broadsword), nameof(IronIngot), 35.4, 85.4, false },
        { 60.0, nameof(Broadsword), nameof(IronIngot), 35.4, 85.4, true }
    };

    [Theory]
    [MemberData(nameof(Windows))]
    public void TheCraftsGainRollUsesTheMetalsCeilingFromTheItemsMaximum(
        double skill, string itemName, string ingotName, double min, double max, bool canGain
    )
    {
        var pm = Player(skill);
        try
        {
            var itemType = typeof(PlateChest).Assembly.GetType($"Server.Items.{itemName}")!;
            var ingot = typeof(IronIngot).Assembly.GetType($"Server.Items.{ingotName}")!;
            var own = SmithSkill(Item(itemType));
            var w = GainWindow(pm, itemType, ingot);
            _out.WriteLine($"{skill} {itemName} in {ingotName}: item {own.MinSkill}-{own.MaxSkill}, gain window {w.Min}-{w.Max}, can gain {CanGain(skill, w)}");

            Assert.Equal(min, w.Min, 6);
            Assert.Equal(max, w.Max, 6);
            Assert.Equal(canGain, CanGain(skill, w));
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- A3

    [Theory]
    [InlineData(150.0, typeof(FrostIngot), true)]
    [InlineData(162.5, typeof(FrostIngot), false)]
    [InlineData(140.0, typeof(ValoriteIngot), false)]
    [InlineData(140.0, typeof(BlazeIngot), true)]
    public void ARealCraftGainFollowsTheCeiling(double skill, Type ingot, bool gains)
    {
        var pm = Player(skill);
        BuiltInRng.Generator = new ConstantRandom { Value = 0.0 }; // every roll that can pass, passes
        try
        {
            Item(typeof(PlateChest)).GetSuccessChance(pm, ingot, Smith, true, out _);
            _out.WriteLine($"{skill} plate in {ingot.Name}: Blacksmithy now {pm.Skills.Blacksmith.Base}");
            // A gain is pinned's 0.1 (more if the shard's gain multiplier wraps the handler); none leaves it as it was.
            Assert.Equal(gains, pm.Skills.Blacksmith.Base > skill);
            Assert.True(pm.Skills.Blacksmith.Base >= skill);
        }
        finally
        {
            BuiltInRng.Reset();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- A4

    // A2's cases: skill, item, ingot.
    public static IEnumerable<object[]> SuccessCases => Windows.Select(r => new[] { r[0], r[1], r[2] });

    [Theory]
    [MemberData(nameof(SuccessCases))]
    public void SuccessChanceIsPinnedsFormulaOnTheItemsOwnRange(double skill, string itemName, string ingotName)
    {
        var pm = Player(skill);
        var handler = Mobile.SkillCheckLocationHandler;
        Mobile.SkillCheckLocationHandler = (_, _, _, _) => true;
        try
        {
            var itemType = typeof(PlateChest).Assembly.GetType($"Server.Items.{itemName}")!;
            var ingot = typeof(IronIngot).Assembly.GetType($"Server.Items.{ingotName}")!;
            var item = Item(itemType);
            var own = SmithSkill(item);

            // Pinned, CraftItem.GetSuccessChance (CraftItem.cs:902-904 at d4531cd9): the item's own min and max.
            var minChance = Smith.GetChanceAtMin(item);
            var expected = skill < own.MinSkill
                ? 0.0
                : minChance + (skill - own.MinSkill) / (own.MaxSkill - own.MinSkill) * (1.0 - minChance);

            var noGain = item.GetSuccessChance(pm, ingot, Smith, false, out _);
            var withGain = item.GetSuccessChance(pm, ingot, Smith, true, out _);
            _out.WriteLine($"{skill} {itemName} in {ingotName}: success {noGain:F4} (pinned {expected:F4}), " +
                           $"exceptional {item.GetExceptionalChance(Smith, noGain, pm):F4}");

            Assert.Equal(expected, noGain, 9);
            Assert.Equal(expected, withGain, 9);
        }
        finally
        {
            Mobile.SkillCheckLocationHandler = handler;
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- A5

    [Fact]
    public void TheTeachingSettingAgreesWithTheCraftsGainRoll()
    {
        var items = SmallBulkEntry.BlacksmithArmor.Concat(SmallBulkEntry.BlacksmithWeapons).Select(e => e.Type).Distinct().ToArray();
        var skills = Enumerable.Range(0, 41).Select(i => 100.0 + i * 2.5).Concat(new[] { 112.4, 124.9, 140.0, 162.4, 199.9 }).ToArray();

        var checkedCount = 0;
        var teachingInBand = 0;
        var pm = Player(100.0);
        try
        {
            foreach (var skill in skills)
            {
                pm.Skills.Blacksmith.Base = skill;
                foreach (var (res, ingot) in Metals)
                {
                    var ceiling = ClusterFMetalTiers.GainCeiling(res);
                    foreach (var type in items)
                    {
                        var roll = CanGain(skill, GainWindow(pm, type, ingot));
                        var teaches = ClusterFSmithTeaching.Teaches(pm, type, ceiling);
                        Assert.True(roll == teaches, $"{type.Name} in {res} at {skill}: the roll {(roll ? "can" : "cannot")} gain, Teaches says {teaches}");
                        checkedCount++;
                        if (teaches && skill >= ClusterFSmithTeaching.MaxSkill(type))
                        {
                            teachingInBand++;
                        }
                    }
                }
            }

            _out.WriteLine($"{checkedCount} (skill, metal, item) cases agree; {teachingInBand} teach past the item's own maximum");

            pm.Skills.Blacksmith.Base = 150.0;
            Assert.True(ClusterFSmithTeaching.Teaches(pm, typeof(PlateChest), ClusterFMetalTiers.GainCeiling(CraftResource.Frost)));
            pm.Skills.Blacksmith.Base = 162.5;
            Assert.False(ClusterFSmithTeaching.Teaches(pm, typeof(PlateChest), ClusterFMetalTiers.GainCeiling(CraftResource.Frost)));
            pm.Skills.Blacksmith.Base = 140.0;
            Assert.False(ClusterFSmithTeaching.Teaches(pm, typeof(PlateChest), ClusterFMetalTiers.GainCeiling(CraftResource.Valorite)));
        }
        finally
        {
            pm.Delete();
        }
    }

    [Fact]
    public void AtOneFiftyAFrostSmallOrderTeachesAndTheOthersFallBack()
    {
        ShardTestHost.EnsureAccounts();
        var account = new Account($"p61a{Guid.NewGuid():N}"[..16], "p61-test-only");
        var pm = Player(150.0);
        account[0] = pm;
        ClusterFAccountPersistence.GetOrCreate(account).GetOrCreateGuildData(pm.Serial).JoinedGuilds.Add("smithing");
        try
        {
            var inMetal = new List<string>();
            var others = 0;
            for (var i = 0; i < 400; i++)
            {
                var bod = SmallSmithBOD.CreateRandomFor(pm, true);
                Assert.NotNull(bod);
                var ceiling = ClusterFMetalTiers.GainCeiling(bod.Material);
                if (ceiling > 150.0)
                {
                    Assert.True(ClusterFSmithTeaching.Teaches(pm, bod.Type, ceiling), $"{bod.Type.Name} in {bod.Material}");
                    inMetal.Add(bod.Type.Name);
                }
                else
                {
                    others++;
                }

                bod.Delete();
            }

            var kinds = inMetal.Distinct().Count();
            _out.WriteLine($"at 150: {inMetal.Count} of 400 in a metal that teaches ({kinds} kinds: " +
                           $"{string.Join(", ", inMetal.GroupBy(n => n).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} x{g.Count()}"))}); {others} others");

            // At 150 only Frost (ceiling 162.5) of the metals a 150 smith is offered still teaches; the order's metal roll
            // gives Frost about 2% of the time (30% post-Valorite x 1/16), so the draws above are a sample, not a count.
            // The pick itself, deterministic: in Frost every armor piece the smith can make teaches; with no metal only
            // the fallback, the hardest (PlateChest).
            var armor = SmallBulkEntry.BlacksmithArmor;
            var frost = ClusterFSmithTeaching.PickItems(pm, armor, e => e.Type, false, true,
                ClusterFMetalTiers.GainCeiling(BulkMaterialType.Frost));
            var none = ClusterFSmithTeaching.PickItems(pm, armor, e => e.Type, false, true, 0.0);
            _out.WriteLine($"the pick at 150: in Frost {frost.Count} of {armor.Length} armor entries; with no metal " +
                           $"{string.Join(", ", none.Select(e => e.Type.Name).Distinct())}");
            Assert.All(frost, e => Assert.True(ClusterFSmithTeaching.Teaches(pm, e.Type, 162.5)));
            Assert.True(frost.Select(e => e.Type).Distinct().Count() > 1, "in Frost the pick is still only the fallback");
            Assert.Equal(new[] { typeof(PlateChest) }, none.Select(e => e.Type).Distinct().ToArray());
        }
        finally
        {
            ClusterFAccountPersistence.Get(account)?.ClearGuildData();
            pm.Delete();
            Accounts.Remove(account);
        }
    }
}
