// cc-P787: beekeeping's start-up and staff command.
//
//   - the table (Configuration/beekeeping.json) is read at Configure, written with the defaults if missing;
//   - one shared sweep (every sweepMinutes) runs each due hive's daily check and regrows due wild hives;
//   - at ServerStarted the candle recipes join Alchemy (a Candles group) and the wax pot joins Tinkering (Miscellaneous);
//   - after every world load (on the next tick, so the world's items are in place) the wild hives are seeded, idempotent;
//   - every beekeeper also sells the table's rows (SBApiarist, the patch Beekeeper-beekeeping.patch); pinned reloads a
//     vendor's lists on load, so standing beekeepers stock them too.
//
//   [Beekeeping status | reload | seed [dryrun]
//
// After the apiculture of Shafev's Cooking Expansion Set (reference only, D:\UO\third-party\code\cooking-expansion);
// rebuilt in our style, with our numbers, stock art, no food buffs and no XmlSpawner.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Commands;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Beekeeping;

public static class BeekeepingSystem
{
    public const string CandlesGroup = "Candles";

    private static Timer? _sweep;
    private static bool _registered;
    private static readonly Dictionary<CraftItem, CandleRecipe> _candles = new();

    public static IReadOnlyDictionary<CraftItem, CandleRecipe> Candles => _candles;

    public static CraftItem? WaxPotCraft { get; private set; }

    public static void Configure()
    {
        foreach (var line in BeekeepingTable.LoadOrWriteDefaults(BeekeepingTable.FilePath))
        {
            Console.WriteLine($"[Beekeeping] {line}");
        }

        CommandSystem.Register("Beekeeping", AccessLevel.GameMaster, OnCommand);
        EventSink.ServerStarted += () =>
        {
            RegisterRecipes();
            StartSweep();
        };
        EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () =>
        {
            foreach (var line in WildHiveSeeder.Seed())
            {
                Console.WriteLine($"[Beekeeping] {line}");
            }
        });
    }

    public static void StartSweep()
    {
        _sweep?.Stop();
        var every = TimeSpan.FromMinutes(BeekeepingTable.Current.SweepMinutes);
        _sweep = Timer.DelayCall(every, every, () => Sweep(Core.Now));
    }

    /// <summary>Runs every due hive's day and regrows due wild hives. Returns the hives that lived a day.</summary>
    public static int Sweep(DateTime now)
    {
        var n = 0;
        foreach (var hive in HouseBeehive.All.ToList())
        {
            if (hive.CheckDue(now))
            {
                n++;
            }
        }

        foreach (var wild in WildBeehive.All.ToList())
        {
            wild.RegrowIfDue(now);
        }

        return n;
    }

    // -- Recipes ------------------------------------------------------------------------------------------

    /// <summary>Idempotent; the test host calls it directly because ServerStarted never fires there.</summary>
    public static int RegisterRecipes()
    {
        if (_registered)
        {
            return 0;
        }

        var alchemy = DefAlchemy.CraftSystem;
        var tinkering = DefTinkering.CraftSystem;
        if (alchemy == null || tinkering == null)
        {
            Console.WriteLine("[Beekeeping] WARNING: Alchemy or Tinkering is not set up; the candle and wax pot recipes are not added.");
            return 0;
        }

        _registered = true;
        var t = BeekeepingTable.Current;
        var n = 0;
        foreach (var c in t.Candles)
        {
            if (c.ItemType == null)
            {
                continue;
            }

            var index = alchemy.AddCraft(c.ItemType, CandlesGroup, NameOf(c.ItemType), c.MinSkill, c.MaxSkill,
                typeof(Beeswax), "beeswax", c.Wax, "You do not have enough beeswax.");
            if (c.Thread > 0)
            {
                alchemy.AddRes(index, typeof(SpoolOfThread), "spool of thread", c.Thread, 1044253);
            }

            _candles[alchemy.CraftItems[index]] = c;
            n++;
        }

        var p = t.WaxPotRecipe;
        var pot = tinkering.AddCraft(typeof(WaxPot), 1044050, "wax pot", p.MinSkill, p.MaxSkill, typeof(IronIngot),
            1044036, p.Ingots, 1044037);
        WaxPotCraft = tinkering.CraftItems[pot];
        n++;

        Console.WriteLine($"[Beekeeping] {n - 1} candle recipes added to Alchemy and the wax pot to Tinkering.");
        return n;
    }

    public static string NameOf(Type candle) =>
        candle.Name switch
        {
            "Candle"      => "candle",
            "CandleShort" => "short candle",
            "CandleLong"  => "long candle",
            "CandleLarge" => "large candle",
            _             => candle.Name
        };

    // -- Staff ---------------------------------------------------------------------------------------------

    [Usage("Beekeeping [status|reload|seed [dryrun]]")]
    [Description("Beekeeping (cc-P787): status says the table in force (Configuration/beekeeping.json), how many hives and wild hives there are, and checks the file on disk; reload reads the file now (care and wild numbers take effect at once, recipes and the beekeeper's rows at the next restart); seed places any missing wild hive (dryrun says what it would place).")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.Any)]
    private static void OnCommand(CommandEventArgs e)
    {
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
        var lines = verb switch
        {
            "status" => Status(),
            "reload" => BeekeepingTable.Reload(BeekeepingTable.FilePath),
            "seed"   => WildHiveSeeder.Seed(e.Length > 1 && e.GetString(1).Equals("dryrun", StringComparison.OrdinalIgnoreCase)),
            _        => ["Usage: [Beekeeping status|reload|seed [dryrun]"]
        };

        foreach (var line in lines)
        {
            e.Mobile.SendMessage(line);
        }
    }

    public static List<string> Status()
    {
        var t = BeekeepingTable.Current;
        var lived = HouseBeehive.All.Count(h => h.Lived);
        var lines = new List<string>
        {
            $"Beekeeping {(t.Enabled ? "on" : "off")} ({t.Source}): {HouseBeehive.All.Count} hives ({lived} with bees), " +
            $"{WildBeehive.All.Count} wild hives of {t.Wild.Spots.Count} spots{(t.Wild.Enabled ? "" : " (wild off)")}; " +
            $"{_candles.Count} candle recipes in Alchemy; beekeepers sell {string.Join(", ", t.Beekeeper.Select(v => $"{v.Type} {v.Price}gp"))}."
        };

        if (System.IO.File.Exists(BeekeepingTable.FilePath))
        {
            var errors = new List<string>();
            BeekeepingTable.Parse(System.IO.File.ReadAllText(BeekeepingTable.FilePath), "check", errors);
            lines.Add(errors.Count == 0
                ? "The file on disk is good."
                : $"The file on disk would be refused: {string.Join("; ", errors)}");
        }

        return lines;
    }
}

/// <summary>What every beekeeper also sells: beekeeping.json's beekeeper rows.</summary>
public class SBApiarist : SBInfo
{
    public override IShopSellInfo SellInfo { get; } = new GenericSellInfo();

    public override List<GenericBuyInfo> BuyInfo { get; } = Build();

    public static List<GenericBuyInfo> Build()
    {
        var list = new List<GenericBuyInfo>();
        var table = BeekeepingTable.Current;
        if (!table.Enabled)
        {
            return list;
        }

        foreach (var row in table.Beekeeper)
        {
            if (row.ItemType == null)
            {
                continue;
            }

            var (name, art, hue) = Display(row.ItemType, table);
            list.Add(new GenericBuyInfo(name, row.ItemType, row.Price, row.Amount, art, hue));
        }

        return list;
    }

    public static (string Name, int Art, int Hue) Display(Type type, BeekeepingTable t) =>
        type.Name switch
        {
            nameof(BeehiveDeed) => ("a beehive deed", 0x14F0, 0),
            nameof(BeeSwarm)    => ("a bee swarm", 0x9AA, 0),
            nameof(HiveTool)    => ("a hive tool", 0x9F5, 0),
            nameof(WaxPot)      => ("a wax pot", 0x142A, 0),
            nameof(JarHoney)    => ("1022540", 0x9EC, 0),
            _                   => ("1025154", 0x1422, 0) // Beeswax
        };
}
