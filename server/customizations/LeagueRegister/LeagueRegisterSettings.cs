// cc-P280 (P179's League design, Chase's answers of 2026-10-21): every number of the League Register, its League actions,
// the consolidation items, the backpack upgrades and the repair items, as a server setting (modernuo.json,
// "clusterf.leagueRegister.*"), read on each use so a change needs no restart. The defaults are Chase's answers; the
// repair items' are the proposal in shard-migration notes/cc-P280-league-register-build.md.
//
// A rank is written as its metal's key, the ladder's own (ClusterFLeagueRanks.Ladder, "DullCopper", "Bronze" ...), so the
// gate follows the metal if the ladder ever changes. A list is "Metal:value,Metal:value".

using System;
using System.Collections.Generic;
using Server.Mobiles;

namespace Server.Engines.LeagueRegister;

public static class LeagueRegisterSettings
{
    private const string Prefix = "clusterf.leagueRegister.";

    private static string Text(string name, string value) => ServerConfiguration.GetOrUpdateSetting(Prefix + name, value);

    private static int Number(string name, int value) => ServerConfiguration.GetOrUpdateSetting(Prefix + name, value);

    // -- The Register ----------------------------------------------------------------------------------------------

    /// <summary>The rank at which the Registrar issues the Register, free (Chase, answer 2).</summary>
    public static int IssueRank => Rank(Text("issueRank", "DullCopper"));

    /// <summary>Guilds the Register holds at each rank (Chase, answer 2): 2, 4, 6, 9, 12, all 18.</summary>
    public static string CapacityTable => Text("capacity", "DullCopper:2,Bronze:4,Verite:6,Blaze:9,Mythril:12,Celestial:18");

    // -- League actions (Chase, answer 4) ---------------------------------------------------------------------------

    public static int StatusRank => Rank(Text("action.status", "DullCopper"));
    public static int ContractsRank => Rank(Text("action.contracts", "Bronze"));
    public static int TurnInRank => Rank(Text("action.turnIn", "Verite"));
    public static int RepairRank => Rank(Text("action.repair", "Blaze"));

    // -- Consolidation items (Chase, answer 5); Renown first, gold a small second cost (answer 8) -------------------

    public static int SatchelRank => Rank(Text("satchel.rank", "Bronze"));
    public static int SatchelRenown => Number("satchel.renown", 5000);
    public static int SatchelGold => Number("satchel.gold", 500);

    /// <summary>The merge fee per satchel put into the League Field Satchel, in that satchel's guild currency.</summary>
    public static int SatchelMergeFee => Number("satchel.mergeFee", 50);

    public static int LogbookRank => Rank(Text("logbook.rank", "DullCopper"));
    public static int LogbookRenown => Number("logbook.renown", 500);
    public static int LogbookGold => Number("logbook.gold", 50);

    // -- Backpack upgrades (Chase, answer 6): item count only -------------------------------------------------------

    /// <summary>Each step: rank, items, Renown, gold. "Metal:items:renown:gold".</summary>
    public static string BackpackTable =>
        Text("backpack.tiers", "Bronze:150:2000:200,Verite:175:6000:600,Blaze:200:15000:1500,Mythril:250:30000:3000");

    // -- Repair items (Chase, answer 4, his addition; proposal) -----------------------------------------------------

    public static int DeedArmsLore => Number("repair.deed.armsLore", 90);
    public static int DeedIngots => Number("repair.deed.ingots", 5);
    public static int KitArmsLore => Number("repair.kit.armsLore", 110);
    public static int KitCharges => Number("repair.kit.charges", 10);
    public static int KitIngots => Number("repair.kit.ingots", 30);
    public static int RemoteArmsLore => Number("repair.remote.armsLore", 120);
    public static int RemoteIngots => Number("repair.remote.ingots", 20);
    public static int RemoteGems => Number("repair.remote.gems", 1);

    /// <summary>Durability a repair item restores; 0 = to full (a smith's repair).</summary>
    public static int RepairPoints => Number("repair.points", 0);

    // -- Helpers ----------------------------------------------------------------------------------------------------

    /// <summary>The ladder index of a metal key (1 = Iron), or int.MaxValue for a key the ladder does not have.</summary>
    public static int Rank(string metalKey)
    {
        foreach (var info in ClusterFLeagueRanks.Ladder)
        {
            if (string.Equals(info.MetalKey, metalKey?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return info.Index;
            }
        }

        return int.MaxValue;
    }

    /// <summary>"Metal:a:b,Metal:c:d" as (rank, numbers) rows, rank order; a row that does not read is skipped.</summary>
    public static List<(int Rank, int[] Values)> Rows(string table)
    {
        var rows = new List<(int, int[])>();
        foreach (var part in (table ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cells = part.Split(':', StringSplitOptions.TrimEntries);
            var rank = Rank(cells[0]);
            if (rank == int.MaxValue || cells.Length < 2)
            {
                continue;
            }

            var values = new int[cells.Length - 1];
            var ok = true;
            for (var i = 1; i < cells.Length && ok; i++)
            {
                ok = int.TryParse(cells[i], out values[i - 1]);
            }

            if (ok)
            {
                rows.Add((rank, values));
            }
        }

        rows.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return rows;
    }

    /// <summary>The Register's capacity for a character at this League rank: the last row at or below it, else 0.</summary>
    public static int CapacityAt(int rank)
    {
        var capacity = 0;
        foreach (var (rowRank, values) in Rows(CapacityTable))
        {
            if (rowRank <= rank)
            {
                capacity = values[0];
            }
        }

        return capacity;
    }

    public static int Capacity(Mobile m) => CapacityAt(ClusterFLeagueRanks.GetRank(m));
}
