// ClusterFPurgeUpgradeCollisions.cs
//
// cc-P37, part 1 of the ModernUO upgrade (pinned 7c9215d97 to upstream d4531cd9). Run ONCE, on the TEST shard,
// in P39, before that shard first boots the new engine. Not on live: the live save holds none of these
// (cc-P30 run 5, 0 failures), and live is wiped at the upgrade anyway.
//
// Why: 11 types we ported from ServUO now ship upstream under the same names, and the bump takes upstream's
// (Chase, 2026-10-02, closes D-57). Ours are [SerializationGenerator(0, false)], which writes the version as a
// 4-byte int; upstream's are (0), one encoded byte. So every saved instance of ours reads 3 bytes short on the new
// engine and the load stops at a "Delete the object and continue? (y/n/a)" prompt, once for mobiles and once for
// items (cc-P30 section 2c and run 2b: 107 creatures and 1 item on the test save). Deleting them here, on the
// engine that can still read them, and then saving, makes the first boot on the new engine prompt-free.
//
// What it does:
//   1. Deletes every mobile and item whose type is one of the 11 (or derives from one; none does today).
//      A creature's pack and equipment go with it, as any Delete does. Tamed or stabled ones are named in the
//      report, and so are items inside a container, with the top-level holder.
//   2. Holds every spawner that names one of the 11 in an entry for SpawnerHoldMinutes. The entries stay: they
//      are type NAMES, read fine on the new engine, and respawn the creatures as upstream's types there (P30 2c).
//      The hold is there because deleting a spawned creature restarts its spawner's timer (pinned
//      BaseSpawner.cs:463-475), so OUR type could respawn, and be saved by SaveOnShutdown, between the purge and
//      the stop. The hold time is saved with the spawner (End, BaseSpawner.cs:1462), so on the new engine it
//      simply runs out and the spawner fills with upstream's creatures.
//   3. Does not save. Run [save straight after it, then dryrun again (it must report nothing), then stop.
//
// The ValkyriesGlaive stealable slot (our StealableArtifacts patch, slot 85) needs nothing: pinned's slot notices
// the deleted item on its 15-minute check and waits its own respawn delay (StealableArtifacts.cs:385-388, :409-419),
// and the new engine fills the same slot with upstream's ValkyriesGlaive.
//
// GUARD. After the bump the same 11 names are UPSTREAM's types, and this command would delete legitimate
// creatures. So it refuses to run unless every one of the 11 still carries our serialization shape,
// [SerializationGenerator(_, false)], which is exactly the property that breaks the load. Upstream's are encoded.
// P38 should delete this file with our 11 collision files anyway.
//
// In game:   [ClusterFPurgeUpgradeCollisions dryrun      reports, changes nothing
//            [ClusterFPurgeUpgradeCollisions             deletes and holds spawners ("purge" says the same)
// Console:   purgecollisions dryrun | purgecollisions     (docker attach sl-modernuo-test; detach Ctrl+P Ctrl+Q)
// As every world command here, the plain form acts and dryrun reports. Any other word does nothing.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ModernUO.Serialization;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;

namespace Server;

public static class ClusterFPurgeUpgradeCollisions
{
    public const int SpawnerHoldMinutes = 30;

    // cc-P30 section 2c, by name. Six SA throwing artifacts and five Ter Mur creatures.
    public static readonly Type[] CollidingTypes =
    [
        typeof(BansheesCall),
        typeof(RaptorClaw),
        typeof(StoneSlithClaw),
        typeof(StormCaller),
        typeof(ValkyriesGlaive),
        typeof(WindOfCorruption),
        typeof(Raptor),
        typeof(Slith),
        typeof(StoneSlith),
        typeof(ToxicSlith),
        typeof(Spellbinder)
    ];

    private const int InGameDetailLines = 25;

    public static void Configure()
    {
        CommandSystem.Register("ClusterFPurgeUpgradeCollisions", AccessLevel.Administrator, OnCommand);

        ConsoleInputHandler.RegisterCommand(
            ["purgecollisions"],
            "cc-P37: purge the 11 upgrade-colliding types. 'dryrun' reports; no argument or 'purge' deletes. Test shard only.",
            args =>
            {
                void Say(string line) => Console.WriteLine($"[PurgeUpgradeCollisions] {line}");

                var purge = ParseMode(args);
                if (purge == null)
                {
                    Say(UsageLine);
                    return;
                }

                Core.LoopContext.Post(() => Run(!purge.Value, Say));
            }
        );
    }

    [Usage("ClusterFPurgeUpgradeCollisions [dryrun|purge]")]
    [Description("cc-P37: deletes every instance of the 11 types upstream now ships under our names, and holds their spawners, so the first boot on the new engine has nothing to prompt about. Test shard only, once, in P39.")]
    [ShardCommand(CommandCategory.WorldRemoval, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.TestOnly,
        DryRun = "dryrun", Summary = "DELETES all Raptors, Sliths, Spellbinders and six SA throwing artifacts before the engine upgrade. Any argument but dryrun or purge does nothing.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var purge = ParseMode(e.ArgString);
        if (purge == null)
        {
            from.SendMessage(UsageLine);
            return;
        }

        var lines = 0;

        Run(
            !purge.Value,
            line =>
            {
                Console.WriteLine($"[PurgeUpgradeCollisions] {line}");

                if (++lines <= InGameDetailLines || line.StartsWith("TOTAL", StringComparison.Ordinal) ||
                    line.StartsWith("REFUSED", StringComparison.Ordinal))
                {
                    from.SendMessage(line);
                }
                else if (lines == InGameDetailLines + 1)
                {
                    from.SendMessage("(more lines in the server log: docker logs sl-modernuo-test)");
                }
            }
        );
    }

    private const string UsageLine = "Usage: ClusterFPurgeUpgradeCollisions [dryrun|purge]. dryrun reports; no argument or purge deletes; nothing else is accepted.";

    // True to purge (no argument or "purge"), false for a dry run ("dryrun"), null for anything else.
    public static bool? ParseMode(string arg)
    {
        var word = arg?.Trim() ?? "";

        if (word.Equals("dryrun", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return word.Length == 0 || word.Equals("purge", StringComparison.OrdinalIgnoreCase) ? true : null;
    }

    // Our ports carry [SerializationGenerator(version, false)]: the version is a plain int. Upstream's carry the
    // encoded form. A type that is not ours any more must never be purged.
    public static bool IsOurPort(Type type) =>
        type.GetCustomAttribute<SerializationGeneratorAttribute>(false) is { EncodedVersion: false };

    public static List<Type> TypesThatAreNotOurPorts(IEnumerable<Type> types) => types.Where(t => !IsOurPort(t)).ToList();

    public static bool IsColliding(Type type)
    {
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            if (Array.IndexOf(CollidingTypes, t) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    public sealed class Report
    {
        public bool Refused;
        public bool DryRun;
        public readonly SortedDictionary<string, int> CountByType = new(StringComparer.Ordinal);
        public readonly List<Mobile> Mobiles = new();
        public readonly List<Item> Items = new();
        public readonly List<BaseSpawner> Spawners = new();
        public int Deleted;
        public int SpawnersHeld;
    }

    public static Report Run(bool dryRun, Action<string> say) => Run(dryRun, say, CollidingTypes);

    // The type list is a parameter only so the guard can be shown to refuse (PurgeUpgradeCollisionsVerification).
    public static Report Run(bool dryRun, Action<string> say, IReadOnlyList<Type> guardedTypes)
    {
        var report = new Report { DryRun = dryRun };

        var notOurs = TypesThatAreNotOurPorts(guardedTypes);
        if (notOurs.Count > 0)
        {
            report.Refused = true;
            say(
                $"REFUSED: {string.Join(", ", notOurs.Select(t => t.Name))} no longer carry our serialization shape " +
                "[SerializationGenerator(_, false)]. After the upgrade these names are upstream's types; nothing was deleted."
            );
            return report;
        }

        foreach (var m in World.Mobiles.Values)
        {
            if (!m.Deleted && IsColliding(m.GetType()))
            {
                report.Mobiles.Add(m);
            }
        }

        foreach (var item in World.Items.Values)
        {
            if (item.Deleted)
            {
                continue;
            }

            if (IsColliding(item.GetType()))
            {
                report.Items.Add(item);
            }
            else if (item is BaseSpawner spawner && NamesACollidingType(spawner))
            {
                report.Spawners.Add(spawner);
            }
        }

        foreach (var m in report.Mobiles)
        {
            Count(report, m.GetType());
        }

        foreach (var item in report.Items)
        {
            Count(report, item.GetType());
        }

        say($"{(dryRun ? "DRY RUN, nothing changed" : "PURGE")}: {report.Mobiles.Count} mobile(s), {report.Items.Count} item(s), {report.Spawners.Count} spawner(s) naming one of the 11.");

        foreach (var (name, count) in report.CountByType)
        {
            say($"  {name}: {count}");
        }

        // The ones a person might miss: tamed, stabled or shrunk creatures, and items someone holds.
        foreach (var m in report.Mobiles)
        {
            if (m is BaseCreature { Controlled: true } or BaseCreature { IsStabled: true } || m.Map == Map.Internal)
            {
                var bc = m as BaseCreature;
                var owner = bc?.ControlMaster?.Name ?? bc?.StabledBy?.Name ?? "nobody";
                say($"  owned: {m.GetType().Name} 0x{m.Serial.Value:X} \"{m.Name}\" owner {owner}, map {m.Map}, stabled {bc?.IsStabled == true}");
            }
        }

        foreach (var item in report.Items)
        {
            var root = item.RootParent;
            var where = root switch
            {
                Mobile rm => $"held by {rm.GetType().Name} \"{rm.Name}\"",
                Item ri   => $"inside {ri.GetType().Name} 0x{ri.Serial.Value:X} at {ri.Location} {ri.Map}",
                _         => $"at {item.Location} {item.Map}"
            };
            say($"  item: {item.GetType().Name} 0x{item.Serial.Value:X} {where}");
        }

        foreach (var spawner in report.Spawners)
        {
            var names = string.Join(", ", spawner.Entries.Select(se => se.SpawnedName));
            say($"  spawner: 0x{spawner.Serial.Value:X} at {spawner.Location} {spawner.Map}, running {spawner.Running}: {names}");
        }

        if (!dryRun)
        {
            // Mobiles first: a creature's own pack and equipment go with it, so an item in that list may already be gone.
            foreach (var m in report.Mobiles)
            {
                if (!m.Deleted)
                {
                    m.Delete();
                    report.Deleted++;
                }
            }

            foreach (var item in report.Items)
            {
                if (!item.Deleted)
                {
                    item.Delete();
                    report.Deleted++;
                }
            }

            foreach (var spawner in report.Spawners)
            {
                if (!spawner.Deleted && spawner.Running)
                {
                    spawner.DoTimer(TimeSpan.FromMinutes(SpawnerHoldMinutes));
                    report.SpawnersHeld++;
                }
            }
        }

        say(
            dryRun
                ? $"TOTAL (dry run): would delete {report.Mobiles.Count + report.Items.Count} and hold {report.Spawners.Count(s => s.Running)} spawner(s) for {SpawnerHoldMinutes} min."
                : $"TOTAL: deleted {report.Deleted}, held {report.SpawnersHeld} spawner(s) for {SpawnerHoldMinutes} min. Now [save, run dryrun again (expect 0), and stop the shard."
        );

        return report;
    }

    private static void Count(Report report, Type type)
    {
        report.CountByType.TryGetValue(type.Name, out var n);
        report.CountByType[type.Name] = n + 1;
    }

    private static bool NamesACollidingType(BaseSpawner spawner)
    {
        foreach (var entry in spawner.Entries)
        {
            var type = AssemblyHandler.FindTypeByName(entry.SpawnedName);
            if (type != null && IsColliding(type))
            {
                return true;
            }
        }

        return false;
    }
}
