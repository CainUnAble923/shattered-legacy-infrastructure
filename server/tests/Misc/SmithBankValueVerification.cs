// SmithBankValueVerification.cs
//
// cc-P56 Part A (bug-list D77, Chase 2026-10-05): banking a smith deed is worth at least what OSI pays a non-member for
// the same deed at a regular smith, valued in Seals the way cc-P55 Part H valued it (gold at the guild's rate, 3 Seals
// per 400 gold; the item at its Seal catalog price, an unpriced item at the midpoint of its priced neighbours on OSI's
// ladder), and cashing out stays below both. Notes: shard-migration notes/cc-P56-smith-economy-and-labels.md, Part A.
//
// Facts:
//   A1. The ladder the bank's item part reads (BlacksmithGuildmaster.OsiRungs) is pinned's own reward ladder, each rung at
//       the best value of the items it can give, valued here from the live catalog. A catalog price change that is not
//       carried into the ladder fails this.
//   A2. Every smith deed shape (small, and the eight large sets; 10, 15 and 20; regular and exceptional; iron, the eight
//       stock colors and the eight post-Valorite metals: 918 deeds): the bank at the lowest roll of its gold is at least
//       OSI's value at OSI's highest roll with OSI's best item for that deed, and OSI's value is above cash out's.
//   A3. The notes' table rows at the middle of the roll: bank >= OSI > cash out, printed for the notes.
//   A4. A post-Valorite deed never banks less than the same deed in Valorite, and the metals bank in order.
//   A5. Existing Seal balances are untouched: a member's saved balance loads as saved, and a turn-in adds the deed's Seals
//       to it and nothing else.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.BulkOrders;
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
public class SmithBankValueVerification
{
    private readonly ITestOutputHelper _out;

    public SmithBankValueVerification(ITestOutputHelper output)
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

    private const double Lowest = 0.0;
    private const double Highest = 0.999999;

    // ---------------------------------------------------------------- valuation, independent of the server's table

    private static int Price(SmithSealCatalogGump.Cat cat, string name) =>
        SmithSealCatalogGump.Rows(cat).Single(r => r.Name == name).Cost;

    // An OSI reward item's Seal catalog price, or null when the catalog does not sell it.
    private static int? CatalogPrice(Item item) => item switch
    {
        SturdyShovel          => Price(SmithSealCatalogGump.Cat.Tools, "Sturdy Shovel"),
        SturdyPickaxe         => Price(SmithSealCatalogGump.Cat.Tools, "Sturdy Pickaxe"),
        GargoylesPickaxe      => Price(SmithSealCatalogGump.Cat.Tools, "Gargoyle's Pickaxe"),
        ProspectorsTool       => Price(SmithSealCatalogGump.Cat.Tools, "Prospector's Tool"),
        PowderOfTemperament   => Price(SmithSealCatalogGump.Cat.Tools, "Powder of Temperament"),
        StuddedGlovesOfMining { Bonus: 3 } => Price(SmithSealCatalogGump.Cat.Tools, "Mining Gloves +3"),
        RingmailGlovesOfMining { Bonus: 5 } => Price(SmithSealCatalogGump.Cat.Tools, "Mining Gloves +5"),
        AncientSmithyHammer h => Price(SmithSealCatalogGump.Cat.AncientHammers, $"Ancient Smithy Hammer (+{h.Bonus} uses)"),
        RunicHammer r         => Price(SmithSealCatalogGump.Cat.RunicVanilla, $"{CraftResources.GetName(r.Resource)} Runic Hammer"),
        _                     => null // mining gloves +1, the colored anvil, the power scrolls
    };

    // Each rung of pinned's ladder at the best value of the items it can give.
    private static (int Points, int Seals, string Items)[] LiveLadder()
    {
        var groups = SmithRewardCalculator.Instance.Groups;
        var items = groups.Select(g => g.Items.Select(i => i.Construct()).ToArray()).ToArray();
        try
        {
            var bestPriced = items.Select(row => row.Select(CatalogPrice).Max()).ToArray();
            var ladder = new (int, int, string)[groups.Length];
            for (var i = 0; i < groups.Length; i++)
            {
                var values = new List<int>();
                foreach (var item in items[i])
                {
                    var price = CatalogPrice(item);
                    if (price == null)
                    {
                        // The midpoint of the best priced item on the nearest priced rung below and above.
                        var lo = Enumerable.Range(0, i).Reverse().Select(j => bestPriced[j]).First(v => v != null)!.Value;
                        var hi = Enumerable.Range(i + 1, groups.Length - i - 1).Select(j => bestPriced[j]).First(v => v != null)!.Value;
                        price = (lo + hi) / 2;
                    }

                    values.Add(price.Value);
                }

                ladder[i] = (groups[i].Points, values.Max(),
                    string.Join(", ", items[i].Select((it, k) => $"{it.GetType().Name} {values[k]}")));
            }

            return ladder;
        }
        finally
        {
            foreach (var item in items.SelectMany(r => r))
            {
                item?.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- deeds

    private static readonly (string Name, Func<SmallBulkEntry[]> Set)[] LargeSets =
    [
        ("ring", () => LargeBulkEntry.LargeRing), ("plate", () => LargeBulkEntry.LargePlate),
        ("chain", () => LargeBulkEntry.LargeChain), ("axes", () => LargeBulkEntry.LargeAxes),
        ("fencing", () => LargeBulkEntry.LargeFencing), ("maces", () => LargeBulkEntry.LargeMaces),
        ("polearms", () => LargeBulkEntry.LargePolearms), ("swords", () => LargeBulkEntry.LargeSwords)
    ];

    private static readonly BulkMaterialType[] Metals =
    [
        BulkMaterialType.None, BulkMaterialType.DullCopper, BulkMaterialType.ShadowIron, BulkMaterialType.Copper,
        BulkMaterialType.Bronze, BulkMaterialType.Gold, BulkMaterialType.Agapite, BulkMaterialType.Verite,
        BulkMaterialType.Valorite, BulkMaterialType.Platinum, BulkMaterialType.Toxic, BulkMaterialType.Blaze,
        BulkMaterialType.Frost, BulkMaterialType.Obsidian, BulkMaterialType.Mythril, BulkMaterialType.Adamantium,
        BulkMaterialType.Celestial
    ];

    private static Item Small(int amount, bool exceptional, BulkMaterialType mat) =>
        new SmallSmithBOD(amount, amount, typeof(PlateChest), 1025141, 0x1415, exceptional, mat);

    private static Item Large(string set, int amount, bool exceptional, BulkMaterialType mat)
    {
        var deed = new LargeSmithBOD(amount, exceptional, mat, null);
        deed.Entries = LargeBulkEntry.ConvertEntries(deed, LargeSets.Single(s => s.Name == set).Set());
        foreach (var entry in deed.Entries)
        {
            entry.Amount = amount;
        }

        return deed;
    }

    private static IEnumerable<(string Name, Func<Item> Make)> EveryShape()
    {
        foreach (var amount in new[] { 10, 15, 20 })
        {
            foreach (var exceptional in new[] { false, true })
            {
                foreach (var mat in Metals)
                {
                    var tag = $"{amount} {(exceptional ? "exc" : "reg")} {mat}";
                    yield return ($"small {tag}", () => Small(amount, exceptional, mat));
                    foreach (var (set, _) in LargeSets)
                    {
                        yield return ($"large {set} {tag}", () => Large(set, amount, exceptional, mat));
                    }
                }
            }
        }
    }

    private static int OsiPoints(Item deed) => deed switch
    {
        SmallBOD s => SmithRewardCalculator.Instance.ComputePoints(s),
        LargeBOD l => SmithRewardCalculator.Instance.ComputePoints(l),
        _          => throw new ArgumentException(deed.GetType().Name)
    };

    private static int OsiGold(Item deed, double roll)
    {
        BuiltInRng.Generator = new ConstantRandom { Value = roll };
        try
        {
            return ((BaseBOD)deed).ComputeGold();
        }
        finally
        {
            BuiltInRng.Reset();
        }
    }

    private static int Bank(Item deed, double roll)
    {
        BuiltInRng.Generator = new ConstantRandom { Value = roll };
        try
        {
            return BlacksmithGuildmaster.ComputeGuildReward(deed).seals;
        }
        finally
        {
            BuiltInRng.Reset();
        }
    }

    // What OSI pays a non-member, in Seals: the gold at the guild's rate and the best item of the deed's rung.
    private static double OsiValue(Item deed, double roll, (int Points, int Seals, string Items)[] ladder, out int gold, out int item)
    {
        gold = OsiGold(deed, roll);
        var group = SmithRewardCalculator.Instance.LookupRewards(OsiPoints(deed));
        item = ladder.Single(r => r.Points == group.Points).Seals;
        return gold * 3 / 400.0 + item;
    }

    private static double CashOutValue(int osiGold, int osiItem) =>
        (int)(osiGold * ClusterFSmithBODPayout.CashOutGoldShare) * 3 / 400.0 + osiItem * ClusterFSmithBODPayout.CashOutItemChance;

    // ---------------------------------------------------------------- A1

    [Fact]
    public void TheBanksLadderIsOsisLadderAtCatalogPrices()
    {
        var live = LiveLadder();
        foreach (var (points, seals, items) in live)
        {
            _out.WriteLine($"rung {points}: {seals} Seals ({items})");
        }

        Assert.Equal(live.Select(r => (r.Points, r.Seals)).ToArray(), BlacksmithGuildmaster.OsiRungs);

        // cc-P55 Part H's valuation of the 115 power scroll is kept: the midpoint of the +10 and +15 hammers.
        Assert.Equal(150, live.Single(r => r.Points == 800).Seals);
    }

    // ---------------------------------------------------------------- A2

    [Fact]
    public void EveryDeedBanksAtLeastWhatOsiPaysAndCashOutLess()
    {
        var ladder = LiveLadder();
        var shapes = 0;
        var tightest = (Name: "", Margin: double.MaxValue);
        var failures = new List<string>();

        foreach (var (name, make) in EveryShape())
        {
            var deed = make();
            try
            {
                var bank = Bank(deed, Lowest);
                var osi = OsiValue(deed, Highest, ladder, out var gold, out var item);
                var cash = CashOutValue(gold, item);
                shapes++;

                if (bank < osi)
                {
                    failures.Add($"{name}: bank {bank} < OSI {osi:F2}");
                }

                if (!(osi > cash))
                {
                    failures.Add($"{name}: OSI {osi:F2} <= cash out {cash:F2}");
                }

                if (bank - osi < tightest.Margin)
                {
                    tightest = (name, bank - osi);
                }
            }
            finally
            {
                deed.Delete();
            }
        }

        _out.WriteLine($"{shapes} deed shapes; tightest margin {tightest.Margin:F2} Seals ({tightest.Name}); failures {failures.Count}");
        foreach (var f in failures.Take(20))
        {
            _out.WriteLine(f);
        }

        Assert.Equal(918, shapes);
        Assert.Empty(failures);
    }

    // ---------------------------------------------------------------- A3

    public static IEnumerable<object[]> TableRows() =>
    [
        ["small iron 10 regular", (Func<Item>)(() => Small(10, false, BulkMaterialType.None))],
        ["small iron 20 exceptional", (Func<Item>)(() => Small(20, true, BulkMaterialType.None))],
        ["small Gold 20 exceptional", (Func<Item>)(() => Small(20, true, BulkMaterialType.Gold))],
        ["small Valorite 20 exceptional", (Func<Item>)(() => Small(20, true, BulkMaterialType.Valorite))],
        ["large iron plate 20 exceptional", (Func<Item>)(() => Large("plate", 20, true, BulkMaterialType.None))],
        ["large Valorite ring 20 exceptional", (Func<Item>)(() => Large("ring", 20, true, BulkMaterialType.Valorite))],
        ["large Valorite plate 20 exceptional", (Func<Item>)(() => Large("plate", 20, true, BulkMaterialType.Valorite))],
        ["large Valorite swords 20 exceptional", (Func<Item>)(() => Large("swords", 20, true, BulkMaterialType.Valorite))],
        ["small Platinum 20 exceptional", (Func<Item>)(() => Small(20, true, BulkMaterialType.Platinum))],
        ["large Platinum plate 20 exceptional", (Func<Item>)(() => Large("plate", 20, true, BulkMaterialType.Platinum))],
        ["small Celestial 10 regular", (Func<Item>)(() => Small(10, false, BulkMaterialType.Celestial))],
        ["small Celestial 20 exceptional", (Func<Item>)(() => Small(20, true, BulkMaterialType.Celestial))],
        ["large Celestial plate 20 exceptional", (Func<Item>)(() => Large("plate", 20, true, BulkMaterialType.Celestial))]
    ];

    [Theory]
    [MemberData(nameof(TableRows))]
    public void TheTableRowsBankAtLeastOsiAndCashOutLess(string name, Func<Item> make)
    {
        var ladder = LiveLadder();
        var deed = make();
        try
        {
            var bank = Bank(deed, 0.5);
            var osi = OsiValue(deed, 0.5, ladder, out var gold, out var item);
            var cash = CashOutValue(gold, item);
            _out.WriteLine($"{name}: OSI {gold:N0} gold + item {item:N0} = {osi:N1} Seals; bank {bank:N0} " +
                           $"(roll {Bank(deed, Lowest):N0} to {Bank(deed, Highest):N0}); cash out {cash:N1}");

            Assert.True(bank >= osi, $"{name}: bank {bank} < OSI {osi:F1}");
            Assert.True(osi > cash, $"{name}: OSI {osi:F1} <= cash out {cash:F1}");
        }
        finally
        {
            deed.Delete();
        }
    }

    // ---------------------------------------------------------------- A4

    [Fact]
    public void PostValoriteNeverBanksLessThanValoriteAndTheMetalsBankInOrder()
    {
        var postValorite = Metals.Where(m => (int)m >= (int)BulkMaterialType.Platinum).ToArray();
        foreach (var amount in new[] { 10, 15, 20 })
        {
            foreach (var exceptional in new[] { false, true })
            {
                foreach (var set in new string?[] { null }.Concat(LargeSets.Select(s => s.Name)))
                {
                    Item Make(BulkMaterialType m) => set == null ? Small(amount, exceptional, m) : Large(set, amount, exceptional, m);

                    var valorite = Make(BulkMaterialType.Valorite);
                    var last = Bank(valorite, 0.5);
                    valorite.Delete();

                    foreach (var mat in postValorite)
                    {
                        var deed = Make(mat);
                        var bank = Bank(deed, 0.5);
                        deed.Delete();
                        Assert.True(bank >= last,
                            $"{set ?? "small"} {amount} {(exceptional ? "exc" : "reg")} {mat}: {bank} below the metal before ({last})");
                        last = bank;
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------- A5

    [Fact]
    public void ExistingSealBalancesAreUntouched()
    {
        // The saved record: the balance reads back as saved (the save format did not change).
        var g = new CharacterGuildData();
        g.JoinedGuilds.Add("smithing");
        g.AddCurrency("smithing", 1_234);
        var buffer = new byte[4096];
        var w = new BufferWriter(buffer, true);
        g.Serialize(w);
        w.Flush();
        var copy = new CharacterGuildData(new BufferReader(buffer));
        Assert.Equal(1_234, copy.GetCurrency("smithing"));

        // A turn-in adds the deed's Seals to an existing balance and nothing else.
        var account = new Account($"p56a{Guid.NewGuid():N}"[..16], "p56-test-only");
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(1530, 1530, 0), Map.Trammel);
        var guild = ClusterFAccountPersistence.GetOrCreateGuild(pm);
        guild.JoinedGuilds.Add("smithing");
        guild.AddCurrency("smithing", 1_234);

        var deed = (SmallSmithBOD)Small(20, true, BulkMaterialType.Valorite);
        pm.Backpack.DropItem(deed);
        try
        {
            BuiltInRng.Generator = new ConstantRandom { Value = 0.5 };
            var seals = BlacksmithGuildmaster.ComputeGuildReward(deed).seals;
            Assert.True(ClusterFSmithBODPayout.Pay(pm, deed, false, null));
            BuiltInRng.Reset();

            _out.WriteLine($"1,234 Seals + a small Valorite exceptional 20 ({seals}) = {guild.GetCurrency("smithing")}");
            Assert.Equal(1_234 + seals, guild.GetCurrency("smithing"));
        }
        finally
        {
            BuiltInRng.Reset();
            deed.Delete();
            ClusterFAccountPersistence.Get(account)?.ClearGuildData();
            pm.Delete();
            Accounts.Remove(account);
        }
    }
}
