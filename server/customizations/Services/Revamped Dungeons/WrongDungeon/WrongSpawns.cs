// ServUO: RevampedSpawns/WrongRevamped.xml (cc-P194) - the 38 XmlSpawner definitions [GenWrongRevamp loads with
// "[XmlLoad RevampedSpawns/WrongRevamped.xml" - carried as data, one row per <Points> element, in file order.
//
// XmlSpawner -> ModernUO Spawner, field by field (the XML is 1,597 lines; parsed by script, 38 rows, 19 per facet
// and the two facets are the same multiset; every value below is the XML's):
//   <Name>                     Name: "#WrongRevamped" (28 rows, the dungeon population) or "WrongPrisoner" (10 rows,
//                              the escort prisoners in their cells).
//   <Map>                      Felucca flag; the spawner and its spawns go on that facet.
//   <CentreX/Y/Z>              Location.
//   <X> <Y> <Width> <Height>   SpawnBounds, the rectangle spawns land in, at full Z range like ModernUO's own
//                              legacy import (BaseSpawner.cs:323-341).
//   <Range>                    WalkingRange, which Spawn() writes to the creature's RangeHome.
//   <IsHomeRangeRelative>      SpawnLocationIsHome: true, the creature's home is where it spawned; false, its
//                              home is the spawner's own tile. Same meaning on both emulators.
//   <MaxCount>                 Count.  <MinDelay>/<MaxDelay> with <DelayInSec>False: minutes.
//   <Objects2> type:MX=n       one entry per type, probability 100, maxCount n. No constructor arguments anywhere
//                              in this file. Type names are the XML's, case-corrected to the real type names.
//   <SmartSpawning>            True on 20 rows; no counterpart (XmlSpawner idles while no player is near).
//   Everything else            XmlSpawner defaults (no triggers, no sequential spawning, no despawn, KillReset 1).
//
// Capacity. Respawn() spawns Count times, but each entry is capped at its own maxCount, so a row never holds more
// than the sum of its entries' maxCounts: 126 requested, 68 possible, on both emulators (XmlSpawner2.cs:9008-9014
// caps at MaxCount; SpawnerEntry.IsFull). Most rows ask for more than their one entry allows (a MaxCount 4 over
// "Fezzik:MX=1" spawns one Fezzik); the test derives both numbers from the data.
//
// Executioner is stock pinned ModernUO (Mobiles/Monsters/Humanoid/Melee/Executioner.cs); the other eleven types
// are this port's.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.Spawners;

namespace Server.Engines.WrongRevamp;

public static class WrongSpawns
{
    public const string DungeonSpawnerName = "#WrongRevamped";
    public const string PrisonerSpawnerName = "WrongPrisoner";

    public readonly record struct Entry(string Type, int MaxCount);

    public readonly record struct Definition(
        string Name,
        bool Felucca,
        int CentreX, int CentreY, int CentreZ,
        int X, int Y, int Width, int Height,
        int Range,
        int MaxCount, int MinDelayMinutes, int MaxDelayMinutes,
        bool HomeRangeRelative,
        Entry[] Entries
    )
    {
        public bool IsPrisoner => Name == PrisonerSpawnerName;

        public Map Map => Felucca ? Map.Felucca : Map.Trammel;

        public int Capacity => Math.Min(MaxCount, Entries.Sum(e => e.MaxCount));
    }

    // Generated from RevampedSpawns/WrongRevamped.xml; columns as in Definition.
    public static readonly Definition[] Definitions =
    {
        new("#WrongRevamped", true, 5690, 534, 0, 5682, 526, 16, 16, 30, 4, 5, 10, false, new Entry[] { new("Fezzik", 1) }),
        new("#WrongRevamped", true, 5724, 561, 20, 5715, 552, 18, 18, 30, 2, 5, 10, false, new Entry[] { new("Archmage", 1) }),
        new("#WrongRevamped", true, 5655, 549, 22, 5653, 547, 4, 4, 15, 1, 5, 10, true, new Entry[] { new("DemonicJailor", 1) }),
        new("#WrongRevamped", true, 5858, 579, 16, 5848, 569, 20, 20, 30, 6, 5, 10, false, new Entry[] { new("CaveTrollWrong", 1) }),
        new("#WrongRevamped", true, 5793, 544, 10, 5788, 539, 10, 10, 5, 2, 5, 10, true, new Entry[] { new("LizardmanSquatter", 2) }),
        new("#WrongRevamped", true, 5669, 523, 0, 5667, 521, 4, 4, 15, 1, 5, 10, true, new Entry[] { new("DemonicJailor", 1) }),
        new("#WrongRevamped", true, 5828, 530, 0, 5820, 522, 16, 16, 30, 2, 5, 10, false, new Entry[] { new("Executioner", 1) }),
        new("#WrongRevamped", true, 5825, 593, 0, 5815, 583, 20, 20, 25, 5, 5, 10, false, new Entry[] { new("LizardmanDefender", 2) }),
        new("#WrongRevamped", true, 5729, 550, 20, 5727, 548, 4, 4, 15, 1, 5, 10, true, new Entry[] { new("DemonicJailor", 1) }),
        new("#WrongRevamped", true, 5703, 639, 0, 5673, 609, 60, 60, 30, 7, 5, 10, true, new Entry[] { new("PrisonRat", 5) }),
        new("#WrongRevamped", true, 5860, 554, 15, 5850, 544, 20, 20, 30, 4, 5, 10, false, new Entry[] { new("HungryOgre", 1) }),
        new("#WrongRevamped", true, 5800, 575, 10, 5785, 560, 30, 30, 30, 10, 5, 10, false, new Entry[] { new("CaveTrollWrong", 2) }),
        new("#WrongRevamped", true, 5659, 562, 20, 5649, 552, 20, 20, 20, 8, 5, 10, true, new Entry[] { new("BrigandCannibal", 5) }),
        new("#WrongRevamped", true, 5691, 565, 25, 5686, 560, 10, 10, 5, 5, 5, 10, true, new Entry[] { new("SavagePackWolf", 5) }),
        new("#WrongRevamped", false, 5691, 565, 25, 5686, 560, 10, 10, 5, 5, 5, 10, true, new Entry[] { new("SavagePackWolf", 5) }),
        new("#WrongRevamped", false, 5690, 534, 0, 5682, 526, 16, 16, 30, 4, 5, 10, false, new Entry[] { new("Fezzik", 1) }),
        new("#WrongRevamped", false, 5724, 561, 20, 5715, 552, 18, 18, 30, 2, 5, 10, false, new Entry[] { new("Archmage", 1) }),
        new("#WrongRevamped", false, 5655, 549, 22, 5653, 547, 4, 4, 15, 1, 5, 10, true, new Entry[] { new("DemonicJailor", 1) }),
        new("#WrongRevamped", false, 5858, 579, 16, 5848, 569, 20, 20, 30, 6, 5, 10, false, new Entry[] { new("CaveTrollWrong", 1) }),
        new("#WrongRevamped", false, 5793, 544, 10, 5788, 539, 10, 10, 5, 2, 5, 10, true, new Entry[] { new("LizardmanSquatter", 2) }),
        new("#WrongRevamped", false, 5669, 523, 0, 5667, 521, 4, 4, 15, 1, 5, 10, true, new Entry[] { new("DemonicJailor", 1) }),
        new("#WrongRevamped", false, 5828, 530, 0, 5820, 522, 16, 16, 30, 2, 5, 10, false, new Entry[] { new("Executioner", 1) }),
        new("#WrongRevamped", false, 5825, 593, 0, 5815, 583, 20, 20, 25, 5, 5, 10, false, new Entry[] { new("LizardmanDefender", 2) }),
        new("#WrongRevamped", false, 5729, 550, 20, 5727, 548, 4, 4, 15, 1, 5, 10, true, new Entry[] { new("DemonicJailor", 1) }),
        new("#WrongRevamped", false, 5703, 639, 0, 5673, 609, 60, 60, 30, 7, 5, 10, true, new Entry[] { new("PrisonRat", 5) }),
        new("#WrongRevamped", false, 5860, 554, 15, 5850, 544, 20, 20, 30, 4, 5, 10, false, new Entry[] { new("HungryOgre", 1) }),
        new("#WrongRevamped", false, 5800, 575, 10, 5785, 560, 30, 30, 30, 10, 5, 10, false, new Entry[] { new("CaveTrollWrong", 2) }),
        new("#WrongRevamped", false, 5659, 562, 20, 5649, 552, 20, 20, 20, 8, 5, 10, true, new Entry[] { new("BrigandCannibal", 5) }),
        new("WrongPrisoner", true, 5665, 553, 20, 5665, 553, 0, 0, 0, 1, 5, 10, true, new Entry[] { new("WrongPrisoner", 1) }),
        new("WrongPrisoner", true, 5651, 553, 20, 5651, 553, 0, 0, 0, 1, 5, 10, true, new Entry[] { new("WrongPrisoner", 1) }),
        new("WrongPrisoner", true, 5653, 553, 20, 5653, 553, 0, 0, 0, 1, 5, 10, true, new Entry[] { new("WrongPrisoner", 1) }),
        new("WrongPrisoner", true, 5658, 553, 20, 5658, 553, 0, 0, 0, 1, 5, 10, true, new Entry[] { new("WrongPrisoner", 1) }),
        new("WrongPrisoner", true, 5668, 553, 20, 5668, 553, 0, 0, 0, 1, 5, 10, true, new Entry[] { new("WrongPrisoner", 1) }),
        new("WrongPrisoner", false, 5665, 553, 20, 5665, 553, 0, 0, 0, 1, 5, 10, true, new Entry[] { new("WrongPrisoner", 1) }),
        new("WrongPrisoner", false, 5651, 553, 20, 5651, 553, 0, 0, 0, 1, 5, 10, true, new Entry[] { new("WrongPrisoner", 1) }),
        new("WrongPrisoner", false, 5653, 553, 20, 5653, 553, 0, 0, 0, 1, 5, 10, true, new Entry[] { new("WrongPrisoner", 1) }),
        new("WrongPrisoner", false, 5658, 553, 20, 5658, 553, 0, 0, 0, 1, 5, 10, true, new Entry[] { new("WrongPrisoner", 1) }),
        new("WrongPrisoner", false, 5668, 553, 20, 5668, 553, 0, 0, 0, 1, 5, 10, true, new Entry[] { new("WrongPrisoner", 1) }),
    };

    public static bool IsWrongSpawner(BaseSpawner spawner) =>
        spawner?.Name != null &&
        (string.Equals(spawner.Name, DungeonSpawnerName, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(spawner.Name, PrisonerSpawnerName, StringComparison.OrdinalIgnoreCase));

    public static bool AnyPresent() => World.Items.Values.OfType<Spawner>().Any(s => !s.Deleted && IsWrongSpawner(s));

    /// <summary>
    ///     Creates the 38 spawners on their facets, tags them for [DeleteWrongRevamp, and fills them, as
    ///     [GenerateSpawners does for ModernUO's own spawn files (ImportSpawnersCommand.cs:168-177).
    /// </summary>
    public static List<Spawner> Generate()
    {
        var list = new List<Spawner>(Definitions.Length);

        foreach (var d in Definitions)
        {
            var spawner = new Spawner(
                d.MaxCount,
                TimeSpan.FromMinutes(d.MinDelayMinutes),
                TimeSpan.FromMinutes(d.MaxDelayMinutes),
                0,
                // The prisoner rows are 0x0 (XmlSpawner spawns on the tile itself); pinned bounds need one tile.
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

            WeakEntityCollection.Add(WrongGenerator.CollectionKey, spawner);
            spawner.MoveToWorld(new Point3D(d.CentreX, d.CentreY, d.CentreZ), d.Map);
            spawner.Respawn();

            list.Add(spawner);
        }

        return list;
    }
}
