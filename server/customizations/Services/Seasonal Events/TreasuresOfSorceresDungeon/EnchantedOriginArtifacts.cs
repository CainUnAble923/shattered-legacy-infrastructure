// cc-P408: the "artifacts of Enchanted Origin", Treasures of Sorcerer's Dungeon's turn-in currency.
//
// ServUO makes one in SorcerersDungeonData.ProcessKill with RunicReforging.GenerateRandomItem(item, killer,
// max(100, GetDifficultyFor(bc)), GetLuckForKiller(bc), ReforgedPrefix.None, ReforgedSuffix.EnchantedOrigin): a
// random weapon, armour, shield, jewel or hat (Loot.RandomArmorOrShieldOrWeaponOrJewelry, by the dead creature's
// region) with a property roll and the EnchantedOrigin suffix, which hues it 1171 (RunicReforging.cs:2067) and names it
// with cliloc 1157614 (the suffix name table, :1730). The researcher accepts exactly those
// (SorcerersDungeonResearcher.IsRedeemableItem reads the suffix through ICombatEquipment).
//
// Pinned ModernUO has no reforging and no suffix on its equipment; cc-P196 met the same gap for Blackthorn's Minax
// artifacts and solved it in Services/Revamped Dungeons/BlackthornDungeon/Items/MinaxArtifacts.cs. This file is that
// solution with the other suffix, reusing its roll (MinaxArtifacts.ApplyProperties and GetDifficultyFor, ServUO's
// budget arithmetic over pinned's BaseRunicTool tables: the deviation cc-P196 recorded):
//   1. The property roll: MinaxArtifacts.ApplyProperties at ServUO's budget and luck.
//   2. The mark: hue 1171, the name "<name> of Enchanted Origin" (the server's copy of the client's text, as Minax's
//      "of Minax"; a stock item has no property-line hook for ServUO's cliloc line), and a registry saved through
//      GenericPersistence "SorcerersDungeonEnchantedOrigin". A turned-in or deleted item leaves it at the next save.
//   3. The question the researcher asks: IsEnchantedOrigin(item).
//
// Also ours: [EnchantedOriginArtifact, so the turn-in can be tested without hunting for the roll (ServUO's chance is
// about 0.09% a kill at first).

using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Engines.Blackthorn;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

public static class EnchantedOriginArtifacts
{
    public const int OfEnchantedOrigin = 1157614; // ServUO RunicReforging suffix name table, "Sorcerers Dungeon"
    public const int EnchantedOriginHue = 1171;   // RunicReforging.cs:2067

    private static readonly HashSet<Item> _items = new();

    public static int Count => _items.Count;

    public static bool IsEnchantedOrigin(Item item) => item != null && !item.Deleted && _items.Contains(item);

    public static void Configure()
    {
        _ = new EnchantedOriginPersistence();
    }

    public static void Initialize()
    {
        CommandSystem.Register("EnchantedOriginArtifact", AccessLevel.GameMaster, EnchantedOriginArtifact_OnCommand);
    }

    [Usage("EnchantedOriginArtifact [budget]")]
    [Description("Makes an artifact of Enchanted Origin in your pack, for testing the Sorcerer's Dungeon researcher.")]
    [ShardCommand(CommandCategory.DevTool, Rerun = CommandRerun.Duplicates, Shard = CommandShard.Any, NoDryRun = true, Summary = "Makes an artifact of Enchanted Origin in your pack, for testing the Treasures of Sorcerer's Dungeon researcher. Optional budget (150-1450; default 700).")]
    public static void EnchantedOriginArtifact_OnCommand(CommandEventArgs e)
    {
        var budget = e.Length > 0 ? Math.Clamp(e.GetInt32(0), MinaxArtifacts.MinAdjustedBudget, MinaxArtifacts.MaxAdjustedBudget) : 700;
        var item = Create(e.Mobile, budget, 0, false, false);

        if (item == null)
        {
            e.Mobile.SendMessage("Nothing came of the roll; try again.");
            return;
        }

        if (!e.Mobile.PlaceInBackpack(item))
        {
            item.MoveToWorld(e.Mobile.Location, e.Mobile.Map);
        }

        e.Mobile.SendMessage($"An artifact of Enchanted Origin ({item.GetType().Name}) at budget {budget} is in your pack.");
    }

    /// <summary>An artifact for a kill in the dungeon (ServUO SorcerersDungeonData.ProcessKill).</summary>
    public static Item CreateForKill(BaseCreature dead, Mobile damager) =>
        Create(
            damager,
            Math.Max(100, MinaxArtifacts.GetDifficultyFor(dead)),
            LootPack.GetLuckChanceForKiller(dead),
            IsInTokuno(dead),
            IsMondain(dead)
        );

    /// <summary>A random weapon, armour, shield, jewel or hat with a property roll, marked and registered.</summary>
    public static Item Create(Mobile damager, int baseBudget, int luckChance, bool inTokuno, bool isMondain)
    {
        var item = Loot.RandomArmorOrShieldOrWeaponOrJewelry(inTokuno, isMondain);

        if (item == null)
        {
            return null;
        }

        MinaxArtifacts.ApplyProperties(item, damager, baseBudget, luckChance);
        Mark(item);
        return item;
    }

    /// <summary>Hues, names and registers an item as an artifact of Enchanted Origin.</summary>
    public static void Mark(Item item)
    {
        if (item == null || item.Deleted)
        {
            return;
        }

        item.Hue = EnchantedOriginHue;

        if (item.Name == null && Localization.GetText(item.LabelNumber) is { Length: > 0 } text)
        {
            item.Name = $"{text} of Enchanted Origin";
        }

        _items.Add(item);
    }

    public static bool Remove(Item item) => item != null && _items.Remove(item);

    // ServUO LootPackEntry.IsInTokuno / IsMondain on the dead creature (private in pinned's LootPack.cs).
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
            throw new InvalidOperationException($"SorcerersDungeonEnchantedOrigin save version {version} is not one this build reads (0).");
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

public sealed class EnchantedOriginPersistence : GenericPersistence
{
    public EnchantedOriginPersistence() : base("SorcerersDungeonEnchantedOrigin", 10)
    {
    }

    public override void Serialize(IGenericWriter writer) => EnchantedOriginArtifacts.Serialize(writer);

    public override void Deserialize(IGenericReader reader) => EnchantedOriginArtifacts.Deserialize(reader);
}
