using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.Spawners;

namespace Server;

/// <summary>
/// Shattered Legacy (cc-P188; Chase 2026-10-18 11:40, standing in Old Haven: the town crier spawner in the ruins goes).
/// Pinned's own spawn data places a TownCrier in the Old Haven ruins: shared/trammel/TownsPeople.json, "Spawner (123)"
/// at 3652,2619, guid 8b75bcf8-7b83-4d94-8573-92aff88cd41f, one TownCrier every 5 to 10 minutes. Old Haven is a ruin
/// on this shard (the New Haven trainers send new players there to fight undead; cc-P189), so a crier calling the news
/// in it is wrong. The skeleton spawner beside it, "Spawner (113)" at 3651,2627 in post-uoml/trammel/Outdoors.json,
/// is kept on purpose (Chase 2026-10-19 14:50: "skeletons can stay in old haven"): nothing here touches it.
///
/// Which spawner is the crier's is read from the shipped file by its guid (ClusterFStockSpawnData), the same way the
/// Despise exclusion reads its file, so an upstream change to the data is seen by the fact that imports the real file.
///
///   Fresh world: ImportSpawnersCommand asks Admit for every spawner it builds (server/patches/
///     ImportSpawners-despise-exclusion.patch, which calls this beside ClusterFDespiseStockSpawns.Admit), so
///     [GenerateSpawners of shared/trammel/TownsPeople.json, of shared/**, or the admin gump's world build never
///     places it.
///   Existing world: [ClusterFOldHavenRetire (ClusterFOldHavenSeeder.Retire, cc-P189) deletes it and its crier with
///     the mage and Vorlak spawners; dry run first.
/// </summary>
public static class ClusterFOldHavenStockSpawns
{
    public const string TrammelFile = "shared/trammel/TownsPeople.json";

    /// <summary>The crier spawner's guid in the shipped file; the importer keeps the data's guid on the placed spawner.</summary>
    public static readonly Guid CrierSpawner = new("8b75bcf8-7b83-4d94-8573-92aff88cd41f");

    /// <summary>Where the file puts it (for the facts and the retire line).</summary>
    public static readonly Point3D CrierAt = new(3652, 2619, 0);

    /// <summary>The crier spawner as the shipped file describes it, or null when the file no longer has it.</summary>
    public static ClusterFStockSpawnData.StockSpawner CrierStock() =>
        ClusterFStockSpawnData.LoadRelative(TrammelFile, Map.Trammel).FirstOrDefault(s => s.Guid == CrierSpawner);

    /// <summary>Import hook: false for the Old Haven crier spawner on Trammel, true for every other spawner on every map.</summary>
    public static bool Admit(BaseSpawner spawner, Map map) => map != Map.Trammel || spawner.Guid != CrierSpawner;

    /// <summary>
    /// The crier spawner in a world, by its guid, or by its spot and its single TownCrier entry for one placed before
    /// guids were kept (StockSpawner.Matches). Null when the world has none.
    /// </summary>
    public static Spawner FindCrierSpawner()
    {
        var stock = CrierStock();

        foreach (var item in World.Items.Values)
        {
            if (item is Spawner { Deleted: false, Map: var m } s && m == Map.Trammel &&
                (s.Guid == CrierSpawner || stock?.Matches(s) == true))
            {
                return s;
            }
        }

        return null;
    }

    /// <summary>The crier spawner's entry names, for the facts: exactly one TownCrier.</summary>
    public static IEnumerable<string> CrierEntries() => CrierStock()?.Names ?? [];
}
