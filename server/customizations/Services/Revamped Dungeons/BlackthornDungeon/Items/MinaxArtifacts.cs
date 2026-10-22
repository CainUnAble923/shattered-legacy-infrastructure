// cc-P196: the "artifacts bearing the crest of Minax", the Blackthorn Dungeon's turn-in currency.
//
// ServUO makes one with RunicReforging.GenerateRandomItem(item, killer, budget, luck, ReforgedPrefix.None,
// ReforgedSuffix.Minax) (RunicReforging.cs:1893-2070): a random weapon, armour, shield, jewel or hat (both
// emulators' RandomArmorOrShieldOrWeaponOrJewelry pools draw hats) gets a budget of
// random properties and the Minax suffix, which hues it 1157 (:2064), names it "~1_ITEM~ of Minax" (1154507, the
// suffix name table :1725) and is what the Agent of the Crown accepts (AgentOfTheCrown.IsRedeemableItem reads the
// suffix off BaseWeapon / BaseArmor / BaseJewel / BaseClothing). Pinned ModernUO has no reforging and no suffix on
// its equipment, so this file carries the three things the dungeon needs of it:
//
//   1. The property roll. ServUO's budget arithmetic is reproduced (Create: the divisor, the luck-weighted spread,
//      the Felucca bonus, the powerful threshold at 550, the property count GetProperties(max) with its weights,
//      the intensity bands), and the properties themselves are laid on by pinned's BaseRunicTool.ApplyAttributesTo
//      (the shard's random-loot path, so F-27's Self Repair rule holds). ServUO draws from its own reforging tables
//      and can add a negative property (antique, brittle); pinned's tables are the runic ones. A deviation (notes).
//   2. The mark. The item is hued 1157, remembered here (a registry saved through GenericPersistence
//      "BlackthornMinax"; a turned-in or deleted item leaves it at the next save), and named "<name> of Minax" when
//      the server has the client's Cliloc text (pinned Localization); a stock item has no property-line hook, so
//      the name is the Name string rather than ServUO's cliloc 1154507 line. A deviation (notes).
//   3. The question the Agent asks: IsMinax(item).
//
// Also ours: the [MinaxArtifact staff command (below), so the turn-in can be tested without hunting for the roll.
//
// The two budgets ServUO uses: a kill in the dungeon (BlackthornData.ProcessKill) rolls at
// max(100, RandomItemGenerator.GetDifficultyFor(bc)) = clamp(fame / 29, 150, 700) with the killer's luck chance; the
// invasion (the beacon, a captain) rolls at 700-800 (InvasionController.CreateItem).

using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Blackthorn;

public static class MinaxArtifacts
{
    public const int OfMinax = 1154507; // ~1_ITEM~ of Minax (ServUO RunicReforging name table)
    public const int MinaxHue = 1157;   // RunicReforging.cs:2064

    // ServUO RandomItemGenerator defaults (Config Loot.*): MaxBaseBudget 700, MinBaseBudget 150, MaxProps 11,
    // MaxAdjustedBudget 1450, MinAdjustedBudget 150, FeluccaBudgetBonus 100.
    public const int MaxBaseBudget = 700;
    public const int MinBaseBudget = 150;
    public const int MaxProps = 11;
    public const int MaxAdjustedBudget = 1450;
    public const int MinAdjustedBudget = 150;
    public const int FeluccaBudgetBonus = 100;

    private static readonly HashSet<Item> _items = new();

    public static int Count => _items.Count;

    public static bool IsMinax(Item item) => item != null && !item.Deleted && _items.Contains(item);

    public static void Configure()
    {
        _ = new MinaxArtifactPersistence();
    }

    public static void Initialize()
    {
        CommandSystem.Register("MinaxArtifact", AccessLevel.GameMaster, MinaxArtifact_OnCommand);
    }

    // Ours: a kill's artifact roll is well under one percent, so a tester of the Agent of the Crown needs a way to
    // hold one. [MinaxArtifact [budget] makes one in the caller's pack at the given budget (700, the invasion's floor).
    [ShardCommand(CommandCategory.DevTool, Rerun = CommandRerun.Duplicates, Shard = CommandShard.Any, NoDryRun = true, Summary = "Makes an artifact bearing the crest of Minax in your pack, for testing the Agent of the Crown. Optional budget (150-1450; 700 is the invasion's).")]
    public static void MinaxArtifact_OnCommand(CommandEventArgs e)
    {
        var budget = e.Length > 0 ? Math.Clamp(e.GetInt32(0), MinAdjustedBudget, MaxAdjustedBudget) : 700;
        var item = Create(e.Mobile, budget, 0);

        if (item == null)
        {
            e.Mobile.SendMessage("Nothing came of the roll; try again.");
            return;
        }

        if (!e.Mobile.PlaceInBackpack(item))
        {
            item.MoveToWorld(e.Mobile.Location, e.Mobile.Map);
        }

        e.Mobile.SendMessage($"A Minax artifact ({item.GetType().Name}) at budget {budget} is in your pack.");
    }

    /// <summary>ServUO RandomItemGenerator.GetDifficultyFor: fame / (20500 / 700), clamped to 150..700.</summary>
    public static int GetDifficultyFor(BaseCreature bc)
    {
        if (bc == null || bc.Fame <= 0)
        {
            return 0;
        }

        var budget = Math.Min(MaxBaseBudget, bc.Fame / (20500 / MaxBaseBudget));
        return Math.Max(MinBaseBudget, budget);
    }

    /// <summary>A Minax artifact for a kill in the dungeon (ServUO BlackthornData.ProcessKill).</summary>
    public static Item CreateForKill(BaseCreature dead, Mobile damager) =>
        Create(damager, Math.Max(100, GetDifficultyFor(dead)), LootPack.GetLuckChanceForKiller(dead));

    /// <summary>A Minax artifact from the invasion (ServUO InvasionController.CreateItem): budget 700-800.</summary>
    public static Item CreateForInvasion(Mobile damager) =>
        Create(damager, Utility.RandomMinMax(700, 800), damager is PlayerMobile ? LootPack.GetLuckChance(damager, null) : 0);

    /// <summary>
    ///     A random weapon, armour, shield, jewel or hat with a Minax-sized property roll, marked and registered. Null when
    ///     pinned's loot table gives nothing (as ServUO's CreateItem returns null then).
    /// </summary>
    public static Item Create(Mobile damager, int baseBudget, int luckChance)
    {
        var item = Loot.RandomArmorOrShieldOrWeaponOrJewelry(IsInTokuno(damager), IsMondain(damager));

        if (item == null)
        {
            return null;
        }

        ApplyProperties(item, damager, baseBudget, luckChance);
        Mark(item);
        return item;
    }

    /// <summary>Hues, names and registers an item as a Minax artifact.</summary>
    public static void Mark(Item item)
    {
        if (item == null || item.Deleted)
        {
            return;
        }

        item.Hue = MinaxHue;

        if (item.Name == null && Localization.GetText(item.LabelNumber) is { Length: > 0 } text)
        {
            item.Name = $"{text} of Minax";
        }

        _items.Add(item);
    }

    public static bool Remove(Item item) => item != null && _items.Remove(item);

    /// <summary>
    ///     ServUO RunicReforging.GenerateRandomItem, the non-artifact branch, down to the property count and the two
    ///     intensity bounds; the properties are then pinned's BaseRunicTool's. Public so a test can read the roll.
    /// </summary>
    public static (int Mods, int PercLow, int PercHigh, int Budget) Roll(Mobile killer, int baseBudget, int luckChance)
    {
        var rawLuck = killer?.Luck ?? 0;
        var budgetBonus = killer?.Map?.Rules == MapRules.FeluccaRules ? FeluccaBudgetBonus : 0;
        var divisor = GetDivisor(baseBudget);

        var highest = 0.0;

        for (var i = 0; i < 1 + rawLuck / 600; i++)
        {
            var perc = (100.0 - Math.Sqrt(Utility.RandomMinMax(0, 10000))) / 100.0;

            if (perc > highest)
            {
                highest = perc;
            }
        }

        var toAdd = Math.Min(500, MaxAdjustedBudget - baseBudget);
        var budget = Utility.RandomMinMax(baseBudget - baseBudget / divisor, (int)(baseBudget + toAdd * Math.Min(1.0, highest))) + budgetBonus;

        // ServUO: a rare chance for a high end item to drop on a low budgeted monster.
        if (rawLuck > 0 && !IsPowerful(budget) && LootPack.CheckLuck(luckChance / 6))
        {
            budget = Utility.RandomMinMax(600, 1150);
        }

        budget = Math.Clamp(budget, MinAdjustedBudget, MaxAdjustedBudget);

        int mods, percLow, percHigh;

        if (!IsPowerful(budget))
        {
            mods = Math.Max(1, GetProperties(5));
            percHigh = Math.Max(50, Math.Min(500, budget) / mods);
            percLow = Math.Max(20, percHigh / 3);
        }
        else
        {
            var maxMods = Math.Max(5, Math.Min(MaxProps - 1, (int)Math.Ceiling(budget / (double)Utility.RandomMinMax(100, 140))));
            var minMods = Math.Max(4, maxMods - 4);

            mods = Math.Max(minMods, GetProperties(maxMods));
            percHigh = 100;
            percLow = Utility.RandomMinMax(50, 70);
        }

        percHigh = Math.Min(100, percHigh);
        percLow = Math.Clamp(percLow, 10, 80);

        if (mods < MaxProps - 1 && LootPack.CheckLuck(luckChance))
        {
            mods++;
        }

        return (mods, percLow, percHigh, budget);
    }

    // Public since cc-P408: the Treasures of Sorcerer's Dungeon artifacts of Enchanted Origin roll the same way
    // (ServUO calls the same RunicReforging.GenerateRandomItem with another suffix).
    public static void ApplyProperties(Item item, Mobile killer, int baseBudget, int luckChance)
    {
        var (mods, percLow, percHigh, _) = Roll(killer, baseBudget, luckChance);

        switch (item)
        {
            case BaseWeapon weapon:
                BaseRunicTool.ApplyAttributesTo(weapon, false, luckChance, mods, percLow, percHigh);
                break;
            case BaseArmor armor:
                BaseRunicTool.ApplyAttributesTo(armor, false, luckChance, mods, percLow, percHigh);
                break;
            case BaseJewel jewel:
                BaseRunicTool.ApplyAttributesTo(jewel, false, luckChance, mods, percLow, percHigh);
                break;
            case BaseHat hat:
                BaseRunicTool.ApplyAttributesTo(hat, false, luckChance, mods, percLow, percHigh);
                break;
        }
    }

    public static bool IsPowerful(int budget) => budget >= 550;

    // ServUO RunicReforging.GetDivisor
    public static int GetDivisor(int baseBudget) =>
        baseBudget < 400 ? 5 :
        baseBudget < 550 ? 4 :
        baseBudget < 650 ? 3 : 2;

    // ServUO RunicReforging.GetProperties(max): a weighted count of properties, 0..max.
    public static int GetProperties(int max)
    {
        max = Math.Min(max, MaxProps - 1);

        int[] p = max switch
        {
            1  => new[] { 3, 1 },
            2  => new[] { 6, 3, 1 },
            3  => new[] { 10, 6, 3, 1 },
            4  => new[] { 16, 12, 6, 5, 1 },
            5  => new[] { 30, 25, 20, 15, 9, 1 },
            6  => new[] { 35, 30, 25, 20, 15, 9, 1 },
            7  => new[] { 40, 35, 30, 25, 20, 15, 9, 1 },
            8  => new[] { 50, 40, 35, 30, 25, 20, 15, 9, 1 },
            9  => new[] { 70, 55, 45, 35, 30, 25, 20, 15, 9, 1 },
            10 => new[] { 90, 70, 55, 45, 40, 35, 25, 15, 10, 5, 1 },
            _  => Array.Empty<int>()
        };

        var total = 0;

        foreach (var weight in p)
        {
            total += weight;
        }

        if (total == 0)
        {
            return 0;
        }

        // ServUO walks from the highest count down: p[max] is the weight of "max" properties.
        var rnd = Utility.Random(total);

        for (var count = p.Length - 1; count >= 1; count--)
        {
            if (rnd < p[count])
            {
                return count;
            }

            rnd -= p[count];
        }

        return 0;
    }

    // ServUO LootPackEntry.IsInTokuno / IsMondain, private in pinned's LootPack.cs:605-620.
    private static bool IsInTokuno(Mobile m) =>
        m?.Region != null && (m.Region.IsPartOf("Fan Dancer's Dojo") || m.Region.IsPartOf("Yomotsu Mines")) ||
        m?.Map == Map.Tokuno;

    private static bool IsMondain(Mobile m) => m?.Region != null && MondainsLegacy.IsMLRegion(m.Region);

    public static void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version

        var live = new List<Item>();

        foreach (var item in _items)
        {
            if (item?.Deleted == false)
            {
                live.Add(item);
            }
        }

        writer.WriteEncodedInt(live.Count);

        foreach (var item in live)
        {
            writer.Write(item);
        }
    }

    public static void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version != 0)
        {
            throw new InvalidOperationException($"BlackthornMinax save version {version} is not one this build reads (0).");
        }

        _items.Clear();

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var item = reader.ReadEntity<Item>();

            if (item?.Deleted == false)
            {
                _items.Add(item);
            }
        }
    }

    /// <summary>Test support: forgets every registered item.</summary>
    public static void Clear() => _items.Clear();
}

public sealed class MinaxArtifactPersistence : GenericPersistence
{
    public MinaxArtifactPersistence() : base("BlackthornMinax", 10)
    {
    }

    public override void Serialize(IGenericWriter writer) => MinaxArtifacts.Serialize(writer);

    public override void Deserialize(IGenericReader reader) => MinaxArtifacts.Deserialize(reader);
}
