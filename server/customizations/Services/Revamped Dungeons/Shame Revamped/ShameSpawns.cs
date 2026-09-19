// ServUO: RevampedSpawns/ShameRevamped.xml (CC4 Shame) - the 194 XmlSpawner definitions the Read Me has a GM
// load with [XmlLoad spawns/shamerevamped.xml - carried as data, one row per <Points> element, in file order.
//
// XmlSpawner -> ModernUO Spawner, field by field (the XML is 8,159 lines; parsed by script, 194 rows, 97 per
// facet and the two facets are the same multiset; every value below is the XML's):
//   <Name>                     Name: "Shame_Revamped" (78 rows, creatures) or "Shame_Chest" (116 rows, chests).
//                              Generate.cs tells our spawners from the old ones by these names, as ServUO does.
//   <Map>                      Felucca flag; the spawner and its spawns go on that facet.
//   <CentreX/Y/Z>              Location.
//   <X> <Y> <Width> <Height>   SpawnBounds, the rectangle spawns land in, at full Z range like ModernUO's own
//                              legacy import (BaseSpawner.cs:323-341). Chest rows are 2x2 tiles.
//   <Range>                    WalkingRange, which Spawn() writes to the creature's RangeHome (1 to 35).
//   <IsHomeRangeRelative>      SpawnLocationIsHome: true, the creature's home is where it spawned (52 rows);
//                              false, its home is the spawner's own tile (142 rows). Same meaning on both.
//   <MaxCount>                 Count.  <MinDelay>/<MaxDelay> with <DelayInSec>False: minutes.
//   <Objects2> type:MX=n[:OBJ=type:MX=n...]  one entry per type, probability 100, maxCount n. No constructor
//                              arguments anywhere in this file (Despise's {RND,a,b} shape does not occur).
//                              Type names are the XML's, case-corrected to the real type names.
//   <SmartSpawning>            True on 70 rows; no counterpart (XmlSpawner idles while no player is near).
//   Everything else            XmlSpawner defaults (no triggers, no sequential spawning, no despawn, KillReset 1).
//
// Capacity. Respawn() spawns Count times, but each entry is capped at its own maxCount, so a row can never
// hold more than the sum of its entries' maxCounts: 386 requested, 384 possible, on both emulators. The two
// short rows are the Shame_Chest at 5402,58 on each facet (Count 2 over one TreasureLevel2 capped at 1). The
// "chaosvortex:MX=0" entry on the row at 5761,98 spawns nothing on either emulator (XmlSpawner2.cs:9008-9014
// caps at MaxCount; SpawnerEntry.IsFull is Spawned.Count >= 0); it is carried so a GM can raise it.
//
// TreasureLevel1-4 are Nerun's Treasure Chest Pack, ported for this file (Items/Containers/TreasureChestMod.cs).

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.Spawners;

namespace Server.Engines.ShameRevamped;

public static class ShameSpawns
{
    public const string CreatureSpawnerName = "Shame_Revamped";
    public const string ChestSpawnerName = "Shame_Chest";

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
        public bool IsChest => Name == ChestSpawnerName;

        public Map Map => Felucca ? Map.Felucca : Map.Trammel;

        public int Capacity => Math.Min(MaxCount, Entries.Sum(e => e.MaxCount));
    }

    // Generated from RevampedSpawns/ShameRevamped.xml; columns as in Definition.
    public static readonly Definition[] Definitions =
    {
        new("Shame_Revamped", false, 5498, 179, 0, 5495, 176, 7, 6, 4, 1, 2, 2, true, new Entry[] { new("EternalGazer", 1) }),
        new("Shame_Revamped", false, 5573, 158, -10, 5543, 128, 60, 60, 30, 2, 2, 2, false, new Entry[] { new("Scorpion", 2) }),
        new("Shame_Revamped", false, 5818, 80, 0, 5808, 70, 20, 20, 30, 0, 2, 2, false, new Entry[] { new("EternalGazer", 1) }),
        new("Shame_Revamped", false, 5609, 19, 10, 5594, 4, 30, 30, 15, 4, 2, 2, true, new Entry[] { new("MudElemental", 3), new("GreaterEarthElemental", 1) }),
        new("Shame_Revamped", false, 5820, 50, 0, 5818, 48, 4, 4, 10, 2, 2, 2, true, new Entry[] { new("CrazedMage", 1), new("VileMage", 1) }),
        new("Shame_Revamped", false, 5708, 44, 0, 5683, 19, 50, 50, 25, 3, 2, 2, false, new Entry[] { new("GreaterWaterElemental", 3) }),
        new("Shame_Revamped", false, 5711, 97, -5, 5696, 91, 23, 10, 25, 3, 2, 2, true, new Entry[] { new("Kraken", 1), new("SeaSerpent", 1), new("GreaterWaterElemental", 1) }),
        new("Shame_Revamped", false, 5445, 56, -10, 5425, 36, 40, 40, 25, 11, 2, 2, true, new Entry[] { new("MudPie", 6), new("Scorpion", 4), new("ShameEarthElemental", 1) }),
        new("Shame_Revamped", false, 5455, 86, 20, 5418, 84, 75, 34, 5, 8, 2, 2, true, new Entry[] { new("MudPie", 5), new("Scorpion", 3) }),
        new("Shame_Revamped", false, 5549, 60, 0, 5519, 30, 60, 60, 30, 5, 2, 2, false, new Entry[] { new("MudElemental", 5) }),
        new("Shame_Revamped", false, 5477, 66, 20, 5472, 36, 12, 42, 5, 6, 2, 2, true, new Entry[] { new("MudPie", 4), new("Scorpion", 2) }),
        new("Shame_Revamped", false, 5507, 186, 0, 5504, 184, 6, 6, 5, 1, 2, 2, true, new Entry[] { new("EternalGazer", 1) }),
        new("Shame_Revamped", false, 5575, 194, 0, 5565, 184, 20, 20, 10, 4, 2, 2, true, new Entry[] { new("CorruptedMage", 2), new("VileMage", 2) }),
        new("Shame_Revamped", false, 5458, 202, -5, 5455, 199, 6, 6, 30, 3, 2, 2, true, new Entry[] { new("GreaterWaterElemental", 1), new("Kraken", 1), new("SeaSerpent", 1) }),
        new("Shame_Revamped", false, 5534, 107, 0, 5514, 87, 40, 40, 5, 5, 2, 2, true, new Entry[] { new("MudElemental", 3), new("GreaterEarthElemental", 2) }),
        new("Shame_Revamped", false, 5424, 20, 10, 5407, 8, 61, 7, 5, 6, 2, 2, true, new Entry[] { new("Scorpion", 2), new("MudPie", 3), new("ShameEarthElemental", 1) }),
        new("Shame_Revamped", false, 5572, 158, -10, 5542, 128, 60, 60, 30, 4, 2, 2, false, new Entry[] { new("GreaterEarthElemental", 4) }),
        new("Shame_Revamped", false, 5443, 187, 0, 5433, 177, 20, 20, 10, 5, 2, 2, true, new Entry[] { new("VileMage", 2), new("EvilMageLord", 2), new("CorruptedMage", 1) }),
        new("Shame_Revamped", false, 5761, 98, 0, 5777, 94, 91, 18, 18, 2, 2, 2, false, new Entry[] { new("UnboundEnergyVortex", 2), new("ChaosVortex", 0) }),
        new("Shame_Revamped", false, 5526, 210, 0, 5511, 195, 30, 30, 30, 1, 2, 2, false, new Entry[] { new("FlameElemental", 1) }),
        new("Shame_Revamped", false, 5474, 17, -11, 5472, 13, 18, 20, 5, 3, 2, 2, true, new Entry[] { new("StoneElemental", 2), new("Scorpion", 1) }),
        new("Shame_Revamped", false, 5759, 100, 0, 5680, 103, 64, 21, 21, 1, 2, 2, false, new Entry[] { new("UnboundEnergyVortex", 3) }),
        new("Shame_Revamped", false, 5478, 184, 0, 5448, 154, 60, 60, 10, 7, 2, 2, true, new Entry[] { new("MoltenEarthElemental", 4), new("GreaterAirElemental", 3) }),
        new("Shame_Revamped", false, 5797, 39, 0, 5785, 27, 24, 24, 5, 1, 2, 2, true, new Entry[] { new("ChaosVortex", 1) }),
        new("Shame_Revamped", false, 5724, 12, 9, 5654, 4, 90, 35, 5, 3, 2, 2, true, new Entry[] { new("DiseasedBloodElemental", 3) }),
        new("Shame_Revamped", false, 5603, 93, 0, 5593, 83, 20, 20, 25, 2, 2, 2, false, new Entry[] { new("ClayGolem", 2) }),
        new("Shame_Revamped", false, 5649, 101, 10, 5641, 47, 51, 71, 30, 4, 2, 2, false, new Entry[] { new("WindElemental", 4) }),
        new("Shame_Revamped", false, 5387, 63, 20, 5384, 25, 16, 69, 3, 5, 2, 2, true, new Entry[] { new("MudPie", 2), new("Scorpion", 3) }),
        new("Shame_Revamped", false, 5394, 230, 10, 5364, 200, 60, 60, 30, 2, 2, 2, false, new Entry[] { new("GreaterBloodElemental", 2) }),
        new("Shame_Revamped", false, 5494, 141, 20, 5492, 135, 69, 20, 30, 2, 2, 2, true, new Entry[] { new("FlameElemental", 2) }),
        new("Shame_Revamped", false, 5760, 99, 0, 5730, 69, 60, 60, 30, 2, 2, 2, false, new Entry[] { new("MoltenEarthElemental", 2) }),
        new("Shame_Revamped", false, 5544, 78, -5, 5532, 66, 24, 24, 25, 4, 2, 2, true, new Entry[] { new("GreaterWaterElemental", 3), new("Kraken", 1) }),
        new("Shame_Revamped", false, 5447, 210, 0, 5426, 207, 56, 38, 25, 3, 2, 2, true, new Entry[] { new("GreaterBloodElemental", 3) }),
        new("Shame_Revamped", false, 5414, 201, 0, 5394, 168, 28, 38, 30, 5, 2, 2, true, new Entry[] { new("MoltenEarthElemental", 2), new("GreaterAirElemental", 1), new("ShameGreaterPoisonElemental", 2) }),
        new("Shame_Revamped", false, 5438, 186, 22, 5426, 174, 24, 24, 10, 5, 2, 2, true, new Entry[] { new("VileMage", 2), new("EvilMageLord", 2), new("CorruptedMage", 1) }),
        new("Shame_Revamped", false, 5780, 21, 0, 5762, 17, 33, 45, 5, 2, 5, 10, true, new Entry[] { new("DiseasedBloodElemental", 2) }),
        new("Shame_Revamped", false, 5593, 78, -5, 5591, 76, 4, 4, 25, 2, 2, 2, true, new Entry[] { new("SeaSerpent", 1), new("Kraken", 1) }),
        new("Shame_Revamped", false, 5474, 139, 20, 5408, 136, 77, 6, 6, 2, 2, 2, false, new Entry[] { new("ShameGreaterPoisonElemental", 2) }),
        new("Shame_Revamped", false, 5750, 42, -5, 5748, 40, 4, 4, 35, 3, 2, 2, true, new Entry[] { new("Kraken", 1), new("SeaSerpent", 1), new("GreaterWaterElemental", 1) }),
        new("Shame_Revamped", true, 5780, 21, 0, 5762, 17, 33, 45, 5, 2, 5, 10, true, new Entry[] { new("DiseasedBloodElemental", 2) }),
        new("Shame_Revamped", true, 5526, 210, 0, 5511, 195, 30, 30, 30, 1, 2, 2, false, new Entry[] { new("FlameElemental", 1) }),
        new("Shame_Revamped", true, 5458, 202, -5, 5455, 199, 6, 6, 30, 3, 2, 2, true, new Entry[] { new("GreaterWaterElemental", 1), new("Kraken", 1), new("SeaSerpent", 1) }),
        new("Shame_Revamped", true, 5507, 186, 0, 5504, 184, 6, 6, 5, 1, 2, 2, true, new Entry[] { new("EternalGazer", 1) }),
        new("Shame_Revamped", true, 5474, 17, -11, 5472, 13, 18, 20, 5, 3, 2, 2, true, new Entry[] { new("StoneElemental", 2), new("Scorpion", 1) }),
        new("Shame_Revamped", true, 5445, 56, -10, 5425, 36, 40, 40, 25, 11, 2, 2, true, new Entry[] { new("MudPie", 6), new("Scorpion", 4), new("ShameEarthElemental", 1) }),
        new("Shame_Revamped", true, 5573, 158, -10, 5543, 128, 60, 60, 30, 2, 2, 2, false, new Entry[] { new("Scorpion", 2) }),
        new("Shame_Revamped", true, 5593, 78, -5, 5591, 76, 4, 4, 25, 2, 2, 2, true, new Entry[] { new("SeaSerpent", 1), new("Kraken", 1) }),
        new("Shame_Revamped", true, 5424, 20, 10, 5407, 8, 61, 7, 5, 6, 2, 2, true, new Entry[] { new("Scorpion", 2), new("MudPie", 3), new("ShameEarthElemental", 1) }),
        new("Shame_Revamped", true, 5760, 99, 0, 5730, 69, 60, 60, 30, 2, 2, 2, false, new Entry[] { new("MoltenEarthElemental", 2) }),
        new("Shame_Revamped", true, 5603, 93, 0, 5593, 83, 20, 20, 25, 2, 2, 2, false, new Entry[] { new("ClayGolem", 2) }),
        new("Shame_Revamped", true, 5477, 66, 20, 5472, 36, 12, 42, 5, 6, 2, 2, true, new Entry[] { new("MudPie", 4), new("Scorpion", 2) }),
        new("Shame_Revamped", true, 5478, 184, 0, 5448, 154, 60, 60, 10, 7, 2, 2, true, new Entry[] { new("MoltenEarthElemental", 4), new("GreaterAirElemental", 3) }),
        new("Shame_Revamped", true, 5414, 201, 0, 5394, 168, 28, 38, 30, 5, 2, 2, true, new Entry[] { new("MoltenEarthElemental", 2), new("GreaterAirElemental", 1), new("ShameGreaterPoisonElemental", 2) }),
        new("Shame_Revamped", true, 5750, 42, -5, 5748, 40, 4, 4, 35, 3, 2, 2, true, new Entry[] { new("Kraken", 1), new("SeaSerpent", 1), new("GreaterWaterElemental", 1) }),
        new("Shame_Revamped", true, 5443, 187, 0, 5433, 177, 20, 20, 10, 5, 2, 2, true, new Entry[] { new("VileMage", 2), new("EvilMageLord", 2), new("CorruptedMage", 1) }),
        new("Shame_Revamped", true, 5447, 210, 0, 5426, 207, 56, 38, 25, 3, 2, 2, true, new Entry[] { new("GreaterBloodElemental", 3) }),
        new("Shame_Revamped", true, 5649, 101, 10, 5641, 47, 51, 71, 30, 4, 2, 2, false, new Entry[] { new("WindElemental", 4) }),
        new("Shame_Revamped", true, 5572, 158, -10, 5542, 128, 60, 60, 30, 4, 2, 2, false, new Entry[] { new("GreaterEarthElemental", 4) }),
        new("Shame_Revamped", true, 5394, 230, 10, 5364, 200, 60, 60, 30, 2, 2, 2, false, new Entry[] { new("GreaterBloodElemental", 2) }),
        new("Shame_Revamped", true, 5498, 179, 0, 5495, 176, 7, 6, 4, 1, 2, 2, true, new Entry[] { new("EternalGazer", 1) }),
        new("Shame_Revamped", true, 5575, 194, 0, 5565, 184, 20, 20, 10, 4, 2, 2, true, new Entry[] { new("CorruptedMage", 2), new("VileMage", 2) }),
        new("Shame_Revamped", true, 5494, 141, 20, 5492, 135, 69, 20, 30, 2, 2, 2, true, new Entry[] { new("FlameElemental", 2) }),
        new("Shame_Revamped", true, 5724, 12, 9, 5654, 4, 90, 35, 5, 3, 2, 2, true, new Entry[] { new("DiseasedBloodElemental", 3) }),
        new("Shame_Revamped", true, 5759, 100, 0, 5680, 103, 64, 21, 21, 1, 2, 2, false, new Entry[] { new("UnboundEnergyVortex", 3) }),
        new("Shame_Revamped", true, 5544, 78, -5, 5532, 66, 24, 24, 25, 4, 2, 2, true, new Entry[] { new("GreaterWaterElemental", 3), new("Kraken", 1) }),
        new("Shame_Revamped", true, 5549, 60, 0, 5519, 30, 60, 60, 30, 5, 2, 2, false, new Entry[] { new("MudElemental", 5) }),
        new("Shame_Revamped", true, 5820, 50, 0, 5818, 48, 4, 4, 10, 2, 2, 2, true, new Entry[] { new("CrazedMage", 1), new("VileMage", 1) }),
        new("Shame_Revamped", true, 5455, 86, 20, 5418, 84, 75, 34, 5, 8, 2, 2, true, new Entry[] { new("MudPie", 5), new("Scorpion", 3) }),
        new("Shame_Revamped", true, 5609, 19, 10, 5594, 4, 30, 30, 15, 4, 2, 2, true, new Entry[] { new("MudElemental", 3), new("GreaterEarthElemental", 1) }),
        new("Shame_Revamped", true, 5474, 139, 20, 5408, 136, 77, 6, 6, 2, 2, 2, false, new Entry[] { new("ShameGreaterPoisonElemental", 2) }),
        new("Shame_Revamped", true, 5711, 97, -5, 5696, 91, 23, 10, 25, 3, 2, 2, true, new Entry[] { new("Kraken", 1), new("SeaSerpent", 1), new("GreaterWaterElemental", 1) }),
        new("Shame_Revamped", true, 5761, 98, 0, 5777, 94, 91, 18, 18, 2, 2, 2, false, new Entry[] { new("UnboundEnergyVortex", 2), new("ChaosVortex", 0) }),
        new("Shame_Revamped", true, 5438, 186, 22, 5426, 174, 24, 24, 10, 5, 2, 2, true, new Entry[] { new("VileMage", 2), new("EvilMageLord", 2), new("CorruptedMage", 1) }),
        new("Shame_Revamped", true, 5387, 63, 20, 5384, 25, 16, 69, 3, 5, 2, 2, true, new Entry[] { new("MudPie", 2), new("Scorpion", 3) }),
        new("Shame_Revamped", true, 5818, 80, 0, 5808, 70, 20, 20, 30, 0, 2, 2, false, new Entry[] { new("EternalGazer", 1) }),
        new("Shame_Revamped", true, 5797, 39, 0, 5785, 27, 24, 24, 5, 1, 2, 2, true, new Entry[] { new("ChaosVortex", 1) }),
        new("Shame_Revamped", true, 5534, 107, 0, 5514, 87, 40, 40, 5, 5, 2, 2, true, new Entry[] { new("MudElemental", 3), new("GreaterEarthElemental", 2) }),
        new("Shame_Revamped", true, 5708, 44, 0, 5683, 19, 50, 50, 25, 3, 2, 2, false, new Entry[] { new("GreaterWaterElemental", 3) }),
        new("Shame_Chest", false, 5468, 106, 35, 5467, 105, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", false, 5439, 137, 20, 5438, 136, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5606, 25, 10, 5605, 24, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5620, 21, 10, 5619, 20, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5415, 189, 0, 5414, 188, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5604, 166, 0, 5603, 165, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5410, 170, 0, 5409, 169, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5390, 145, 20, 5389, 144, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5570, 118, 5, 5569, 117, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5536, 119, -5, 5535, 118, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", false, 5434, 100, 20, 5433, 99, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", false, 5403, 235, 10, 5402, 234, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5701, 58, 2, 5700, 57, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel4", 1) }),
        new("Shame_Chest", false, 5434, 89, 20, 5433, 88, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5817, 78, 0, 5816, 77, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5717, 58, 5, 5716, 57, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel4", 1) }),
        new("Shame_Chest", false, 5445, 12, 0, 5444, 11, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5405, 200, 0, 5404, 199, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5725, 78, 0, 5724, 77, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5546, 113, -5, 5545, 112, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5723, 77, 0, 5722, 76, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5608, 190, 0, 5607, 189, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5595, 147, 10, 5594, 146, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5733, 93, 0, 5732, 92, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel4", 1) }),
        new("Shame_Chest", false, 5437, 11, 0, 5436, 10, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", false, 5468, 100, 35, 5467, 99, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5605, 202, 0, 5604, 201, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5545, 153, 20, 5544, 152, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5594, 139, 10, 5593, 138, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5405, 185, 0, 5404, 184, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5492, 56, 20, 5491, 55, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", false, 5475, 190, 0, 5474, 189, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel4", 1) }),
        new("Shame_Chest", false, 5432, 158, 0, 5431, 157, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5698, 57, 5, 5697, 56, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5730, 90, 0, 5729, 89, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", false, 5699, 61, 0, 5698, 60, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5701, 60, 5, 5700, 59, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", false, 5396, 226, 10, 5395, 225, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5456, 157, 0, 5455, 156, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5456, 143, 20, 5455, 142, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5474, 176, 0, 5473, 175, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5553, 146, 20, 5552, 145, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5715, 61, 5, 5714, 60, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", false, 5406, 177, 0, 5405, 176, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5717, 60, 0, 5716, 59, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5572, 113, 5, 5571, 112, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5402, 58, -20, 5401, 57, 2, 2, 1, 2, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5615, 173, 0, 5614, 172, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5616, 184, 0, 5615, 183, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5733, 91, 0, 5732, 90, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5493, 51, 20, 5492, 50, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5715, 58, 2, 5714, 57, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", false, 5817, 83, 0, 5816, 82, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5730, 93, 0, 5729, 92, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", false, 5727, 72, 0, 5726, 71, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel4", 1) }),
        new("Shame_Chest", false, 5723, 73, 0, 5722, 72, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", false, 5398, 148, 20, 5397, 147, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", false, 5546, 118, -5, 5545, 117, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5570, 118, 5, 5569, 117, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5620, 21, 10, 5619, 20, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5475, 190, 0, 5474, 189, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel4", 1) }),
        new("Shame_Chest", true, 5616, 184, 0, 5615, 183, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5403, 235, 10, 5402, 234, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5701, 58, 2, 5700, 57, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel4", 1) }),
        new("Shame_Chest", true, 5402, 58, -20, 5401, 57, 2, 2, 1, 2, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5456, 157, 0, 5455, 156, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5439, 137, 20, 5438, 136, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5606, 25, 10, 5605, 24, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5434, 89, 20, 5433, 88, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5398, 148, 20, 5397, 147, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", true, 5415, 189, 0, 5414, 188, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5468, 106, 35, 5467, 105, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", true, 5715, 61, 5, 5714, 60, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", true, 5474, 176, 0, 5473, 175, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5717, 60, 0, 5716, 59, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5817, 78, 0, 5816, 77, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5390, 145, 20, 5389, 144, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5730, 90, 0, 5729, 89, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", true, 5698, 57, 5, 5697, 56, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5492, 56, 20, 5491, 55, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", true, 5468, 100, 35, 5467, 99, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5604, 166, 0, 5603, 165, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5615, 173, 0, 5614, 172, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5594, 139, 10, 5593, 138, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5727, 72, 0, 5726, 71, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel4", 1) }),
        new("Shame_Chest", true, 5572, 113, 5, 5571, 112, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5553, 146, 20, 5552, 145, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5406, 177, 0, 5405, 176, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5608, 190, 0, 5607, 189, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5546, 113, -5, 5545, 112, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5723, 73, 0, 5722, 72, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", true, 5456, 143, 20, 5455, 142, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5434, 100, 20, 5433, 99, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", true, 5730, 93, 0, 5729, 92, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5699, 61, 0, 5698, 60, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5405, 185, 0, 5404, 184, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5536, 119, -5, 5535, 118, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", true, 5405, 200, 0, 5404, 199, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5432, 158, 0, 5431, 157, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5396, 226, 10, 5395, 225, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5493, 51, 20, 5492, 50, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5723, 77, 0, 5722, 76, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5717, 58, 5, 5716, 57, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel4", 1) }),
        new("Shame_Chest", true, 5437, 11, 0, 5436, 10, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", true, 5546, 118, -5, 5545, 117, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5445, 12, 0, 5444, 11, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5701, 60, 5, 5700, 59, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel1", 1) }),
        new("Shame_Chest", true, 5715, 58, 2, 5714, 57, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5595, 147, 10, 5594, 146, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5545, 153, 20, 5544, 152, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5733, 91, 0, 5732, 90, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5725, 78, 0, 5724, 77, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel3", 1) }),
        new("Shame_Chest", true, 5733, 93, 0, 5732, 92, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel4", 1) }),
        new("Shame_Chest", true, 5605, 202, 0, 5604, 201, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5410, 170, 0, 5409, 169, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) }),
        new("Shame_Chest", true, 5817, 83, 0, 5816, 82, 2, 2, 1, 1, 1, 2, false, new Entry[] { new("TreasureLevel2", 1) })
    };

    public static bool IsShameSpawner(BaseSpawner spawner) =>
        spawner?.Name != null &&
        (string.Equals(spawner.Name, CreatureSpawnerName, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(spawner.Name, ChestSpawnerName, StringComparison.OrdinalIgnoreCase));

    public static bool AnyPresent() => World.Items.Values.OfType<Spawner>().Any(s => !s.Deleted && IsShameSpawner(s));

    /// <summary>
    ///     Creates the 194 spawners on their facets, tags them for [DeleteShame, and fills them, as
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
                new Rectangle3D(d.X, d.Y, Region.MinZ, d.Width, d.Height, Region.MaxZ - Region.MinZ)
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

            WeakEntityCollection.Add(ShameGenerator.CollectionKey, spawner);
            spawner.MoveToWorld(new Point3D(d.CentreX, d.CentreY, d.CentreZ), d.Map);
            spawner.Respawn();

            list.Add(spawner);
        }

        return list;
    }
}
