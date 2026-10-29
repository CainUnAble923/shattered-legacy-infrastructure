// SosTeleporters.cs
//
// cc-P679 (SoS swap S4): the stock teleporters on the Siege of Sosaria land. Trammel and Felucca both stand on the SoS
// map (SosMaps.cs), so pinned's Data/teleporters.json is right for every other facet but not for those two: some of
// its ends are now water, rock or a different floor. Data/ShatteredLegacy/teleporters-sos.json holds every pinned entry
// with an end on Trammel or Felucca, as decided for SoS (re-placed onto a floor, or left out with its reason in
// shard-migration notes/cc-P679-tools/out-tele-decisions.csv; made by tele_fix.py there). [TelGen and [TelGenDelete
// read pinned's list through Merge (server/patches/GenTeleporter-sos-teleporters.patch): pinned's entries that touch
// neither facet, then ours. No pinned entry outside those facets shares a teleporter's tile with ours, so the order
// changes nothing.

using System;
using System.Collections.Generic;
using System.IO;
using Server.Json;
using Server.Logging;

namespace Server.Commands;

public static class SosTeleporters
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(SosTeleporters));

    /// <summary>Our Trammel and Felucca teleporters, beside pinned's Data/teleporters.json.</summary>
    public static string DataPath => Path.Combine(Core.BaseDirectory, "Data/ShatteredLegacy/teleporters-sos.json");

    /// <summary>True when the entry has an end on a facet that stands on the SoS land.</summary>
    public static bool OnSosLand(TeleporterDefinition d) => IsSos(d.Source.Map) || IsSos(d.Destination.Map);

    private static bool IsSos(Map map) => map == Map.Trammel || map == Map.Felucca;

    /// <summary>Pinned's entries that touch neither Trammel nor Felucca, then <paramref name="ours"/>.</summary>
    public static List<TeleporterDefinition> Merge(List<TeleporterDefinition> pinned, List<TeleporterDefinition> ours)
    {
        var merged = new List<TeleporterDefinition>(pinned.Count);
        foreach (var d in pinned)
        {
            if (!OnSosLand(d))
            {
                merged.Add(d);
            }
        }

        foreach (var d in ours)
        {
            if (!OnSosLand(d))
            {
                throw new InvalidDataException($"{DataPath}: {d} has no end on Trammel or Felucca");
            }

            merged.Add(d);
        }

        return merged;
    }

    /// <summary>
    /// Pinned's list with our file in place of its Trammel and Felucca entries. Without our file (a host that ships no
    /// Data/ShatteredLegacy) pinned's list is used as it is, and the log says so.
    /// </summary>
    public static List<TeleporterDefinition> Apply(List<TeleporterDefinition> pinned)
    {
        if (!File.Exists(DataPath))
        {
            logger.Warning("Teleporters: no {File}, so pinned's Trammel and Felucca teleporters are used", DataPath);
            return pinned;
        }

        var ours = JsonConfig.Deserialize<List<TeleporterDefinition>>(DataPath) ??
                   throw new InvalidDataException($"Failed to read {DataPath}");
        return Merge(pinned, ours);
    }
}
