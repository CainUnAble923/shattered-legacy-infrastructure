// CasinoSeeder.cs
//
// cc-P811: puts the casino in the world by itself, on a new world and on an existing one: at every world load each placement
// in casino.json (two slot machines and a blackjack table in the Haven inn's east room, by default) gets its machine or table
// unless one already stands there. Idempotent; [Casino seed runs the same pass, and [Casino status shows the books.

using System;
using System.Collections.Generic;
using Server.Commands;

namespace Server.Casino;

public static class CasinoSeeder
{
    public static void Configure()
    {
        foreach (var line in CasinoSettings.LoadOrWriteDefaults(CasinoSettings.FilePath))
        {
            Console.WriteLine($"[Casino] {line}");
        }

        EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () =>
        {
            var placed = Seed();
            if (placed > 0)
            {
                Console.WriteLine($"[Casino] placed {placed} machine(s) and table(s).");
            }
        });

        Register();
    }

    public static void Register()
    {
        if (!CommandSystem.Entries.ContainsKey("Casino"))
        {
            CommandSystem.Register("Casino", AccessLevel.GameMaster, OnCommand);
        }
    }

    private static bool StandsAt<T>(Map map, Point3D p) where T : Item
    {
        foreach (var item in map.GetItemsAt(p.X, p.Y))
        {
            if (item is T && !item.Deleted)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Places what is missing; returns how many it placed. Nothing when the casino is off.</summary>
    public static int Seed()
    {
        var s = CasinoSettings.Current;
        if (!s.Enabled)
        {
            return 0;
        }

        var placed = 0;
        foreach (var p in s.Placements)
        {
            var map = Map.Parse(p.Map);
            if (map == null || map == Map.Internal)
            {
                continue;
            }

            var at = new Point3D(p.X, p.Y, p.Z);
            if (p.Kind == "Slot" && !StandsAt<SlotMachine>(map, at))
            {
                new SlotMachine { Where = p.Where }.MoveToWorld(at, map);
                placed++;
            }
            else if (p.Kind == "Blackjack" && !StandsAt<BlackjackTable>(map, at))
            {
                var table = new BlackjackTable { Where = p.Where };
                table.MoveToWorld(at, map);
                table.Dress();
                placed++;
            }
        }

        return placed;
    }

    [Usage("Casino [status|reload|seed]")]
    [Description("The casino (cc-P811): status shows the books (gold in, out and kept, today and all time) and the house edge; reload reads Configuration/casino.json again (a bad file is refused whole); seed places any missing slot machine or blackjack table.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.Any, NoDryRun = true,
        Summary = "Shows the casino's books; seed places any missing slot machine or blackjack table.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
        List<string> lines = verb switch
        {
            "status" => CasinoLedger.Status(),
            "reload" => CasinoSettings.Reload(CasinoSettings.FilePath),
            "seed"   => [$"Placed {Seed()} machine(s) and table(s)."],
            _        => ["Usage: [Casino [status|reload|seed]"]
        };

        foreach (var line in lines)
        {
            e.Mobile.SendMessage(line);
        }
    }
}
