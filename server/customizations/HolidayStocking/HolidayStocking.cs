// cc-P784: the holiday stocking at the winter Festival stall. The idea is from Candy Apple's "Stocking full of goodies"
// (D:\UO\third-party\code\stocking-snowman, reference only); the code, the limits and the prizes are ours.
//
// A red stocking hangs on the Festival Peddler's table while winter is on (the table's season, HolidayStockingTable).
// A player double-clicks it within range and holds still for 6 seconds; then, once a day per account, they get one
// winter treat or small decor piece drawn by weight from the table, with a System line, and 1 Festival Token on the
// day's first grab. Moving, dying or walking off cancels the grab and costs nothing.
//
// The day: the shard's local day (FestivalSeasons.Clock, FestivalTokens.DayIndex), kept per account in the achievement
// counters as the daily login token is: "festival.stocking.day" (the last day grabbed) and "festival.stocking.grabs"
// (grabs that day).
//
// Placing: on every world load (so an existing world gets it by itself), every hour, and whenever the Festival stall
// goes up or down, HolidayStockings.Sync() hangs one stocking while the stall is up in the table's season and takes it
// away otherwise, tracked by serial in ClusterFSeasonalState ("festival-stocking"). Sync twice places once.
// [HolidayStocking status|reload|place|clear] for staff.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Commands;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.RewardStores;

/// <summary>The stocking on the Festival stall: double-click and hold still for a treat, once a day.</summary>
[SerializationGenerator(0, false)]
public partial class HolidayStocking : Item
{
    [Constructible]
    public HolidayStocking() : base(HolidayStockingTable.Current.ItemID)
    {
        Name = "a holiday stocking";
        Hue = 0;
        Movable = false;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Hold still beside it for a treat, once a day");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile pm)
        {
            HolidayStockings.Begin(pm, this);
        }
    }
}

public static class HolidayStockings
{
    public const string StateName = "festival-stocking";
    public const string DayCounter = "festival.stocking.day";
    public const string GrabsCounter = "festival.stocking.grabs";

    /// <summary>The prize roll in [0, 1). Tests replace it.</summary>
    public static Func<double> Roll = Utility.RandomDouble;

    private static readonly HashSet<PlayerMobile> _grabbing = [];

    public static HolidayStockingTable Table => HolidayStockingTable.Current;

    public static bool InSeason => FestivalSeasons.Current() == Table.Season;

    public static int Today => FestivalTokens.DayIndex(FestivalSeasons.Clock());

    /// <summary>Grabs the account has made today (0 when its last grab was another day).</summary>
    public static int GrabsToday(string username) =>
        ClusterFAchievementSystem.GetCounter(username, DayCounter) == Today
            ? ClusterFAchievementSystem.GetCounter(username, GrabsCounter)
            : 0;

    public static void Configure()
    {
        foreach (var line in HolidayStockingTable.LoadOrWriteDefaults(HolidayStockingTable.FilePath))
        {
            Console.WriteLine($"[HolidayStocking] {line}");
        }

        CommandSystem.Register("HolidayStocking", AccessLevel.GameMaster, OnCommand);

        EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () =>
        {
            Report(Sync());
            Timer.DelayCall(TimeSpan.FromHours(1), TimeSpan.FromHours(1), () => Report(Sync()));
        });
    }

    private static void Report(List<string> lines)
    {
        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }
    }

    // -- The grab -----------------------------------------------------------------------------------------------------

    /// <summary>Why the player cannot start a grab now, or null when they can.</summary>
    public static string? CannotGrab(PlayerMobile pm, Item stocking)
    {
        var t = Table;
        if (!t.Enabled || !InSeason)
        {
            return "The stocking is empty. Come back when the winter festival is on.";
        }

        if (pm.Account is not IAccount acct)
        {
            return "The stocking is empty.";
        }

        if (!pm.Alive)
        {
            return "You cannot reach into the stocking as a ghost.";
        }

        if (stocking.Deleted || pm.Map != stocking.Map || !pm.InRange(stocking.GetWorldLocation(), t.Range))
        {
            return "You must stand beside the stocking to reach into it.";
        }

        if (GrabsToday(acct.Username) >= t.GrabsPerDay)
        {
            return "You have had your treat from the stocking today. It will be full again tomorrow.";
        }

        return null;
    }

    /// <summary>A double-click: start the hold-still wait, or say why not.</summary>
    public static bool Begin(PlayerMobile pm, Item stocking)
    {
        if (_grabbing.Contains(pm))
        {
            pm.SendMessage("You are already reaching into the stocking. Hold still.");
            return false;
        }

        if (CannotGrab(pm, stocking) is { } why)
        {
            pm.SendMessage(0x22, why);
            return false;
        }

        var startedAt = pm.Location;
        _grabbing.Add(pm);
        pm.SendMessage($"You reach into the stocking. Hold still for {Table.HoldSeconds:0.#} seconds...");
        Timer.StartTimer(TimeSpan.FromSeconds(Table.HoldSeconds), () =>
        {
            _grabbing.Remove(pm);
            Finish(pm, stocking, startedAt, Roll());
        });
        return true;
    }

    /// <summary>
    /// The end of the wait: the prize when the player held still (still at startedAt, alive, in range, in season, and
    /// with a grab left today). Returns the prize given, or null.
    /// </summary>
    public static Item? Finish(PlayerMobile pm, Item stocking, Point3D startedAt, double roll)
    {
        if (pm.Deleted || pm.NetState == null && pm.Map == Map.Internal)
        {
            return null;
        }

        if (pm.Location != startedAt)
        {
            pm.SendMessage(0x22, "You moved, and the stocking slips from your grasp.");
            return null;
        }

        if (CannotGrab(pm, stocking) is { } why)
        {
            pm.SendMessage(0x22, why);
            return null;
        }

        var acct = (IAccount)pm.Account;
        var t = Table;
        var today = Today;
        var first = GrabsToday(acct.Username) == 0;

        // The day's counters: the day itself, and the grabs on it (reset when the day changed).
        var lastDay = ClusterFAchievementSystem.GetCounter(acct.Username, DayCounter);
        if (lastDay != today)
        {
            ClusterFAchievementSystem.AddToCounter(acct.Username, DayCounter, today - lastDay);
            ClusterFAchievementSystem.AddToCounter(acct.Username, GrabsCounter,
                -ClusterFAchievementSystem.GetCounter(acct.Username, GrabsCounter));
        }

        ClusterFAchievementSystem.AddToCounter(acct.Username, GrabsCounter, 1);

        var row = t.Draw(roll);
        var prize = Make(row);
        if (prize == null)
        {
            pm.SendMessage(0x22, "The stocking is empty.");
            return null;
        }

        pm.AddToBackpack(prize);
        pm.SendMessage(0x3B2, $"You pull {Describe(prize)} from the holiday stocking!");

        if (first && t.TokensOnFirstGrab > 0)
        {
            FestivalTokens.Grant(pm, t.TokensOnFirstGrab, "the holiday stocking");
        }

        return prize;
    }

    /// <summary>The prize a row makes: one item, a stack when the type stacks, else a small bag of them.</summary>
    public static Item? Make(StockingPrizeRow row)
    {
        var first = New(row.ItemType);
        if (first == null)
        {
            return null;
        }

        if (row.Amount <= 1)
        {
            return first;
        }

        if (first.Stackable)
        {
            first.Amount = row.Amount;
            return first;
        }

        var bag = new Bag { Name = "a little bag of holiday treats", Hue = 0x21 };
        bag.DropItem(first);
        for (var i = 1; i < row.Amount; i++)
        {
            if (New(row.ItemType) is { } more)
            {
                bag.DropItem(more);
            }
        }

        return bag;
    }

    private static Item? New(Type? type)
    {
        if (type == null)
        {
            return null;
        }

        try
        {
            if (type.GetConstructor(Type.EmptyTypes) != null)
            {
                return Activator.CreateInstance(type) as Item;
            }

            return type.GetConstructor([typeof(int)])?.Invoke([1]) as Item;
        }
        catch
        {
            return null;
        }
    }

    private static string Describe(Item item) =>
        item is Container c && c.Items.Count > 0
            ? $"a little bag of {c.Items.Count} treats"
            : item.Amount > 1
                ? $"{item.Amount} {item.Name ?? "treats"}"
                : item.Name ?? "a holiday treat";

    // -- Placing ------------------------------------------------------------------------------------------------------

    public static HashSet<Serial> Placed() => ClusterFSeasonalState.Placed(StateName);

    public static HolidayStocking? Stocking =>
        Placed().Select(s => World.FindItem(s)).OfType<HolidayStocking>().FirstOrDefault(s => !s.Deleted);

    public static Point3D Spot
    {
        get
        {
            var at = FestivalMarket.Anchor;
            var t = Table;
            return new Point3D(at.X + t.OffsetX, at.Y + t.OffsetY, at.Z + t.OffsetZ);
        }
    }

    /// <summary>Hangs the stocking while the stall is up in the table's season, takes it away otherwise. Idempotent.</summary>
    public static List<string> Sync() => InSeason && FestivalMarket.IsUp ? Place() : Clear();

    /// <summary>Hangs one stocking at the stall, if none hangs there. Place twice places once.</summary>
    public static List<string> Place()
    {
        var placed = Placed();
        placed.RemoveWhere(s => World.FindItem(s) is not HolidayStocking { Deleted: false });
        if (placed.Count > 0)
        {
            return [];
        }

        var stocking = new HolidayStocking();
        stocking.MoveToWorld(Spot, FestivalMarket.Facet);
        placed.Add(stocking.Serial);
        return [$"[HolidayStocking] A holiday stocking hangs at the Festival stall, {Spot}."];
    }

    /// <summary>Takes away exactly the stockings Place hung (by serial).</summary>
    public static List<string> Clear()
    {
        var placed = Placed();
        var n = 0;
        foreach (var serial in placed.ToList())
        {
            if (World.FindItem(serial) is { Deleted: false } item)
            {
                item.Delete();
                n++;
            }
        }

        placed.Clear();
        return n == 0 ? [] : [$"[HolidayStocking] The holiday stocking is taken down ({n})."];
    }

    [Usage("HolidayStocking [status|reload|place|clear]")]
    [Description("The holiday stocking at the Festival stall (cc-P784). status says the season, whether it hangs, and the table in force; reload reads Configuration/holiday-stocking.json again (a bad file is refused whole); place hangs it now at the stall (any season, for testing); clear takes it down. It also hangs and comes down by itself with the winter stall.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, NoDryRun = true, Summary = "Hangs (or takes down) the holiday stocking on the Festival Peddler's table. It hangs by itself while the winter stall is up; reload reads its table again.")]
    private static void OnCommand(CommandEventArgs e)
    {
        foreach (var line in Run(e.Arguments))
        {
            e.Mobile.SendMessage(line);
        }
    }

    public static List<string> Run(string[] args)
    {
        var verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
        switch (verb)
        {
            case "reload":
                return HolidayStockingTable.Reload(HolidayStockingTable.FilePath);
            case "place":
                var placed = Place();
                return placed.Count > 0 ? placed : [$"[HolidayStocking] It already hangs at {Stocking?.Location}."];
            case "clear":
                var cleared = Clear();
                return cleared.Count > 0 ? cleared : ["[HolidayStocking] No stocking was hanging."];
            default:
                var t = Table;
                return
                [
                    $"Holiday stocking: {(t.Enabled ? "on" : "off")}, season {t.Season} (now {FestivalSeasons.Label(FestivalSeasons.Current())}); " +
                    $"{(Stocking is { } s ? $"hangs at {s.Location}" : "not hanging")}.",
                    $"Table ({t.Source}): hold {t.HoldSeconds}s within {t.Range}, {t.GrabsPerDay} a day, {t.TokensOnFirstGrab} token(s) on the first; " +
                    string.Join(", ", t.Prizes.Select(p => $"{p.Type} x{p.Amount} w{p.Weight}")),
                    "[HolidayStocking status|reload|place|clear]"
                ];
        }
    }
}
