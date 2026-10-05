// SmithSealPriceVerification.cs
//
// cc-P57 Parts A and B (Chase 2026-10-05, after cc-P56 raised Seal income). Notes: shard-migration
// notes/cc-P57-batch-6.md, Parts A and B.
//
// Part A: every Seal catalog price is three times what it was (SmithSealCatalogGump.PriceFactor), and the bank's item part
// stays at the old prices (the catalog's baseline), so a price rise really lowers what a banked Seal buys. Bank >= OSI
// stays checked over all 918 deed shapes by SmithBankValueVerification A2, with OSI's item at the same baseline.
// Part B: a post-Valorite deed's item part scales with the metal at half the gold multiplier's step,
// 1 + (gold multiplier - 1) / 2: Platinum x1.25 .. Celestial x3.0.
//
// Facts:
//   P1. Every catalog line's price is its cc-P56 price times 3, and its baseline is the cc-P56 price.
//   P2. The bank does not follow the price: the cc-P56 table's stock-metal rows bank exactly what they banked under
//       cc-P56 at the middle roll.
//   P3. The pace: average Seals per small and large deed a member is offered at 100 and 120 Blacksmithy (printed for
//       the notes' table); a catalog item takes exactly PriceFactor times the deeds it took before.
//   B1. Small and large exceptional plate 20 in each post-Valorite metal: the item part is the best of the deed's own rung
//       and its Valorite rung times 1 + (m - 1) / 2, rounded up; the gold part is unchanged (printed before and after).

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SmithSealPriceVerification
{
    private readonly ITestOutputHelper _out;

    public SmithSealPriceVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsureCraftSystems();
        ClusterFGuildSystem.EnsureRegistered();
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

    private static T AtMiddleRoll<T>(Func<T> f)
    {
        BuiltInRng.Generator = new ConstantRandom { Value = 0.5 };
        try
        {
            return f();
        }
        finally
        {
            BuiltInRng.Reset();
        }
    }

    private static Item Small(int amount, bool exceptional, BulkMaterialType mat) =>
        new SmallSmithBOD(amount, amount, typeof(PlateChest), 1025141, 0x1415, exceptional, mat);

    private static LargeSmithBOD Large(SmallBulkEntry[] set, int amount, bool exceptional, BulkMaterialType mat)
    {
        var deed = new LargeSmithBOD(amount, exceptional, mat, null);
        deed.Entries = LargeBulkEntry.ConvertEntries(deed, set);
        foreach (var entry in deed.Entries)
        {
            entry.Amount = amount;
        }

        return deed;
    }

    // ---------------------------------------------------------------- P1

    // The catalog as cc-P56 left it (SmithRunicPricesVerification's cc-P46 numbers, tools and hammers unchanged since).
    public static IEnumerable<object[]> OldPrices() =>
    [
        [SmithSealCatalogGump.Cat.Tools, new[] { 50, 50, 100, 200, 300, 600, 150, 250 }],
        [SmithSealCatalogGump.Cat.AncientHammers, new[] { 100, 200, 500, 1_000 }],
        [SmithSealCatalogGump.Cat.RunicVanilla, new[] { 200, 350, 550, 800, 3_600, 5_400, 9_000, 15_000 }],
        [SmithSealCatalogGump.Cat.RunicPostVal, new[] { 22_500, 30_000, 39_000, 51_000, 66_000, 84_000, 108_000, 150_000 }]
    ];

    [Theory]
    [MemberData(nameof(OldPrices))]
    public void EveryPriceIsTheOldPriceTimesTheFactor(SmithSealCatalogGump.Cat cat, int[] old)
    {
        var rows = SmithSealCatalogGump.Rows(cat);
        Assert.Equal(3, SmithSealCatalogGump.PriceFactor);
        Assert.Equal(old, rows.Select(r => r.BaseCost).ToArray());
        Assert.Equal(old.Select(p => p * 3).ToArray(), rows.Select(r => r.Cost).ToArray());
        foreach (var r in rows)
        {
            _out.WriteLine($"{cat} {r.Name}: {r.BaseCost:N0} -> {r.Cost:N0}");
        }
    }

    // ---------------------------------------------------------------- P2

    public static IEnumerable<object[]> StockRows() =>
    [
        ["small iron 10 regular", (Func<Item>)(() => Small(10, false, BulkMaterialType.None)), 52],
        ["small iron 20 exceptional", (Func<Item>)(() => Small(20, true, BulkMaterialType.None)), 305],
        ["small Gold 20 exceptional", (Func<Item>)(() => Small(20, true, BulkMaterialType.Gold)), 579],
        ["small Valorite 20 exceptional", (Func<Item>)(() => Small(20, true, BulkMaterialType.Valorite)), 264],
        ["large iron plate 20 exceptional", (Func<Item>)(() => Large(LargeBulkEntry.LargePlate, 20, true, BulkMaterialType.None)), 739],
        ["large Valorite ring 20 exceptional", (Func<Item>)(() => Large(LargeBulkEntry.LargeRing, 20, true, BulkMaterialType.Valorite)), 2_386],
        ["large Valorite plate 20 exceptional", (Func<Item>)(() => Large(LargeBulkEntry.LargePlate, 20, true, BulkMaterialType.Valorite)), 16_886],
        ["large Valorite swords 20 exceptional", (Func<Item>)(() => Large(LargeBulkEntry.LargeSwords, 20, true, BulkMaterialType.Valorite)), 9_001]
    ];

    [Theory]
    [MemberData(nameof(StockRows))]
    public void TheBankDoesNotFollowThePrice(string name, Func<Item> make, int cc56)
    {
        var deed = make();
        try
        {
            var bank = AtMiddleRoll(() => BlacksmithGuildmaster.ComputeGuildReward(deed).seals);
            _out.WriteLine($"{name}: banks {bank:N0} (cc-P56 {cc56:N0}); the catalog is x{SmithSealCatalogGump.PriceFactor}");
            Assert.Equal(cc56, bank);
        }
        finally
        {
            deed.Delete();
        }
    }

    // ---------------------------------------------------------------- P3

    [Theory]
    [InlineData(100.0)]
    [InlineData(120.0)]
    public void ThePaceFallsByTheFactor(double skill)
    {
        var account = new Account($"p57a{Guid.NewGuid():N}"[..16], "p57-test-only");
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(1530, 1530, 0), Map.Trammel);
        pm.Skills.Blacksmith.Cap = 200.0;
        pm.Skills.Blacksmith.Base = skill;

        try
        {
            double Average(Func<Item> make, int n)
            {
                long sum = 0;
                var count = 0;
                for (var i = 0; i < n; i++)
                {
                    var deed = make();
                    if (deed == null)
                    {
                        continue;
                    }

                    sum += BlacksmithGuildmaster.ComputeGuildReward(deed).seals;
                    count++;
                    deed.Delete();
                }

                Assert.True(count > n / 2, $"only {count} of {n} deeds made");
                return sum / (double)count;
            }

            var small = Average(() => SmallSmithBOD.CreateRandomFor(pm), 2_000);
            var large = Average(() => LargeSmithBOD.CreateRandomFor(pm), 1_000);
            _out.WriteLine($"Blacksmithy {skill}: average bank {small:N1} Seals a small deed, {large:N1} a large deed");

            foreach (var (cat, item) in new[]
                     {
                         (SmithSealCatalogGump.Cat.Tools, "Sturdy Shovel"), (SmithSealCatalogGump.Cat.Tools, "Mining Gloves +5"),
                         (SmithSealCatalogGump.Cat.Tools, "Smith Guild Salvage Bag"),
                         (SmithSealCatalogGump.Cat.RunicVanilla, "Gold Runic Hammer"),
                         (SmithSealCatalogGump.Cat.RunicVanilla, "Valorite Runic Hammer"),
                         (SmithSealCatalogGump.Cat.RunicPostVal, "Platinum Runic Hammer")
                     })
            {
                var row = SmithSealCatalogGump.Rows(cat).Single(r => r.Name == item);
                _out.WriteLine($"  {item}: {row.BaseCost:N0} -> {row.Cost:N0} Seals");
                Assert.Equal(row.BaseCost * SmithSealCatalogGump.PriceFactor, row.Cost);
            }
        }
        finally
        {
            pm.Delete();
            Accounts.Remove(account);
        }
    }

    // ---------------------------------------------------------------- B1

    public static IEnumerable<object[]> PostValorite() =>
        new[]
        {
            (BulkMaterialType.Platinum, 1.25), (BulkMaterialType.Toxic, 1.5), (BulkMaterialType.Blaze, 1.75),
            (BulkMaterialType.Frost, 2.0), (BulkMaterialType.Obsidian, 2.25), (BulkMaterialType.Mythril, 2.5),
            (BulkMaterialType.Adamantium, 2.75), (BulkMaterialType.Celestial, 3.0)
        }.SelectMany(m => new[] { new object[] { m.Item1, m.Item2, false }, new object[] { m.Item1, m.Item2, true } });

    [Theory]
    [MemberData(nameof(PostValorite))]
    public void PostValoriteItemPartScalesAtHalfTheStep(BulkMaterialType mat, double itemMultiplier, bool large)
    {
        var calc = SmithRewardCalculator.Instance;
        Item deed = large ? Large(LargeBulkEntry.LargePlate, 20, true, mat) : Small(20, true, mat);
        try
        {
            int amountMax, itemCount;
            Type type;
            int gold;
            if (deed is LargeSmithBOD l)
            {
                (amountMax, itemCount, type) = (l.AmountMax, l.Entries.Length, l.Entries[0].Details.Type);
                gold = AtMiddleRoll(() => BlacksmithGuildmaster.GoldEquivalentForSeals(l));
            }
            else
            {
                var s = (SmallSmithBOD)deed;
                (amountMax, itemCount, type) = (s.AmountMax, 1, s.Type);
                gold = AtMiddleRoll(() => BlacksmithGuildmaster.GoldEquivalentForSeals(s));
            }

            var own = BlacksmithGuildmaster.OsiItemSeals(calc.ComputePoints(amountMax, true, mat, itemCount, type));
            var valorite = BlacksmithGuildmaster.OsiItemSeals(
                calc.ComputePoints(amountMax, true, BulkMaterialType.Valorite, itemCount, type));
            var item = Math.Max(own, valorite);

            var goldPart = BlacksmithGuildmaster.SealsForGold(gold);
            var before = goldPart + item;
            var after = AtMiddleRoll(() => BlacksmithGuildmaster.ComputeGuildReward(deed).seals);

            _out.WriteLine($"{(large ? "large" : "small")} {mat} exceptional plate 20: gold part {goldPart:N0}, item {item:N0} " +
                           $"x{itemMultiplier} = {(int)Math.Ceiling(item * itemMultiplier):N0}; bank {before:N0} -> {after:N0}");

            Assert.Equal(itemMultiplier, BlacksmithGuildmaster.ItemMultiplier(mat));
            Assert.Equal(goldPart + (int)Math.Ceiling(item * itemMultiplier), after);
            Assert.True(after > before);
        }
        finally
        {
            deed.Delete();
        }
    }
}
