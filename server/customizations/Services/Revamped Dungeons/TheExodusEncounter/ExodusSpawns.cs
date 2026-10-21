// ServUO: Spawns/TheExodusEncounterQuest.xml (cc-P195), the 17 XmlSpawner rows [GenExodusQuest loads with
// "[XmlLoad Spawns/TheExodusEncounterQuest.xml", carried as data, one row per <Points> element in file order.
//
// XmlSpawner -> ModernUO Spawner, field by field (the XML is 793 lines; parsed by script; every value below is the
// XML's; the shape is cc-P194's WrongSpawns):
//   <Name>                     Name: "Exodus Dungeon" (4), "Arch Zealot" (1), "#ExodusChest" (12).
//   <Map>                      Ilshenar, every row.
//   <CentreX/Y/Z>              Location.
//   <X> <Y> <Width> <Height>   SpawnBounds, the rectangle spawns land in, at full Z range.
//   <Range>                    WalkingRange (the creature's RangeHome).
//   <IsHomeRangeRelative>      SpawnLocationIsHome: true on every row.
//   <MaxCount>                 Count.  <MinDelay>/<MaxDelay> with <DelayInSec>False: minutes.
//   <Objects2> type:MX=n       one entry per type, probability 100, maxCount n; no constructor arguments.
//   Everything else            XmlSpawner defaults (no triggers, no sequential spawning, KillReset 1, not smart).
//
// Two pairs of rows are byte-for-byte duplicates in ServUO's file (rows 0 and 3, the juggernaut and minion lord; rows
// 1 and 2, the dungeon population at 40 each), the second of each pair with no UniqueId: a copy-paste in ServUO's
// data. They are carried as ServUO ships them, so a ServUO shard and this one spawn the same; see the notes
// (Deviations and decisions, Q-2) for the one-line change if Chase wants one of each.
//
// The "Arch Zealot" row spawns ExodusArchZealot, a plain Mobile (not a creature) at range 0, count 1: the generator
// places him as a mobile instead of a spawner (pinned's Spawner spawns ISpawnable mobiles, which a Mobile is, but a
// spawner for one unmoving NPC would respawn him on a delete; the XML's intent is one fixed NPC).
//
// Capacity: a row never holds more than the sum of its entries' maxCounts (the dungeon rows ask 40 and allow 38).

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.Spawners;
using Server.Mobiles;

namespace Server.Engines.Exodus;

public static class ExodusSpawns
{
    public const string DungeonSpawnerName = "Exodus Dungeon";
    public const string ArchZealotSpawnerName = "Arch Zealot";
    public const string ChestSpawnerName = "#ExodusChest";

    public readonly record struct Entry(string Type, int MaxCount);

    public readonly record struct Definition(
        string Name,
        int CentreX, int CentreY, int CentreZ,
        int X, int Y, int Width, int Height,
        int Range,
        int MaxCount, int MinDelayMinutes, int MaxDelayMinutes,
        bool HomeRangeRelative,
        Entry[] Entries
    )
    {
        public bool IsArchZealot => Name == ArchZealotSpawnerName;

        public Map Map => Map.Ilshenar;

        public int Capacity => Math.Min(MaxCount, Entries.Sum(e => e.MaxCount));
    }

    private static readonly Entry[] Population =
    {
        new("DupresChampion", 1), new("DupresKnight", 1), new("DupresSquire", 3),
        new("ExodusDrone", 10), new("ExodusSentinel", 3), new("ExodusZealot", 20)
    };

    private static readonly Entry[] Lords = { new("ExodusJuggernaut", 1), new("ExodusMinionLord", 1) };

    private static readonly Entry[] Chest = { new("ExodusChest", 1) };

    // Generated from Spawns/TheExodusEncounterQuest.xml; columns as in Definition.
    public static readonly Definition[] Definitions =
    {
        new(DungeonSpawnerName, 1859, 115, -28, 1854, 110, 10, 10, 40, 1, 5, 10, true, Lords),
        new(DungeonSpawnerName, 1969, 114, -28, 1869, 14, 200, 200, 200, 40, 5, 10, true, Population),
        new(DungeonSpawnerName, 1969, 114, -28, 1869, 14, 200, 200, 200, 40, 5, 10, true, Population),
        new(DungeonSpawnerName, 1859, 115, -28, 1854, 110, 10, 10, 40, 1, 5, 10, true, Lords),
        new(ArchZealotSpawnerName, 946, 641, -79, 946, 641, 0, 0, 0, 1, 5, 10, true, new Entry[] { new("ExodusArchZealot", 1) }),
        new(ChestSpawnerName, 1985, 116, -28, 1935, 66, 100, 100, 50, 1, 5, 10, true, Chest),
        new(ChestSpawnerName, 2036, 176, -28, 1986, 126, 100, 100, 50, 1, 5, 10, true, Chest),
        new(ChestSpawnerName, 1892, 107, -28, 1842, 57, 100, 100, 50, 1, 5, 10, true, Chest),
        new(ChestSpawnerName, 1996, 164, -28, 1946, 114, 100, 100, 50, 1, 5, 10, true, Chest),
        new(ChestSpawnerName, 2051, 140, -28, 2001, 90, 100, 100, 50, 1, 5, 10, true, Chest),
        new(ChestSpawnerName, 1932, 53, -26, 1882, 3, 100, 100, 50, 1, 5, 10, true, Chest),
        new(ChestSpawnerName, 1933, 163, -28, 1883, 113, 100, 100, 50, 1, 5, 10, true, Chest),
        new(ChestSpawnerName, 2071, 106, -28, 2021, 56, 100, 100, 50, 1, 5, 10, true, Chest),
        new(ChestSpawnerName, 2019, 105, -28, 2014, 100, 10, 10, 50, 1, 5, 10, true, Chest),
        new(ChestSpawnerName, 1972, 59, -28, 1922, 9, 100, 100, 50, 1, 5, 10, true, Chest),
        new(ChestSpawnerName, 2044, 65, -28, 1994, 15, 100, 100, 50, 1, 5, 10, true, Chest),
        new(ChestSpawnerName, 1933, 91, -28, 1883, 41, 100, 100, 50, 1, 5, 10, true, Chest)
    };

    public static bool IsExodusSpawner(BaseSpawner spawner) =>
        spawner?.Name != null &&
        (string.Equals(spawner.Name, DungeonSpawnerName, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(spawner.Name, ChestSpawnerName, StringComparison.OrdinalIgnoreCase));

    public static bool AnyPresent() =>
        World.Items.Values.OfType<Spawner>().Any(s => !s.Deleted && IsExodusSpawner(s));

    /// <summary>
    ///     Creates the sixteen spawners on Ilshenar, tags them for [DeleteExodusEncounter, and fills them, as
    ///     [GenerateSpawners does for ModernUO's own spawn files. The Arch Zealot row is placed by the generator.
    /// </summary>
    public static List<Spawner> Generate()
    {
        var list = new List<Spawner>();

        foreach (var d in Definitions)
        {
            if (d.IsArchZealot)
            {
                continue;
            }

            var spawner = new Spawner(
                d.MaxCount,
                TimeSpan.FromMinutes(d.MinDelayMinutes),
                TimeSpan.FromMinutes(d.MaxDelayMinutes),
                0,
                new Rectangle3D(d.X, d.Y, Region.MinZ, Math.Max(1, d.Width), Math.Max(1, d.Height), Region.MaxZ - Region.MinZ)
            )
            {
                Name = d.Name,
                WalkingRange = d.Range,
                SpawnLocationIsHome = d.HomeRangeRelative
            };

            foreach (var entry in d.Entries)
            {
                spawner.AddEntry(entry.Type, 100, entry.MaxCount, false);
            }

            WeakEntityCollection.Add(ExodusEncounter.CollectionKey, spawner);
            spawner.MoveToWorld(new Point3D(d.CentreX, d.CentreY, d.CentreZ), d.Map);
            spawner.Respawn();

            list.Add(spawner);
        }

        return list;
    }

    /// <summary>The Arch Zealot row: Hunter, placed once at his tile.</summary>
    public static ExodusArchZealot PlaceArchZealot()
    {
        var d = Definitions.First(x => x.IsArchZealot);
        var zealot = new ExodusArchZealot();

        WeakEntityCollection.Add(ExodusEncounter.CollectionKey, zealot);
        zealot.MoveToWorld(new Point3D(d.CentreX, d.CentreY, d.CentreZ), d.Map);

        return zealot;
    }
}
