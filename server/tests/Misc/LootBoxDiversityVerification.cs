// LootBoxDiversityVerification.cs
//
// cc-P763 (P759's approved plan, shard-migration notes/cc-P759-loot-plan.md; notes/cc-P763-cont-loot-boxes-diversified.md).
//
// Facts:
//   1. Every theme at every grade and every rarity rolls with no empty slot: the theme slot gives something, every pool
//      slot an item, an Epic or Legendary box its list, and none of it is a placeholder, a loop fragment or an epic
//      component (the "does nothing" items).
//   2. Every entry of the table can be made (types and specials), the default table round-trips, and a bad entry refuses
//      the file whole.
//   3. Seasonal pools roll only in their window: Halloween, winter and spring add a slot and their name; out of season none.
//   4. The rarity odds and bonuses come from the json: an odds row and a bonus row edited in a file change the roll.
//   5. The box's name and colour: season, rarity, grade and theme; the rarity's hue.
//   6. Recipes only for the opener's crafts (50 or more), else a plain resource.
//   7. The Bags of Holding's caps (no weight cap of their own), and the Reagent Bag's reagents-only rule and weight cut,
//      which reaches the carrier; a Legendary Scholar's bag cuts 90 percent.
//   8. The Fisher, Sailor, Herbalist, Society, Delver and League Dive sources each award once (a later loop: nothing).
//   9. The joke items are 3 percent of Cosmetics and in no other pool; helpful items are 95 percent or more of each.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.Plants;
using Server.Items;
using Server.LootBoxes;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LootBoxDiversityVerification
{
    private readonly ITestOutputHelper _out;

    private static bool _startupHooksRun;

    private static readonly DateTime NoSeason = new(2026, 6, 15);

    public LootBoxDiversityVerification(ITestOutputHelper output)
    {
        _out = output;
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }

        LootTable.Current = LootTable.Defaults;
        FanfareLines.Current = FanfareLines.Defaults;
        ClusterFLootBoxes.LoopProvider = _ => 1;
        ClusterFLootBoxes.RarityRoll = _ => BoxRarity.Common;
        ClusterFLootBoxes.SeasonClock = () => NoSeason;
        ClusterFFanfare.FlushAll();
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account;
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player()
        {
            Account = new Account($"p763{Guid.NewGuid():N}"[..16], "p763-test-only");
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1400, 1400, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public List<LootBox> Boxes() => Pm.Backpack.Items.OfType<LootBox>().Concat(Pm.BankBox.Items.OfType<LootBox>()).ToList();

        public void Dispose()
        {
            ClusterFFanfare.FlushAll();
            foreach (var box in Boxes())
            {
                box.Delete();
            }

            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static bool DoesNothing(Item item) => item is LootBoxPlaceholder or LoopScrollFragment or EpicComponent;

    private static void DeleteAll(LootBoxRoller.Rolled r)
    {
        foreach (var (item, _) in r.Items)
        {
            item.Delete();
        }
    }

    [Fact]
    public void EveryThemeGradeAndRarityRollsWithNoEmptySlotAndNothingThatDoesNothing()
    {
        using var p = new Player();
        p.Pm.Skills[SkillName.Blacksmith].Base = 60;
        p.Pm.Skills[SkillName.Mining].Base = 70;
        var t = LootTable.Current;
        var rolls = 0;
        var made = new SortedDictionary<string, int>();

        foreach (var theme in Enum.GetValues<BoxTheme>())
        {
            for (var grade = 1; grade <= 6; grade++)
            {
                foreach (var rarity in Enum.GetValues<BoxRarity>())
                {
                    for (var n = 0; n < 3; n++)
                    {
                        var r = LootBoxRoller.Roll(p.Pm, theme, grade, null, "a test", rarity);
                        rolls++;
                        try
                        {
                            var at = $"{theme} grade {grade} {rarity}";
                            var poolSlots = t.SlotsFor(grade) - 2 + t.RarityOf(rarity).ExtraSlots;
                            Assert.True(poolSlots == r.Pools.Count, $"{at}: {r.Pools.Count} pool slots, expected {poolSlots}");

                            // Every pool slot gives an item, and the theme slot at least one more (League's and the
                            // League Dive's Renown bonus is not an item, so theirs is the cloak or the potions).
                            Assert.True(r.Items.Count >= poolSlots + 1, $"{at}: {r.Items.Count} items for {poolSlots} pool slots");
                            Assert.True(r.Gold > 0, $"{at}: no gold");

                            if (rarity >= BoxRarity.Epic)
                            {
                                Assert.True(r.Items.Count(i => i.Rare) >= 1, $"{at}: nothing from the {rarity} list or the Rare pool");
                            }

                            foreach (var (item, _) in r.Items)
                            {
                                Assert.False(item.Deleted, $"{at}: a deleted {item.GetType().Name}");
                                Assert.False(DoesNothing(item), $"{at}: {item.GetType().Name} does nothing on the shard");
                                made[item.GetType().Name] = made.GetValueOrDefault(item.GetType().Name) + 1;
                            }
                        }
                        finally
                        {
                            DeleteAll(r);
                        }
                    }
                }
            }
        }

        _out.WriteLine($"{rolls} rolls; {made.Count} kinds of item: " + string.Join(", ", made.Select(kv => $"{kv.Key} {kv.Value}")));
        Assert.Equal(Enum.GetValues<BoxTheme>().Length * 6 * 4 * 3, rolls);
    }

    [Fact]
    public void EveryEntryCanBeMadeTheDefaultsRoundTripAndABadEntryRefusesTheFile()
    {
        var t = LootTable.Defaults;
        var entries = new List<(string Where, PoolEntry E)>();
        foreach (var (pool, list) in t.Pools)
        {
            entries.AddRange(list.Select(e => ($"pool {pool}", e)));
        }

        foreach (var (theme, def) in t.Themes)
        {
            entries.AddRange(def.Slot.Concat(def.Epic).Concat(def.Legendary).Select(e => ($"theme {theme}", e)));
        }

        foreach (var (season, def) in t.Seasons)
        {
            entries.AddRange(def.Items.Select(e => ($"season {season}", e)));
        }

        // Every pool, theme and season the plan names has entries.
        Assert.Equal(Enum.GetNames<BoxPool>().OrderBy(x => x), t.Pools.Keys.OrderBy(x => x));
        Assert.Equal(Enum.GetNames<BoxTheme>().OrderBy(x => x), t.Themes.Keys.OrderBy(x => x));
        Assert.Equal(new[] { "halloween", "spring", "winter" }, t.Seasons.Keys.OrderBy(x => x));

        foreach (var (where, e) in entries)
        {
            Assert.True(LootBoxRoller.CheckEntry(e) == null, $"{where}: {e.Item}: {LootBoxRoller.CheckEntry(e)}");
            if (!e.Item.StartsWith('@'))
            {
                var item = AssemblyHandler.FindTypeByName(e.Item)!.CreateInstance<Item>();
                Assert.NotNull(item);
                Assert.False(DoesNothing(item!), $"{where}: {e.Item} does nothing on the shard");
                item!.Delete();
            }
        }

        _out.WriteLine($"{entries.Count} entries, every one can be made.");

        var errors = new List<string>();
        var parsed = LootTable.Parse(LootTable.ToJson(t), "test", errors);
        Assert.True(parsed != null, string.Join("; ", errors));
        Assert.Equal(t.Pools["Rare"].Length, parsed!.Pools["Rare"].Length);
        Assert.Equal(t.RarityOdds[5], parsed.RarityOdds[5]);

        // A file from before cc-P763 (no Tools row, no contents) still loads, with the defaults for what it leaves out.
        errors.Clear();
        var old = LootTable.Parse("{ \"PoolWeights\": { \"Consumables\": [50,40,30,20,15,10], \"Resources\": [30,30,25,20,15,10], " +
                                  "\"MagicGear\": [15,20,25,25,25,20], \"Recipes\": [5,8,12,15,15,15], \"Cosmetics\": [0,2,6,12,15,20], " +
                                  "\"Rare\": [0,0,2,8,15,25] } }", "old", errors);
        Assert.True(old != null, string.Join("; ", errors));
        Assert.Equal(8, old!.Weight(BoxPool.Tools, 1));

        // An entry naming no item refuses the file whole, and the table in force stays.
        var bad = LootTable.Apply("{ \"Pools\": { \"Tools\": [ { \"Item\": \"NoSuchItemAnywhere\" } ] } }", "bad");
        Assert.Contains("REFUSED", bad[0]);
        Assert.Contains(bad, l => l.Contains("NoSuchItemAnywhere"));
        Assert.Same(LootTable.Defaults, LootTable.Current);

        var badSpecial = LootTable.Apply("{ \"Pools\": { \"Tools\": [ { \"Item\": \"@NoSuchSpecial\" } ] } }", "bad");
        Assert.Contains("REFUSED", badSpecial[0]);
        Assert.Same(LootTable.Defaults, LootTable.Current);
    }

    [Fact]
    public void SeasonalPoolsRollOnlyInTheirWindow()
    {
        using var p = new Player();
        var t = LootTable.Current;
        var cases = new (DateTime Day, string? Season)[]
        {
            (new DateTime(2026, 10, 30), "halloween"), (new DateTime(2026, 11, 2), "halloween"), (new DateTime(2026, 11, 3), null),
            (new DateTime(2026, 12, 25), "winter"), (new DateTime(2027, 1, 6), "winter"), (new DateTime(2027, 1, 7), null),
            (new DateTime(2027, 3, 20), "spring"), (new DateTime(2027, 4, 20), "spring"), (new DateTime(2027, 4, 21), null),
            (NoSeason, null),
        };

        foreach (var (day, season) in cases)
        {
            ClusterFLootBoxes.SeasonClock = () => day;
            Assert.True(season == ClusterFLootBoxes.ActiveSeason(), $"{day:MM-dd}: {ClusterFLootBoxes.ActiveSeason() ?? "none"}, not {season ?? "none"}");

            for (var n = 0; n < 6; n++)
            {
                var box = ClusterFLootBoxes.MakeBox(p.Pm, BoxTheme.Explorer, 2, null, "a test", null);
                try
                {
                    var plain = t.SlotsFor(2) - 2 + 2; // the pool slots, the treasure map and the blank map
                    if (season == null)
                    {
                        Assert.Equal(plain, box.Contents.Count);
                        Assert.DoesNotContain("Halloween", box.DefaultName);
                        continue;
                    }

                    var def = t.Seasons[season];
                    Assert.StartsWith(def.Label + " ", box.DefaultName);
                    Assert.Equal(plain + 1, box.Contents.Count);

                    // The last item is the season's, from its own pool.
                    var last = box.Contents[^1];
                    var types = def.Items.Where(e => !e.Item.StartsWith('@')).Select(e => e.Item).ToHashSet();
                    var fromPool = types.Contains(last.GetType().Name) ||
                                   last is LootDecor && def.Items.Any(e => e.Item == "@Decor") ||
                                   last is Seed && def.Items.Any(e => e.Item == "@Seed");
                    Assert.True(fromPool, $"{season}: {last.GetType().Name} is not from its pool");
                }
                finally
                {
                    box.Delete();
                }
            }
        }
    }

    [Fact]
    public void RarityOddsAndBonusesComeFromTheJson()
    {
        using var p = new Player();
        var d = LootTable.Defaults;

        // The plan's odds, by the cumulative rows.
        Assert.Equal(BoxRarity.Common, d.RarityForRoll(1, 84.9));
        Assert.Equal(BoxRarity.Rare, d.RarityForRoll(1, 85.0));
        Assert.Equal(BoxRarity.Epic, d.RarityForRoll(1, 99.9));
        Assert.Equal(BoxRarity.Legendary, d.RarityForRoll(2, 99.6));
        Assert.Equal(BoxRarity.Legendary, d.RarityForRoll(6, 90.0));
        Assert.Equal(BoxRarity.Epic, d.RarityForRoll(6, 89.9));

        // A file's odds row and bonus row change the roll.
        var lines = LootTable.Apply(
            "{ \"RarityOdds\": [[0,0,0,100],[75,20,4.5,0.5],[65,25,8,2],[50,32,14,4],[40,35,18,7],[25,40,25,10]], " +
            "\"RarityBonus\": [ { \"Name\": \"Common\", \"Hue\": 1 }, { \"Name\": \"Rare\", \"Hue\": 2, \"ExtraSlots\": 1 }, " +
            "{ \"Name\": \"Epic\", \"Hue\": 3, \"ExtraSlots\": 1, \"Fanfare\": \"Major\" }, " +
            "{ \"Name\": \"Mythic\", \"Hue\": 1234, \"ExtraSlots\": 3, \"StackMultiplier\": 4, \"Fanfare\": \"Capstone\" } ] }", "test");
        try
        {
            Assert.StartsWith("Loaded", lines[0]);
            Assert.Equal(BoxRarity.Legendary, LootTable.Current.RarityForRoll(1, 0));

            ClusterFLootBoxes.RarityRoll = g => LootTable.Current.RarityForRoll(g, Utility.RandomDouble() * 100);
            var box = ClusterFLootBoxes.MakeBox(p.Pm, BoxTheme.Explorer, 1, null, "a test", null);
            try
            {
                Assert.Equal(BoxRarity.Legendary, box.Rarity);
                Assert.Equal(1234, box.Hue);
                Assert.StartsWith("Mythic Plain Explorer's", box.DefaultName);
                Assert.Equal(LootTable.Defaults.Gold[0] * 4, box.Gold);
                Assert.Equal(FanfareLevel.Capstone, ClusterFLootBoxes.LevelFor(1, box.Rarity));
            }
            finally
            {
                box.Delete();
            }

            var r = LootBoxRoller.Roll(p.Pm, BoxTheme.Explorer, 1, null, "a test", BoxRarity.Legendary);
            Assert.Equal(LootTable.Current.SlotsFor(1) - 2 + 3, r.Pools.Count);
            DeleteAll(r);
        }
        finally
        {
            LootTable.Current = LootTable.Defaults;
        }

        // A row that does not sum to 100 refuses the file.
        var refused = LootTable.Apply("{ \"RarityOdds\": [[50,0,0,0],[75,20,4.5,0.5],[65,25,8,2],[50,32,14,4],[40,35,18,7],[25,40,25,10]] }", "bad");
        Assert.Contains("REFUSED", refused[0]);
        Assert.Same(LootTable.Defaults, LootTable.Current);
    }

    [Fact]
    public void TheBoxNameAndColourPutTheSeasonRarityGradeAndThemeTogether()
    {
        using var p = new Player();
        var t = LootTable.Current;

        var epic = ClusterFLootBoxes.MakeBox(p.Pm, BoxTheme.Explorer, 3, null, "a test", null, BoxRarity.Epic);
        var rare = ClusterFLootBoxes.MakeBox(p.Pm, BoxTheme.Miner, 4, null, "a test", null, BoxRarity.Rare);
        var common = ClusterFLootBoxes.MakeBox(p.Pm, BoxTheme.Fisher, 1, null, "a test", null, BoxRarity.Common);
        ClusterFLootBoxes.SeasonClock = () => new DateTime(2026, 10, 30);
        var spooky = ClusterFLootBoxes.MakeBox(p.Pm, BoxTheme.Explorer, 3, null, "a test", null, BoxRarity.Epic);
        try
        {
            Assert.Equal("Epic Fine Explorer's Box", epic.DefaultName);
            Assert.Equal(t.RarityOf(BoxRarity.Epic).Hue, epic.Hue);
            Assert.Equal("Rare Valorite Miner's Box", rare.DefaultName);
            Assert.Equal(t.RarityOf(BoxRarity.Rare).Hue, rare.Hue);
            Assert.Equal("Common Plain Fisher's Box", common.DefaultName);
            Assert.Equal("Halloween Epic Fine Explorer's Box", spooky.DefaultName);

            // The fanfare: Major from grade 4 as before, and at least the rarity's.
            Assert.Equal(FanfareLevel.Minor, ClusterFLootBoxes.LevelFor(3, BoxRarity.Rare));
            Assert.Equal(FanfareLevel.Major, ClusterFLootBoxes.LevelFor(1, BoxRarity.Epic));
            Assert.Equal(FanfareLevel.Major, ClusterFLootBoxes.LevelFor(4, BoxRarity.Common));
            Assert.Equal(FanfareLevel.Capstone, ClusterFLootBoxes.LevelFor(1, BoxRarity.Legendary));
        }
        finally
        {
            epic.Delete();
            rare.Delete();
            common.Delete();
            spooky.Delete();
        }
    }

    // Only the Recipes pool, for the recipe fact.
    private const string RecipesOnly =
        "{ \"PoolWeights\": { \"Consumables\": [0,0,0,0,0,0], \"Resources\": [0,0,0,0,0,0], \"MagicGear\": [0,0,0,0,0,0], " +
        "\"Recipes\": [100,100,100,100,100,100], \"Cosmetics\": [0,0,0,0,0,0], \"Rare\": [0,0,0,0,0,0], \"Tools\": [0,0,0,0,0,0] } }";

    [Fact]
    public void RecipesOnlyForTheOpenersCrafts()
    {
        Assert.StartsWith("Loaded", LootTable.Apply(RecipesOnly, "test")[0]);
        using var p = new Player();
        try
        {
            // No craft at 50: a plain resource, never a recipe scroll.
            for (var n = 0; n < 20; n++)
            {
                var r = LootBoxRoller.Roll(p.Pm, BoxTheme.Explorer, 6, null, "a test");
                Assert.DoesNotContain(r.Items, i => i.Item is RecipeScroll);
                DeleteAll(r);
            }

            // Blacksmithing at 60: a stock scroll is a Blacksmithing one.
            p.Pm.Skills[SkillName.Blacksmith].Base = 60;
            var scrolls = 0;
            for (var n = 0; n < 40; n++)
            {
                var r = LootBoxRoller.Roll(p.Pm, BoxTheme.Explorer, 6, null, "a test");
                foreach (var (item, _) in r.Items)
                {
                    if (item is RecipeScroll scroll)
                    {
                        scrolls++;
                        Assert.True(LootBoxRoller.CanUseRecipe(p.Pm, scroll.RecipeID), $"recipe {scroll.RecipeID} is not one the opener can use");
                    }
                }

                DeleteAll(r);
            }

            _out.WriteLine($"{scrolls} stock recipe scrolls, every one Blacksmithing.");
        }
        finally
        {
            LootTable.Current = LootTable.Defaults;
        }
    }

    [Fact]
    public void BagsOfHoldingHaveTheirCapsAndTheReagentBagHoldsOnlyReagentsAndCutsTheirWeight()
    {
        using var p = new Player();
        var caps = new[] { 12, 24, 36, 48, 125 };
        var names = new[] { "Lesser Bag of Holding", "Bag of Holding", "Greater Bag of Holding", "Legendary Bag of Holding", "Grand Bag of Holding" };
        for (var size = 1; size <= 5; size++)
        {
            var bag = new BagOfHolding(size);
            try
            {
                Assert.Equal(caps[size - 1], bag.MaxItems);
                Assert.Equal(0, bag.MaxWeight);
                Assert.Equal(names[size - 1], bag.DefaultName);
            }
            finally
            {
                bag.Delete();
            }
        }

        // A Lesser bag holds 12, refuses the 13th, and takes a heavy thing its pack can carry.
        var lesser = new BagOfHolding(1);
        p.Pm.Backpack.DropItem(lesser);
        for (var i = 0; i < 12; i++)
        {
            lesser.DropItem(new Apple());
        }

        Assert.False(lesser.CheckHold(p.Pm, new Apple(), false, true, 0, 0));
        lesser.Delete();

        // The Reagent Bag: reagents only; their weight cut, and the cut reaches the pack.
        var regBag = new ReagentBag(90);
        p.Pm.Backpack.DropItem(regBag);
        var shirt = new Shirt();
        Assert.False(regBag.CheckHold(p.Pm, shirt, false, true, 0, 0));
        shirt.Delete();
        var pearls = new BlackPearl(100);
        Assert.True(regBag.CheckHold(p.Pm, pearls, false, true, 0, 0));
        var packBefore = p.Pm.Backpack.TotalWeight;
        regBag.DropItem(pearls);
        var raw = pearls.PileWeight + pearls.TotalWeight;
        Assert.Equal(raw - raw * 90 / 100, regBag.TotalWeight);
        Assert.Equal(packBefore + regBag.TotalWeight, p.Pm.Backpack.TotalWeight);
        _out.WriteLine($"100 black pearls weigh {raw} loose and {regBag.TotalWeight} in the bag.");
        regBag.Delete();

        // A Legendary Scholar's box gives the bag at its 90 percent; a Common one's, when it comes, 25.
        var legendary = LootBoxRoller.Roll(p.Pm, BoxTheme.Scholar, 6, null, "a test", BoxRarity.Legendary);
        var given = legendary.Items.Select(i => i.Item).OfType<ReagentBag>().ToList();
        Assert.NotEmpty(given);
        Assert.All(given, b => Assert.Equal(90, b.WeightCut));
        DeleteAll(legendary);
    }

    [Fact]
    public void TheNewSourcesEachAwardOnce()
    {
        using var p = new Player();
        var pm = p.Pm;

        int BoxesOf(BoxTheme theme) => p.Boxes().Count(b => b.Theme == theme);

        // Guilds, first (the first check of any kind records the ranks held): those ranks give nothing; a promotion after
        // it gives its theme's box.
        var guild = ClusterFAccountPersistence.GetOrCreateGuild(pm);
        guild.JoinedGuilds.Add("healers");
        guild.AddReputation("healers", 2_000);
        var heldRank = ClusterFGuildSystem.GuildRankIndex("healers", guild);
        Assert.Equal(0, ClusterFLootBoxes.CheckGuildRanks(pm));
        guild.AddReputation("healers", 1_000_000);
        var newRank = ClusterFGuildSystem.GuildRankIndex("healers", guild);
        Assert.True(newRank > heldRank, $"healers rank {heldRank} to {newRank}");
        Assert.Equal(newRank - heldRank, ClusterFLootBoxes.CheckGuildRanks(pm));
        Assert.Equal(newRank - heldRank, BoxesOf(BoxTheme.Herbalist));
        Assert.Equal(0, ClusterFLootBoxes.CheckGuildRanks(pm));

        // Fisher: a High Seas fish's first catch of its kind; a stock fish gives nothing.
        var fish = new Amberjack();
        ClusterFLootBoxes.OnFished(pm, fish);
        ClusterFLootBoxes.OnFished(pm, fish);
        var plain = new Fish();
        ClusterFLootBoxes.OnFished(pm, plain);
        Assert.Equal(1, BoxesOf(BoxTheme.Fisher));
        fish.Delete();
        plain.Delete();

        // Sailor: an SOS chest's first recovery at each level.
        ClusterFLootBoxes.OnSosRecovered(pm, 2);
        ClusterFLootBoxes.OnSosRecovered(pm, 2);
        ClusterFLootBoxes.OnSosRecovered(pm, 3);
        Assert.Equal(2, BoxesOf(BoxTheme.Sailor));
        Assert.Contains(p.Boxes(), b => b.Theme == BoxTheme.Sailor && b.Tier == LootTable.Current.SosGrade[2]);

        // Herbalist: a plant type first set to decorative.
        var herbalists = BoxesOf(BoxTheme.Herbalist);
        var plant = new PlantItem();
        ClusterFLootBoxes.OnPlantDecorative(pm, plant);
        ClusterFLootBoxes.OnPlantDecorative(pm, plant);
        Assert.Equal(herbalists + 1, BoxesOf(BoxTheme.Herbalist));
        plant.Delete();

        // Delver: a boss's first kill inside a dungeon, beside its Boss box; outside a dungeon, only the Boss box.
        ClusterFLootBoxes.OnBossKilled(pm, typeof(Balron), "a balron", "Despise");
        ClusterFLootBoxes.OnBossKilled(pm, typeof(Balron), "a balron", "Despise");
        ClusterFLootBoxes.OnBossKilled(pm, typeof(Lich), "a lich");
        Assert.Equal(1, BoxesOf(BoxTheme.Delver));
        Assert.Equal(2, BoxesOf(BoxTheme.Boss));

        // The Boss box's trophy is a real piece of decor named for the boss.
        var balronBox = p.Boxes().First(b => b.Theme == BoxTheme.Boss && b.EarnedFor == "slaying a balron");
        Assert.Contains(balronBox.Contents, i => i is LootDecor && i.Name == "Trophy of a balron");

        // League Dive: the tenth work order, not the ninth.
        for (var i = 0; i < 9; i++)
        {
            Assert.Null(ClusterFLootBoxes.OnWorkOrderCompleted(pm));
        }

        Assert.Equal(0, BoxesOf(BoxTheme.LeagueDive));
        Assert.Equal(ClusterFLootBoxes.AwardResult.Box, ClusterFLootBoxes.OnWorkOrderCompleted(pm));
        Assert.Equal(1, BoxesOf(BoxTheme.LeagueDive));

        // The maritime guild alternates Fisher and Sailor; a guild the table does not name gives none.
        Assert.Equal(BoxTheme.Fisher, LootTable.Current.ThemeForGuildRank("maritime", 1));
        Assert.Equal(BoxTheme.Sailor, LootTable.Current.ThemeForGuildRank("maritime", 2));
        Assert.Equal(BoxTheme.Society, LootTable.Current.ThemeForGuildRank("smithing", 3));
        Assert.Null(LootTable.Current.ThemeForGuildRank("thieves", 1));

        // A later loop: the first-time sources give nothing again, not even the repeat line.
        ClusterFLootBoxes.LoopProvider = _ => 2;
        var before = p.Boxes().Count;
        Assert.Equal(ClusterFLootBoxes.AwardResult.AlreadyThisLoop,
            ClusterFLootBoxes.AwardOnce(pm, "sos:2", BoxTheme.Sailor, 3, null, "again", out _));
        ClusterFLootBoxes.OnSosRecovered(pm, 2);
        Assert.Equal(before, p.Boxes().Count);

        // The per-key achievement theme: the one fishing achievement goes to Fisher, others by category.
        Assert.Equal(BoxTheme.Fisher, LootTable.Current.ThemeForAchievement("joke.sole_survivor", "Mischief"));
        Assert.Equal(BoxTheme.Hunter, LootTable.Current.ThemeForAchievement("combat.first_blood", "Combat"));
    }

    [Fact]
    public void JokeItemsAreThreePercentOfCosmeticsAndInNoOtherPool()
    {
        foreach (var (pool, entries) in LootTable.Defaults.Pools)
        {
            var total = entries.Sum(e => e.Weight);
            var joke = entries.Where(e => !e.Item.StartsWith('@') &&
                                          typeof(INotedJoke).IsAssignableFrom(AssemblyHandler.FindTypeByName(e.Item))).Sum(e => e.Weight);
            var share = 100.0 * joke / total;
            _out.WriteLine($"{pool}: jokes {joke} of {total} ({share:F1}%)");
            Assert.True(share <= 5.0, $"{pool}: jokes are {share:F1}%");
            if (pool == "Cosmetics")
            {
                Assert.InRange(share, 2.5, 3.5);
            }
            else
            {
                Assert.Equal(0, joke);
            }
        }
    }
}
