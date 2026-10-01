// SmithSealScrollCensus.cs
//
// cc-P23, D43. The Smith Seal catalog sold Blacksmithing power scrolls at 305 to 320. Using one sets
// the skill's cap (pinned PowerScroll.Use, PowerScroll.cs:272), and ClusterFSkillCaps.Apply writes the
// flat individual cap back over every skill at the next login or world load, so the buyer loses it.
// The catalog no longer sells them. Whether anyone is refunded is Chase's decision, and this command
// gives him the numbers: it changes nothing, so it has no dry run to declare, and it refunds nothing.
//
// What it reports:
//   - every Blacksmithing PowerScroll in the world, by where it is (a player's backpack, a player's
//     bank, another mobile such as a vendor, a container in the world, loose on a map, Map.Internal)
//     and by value (305 and up, which only the catalog made; up to 120, which is stock content);
//   - characters whose Blacksmithing cap is above their other skill caps right now: a scroll used
//     since the last Apply, which the next login will undo;
//   - accounts holding Smithing Seals, and how many.
// Notes in shard-migration notes/cc-P23-live-defect-sweep.md.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;

namespace Server;

public static class SmithSealScrollCensus
{
    // The cheapest scroll the catalog sold. Stock Blacksmithing scrolls stop at 120.
    public const double CatalogScrollMinimum = 305.0;

    public enum Where
    {
        PlayerBackpack,  // carried by a player, worn or in the backpack at any depth
        PlayerBank,
        OtherMobile,     // a vendor, a pet's pack, any non-player mobile
        WorldContainer,  // in a container that is not on a mobile: a house chest, a corpse
        LooseOnMap,
        Internal         // not in any container and on Map.Internal (or no map)
    }

    public sealed record ScrollEntry(Serial Serial, double Value, Where Where, string Owner);

    public sealed class Report
    {
        public List<ScrollEntry> Scrolls { get; } = new();
        public List<string> RaisedCaps { get; } = new();
        public int AccountsWithSeals { get; set; }
        public int CharactersWithSeals { get; set; }
        public long TotalSeals { get; set; }

        public int CatalogScrolls => Scrolls.Count(s => s.Value >= CatalogScrollMinimum);
        public int StockScrolls => Scrolls.Count(s => s.Value < CatalogScrollMinimum);
        public int Count(Where where) => Scrolls.Count(s => s.Where == where);
        public int CatalogCount(Where where) => Scrolls.Count(s => s.Where == where && s.Value >= CatalogScrollMinimum);
    }

    public static void Configure()
    {
        CommandSystem.Register("SmithSealScrollCensus", AccessLevel.Administrator, OnCommand);
    }

    public static Report Take()
    {
        var report = new Report();

        foreach (var item in World.Items.Values)
        {
            if (item is PowerScroll { Deleted: false, Skill: SkillName.Blacksmith } scroll)
            {
                report.Scrolls.Add(new ScrollEntry(scroll.Serial, scroll.Value, Locate(scroll, out var owner), owner));
            }
        }

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is not PlayerMobile { Deleted: false } pm || pm.Skills == null)
            {
                continue;
            }

            var smith = pm.Skills.Blacksmith.Cap;
            var highestOther = 0.0;

            for (var i = 0; i < pm.Skills.Length; i++)
            {
                if (i != (int)SkillName.Blacksmith)
                {
                    highestOther = Math.Max(highestOther, pm.Skills[i].Cap);
                }
            }

            if (smith > highestOther)
            {
                report.RaisedCaps.Add($"{pm.Name} ({pm.Serial}, account {pm.Account?.Username ?? "none"}): Blacksmithing cap {smith:0.#}, others {highestOther:0.#}");
            }
        }

        foreach (var (_, data) in ClusterFAccountPersistence.All)
        {
            var any = false;

            foreach (var (_, guild) in data.AllGuildData)
            {
                var seals = guild.GetCurrency("smithing");
                if (seals > 0)
                {
                    any = true;
                    report.CharactersWithSeals++;
                    report.TotalSeals += seals;
                }
            }

            if (any)
            {
                report.AccountsWithSeals++;
            }
        }

        return report;
    }

    private static Where Locate(Item scroll, out string owner)
    {
        owner = "";

        if (scroll.RootParent is Mobile m)
        {
            owner = m.Account != null ? $"{m.Name} (account {m.Account.Username})" : m.Name ?? m.GetType().Name;

            if (m is not PlayerMobile)
            {
                return Where.OtherMobile;
            }

            var bank = m.FindBankNoCreate();
            return bank != null && scroll.IsChildOf(bank) ? Where.PlayerBank : Where.PlayerBackpack;
        }

        if (scroll.RootParent is Item container)
        {
            owner = $"{container.GetType().Name} {container.Serial} at {container.GetWorldLocation()} {container.Map}";
            return Where.WorldContainer;
        }

        if (scroll.Map == null || scroll.Map == Map.Internal)
        {
            return Where.Internal;
        }

        owner = $"{scroll.Location} {scroll.Map}";
        return Where.LooseOnMap;
    }

    public static IEnumerable<string> Describe(Report r)
    {
        yield return $"Blacksmithing power scrolls: {r.Scrolls.Count} ({r.CatalogScrolls} at 305 or above, from the Smith Seal catalog; {r.StockScrolls} stock, 120 or below).";

        foreach (var where in Enum.GetValues<Where>())
        {
            yield return $"  {where}: {r.Count(where)} ({r.CatalogCount(where)} from the catalog)";
        }

        foreach (var s in r.Scrolls.OrderByDescending(s => s.Value))
        {
            yield return $"  scroll {s.Serial} {s.Value:0.#} {s.Where} {s.Owner}";
        }

        yield return $"Characters with a Blacksmithing cap above their other caps now (a scroll used since the last login; the next login resets it): {r.RaisedCaps.Count}.";

        foreach (var line in r.RaisedCaps)
        {
            yield return $"  {line}";
        }

        yield return $"Accounts holding Smithing Seals: {r.AccountsWithSeals} ({r.CharactersWithSeals} characters, {r.TotalSeals:N0} seals in all).";
        yield return "Nothing was changed or refunded.";
    }

    [Usage("SmithSealScrollCensus")]
    [Description("Counts Blacksmithing power scrolls in the world and accounts holding Smithing Seals (D43). Read only.")]
    [ShardCommand(CommandCategory.Diagnostic, Shard = CommandShard.Any, Summary = "Counts Smith Seal power scrolls and seal holders for the D43 refund decision. Changes nothing.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var lines = Describe(Take()).ToList();

        foreach (var line in lines)
        {
            Console.WriteLine($"[SmithSealScrollCensus] {line}");
        }

        // The full list is on the console; in game, the totals and the first scrolls.
        foreach (var line in lines.Take(40))
        {
            e.Mobile.SendMessage(line);
        }

        if (lines.Count > 40)
        {
            e.Mobile.SendMessage($"... {lines.Count - 40} more lines on the server console.");
        }
    }
}
