using System.Collections.Generic;
using System.IO;
using Server.Engines.Spawners;

namespace Server;

/// <summary>
/// Diagnostic: dumps all mobiles and notable items in a rectangular area
/// to a text file on the server so it can be SCP'd for review.
///
/// Usage (in-game, admin):
///   [ClusterFAreaScan haven      — entire New Haven + Old Haven island
///   [ClusterFAreaScan oldhaven   — Old Haven ruins only
///   [ClusterFAreaScan newhaven   — New Haven town only
///
/// Output file: /tmp/areascan-{zone}.txt
/// Retrieve with: scp boblin@10.7.4.120:/tmp/areascan-haven.txt .
/// </summary>
public static class ClusterFAreaScan
{
    private static readonly Dictionary<string, (int x1, int y1, int x2, int y2)> Zones = new()
    {
        ["haven"]    = (3400, 2400, 3760, 2760),   // whole island
        ["oldhaven"] = (3520, 2420, 3700, 2640),   // Old Haven ruins
        ["newhaven"] = (3400, 2490, 3570, 2760),   // New Haven town
    };

    public static void Configure()
    {
        CommandSystem.Register("ClusterFAreaScan", AccessLevel.Administrator, OnCommand);
    }

    [Usage("ClusterFAreaScan [haven|oldhaven|newhaven]")]
    [Description("Writes all mobiles and notable items in the zone to /tmp/areascan-<zone>.txt")]
    private static void OnCommand(CommandEventArgs e)
    {
        var zone = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "oldhaven";
        if (!Zones.TryGetValue(zone, out var rect))
        {
            e.Mobile.SendMessage($"Unknown zone '{zone}'. Valid: haven, oldhaven, newhaven");
            return;
        }

        var (x1, y1, x2, y2) = rect;
        var outPath = $"/tmp/areascan-{zone}.txt";
        e.Mobile.SendMessage($"Scanning {zone} ({x1},{y1})-({x2},{y2})...");

        using var w = new StreamWriter(outPath, append: false);
        w.WriteLine($"# Area scan: {zone}  bounds ({x1},{y1})-({x2},{y2}) Trammel");
        w.WriteLine();
        ScanMobiles(w, x1, y1, x2, y2);
        ScanItems(w, x1, y1, x2, y2);

        e.Mobile.SendMessage($"Done — retrieve with: scp boblin@10.7.4.120:{outPath} .");
    }

    // ------------------------------------------------------------------
    private static void ScanMobiles(StreamWriter w, int x1, int y1, int x2, int y2)
    {
        var lines = new List<string>();
        int total = 0, players = 0;

        foreach (var m in World.Mobiles.Values)
        {
            if (m.Deleted || m.Map != Map.Trammel) continue;
            if (m.X < x1 || m.X > x2 || m.Y < y1 || m.Y > y2) continue;
            if (m.Player) { players++; continue; }

            total++;
            string flags = "";
            if (m is Spawner) flags += " [SPAWNER]";
            else if (m is Server.Mobiles.BaseVendor) flags += " [VENDOR]";
            if (m is Server.Mobiles.BaseCreature bc && bc.CantWalk) flags += " [STATIC]";

            lines.Add($"MOB  ({m.X,4},{m.Y,4},{m.Z,3})  {m.GetType().Name,-40} \"{m.Name}\"{flags}");
        }

        lines.Sort();
        w.WriteLine($"=== MOBILES  ({total} NPCs, {players} players) ===");
        foreach (var l in lines) w.WriteLine(l);
        w.WriteLine();
    }

    // ------------------------------------------------------------------
    private static void ScanItems(StreamWriter w, int x1, int y1, int x2, int y2)
    {
        var lines = new List<string>();

        foreach (var item in World.Items.Values)
        {
            if (item.Deleted || item.Map != Map.Trammel) continue;
            if (item.X < x1 || item.X > x2 || item.Y < y1 || item.Y > y2) continue;
            if (item.Parent != null) continue;  // skip equipped/contained

            var name = item.GetType().Name;

            // Keep only things that could be misplaced: spawners, signs,
            // moongates, teleporters, addons, books, ankh-type objects.
            bool keep =
                item is Spawner ||
                item is Server.Items.Sign ||
                item is Server.Items.Moongate ||
                item is Server.Items.PublicMoongate ||
                item is Server.Items.Teleporter ||
                item is Server.Items.BaseAddon ||
                item is Server.Items.AddonComponent ||
                item is Server.Items.BaseBook ||
                name.Contains("Moongate", System.StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Teleport", System.StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Sign",     System.StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Spawn",    System.StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Addon",    System.StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Ankh",     System.StringComparison.OrdinalIgnoreCase);

            if (!keep) continue;

            lines.Add($"ITEM ({item.X,4},{item.Y,4},{item.Z,3})  {name,-40} \"{item.Name}\"  ItemID=0x{item.ItemID:X4}");
        }

        lines.Sort();
        w.WriteLine($"=== ITEMS of interest  ({lines.Count}) ===");
        foreach (var l in lines) w.WriteLine(l);
    }
}
