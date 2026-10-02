// GuildResources.cs
//
// F-11 (cc-P22): guild upgrades, restorations and work order turn-ins count and take resources, and the specific
// items a turn-in asks for, from the backpack AND the bank box. Ours; OSI crafting stays pack-only and does not use
// this. Gold is not handled here: CompactGoldHelper already does pack plus bank for gold (bank checks and account
// gold included, through Banker.GetBalance and Banker.Withdraw), and every caller keeps using it for gold.
//
// The rules (Chase, 2026-09-30), and how each is kept:
//   - Pack first, then bank. Loose first, then satchels. The order an item is taken in is one list:
//       1. pack, outside any satchel   2. pack, inside a satchel   3. bank, outside   4. bank, inside a satchel
//     Inside each, breadth first, as pinned's Container.GetAmount walks (Server/Items/Container.cs:857-869).
//   - All or nothing: TryConsume plans every cost against what is there, item by item, before it touches anything.
//     If any cost is short, nothing is taken. Costs that match the same items (two costs on one type) cannot
//     double count, because the plan reserves what it has already promised.
//   - Never taken: equipped items (only the pack and bank are searched), blessed items (LootType.Blessed or
//     BlessedFor), and anything inside a locked container (ILockable.Locked) or a secure or locked-down one.
//     A container is never taken while it holds anything.
//
// Every guild path that counts or takes resources from a player goes through here, so they cannot drift.
// Imbuing essences joined them in cc-P27 (ArtificersImbueGump.EssenceCost; Chase, 2026-10-01).

using System;
using System.Collections.Generic;
using Server.Items;
using Server.Mobiles;

namespace Server;

/// <summary>One thing a guild service asks for: an item type (or a test for specific items) and an amount.</summary>
public sealed class GuildCost
{
    private GuildCost(Type type, Predicate<Item> match, int amount, string label)
    {
        Type = type;
        Match = match;
        Amount = amount;
        Label = label;
    }

    public Type Type { get; }
    public Predicate<Item> Match { get; }
    public int Amount { get; }
    public string Label { get; }

    /// <summary>Any item of this type or a subtype, as pinned's GetAmount and ConsumeTotal match (IsInstanceOfType).</summary>
    public static GuildCost Of(Type type, int amount, string label = null) =>
        new(type, null, amount, label ?? GuildResources.LabelFor(type));

    public static GuildCost Of<T>(int amount, string label = null) where T : Item => Of(typeof(T), amount, label);

    /// <summary>Specific items, e.g. a crafted piece that must meet a work order's requirements.</summary>
    public static GuildCost Where(Predicate<Item> match, int amount, string label) => new(null, match, amount, label);

    public bool Matches(Item item) => Type != null ? Type.IsInstanceOfType(item) : Match(item);
}

/// <summary>How much of a cost a player has, and where.</summary>
public readonly record struct GuildStock(int Pack, int Bank)
{
    public int Total => Pack + Bank;
}

public static class GuildResources
{
    // Our resource satchels (the ore, lumber, hunter's and artificer's satchels with every tier deriving from
    // them) and the ported Gemologist's Satchel. Quest "satchels" that are plain backpacks are bags, not these.
    private static readonly Type[] SatchelTypes =
    [
        typeof(CompactOreSatchel),
        typeof(ForestersLumberSatchel),
        typeof(HuntersSatchel),
        typeof(ArtificersSatchel),
        typeof(GemologistsSatchel)
    ];

    public static bool IsSatchel(Item item)
    {
        foreach (var t in SatchelTypes)
        {
            if (t.IsInstanceOfType(item))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsBlessed(Item item) => item.LootType == LootType.Blessed || item.BlessedFor != null;

    /// <summary>A container whose contents are never taken: locked, secure, or locked down.</summary>
    public static bool IsClosedToGuild(Item container) =>
        container is ILockable { Locked: true } || container.IsSecure || container.IsLockedDown;

    /// <summary>Every item that may be taken for this cost, in the order it would be taken.</summary>
    public static List<Item> Candidates(PlayerMobile pm, GuildCost cost)
    {
        var list = new List<Item>();
        if (pm == null || cost == null)
        {
            return list;
        }

        var later = new List<Item>();

        Walk(pm.Backpack, cost, list, later);
        list.AddRange(later);

        var bankLater = new List<Item>();
        Walk(pm.FindBankNoCreate(), cost, list, bankLater);
        list.AddRange(bankLater);

        return list;
    }

    // Breadth first from root. Matches outside any satchel go to `loose`, inside one to `inSatchel`.
    private static void Walk(Container root, GuildCost cost, List<Item> loose, List<Item> inSatchel)
    {
        if (root == null || root.Deleted)
        {
            return;
        }

        var queue = new Queue<(Container Box, bool InSatchel)>();
        queue.Enqueue((root, false));

        while (queue.Count > 0)
        {
            var (box, satchel) = queue.Dequeue();

            foreach (var item in box.Items)
            {
                if (item.Deleted)
                {
                    continue;
                }

                if (item is Container inner)
                {
                    if (!IsClosedToGuild(inner))
                    {
                        queue.Enqueue((inner, satchel || IsSatchel(inner)));
                    }

                    if (inner.Items.Count > 0)
                    {
                        continue;
                    }
                }

                if (!IsBlessed(item) && cost.Matches(item))
                {
                    (satchel ? inSatchel : loose).Add(item);
                }
            }
        }
    }

    public static GuildStock Count(PlayerMobile pm, GuildCost cost)
    {
        int pack = 0, bank = 0;
        var bankBox = pm?.FindBankNoCreate();

        foreach (var item in Candidates(pm, cost))
        {
            if (bankBox != null && item.IsChildOf(bankBox))
            {
                bank += item.Amount;
            }
            else
            {
                pack += item.Amount;
            }
        }

        return new GuildStock(pack, bank);
    }

    public static bool Has(PlayerMobile pm, params GuildCost[] costs) => Plan(pm, costs, out _, out _);

    /// <summary>
    /// Takes every cost, or nothing. Returns false and names the first cost that is short when any is.
    /// </summary>
    public static bool TryConsume(PlayerMobile pm, out GuildCost shortOf, params GuildCost[] costs)
    {
        if (!Plan(pm, costs, out var plan, out shortOf))
        {
            return false;
        }

        foreach (var (item, take) in plan)
        {
            if (!item.Deleted)
            {
                item.Consume(take);
            }
        }

        return true;
    }

    public static bool TryConsume(PlayerMobile pm, params GuildCost[] costs) => TryConsume(pm, out _, costs);

    private static bool Plan(PlayerMobile pm, GuildCost[] costs, out List<(Item Item, int Take)> plan, out GuildCost shortOf)
    {
        plan = new List<(Item, int)>();
        shortOf = null;
        var promised = new Dictionary<Item, int>();

        foreach (var cost in costs)
        {
            if (cost == null || cost.Amount <= 0)
            {
                continue;
            }

            var need = cost.Amount;

            foreach (var item in Candidates(pm, cost))
            {
                promised.TryGetValue(item, out var already);
                var take = Math.Min(item.Amount - already, need);

                if (take <= 0)
                {
                    continue;
                }

                promised[item] = already + take;
                plan.Add((item, take));
                need -= take;

                if (need == 0)
                {
                    break;
                }
            }

            if (need > 0)
            {
                shortOf = cost;
                plan.Clear();
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// For a gump: "60 valorite ingots: 20 in pack, 40 in bank" when there is enough (where it would come from),
    /// "60 valorite ingots: have 45 (20 in pack, 25 in bank)" when there is not.
    /// </summary>
    public static string Describe(PlayerMobile pm, GuildCost cost)
    {
        var stock = Count(pm, cost);

        if (stock.Total < cost.Amount)
        {
            return $"{cost.Amount:N0} {cost.Label}: have {stock.Total:N0} ({stock.Pack:N0} in pack, {stock.Bank:N0} in bank)";
        }

        var fromPack = Math.Min(stock.Pack, cost.Amount);
        return $"{cost.Amount:N0} {cost.Label}: {fromPack:N0} in pack, {cost.Amount - fromPack:N0} in bank";
    }

    /// <summary>The refusal line for a short cost.</summary>
    public static string ShortMessage(PlayerMobile pm, GuildCost cost)
    {
        var stock = Count(pm, cost);
        return $"You need {cost.Amount:N0} {cost.Label} in your backpack or bank (you have {stock.Total:N0}: {stock.Pack:N0} in pack, {stock.Bank:N0} in bank).";
    }

    /// <summary>One line per material for a gump, each from Describe.</summary>
    public static string DescribeAll(PlayerMobile pm, GuildCost[] materials, string separator = "<BR>")
    {
        var parts = new List<string>();
        foreach (var cost in materials)
        {
            if (cost != null && cost.Amount > 0)
            {
                parts.Add(Describe(pm, cost));
            }
        }

        return string.Join(separator, parts);
    }

    /// <summary>
    /// The price of a guild upgrade or restoration: guild scrip, gold (pack then bank, CompactGoldHelper) and
    /// materials (pack then bank, loose then satchels). Checks everything first and tells the player the first thing
    /// that is short; takes nothing unless all of it is there. Scrip is taken last, after the materials and gold.
    /// </summary>
    public static bool TryPay(
        PlayerMobile pm, CharacterGuildData guild, string scripKey, int scrip, string scripName, int gold,
        params GuildCost[] materials
    )
    {
        var held = 0;
        if (scrip > 0)
        {
            guild.GuildCurrency.TryGetValue(scripKey, out held);
            if (held < scrip)
            {
                pm.SendMessage(0x22, $"You need {scrip:N0} {scripName} (you have {held:N0}).");
                return false;
            }
        }

        foreach (var cost in materials)
        {
            if (cost != null && cost.Amount > 0 && Count(pm, cost).Total < cost.Amount)
            {
                pm.SendMessage(0x22, ShortMessage(pm, cost));
                return false;
            }
        }

        if (!Has(pm, materials))
        {
            // Each is there on its own but not all at once (two costs on the same items).
            pm.SendMessage(0x22, "You do not have all of the materials in your backpack and bank together.");
            return false;
        }

        if (gold > 0 && CompactGoldHelper.GetTotalGold(pm) < gold)
        {
            pm.SendMessage(0x22, $"You need {gold:N0} gold in your backpack or bank (you have {CompactGoldHelper.GetTotalGold(pm):N0}).");
            return false;
        }

        if (!TryConsume(pm, materials))
        {
            return false;
        }

        if (gold > 0)
        {
            CompactGoldHelper.ConsumeGold(pm, gold);
        }

        if (scrip > 0)
        {
            guild.GuildCurrency[scripKey] = held - scrip;
        }

        return true;
    }

    /// <summary>Whether TryPay would succeed, for a gump's button colour.</summary>
    public static bool CanPay(PlayerMobile pm, CharacterGuildData guild, string scripKey, int scrip, int gold, params GuildCost[] materials)
    {
        guild.GuildCurrency.TryGetValue(scripKey, out var held);
        return held >= scrip && (gold <= 0 || CompactGoldHelper.GetTotalGold(pm) >= gold) && Has(pm, materials);
    }

    // "IronIngot" -> "iron ingots", "DullCopperIngot" -> "dull copper ingots", "Board" -> "boards".
    public static string LabelFor(Type type)
    {
        var name = type.Name;
        var sb = new System.Text.StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                sb.Append(' ');
            }

            sb.Append(char.ToLowerInvariant(name[i]));
        }

        if (sb.Length > 0 && sb[^1] != 's')
        {
            sb.Append('s');
        }

        return sb.ToString();
    }
}
