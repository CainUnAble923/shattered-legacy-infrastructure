// HammerFamiliarityPaceVerification.cs
//
// cc-P55 Part A (bug-list D69, Chase 2026-10-05). Metal Familiarity rose +1 on every successful craft to caps of 100 (T1)
// and 250 (T2), so the familiarity bonuses came almost at once. Now: caps ten times higher (1,000 and 2,500 per metal);
// each successful craft rolls once to gain, with a chance of max(0.05, 1 - current / cap); a gain is +3 for an
// exceptional item and +1 otherwise, never past the cap; every familiarity bonus still reaches its full value at the
// cap; stored values are kept as they are. Notes: shard-migration notes/cc-P55-bug-batch-5.md, Part A.
//
// Facts:
//   A1. The caps are 1,000 (T1) and 2,500 (T2), and the familiarity panel shows them.
//   A2. The gain chance is 1 at 0, 0.5 at half the cap and the 5% floor near it, exactly and over seeded rolls.
//   A3. A gain is +3 for an exceptional craft and +1 otherwise, and never passes the cap (both tiers).
//   A4. At the new cap the bonuses are today's at today's cap: T1 refunds at 20% and T2 gives +2.5 Blacksmithy.
//   A5. A hammer's stored values come back unchanged after a save and load (not rescaled), so they sit lower.
//   A6. Through the craft hook, exceptional ringmail gloves add 3 and normal ones 1.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Random;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HammerFamiliarityPaceVerification
{
    private readonly ITestOutputHelper _out;

    public HammerFamiliarityPaceVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureCraftSystems();
        ShardTestHost.EnsureSkillChecks();
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

    private static int Iron(BaseTool hammer) => Snapshot(hammer).TryGetValue((int)CraftResource.Iron, out var v) ? v : 0;

    private static Dictionary<int, int> Snapshot(BaseTool hammer) => hammer switch
    {
        HammerOfHephaestus h           => h.GetFamiliaritySnapshot(),
        ReinforcedHammerOfHephaestus r => r.GetFamiliaritySnapshot(),
        _                              => throw new ArgumentException(hammer.GetType().Name)
    };

    private static void Record(BaseTool hammer, Mobile from, bool exceptional)
    {
        switch (hammer)
        {
            case HammerOfHephaestus h:
                h.RecordFamiliarity(typeof(IronIngot), from, exceptional);
                break;
            case ReinforcedHammerOfHephaestus r:
                r.RecordFamiliarity(typeof(IronIngot), from, exceptional);
                break;
        }
    }

    private static void Load(BaseTool hammer, Dictionary<int, int> values, int cap)
    {
        switch (hammer)
        {
            case HammerOfHephaestus h:
                h.LoadFamiliaritySnapshot(values, cap);
                break;
            case ReinforcedHammerOfHephaestus r:
                r.LoadFamiliaritySnapshot(values, cap);
                break;
        }
    }

    private static PlayerMobile Smith()
    {
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.RawStr = pm.RawDex = pm.RawInt = 100;
        pm.MoveToWorld(Spot, Map.Trammel);
        pm.Skills.Blacksmith.Cap = 120.0;
        pm.Skills.Blacksmith.Base = 100.0;
        return pm;
    }

    private static List<string> Labels(Gump gump) => gump.Entries.OfType<GumpLabel>().Select(l => l.Text).ToList();

    // ---------------------------------------------------------------- A1

    [Fact]
    public void TheCapsAreTenTimesHigherAndThePanelShowsThem()
    {
        Assert.Equal(1000, HammerOfHephaestus.FamCap);
        Assert.Equal(2500, ReinforcedHammerOfHephaestus.FamCap);

        var pm = Smith();
        var t1 = new HammerOfHephaestus();
        var t2 = new ReinforcedHammerOfHephaestus();
        try
        {
            pm.Backpack.DropItem(t1);
            pm.Backpack.DropItem(t2);
            var l1 = Labels(new HammerFamiliarityGump(pm, t1));
            var l2 = Labels(new HammerFamiliarityGump(pm, t2));
            _out.WriteLine($"T1 rows: {string.Join(" | ", l1.Where(l => l.Contains('/')).Take(3))}");
            _out.WriteLine($"T2 rows: {string.Join(" | ", l2.Where(l => l.Contains('/')).Take(3))}");

            Assert.Equal(HammerMetal.Count, l1.Count(l => l == "0/1,000"));
            Assert.Equal(HammerMetal.Count, l2.Count(l => l == "0/2,500"));
            Assert.Contains(l1, l => l.Contains("familiarity/1,000"));
            Assert.Contains(l2, l => l.Contains("/42,500"));
        }
        finally
        {
            t1.Delete();
            t2.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- A2

    [Fact]
    public void TheGainChanceFallsTowardTheCapWithAFivePercentFloor()
    {
        const int cap = 1000;
        Assert.Equal(1.0, HammerFamiliarityPace.GainChance(0, cap));
        Assert.Equal(0.5, HammerFamiliarityPace.GainChance(500, cap));
        Assert.Equal(0.05, HammerFamiliarityPace.GainChance(990, cap));
        Assert.Equal(0.05, HammerFamiliarityPace.GainChance(999, cap));
        Assert.Equal(0.5, HammerFamiliarityPace.GainChance(1250, 2500));

        try
        {
            BuiltInRng.Generator = new System.Random(5505);
            const int trials = 4000;
            foreach (var (start, low, high) in new[] { (0, 1.0, 1.0), (500, 0.46, 0.54), (990, 0.035, 0.065) })
            {
                var gains = 0;
                for (var i = 0; i < trials; i++)
                {
                    if (HammerFamiliarityPace.Roll(start, cap, false) > start)
                    {
                        gains++;
                    }
                }

                var rate = gains / (double)trials;
                _out.WriteLine($"from {start} of {cap}: {gains}/{trials} gained ({rate:P1})");
                Assert.InRange(rate, low, high);
            }
        }
        finally
        {
            BuiltInRng.Reset();
        }
    }

    // ---------------------------------------------------------------- A3

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnExceptionalGainIsThreeANormalOneOneAndNeverPastTheCap(bool reinforced)
    {
        var pm = Smith();
        BaseTool hammer = reinforced ? new ReinforcedHammerOfHephaestus() : new HammerOfHephaestus();
        var cap = reinforced ? ReinforcedHammerOfHephaestus.FamCap : HammerOfHephaestus.FamCap;
        try
        {
            pm.Backpack.DropItem(hammer);
            BuiltInRng.Generator = new ConstantRandom { Value = 0.0 }; // every roll gains

            Record(hammer, pm, true);
            Assert.Equal(3, Iron(hammer));
            Record(hammer, pm, false);
            Assert.Equal(4, Iron(hammer));

            Load(hammer, new Dictionary<int, int> { [(int)CraftResource.Iron] = cap - 1 }, cap);
            Record(hammer, pm, true);
            _out.WriteLine($"{hammer.GetType().Name}: 3, then 4, then {cap - 1} + exceptional = {Iron(hammer)}");
            Assert.Equal(cap, Iron(hammer));
            Record(hammer, pm, true);
            Assert.Equal(cap, Iron(hammer));
        }
        finally
        {
            BuiltInRng.Reset();
            hammer.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- A4

    [Fact]
    public void TheBonusesAtTheNewCapAreTodaysBonusesAtTodaysCap()
    {
        var pm = Smith();
        var t1 = new HammerOfHephaestus();
        var t2 = new ReinforcedHammerOfHephaestus();
        try
        {
            // T1: the panel's per-metal refund chance, the same formula RecordFamiliarity rolls (familiarity / cap * 20%).
            pm.Backpack.DropItem(t1);
            t1.LoadFamiliaritySnapshot(new() { [(int)CraftResource.Iron] = HammerOfHephaestus.FamCap }, HammerOfHephaestus.FamCap);
            var refund = Labels(new HammerFamiliarityGump(pm, t1));
            Assert.Contains("20.0%", refund);

            // A value at the old cap (100) now sits at a tenth of the bonus: 2.0%.
            t1.LoadFamiliaritySnapshot(new() { [(int)CraftResource.Iron] = 100 }, HammerOfHephaestus.FamCap);
            Assert.Contains("2.0%", Labels(new HammerFamiliarityGump(pm, t1)));

            // T2: every metal at the cap gives +2.5 on top of the +10 base, held.
            var all = HammerMetal.All.ToDictionary(m => m.Resource, _ => ReinforcedHammerOfHephaestus.FamCap);
            t2.LoadFamiliaritySnapshot(all, ReinforcedHammerOfHephaestus.FamCap);
            Assert.True(pm.EquipItem(t2));
            var bonus = Math.Round(pm.Skills.Blacksmith.Value - pm.Skills.Blacksmith.Base, 1);
            _out.WriteLine($"T1 at cap: 20.0% refund; T2 all metals at {ReinforcedHammerOfHephaestus.FamCap}: +{bonus}");
            Assert.Equal(12.5, bonus);
        }
        finally
        {
            t1.Delete();
            t2.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- A5

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AStoredHammerLoadsItsValuesUnchanged(bool reinforced)
    {
        ShardTestClock.Arm();
        BaseTool original = reinforced ? new ReinforcedHammerOfHephaestus() : new HammerOfHephaestus();
        BaseTool copy = null;
        var oldCap = reinforced ? 250 : 100;
        var values = new Dictionary<int, int>
        {
            [(int)CraftResource.Iron] = oldCap,     // at the old cap
            [(int)CraftResource.Valorite] = 37
        };

        try
        {
            Load(original, values, oldCap);

            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            original.Serialize(writer);
            writer.Flush();

            copy = reinforced ? new ReinforcedHammerOfHephaestus(original.Serial) : new HammerOfHephaestus(original.Serial);
            copy.Deserialize(new BufferReader(buffer));

            var loaded = Snapshot(copy);
            _out.WriteLine($"{copy.GetType().Name}: Iron {loaded[(int)CraftResource.Iron]}, Valorite {loaded[(int)CraftResource.Valorite]}");
            Assert.Equal(oldCap, loaded[(int)CraftResource.Iron]);
            Assert.Equal(37, loaded[(int)CraftResource.Valorite]);
            Assert.Equal(2, loaded.Count);
        }
        finally
        {
            original.Delete();
            copy?.Delete();
        }
    }

    // ---------------------------------------------------------------- A6

    [Fact]
    public void TheCraftHookPassesExceptionalQuality()
    {
        ShardTestClock.Arm();
        var pm = Smith();
        pm.Skills.Blacksmith.Base = 65.0; // ringmail gloves (12 to 62): success chance 1.06, exceptional 0.46; Dull Copper needs 65
        var hammer = new HammerOfHephaestus();
        var anvil = new Item(4015) { Movable = false };
        var forge = new Item(4017) { Movable = false };
        var ns = PacketTestUtilities.CreateTestNetState();

        try
        {
            pm.NetState = ns;
            ns.Mobile = pm;
            pm.Backpack.DropItem(hammer);
            pm.Backpack.DropItem(new IronIngot(500));
            pm.Backpack.DropItem(new DullCopperIngot(100));
            anvil.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
            forge.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, Spot.Z), Map.Trammel);

            var system = DefBlacksmithy.CraftSystem;
            var entry = system.CraftItems.SearchFor(typeof(RingmailGloves));
            system.GetContext(pm).MarkOption = CraftMarkOption.DoNotMark;

            // 0.0: the craft succeeds, is exceptional (0.46 > 0.0) and gains.
            // 0.999: the craft still succeeds (1.0 > 0.999), is not exceptional (0.46), and gains (chance 0.997 at 3 of 1,000
            // is below 0.999, so this one starts from a fresh metal: Dull Copper ingots, chance 1.0).
            foreach (var (roll, expect) in new[] { (0.0, 3), (0.999, 1) })
            {
                BuiltInRng.Generator = new ConstantRandom { Value = roll };
                var metal = roll == 0.0 ? CraftResource.Iron : CraftResource.DullCopper;
                var before = Snapshot(hammer).TryGetValue((int)metal, out var b) ? b : 0;
                var made = pm.Backpack.Items.OfType<RingmailGloves>().Count();
                pm.CloseGump<CraftGump>();
                system.CreateItem(pm, entry.ItemType, roll == 0.0 ? null : typeof(DullCopperIngot), hammer, entry);
                for (var i = 0; i < 200 && pm.FindGump<CraftGump>() == null; i++)
                {
                    ShardTestClock.Advance(TimeSpan.FromMilliseconds(250));
                }

                var gloves = pm.Backpack.Items.OfType<RingmailGloves>().ToList();
                var after = Snapshot(hammer).TryGetValue((int)metal, out var a) ? a : 0;
                _out.WriteLine($"roll {roll}: gloves {gloves.Last().Quality} ({gloves.Last().Resource}), {metal} familiarity {before} -> {after}");
                Assert.Equal(made + 1, gloves.Count);
                Assert.Equal(roll == 0.0 ? ArmorQuality.Exceptional : ArmorQuality.Regular, gloves.Last().Quality);
                Assert.Equal(before + expect, after);
            }
        }
        finally
        {
            BuiltInRng.Reset();
            DefBlacksmithy.CraftSystem.GetContext(pm).Run = null;
            pm.NetState = null;
            ns.Mobile = null;
            hammer.Delete();
            anvil.Delete();
            forge.Delete();
            pm.Delete();
        }
    }
}
