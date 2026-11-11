// cc-P861: our extra NPC name lists, and one rule so people in the same town stop sharing a name.
//
// The lists (Chase 11-08, P854 item 1) are data, server/data/names-shard.json, which the Dockerfile copies to
// Data/ShatteredLegacy/ beside pinned's Data/names.json (never edited). They come from Sirenix's "Custom expanded
// names.xml" (servuo.dev, 2014; reference only, D:\UO\third-party\code\expanded-names), converted and read by eye by
// shard-migration notes/cc-P861-tools/names861.py. Two kinds of row:
//   - "new": a list pinned does not have (elf male and female, gargoyle male and female, druid, paladin and paladin
//     title, juka, meer, gladiator male, female and title), added to pinned's table so NameList.RandomName finds it;
//   - "extend": more names for a list pinned has (gargoyle vendor, savage rider, golem controller), only the names it
//     lacks, so the gargoyle vendors (19 names in pinned) stop repeating each other.
// Applied at Initialize, after pinned's NameList.Configure has read names.json; a second apply changes nothing.
//
// The rule: within one town region, two people (not players, pets or summons) whose names were drawn from the same
// list never share it. After every world load (on the next tick, so the world is in place), five minutes on (the start's
// spawns) and every hour after, each
// repeat but the oldest (lowest serial) gets a new name from the same list that nobody in that town has. A name not in
// any list (Elara Voss) is a fixed identity and is never touched, and so is a fixed-identity person whose name is also
// in a list (Mara, FixedNameTypes; cc-P877): their name is reserved in their town; a second sweep changes nothing. Pinned keeps its
// table private, so the new lists are added through it by reflection (ShardBodyTypes does the same for body types):
// if pinned changes that field, the start fails loudly here and the facts go red.
//
//   [ShardNames status | dedupe [dryrun]

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server.Commands;
using Server.Mobiles;
using Server.Regions;

namespace Server.Misc;

public sealed class ShardNameRow
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("mode")] public string Mode { get; set; } = "new";
    [JsonPropertyName("names")] public string[] Names { get; set; } = [];
}

public sealed class ShardNameFile
{
    [JsonPropertyName("credit")] public string Credit { get; set; } = "";
    [JsonPropertyName("lists")] public List<ShardNameRow> Lists { get; set; } = [];
}

public static class ShardNames
{
    public const string FileName = "names-shard.json";

    /// <summary>The lists the sweep keeps unique in a town: people's first names. Titles are never swept.</summary>
    public static readonly string[] PersonLists =
    [
        "female", "male", "elf female", "elf male", "gargoyle female", "gargoyle male", "gargoyle vendor", "druid",
        "paladin", "juka", "meer", "gladiator female", "gladiator male", "savage rider", "golem controller"
    ];

    /// <summary>
    /// A shipped data file: Data/ShatteredLegacy/ beside the server (the runtime image), else Distribution/Data/ShatteredLegacy/
    /// (or, for pinned's own, Distribution/Data/) in a folder above it (the builder, where the facts run); ClusterFTalkRules'
    /// lookup. Null when neither exists.
    /// </summary>
    public static string? FindDataFile(string fileName, bool ours = true)
    {
        var sub = ours ? Path.Combine("Data", "ShatteredLegacy") : "Data";
        var beside = Path.Combine(Core.BaseDirectory, sub, fileName);
        if (File.Exists(beside))
        {
            return beside;
        }

        for (var d = new DirectoryInfo(Core.BaseDirectory); d != null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, "Distribution", sub, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static ShardNameFile? Loaded { get; private set; }

    private static Timer? _hourly;

    // The world loads after every Configure and before any Initialize, so the load hook is set here (PeacockSystem's way);
    // its sweep runs on the first tick after the load, when Initialize has applied the lists. The spawners fill the towns
    // in the minutes after a start, so a second sweep follows five minutes on, then one every hour (cc-P861 run 1 found
    // the hook set in Initialize never fired, and the start's spawns sharing names).
    public static void Configure()
    {
        EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () =>
        {
            Print(DedupeAll());
            _hourly ??= Timer.DelayCall(TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), () => Print(DedupeAll()));
        });
    }

    public static void Initialize()
    {
        var path = FindDataFile(FileName);
        Print(path == null ? [$"{FileName} not found beside the server: no extra name lists."] : Apply(Read(path)));
        CommandSystem.Register("ShardNames", AccessLevel.GameMaster, OnCommand);
    }

    private static void Print(List<string> lines)
    {
        foreach (var line in lines)
        {
            Console.WriteLine($"[ShardNames] {line}");
        }
    }

    public static ShardNameFile Read(string path)
    {
        var file = JsonSerializer.Deserialize<ShardNameFile>(File.ReadAllText(path));
        if (file == null || file.Lists.Count == 0)
        {
            throw new InvalidDataException($"ShardNames: {path} has no lists.");
        }

        return file;
    }

    private static Dictionary<string, NameList> Table()
    {
        var field = typeof(NameList).GetField("m_Table", BindingFlags.NonPublic | BindingFlags.Static);
        if (field?.GetValue(null) is not Dictionary<string, NameList> table)
        {
            throw new InvalidOperationException(
                "ShardNames: pinned NameList has no static Dictionary<string, NameList> m_Table (UOContent/Misc/NameList.cs). " +
                "The name table changed shape; fix ShardNames before starting the shard."
            );
        }

        return table;
    }

    /// <summary>Adds the new lists and the extra names to pinned's table. Idempotent.</summary>
    public static List<string> Apply(ShardNameFile file)
    {
        var table = Table();
        var report = new List<string>();
        int added = 0, extended = 0, names = 0;

        foreach (var row in file.Lists)
        {
            var clean = row.Names.Select(n => n.Trim()).Where(n => n.Length > 0).ToArray();
            if (row.Mode == "extend")
            {
                if (!table.TryGetValue(row.Type, out var list))
                {
                    report.Add($"refused: '{row.Type}' is to extend a list pinned does not have");
                    continue;
                }

                var have = new HashSet<string>(list.List, StringComparer.OrdinalIgnoreCase);
                var more = clean.Where(have.Add).ToArray();
                if (more.Length > 0)
                {
                    list.List = list.List.Concat(more.Select(n => n.Intern())).ToArray();
                    extended++;
                    names += more.Length;
                }
            }
            else
            {
                var unique = clean.Distinct(StringComparer.OrdinalIgnoreCase).Select(n => n.Intern()).ToArray();
                if (!table.TryGetValue(row.Type, out var list))
                {
                    table[row.Type] = new NameList { Type = row.Type, List = unique };
                    added++;
                    names += unique.Length;
                }
                else if (!list.List.SequenceEqual(unique))
                {
                    list.List = unique;
                }
            }
        }

        Loaded = file;
        report.Add($"{added} list(s) added, {extended} extended, {names} name(s) new ({file.Lists.Count} rows; {file.Credit}).");
        return report;
    }

    /// <summary>
    /// A name from <paramref name="type"/>, or from <paramref name="fallback"/> while ours are not loaded (the test host
    /// runs no Initialize; a live server always has them).
    /// </summary>
    public static string RandomName(string type, string fallback) =>
        NameList.RandomName(type) is { Length: > 0 } name ? name : NameList.RandomName(fallback);

    /// <summary>The list a person's name came from: their sex's list first, then the others in PersonLists.</summary>
    public static string? ListOf(BaseCreature m)
    {
        if (string.IsNullOrEmpty(m.Name))
        {
            return null;
        }

        var first = m.Female ? "female" : "male";
        foreach (var type in PersonLists.OrderBy(t => t == first ? 0 : 1))
        {
            if (NameList.GetNameList(type)?.ContainsName(m.Name) == true)
            {
                return type;
            }
        }

        return null;
    }

    /// <summary>
    /// cc-P877 (Codex's second P2 on P861): the people whose name is their identity, set by their own code, never drawn
    /// from a list, though the name may also be in one (Mara is in pinned's female list). They are never renamed, and
    /// their name is theirs in their town: an ordinary person sharing it is renamed however old they are.
    /// </summary>
    public static readonly Type[] FixedNameTypes =
    [
        typeof(ClusterFTownsfolk), typeof(Server.Tutorial.TutorialGuide), typeof(Server.Tutorial.TutorialHealer),
        typeof(TutorialWoundedHelper), typeof(ThievesDenLookout)
    ];

    public static bool FixedName(BaseCreature m) =>
        m is Server.Engines.Townsfolk.ITalkResident || FixedNameTypes.Any(t => t.IsInstanceOfType(m));

    public static bool Sweeps(Mobile m) =>
        m is BaseCreature { Deleted: false, Player: false, Controlled: false, Summoned: false } bc &&
        bc.Map != null && bc.Map != Map.Internal && bc.Region.GetRegion<TownRegion>() != null;

    /// <summary>Renames repeats within each town on <paramref name="map"/>. Idempotent.</summary>
    public static List<string> Dedupe(Map map, bool dryRun = false)
    {
        var people = World.Mobiles.Values.Where(m => m.Map == map && Sweeps(m)).Cast<BaseCreature>().ToList();
        var report = DedupeAmong(people, dryRun, out var renamed);
        report.Add($"{map}: {renamed} repeated name(s) {(dryRun ? "to rename" : "renamed")} across {people.Count} town people.");
        return report;
    }

    /// <summary>The rule over <paramref name="people"/> (those Sweeps keeps), grouped by the town region each stands in.</summary>
    public static List<string> DedupeAmong(IEnumerable<BaseCreature> people, bool dryRun, out int renamed)
    {
        var report = new List<string>();
        renamed = 0;

        foreach (var town in people.Where(Sweeps).GroupBy(m => m.Region.GetRegion<TownRegion>()!))
        {
            var used = new HashSet<string>(town.Select(m => m.Name ?? ""), StringComparer.OrdinalIgnoreCase);
            var reserved = new HashSet<string>(town.Where(FixedName).Select(m => m.Name ?? ""), StringComparer.OrdinalIgnoreCase);
            var withList = town.Where(m => !FixedName(m)).Select(m => (m, list: ListOf(m))).Where(t => t.list != null).ToList();

            foreach (var group in withList.GroupBy(t => (t.m.Name ?? "").ToLowerInvariant())
                         .Where(g => g.Count() > 1 || reserved.Contains(g.Key)))
            {
                // the oldest keeps a shared name, unless a fixed-identity person in the town holds it
                foreach (var (m, list) in group.OrderBy(t => t.m.Serial.Value).Skip(reserved.Contains(group.Key) ? 0 : 1))
                {
                    var free = NameList.GetNameList(list!)!.List.Where(n => !used.Contains(n)).ToList();
                    if (free.Count == 0)
                    {
                        report.Add($"{town.Key.Name}: no free name left in '{list}' for {m.Serial}");
                        continue;
                    }

                    var name = free.RandomElement();
                    used.Add(name);
                    report.Add($"{town.Key.Name}: {m.Serial} {m.Name} -> {name} ('{list}')");
                    if (!dryRun)
                    {
                        m.Name = name;
                    }

                    renamed++;
                }
            }
        }

        return report;
    }

    public static List<string> DedupeAll(bool dryRun = false)
    {
        var lines = new List<string>();
        foreach (var map in new[] { Map.Trammel, Map.Felucca })
        {
            if (map != null && map != Map.Internal)
            {
                lines.AddRange(Dedupe(map, dryRun));
            }
        }

        return lines;
    }

    public static List<string> Status()
    {
        var lines = new List<string> { $"Names: {(Loaded == null ? "our file not loaded" : Loaded.Credit)}" };
        foreach (var row in Loaded?.Lists ?? [])
        {
            lines.Add($"  {row.Type} ({row.Mode}): {NameList.GetNameList(row.Type)?.List.Length ?? 0} names in the table");
        }

        return lines;
    }

    [Usage("ShardNames [status | dedupe [dryrun]]")]
    [Description("cc-P861: our extra NPC name lists (Data/ShatteredLegacy/names-shard.json, after Sirenix) and the town rule. status lists them; dedupe renames people in the same town who share a name drawn from a list (the oldest keeps it; fixed names and fixed-identity people such as Mara, players and pets are never touched, and a fixed-identity person's name is theirs in their town); dedupe dryrun says what it would rename. The sweep also runs after every world load and hourly.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.Any, DryRun = "dryrun",
        Summary = "Renames NPCs who share a name in the same town (the oldest keeps it). dryrun only lists them.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
        var lines = verb switch
        {
            "status" => Status(),
            "dedupe" => DedupeAll(e.Length > 1 && e.GetString(1).Equals("dryrun", StringComparison.OrdinalIgnoreCase)),
            _        => ["Usage: [ShardNames status | dedupe [dryrun]"]
        };

        foreach (var line in lines)
        {
            e.Mobile.SendMessage(line);
        }
    }
}
