using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.Spawners;
using Server.Tutorial;

namespace Server;

/// <summary>
/// Shattered Legacy (cc-P678, SoS swap S7; Chase 2026-10-27: "stock pre-UOML Haven plus our pieces"). On the Siege of
/// Sosaria map New Haven is the author's Haven town, which is OSI's pre-UOML Haven rebuilt, and the ML plaza it replaced
/// is forest. So the town's people come from pinned's pre-UOML spawn data and the plaza's stop coming:
///
///   Haven's own: pinned uoml/trammel/Vendors.json and uoml/trammel/TownsLife.json, the spawners inside the town
///     (HavenSpots.Town): the Second Bank's banker, the healers, the smiths, the inn, the shops and the town's pets. The
///     shard imports post-uoml/** and shared/** (ClusterFStockSpawnData), never all of uoml/** (P32): these two files are
///     imported with every spawner outside the town refused, so the other pre-UOML towns never come in.
///   The plaza's: every spawner of pinned post-uoml/trammel/Vendors.json and TownsLife.json inside pinned's ML "New
///     Haven" region (3416-3545, 2480-2648) is refused: 58 vendors, nobles, escorts and town pets that would stand in the
///     forest. Wild spawners there (Outdoors.json, Reagents.json) are S9's to judge and are admitted.
///   Old Haven's ruin: the post-uoml/shared spawners inside the town whose entries are the ruin's (undead, the spellbinder,
///     the brigand camps, the Mongbat wildlife at the town's west and east edges, and the lone Healer in the bank's
///     ruin) are refused: 10 spawners, the town is not a ruin any more. The pumpkin and sheep fields and the town crier stay (the
///     crier was refused here while Old Haven was a ruin, cc-P188; he is the town's again).
///
/// The ML trainers' quests train in "Old Haven Training" against that undead field (ApprenticeTraining), so the field
/// moves out of the town to the north woods (TrainingField, 3640,2418,34, the woods between the town and the north shore),
/// with pinned's entries, count and delays and a home and walking range of 40 instead of 60, so nothing it spawns reaches
/// the town (y 2460). The region moves with it (ApprenticeTrainingRegion.OldHavenArea). It is placed at world load when
/// missing, found by its guid.
///
/// Every refusal is read from the shipped files by guid, so an upstream change to the data is seen by the facts that read
/// the real files (HavenTownSpawnsVerification). The import hook is server/patches/ImportSpawners-despise-exclusion.patch,
/// which calls Admit beside ClusterFDespiseStockSpawns.Admit for every spawner [GenerateSpawners builds.
/// A fresh test world is built on swap day (Chase 2026-10-27), so nothing here retires spawners from an existing world.
/// </summary>
public static class ClusterFHavenTownSpawns
{
    /// <summary>Pinned's pre-UOML files Haven's people come from; the console's fresh-world order imports them.</summary>
    public static readonly string[] HavenFiles = ["uoml/trammel/Vendors.json", "uoml/trammel/TownsLife.json"];

    /// <summary>The ML plaza's town files: their spawners inside the old New Haven region are refused.</summary>
    public static readonly string[] PlazaFiles = ["post-uoml/trammel/Vendors.json", "post-uoml/trammel/TownsLife.json"];

    /// <summary>Pinned regions.json's "New Haven" town region (ML), inclusive.</summary>
    public static bool InOldPlaza(Point3D p) => p.X >= 3416 && p.X <= 3545 && p.Y >= 2480 && p.Y <= 2648;

    /// <summary>The entries that mark a spawner as Old Haven's ruin, not the town's.</summary>
    public static readonly HashSet<string> RuinEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        "Zombie", "Skeleton", "spellbinder", "BrigandCamp", "Mongbat", "Healer"
    };

    /// <summary>Pinned's Old Haven undead field, "Spawner (113)" at 3650,2509 in post-uoml/trammel/Outdoors.json.</summary>
    public static readonly Guid RuinFieldGuid = new("1af71e9c-8891-43d9-940a-bdbd94560583");

    /// <summary>The field's new spawner: its own guid, so the stock one's refusal and this one never meet.</summary>
    public static readonly Guid TrainingFieldGuid = new("6f3e7c2a-5b1d-4c8e-9a47-0d2b6e8f1678");

    /// <summary>The north woods, standable on SoS (soswalk, reachable on foot from the town).</summary>
    public static readonly Point3D TrainingField = new(3640, 2418, 34);

    public const int TrainingFieldRange = 40;

    private static HashSet<Guid> _refused;

    /// <summary>
    /// Guids of the stock spawners this refuses on Trammel (the plaza's, the ruin's, and the pre-UOML files' outside the
    /// town), from the shipped files. By guid and the file's location, since the importer asks before it places the
    /// spawner (the spawner has no location yet).
    /// </summary>
    public static HashSet<Guid> Refused
    {
        get
        {
            if (_refused == null)
            {
                var set = new HashSet<Guid>();
                foreach (var file in PlazaFiles)
                {
                    foreach (var s in ClusterFStockSpawnData.LoadRelative(file, Map.Trammel))
                    {
                        if (InOldPlaza(s.Location))
                        {
                            set.Add(s.Guid);
                        }
                    }
                }

                foreach (var s in ClusterFStockSpawnData.Load(Map.Trammel, ClusterFStockSpawnData.ShardFolders))
                {
                    if (HavenSpots.InTown(s.Location) && s.Names.Any(RuinEntries.Contains))
                    {
                        set.Add(s.Guid);
                    }
                }

                // the pre-UOML files: everything outside the town (the other pre-UOML towns)
                foreach (var file in HavenFiles)
                {
                    foreach (var s in ClusterFStockSpawnData.LoadRelative(file, Map.Trammel))
                    {
                        if (!HavenSpots.InTown(s.Location))
                        {
                            set.Add(s.Guid);
                        }
                    }
                }

                _refused = set;
            }

            return _refused;
        }
    }

    public static void Configure()
    {
        EventSink.WorldLoad += () => SeedTrainingField(Map.Trammel);
    }

    public static Spawner FindTrainingField(Map map)
    {
        foreach (var item in World.Items.Values)
        {
            if (item is Spawner { Deleted: false } s && s.Map == map && s.Guid == TrainingFieldGuid)
            {
                return s;
            }
        }

        return null;
    }

    /// <summary>Places the training field when the world has none; returns the one placed, or null.</summary>
    public static Spawner SeedTrainingField(Map map)
    {
        if (map == null || FindTrainingField(map) != null)
        {
            return null;
        }

        var stock = ClusterFStockSpawnData.LoadRelative("post-uoml/trammel/Outdoors.json", map)
            .FirstOrDefault(s => s.Guid == RuinFieldGuid);
        if (stock == null)
        {
            return null;
        }

        // pinned's entries (70 zombies, 70 skeletons, 20 spellbinders, 90 at once, every 5 to 10 minutes)
        var spawner = new Spawner(90, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), 0, default, stock.Names)
        {
            Guid = TrainingFieldGuid
        };

        foreach (var entry in spawner.Entries)
        {
            entry.SpawnedMaxCount = entry.SpawnedName.Equals("spellbinder", StringComparison.OrdinalIgnoreCase) ? 20 : 70;
        }

        // As pinned's importer does (ImportSpawnersCommand.cs:248-249); the range after the move, so it centres on the spot.
        spawner.MoveToWorld(TrainingField, map);
        spawner.HomeRange = TrainingFieldRange;
        spawner.WalkingRange = TrainingFieldRange;
        spawner.Respawn();
        return spawner;
    }

    /// <summary>Import hook: false for a refused spawner on Trammel, true for every other spawner on every map.</summary>
    public static bool Admit(BaseSpawner spawner, Map map) => map != Map.Trammel || !Refused.Contains(spawner.Guid);
}
