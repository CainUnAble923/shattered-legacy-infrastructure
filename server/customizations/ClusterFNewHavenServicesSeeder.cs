// cc-P17 PT-01 and PT-07: New Haven's decoration, ankh and Healer, which a fresh world never gets.
//
// PT-01. Pinned places decoration only by [Decorate (Commands/Object Creation/Decorate.cs:23, :37-42),
// and pinned's data has no New Haven set: its Trammel/haven.cfg is pre-UOML Haven (forges and anvils
// at x 3592 and east, the ankh at 3606,2582). Only three statics and one chest fall inside New Haven.
// So New Haven has no forge and no anvil, and a new player cannot smelt or smith at the town smithy.
// ServUO pub57 ships the set as Data/Decoration/Trammel/NewHaven.cfg, plus three New Haven lines in
// its haven.cfg (the ankh, an east anvil and a small forge). Those lines are below, verbatim, one
// entry each, and each is built by pinned's own DecorationList.Read and Generate (Decorate.cs:1308-1380,
// :1220-1292), so construction and the duplicate check (FindItem, :1122, :1251) are pinned's.
//
// Departures from the files, argued in notes/cc-P17-playtest-bugs-1.md:
//   - NewHaven.cfg:177-178, "Static 0x F7B", is left out. Its item id does not parse (a space after
//     0x), so ServUO places an item with id 0 there, which draws nothing.
//   - An entry is kept out when pinned's FindItem would delete something to place it. FindItem does
//     not only find a match: it deletes what it takes for an out-of-date copy (Decorate.cs:1122-1215),
//     which is any door on the tile within 8 z (:1135-1168), and, for a light source, a lit item at the
//     same z with the same id and another light, or the same tile name (:1170-1190). Our worlds' doors
//     came from elsewhere (48 in New Haven on the test world; 24 of ServUO's New Haven doors share a
//     tile with one of them at another facing), and live's hand-placed large forge has lit pieces on
//     ServUO's forge-fire tile. Placing nothing is the price of deleting nothing.
//   - An entry is also kept out when its kind is already there, since FindItem only sees an item of the
//     same type and id on the same spot as the same thing:
//       a forge or anvil, when an item Blacksmithy counts as a forge or an anvil (DefBlacksmithy.cs:
//         45-46) stands on its tile within 3 z. The live world has hand-placed Forge and Anvil items on
//         ServUO's smithy tiles (2026-05-15), which are not the addon types the data names;
//       the ankh, when an ankh stands within 10 tiles. Live has a hand-placed one 5 tiles north.
//
// PT-07. Pinned's post-uoml Vendors.json puts a Healer spawner (count 2) at 3463,2558,35 and a
// HealerGuildmaster spawner at 3463,2558,36. The importer's dedupe finds existing spawners with
// GetItemsAt(Point3D), which ignores z (Engines/Spawners/ImportSpawnersCommand.cs:228-241;
// Map.ItemEnumerator.cs:31), so the second deletes the first and New Haven gets no Healer. The
// Healer spawner is placed here with pinned's settings at ServUO's healer tile (3462,2559,35;
// ServUO Spawns/trammel.xml), one tile off pinned's, so a later [GenerateSpawners of Vendors.json
// does not delete it the same way.
//
// Runs at world load only if clusterf.newHavenServices.seedOnWorldLoad is true (default false; the
// server writes the default into modernuo.json on first start). Otherwise an Administrator runs
// [ClusterFSeedNewHavenServices [dryrun]. Nothing is ever moved or deleted, and a re-run places
// nothing already there: pinned's FindItem for decoration, a door on the tile for doors, and any
// Healer or Healer spawner in New Haven for the Healer.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Server.Commands;
using Server.Engines.Craft;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;

namespace Server;

public static class ClusterFNewHavenServicesSeeder
{
    // The header as the .cfg writes it, then the entry's x, y, z. Source file and line beside each.
    internal static readonly DecorEntry[] Decoration =
    {
        new("MetalDoor 0x0677 (Facing=EastCCW)", 3486, 2503, 51), // NewHaven.cfg:9
        new("MetalDoor 0x0677 (Facing=EastCCW)", 3485, 2496, 71), // NewHaven.cfg:10
        new("MetalDoor 0x0675 (Facing=WestCW)", 3481, 2579, 20), // NewHaven.cfg:14
        new("MetalDoor 0x0675 (Facing=WestCW)", 3481, 2568, 20), // NewHaven.cfg:15
        new("MetalDoor 0x0675 (Facing=WestCW)", 3485, 2503, 51), // NewHaven.cfg:16
        new("MetalDoor 0x0675 (Facing=WestCW)", 3484, 2496, 71), // NewHaven.cfg:17
        new("MetalDoor 0x0675 (Facing=WestCW)", 3555, 2466, 15), // NewHaven.cfg:18
        new("MetalDoor 0x067F (Facing=NorthCCW)", 3491, 2571, 21), // NewHaven.cfg:21
        new("MetalDoor 0x067D (Facing=SouthCW)", 3491, 2573, 21), // NewHaven.cfg:24
        new("MetalDoor 0x067D (Facing=SouthCW)", 3472, 2489, 91), // NewHaven.cfg:25
        new("MetalDoor 0x067D (Facing=SouthCW)", 3550, 2453, 15), // NewHaven.cfg:26
        new("MetalDoor 0x067D (Facing=SouthCW)", 3550, 2458, 15), // NewHaven.cfg:27
        new("MetalDoor 0x067D (Facing=SouthCW)", 3550, 2462, 15), // NewHaven.cfg:28
        new("MetalDoor 0x0683 (Facing=NorthCW)", 3559, 2452, 15), // NewHaven.cfg:31
        new("MetalDoor 0x0683 (Facing=NorthCW)", 3559, 2458, 15), // NewHaven.cfg:32
        new("MetalDoor 0x0683 (Facing=NorthCW)", 3559, 2462, 15), // NewHaven.cfg:33
        new("MetalDoor 0x0681 (Facing=SouthCCW)", 3482, 2489, 91), // NewHaven.cfg:36
        new("DarkWoodDoor 0x06A7 (Facing=EastCCW)", 3470, 2522, 68), // NewHaven.cfg:41
        new("DarkWoodDoor 0x06A7 (Facing=EastCCW)", 3500, 2518, 47), // NewHaven.cfg:42
        new("DarkWoodDoor 0x06A7 (Facing=EastCCW)", 3495, 2518, 47), // NewHaven.cfg:43
        new("DarkWoodDoor 0x06AB (Facing=EastCW)", 3495, 2516, 47), // NewHaven.cfg:46
        new("DarkWoodDoor 0x06AB (Facing=EastCW)", 3500, 2516, 47), // NewHaven.cfg:47
        new("DarkWoodDoor 0x06A5 (Facing=WestCW)", 3469, 2522, 68), // NewHaven.cfg:50
        new("DarkWoodDoor 0x06A5 (Facing=WestCW)", 3442, 2570, 35), // NewHaven.cfg:51
        new("DarkWoodDoor 0x06A5 (Facing=WestCW)", 3458, 2526, 53), // NewHaven.cfg:52
        new("DarkWoodDoor 0x06A5 (Facing=WestCW)", 3458, 2531, 53), // NewHaven.cfg:53
        new("DarkWoodDoor 0x06A5 (Facing=WestCW)", 3506, 2526, 27), // NewHaven.cfg:54
        new("DarkWoodDoor 0x06A5 (Facing=WestCW)", 3508, 2549, 20), // NewHaven.cfg:55
        new("DarkWoodDoor 0x06A5 (Facing=WestCW)", 3497, 2549, 20), // NewHaven.cfg:56
        new("DarkWoodDoor 0x06A5 (Facing=WestCW)", 3497, 2556, 20), // NewHaven.cfg:57
        new("DarkWoodDoor 0x06A5 (Facing=WestCW)", 3497, 2554, 40), // NewHaven.cfg:58
        new("DarkWoodDoor 0x06A5 (Facing=WestCW)", 3508, 2556, 20), // NewHaven.cfg:59
        new("DarkWoodDoor 0x06A5 (Facing=WestCW)", 3503, 2549, 40), // NewHaven.cfg:60
        new("DarkWoodDoor 0x06A9 (Facing=WestCCW)", 3459, 2596, 15), // NewHaven.cfg:63
        new("DarkWoodDoor 0x06A9 (Facing=WestCCW)", 3508, 2545, 17), // NewHaven.cfg:64
        new("DarkWoodDoor 0x06AF (Facing=NorthCCW)", 3461, 2524, 73), // NewHaven.cfg:67
        new("DarkWoodDoor 0x06AF (Facing=NorthCCW)", 3497, 2534, 27), // NewHaven.cfg:68
        new("DarkWoodDoor 0x06B3 (Facing=NorthCW)", 3457, 2599, 18), // NewHaven.cfg:72
        new("DarkWoodDoor 0x06B3 (Facing=NorthCW)", 3496, 2589, 35), // NewHaven.cfg:73
        new("DarkWoodDoor 0x06B3 (Facing=NorthCW)", 3501, 2547, 20), // NewHaven.cfg:74
        new("DarkWoodDoor 0x06B3 (Facing=NorthCW)", 3491, 2547, 17), // NewHaven.cfg:75
        new("DarkWoodDoor 0x06B3 (Facing=NorthCW)", 3495, 2547, 40), // NewHaven.cfg:76
        new("DarkWoodDoor 0x06B3 (Facing=NorthCW)", 3501, 2547, 40), // NewHaven.cfg:77
        new("DarkWoodDoor 0x06B3 (Facing=NorthCW)", 3504, 2547, 40), // NewHaven.cfg:78
        new("DarkWoodDoor 0x06B3 (Facing=NorthCW)", 3496, 2517, 27), // NewHaven.cfg:79
        new("DarkWoodDoor 0x06B3 (Facing=NorthCW)", 3509, 2513, 47), // NewHaven.cfg:80
        new("DarkWoodDoor 0x06B3 (Facing=NorthCW)", 3509, 2518, 27), // NewHaven.cfg:81
        new("DarkWoodDoor 0x06AD (Facing=SouthCW)", 3473, 2530, 68), // NewHaven.cfg:85
        new("DarkWoodDoor 0x06AD (Facing=SouthCW)", 3477, 2527, 48), // NewHaven.cfg:86
        new("DarkWoodDoor 0x06AD (Facing=SouthCW)", 3483, 2533, 38), // NewHaven.cfg:87
        new("DarkWoodDoor 0x06AD (Facing=SouthCW)", 3474, 2519, 48), // NewHaven.cfg:88
        new("DarkWoodDoor 0x06B1 (Facing=SouthCCW)", 3504, 2527, 47), // NewHaven.cfg:91
        new("DarkWoodDoor 0x06B1 (Facing=SouthCCW)", 3504, 2522, 47), // NewHaven.cfg:92
        new("DarkWoodDoor 0x06B1 (Facing=SouthCCW)", 3496, 2518, 27), // NewHaven.cfg:93
        new("LightWoodDoor 0x06DD (Facing=SouthCW)", 3456, 2567, 32), // NewHaven.cfg:97
        new("LightWoodDoor 0x06DD (Facing=SouthCW)", 3465, 2567, 35), // NewHaven.cfg:98
        new("LightWoodDoor 0x06DD (Facing=SouthCW)", 3462, 2551, 35), // NewHaven.cfg:99
        new("LightWoodDoor 0x06DD (Facing=SouthCW)", 3468, 2559, 35), // NewHaven.cfg:100
        new("LightWoodDoor 0x06DD (Facing=SouthCW)", 3459, 2558, 32), // NewHaven.cfg:101
        new("LightWoodDoor 0x06DD (Facing=SouthCW)", 3447, 2589, 35), // NewHaven.cfg:102
        new("LightWoodDoor 0x06DD (Facing=SouthCW)", 3446, 2579, 35), // NewHaven.cfg:103
        new("LightWoodDoor 0x06DD (Facing=SouthCW)", 3446, 2582, 35), // NewHaven.cfg:104
        new("LightWoodDoor 0x06DD (Facing=SouthCW)", 3448, 2552, 35), // NewHaven.cfg:105
        new("LightWoodDoor 0x06DD (Facing=SouthCW)", 3449, 2606, 30), // NewHaven.cfg:106
        new("LightWoodDoor 0x06DF (Facing=NorthCCW)", 3448, 2551, 35), // NewHaven.cfg:110
        new("LightWoodDoor 0x06D5 (Facing=WestCW)", 3435, 2572, 53), // NewHaven.cfg:113
        new("LightWoodDoor 0x06D5 (Facing=WestCW)", 3443, 2563, 55), // NewHaven.cfg:114
        new("LightWoodDoor 0x06D5 (Facing=WestCW)", 3457, 2555, 35), // NewHaven.cfg:115
        new("LightWoodDoor 0x06D5 (Facing=WestCW)", 3462, 2562, 35), // NewHaven.cfg:116
        new("LightWoodDoor 0x06D5 (Facing=WestCW)", 3446, 2603, 30), // NewHaven.cfg:117
        new("LightWoodDoor 0x06D7 (Facing=EastCCW)", 3436, 2572, 53), // NewHaven.cfg:120
        new("LightWoodDoor 0x06D7 (Facing=EastCCW)", 3445, 2563, 35), // NewHaven.cfg:121
        new("LightWoodDoor 0x06DB (Facing=EastCW)", 3436, 2563, 48), // NewHaven.cfg:124
        new("LightWoodDoor 0x06D9 (Facing=WestCCW)", 3435, 2563, 49), // NewHaven.cfg:127
        new("LightWoodGate 0x0845 (Facing=SouthCCW)", 3522, 2577, 7), // NewHaven.cfg:132
        new("LightWoodGate 0x0847 (Facing=NorthCW)", 3522, 2576, 7), // NewHaven.cfg:135
        new("Forge 0x197A (Light=Circle300)", 3474, 2538, 31), // NewHaven.cfg:139
        new("Static 0x197E (Light=Circle300)", 3475, 2538, 31), // NewHaven.cfg:142
        new("Forge 0x19A2 (Light=Circle300)", 3476, 2538, 31), // NewHaven.cfg:145
        new("Forge 0x199E (Light=Circle300)", 3477, 2538, 31), // NewHaven.cfg:148
        new("Forge 0xFB1 (Light=Circle300)", 3467, 2539, 36), // NewHaven.cfg:152
        new("Forge 0xFB1 (Light=Circle300)", 3467, 2541, 36), // NewHaven.cfg:153
        new("AnvilSouthAddon 0xFB0", 3470, 2535, 41), // NewHaven.cfg:157
        new("AnvilSouthAddon 0xFB0", 3468, 2535, 41), // NewHaven.cfg:160
        new("Static 0x1062", 3499, 2546, 20), // NewHaven.cfg:164
        new("Static 0x1061", 3498, 2546, 20), // NewHaven.cfg:167
        new("Static 0xF21", 3505, 2552, 23), // NewHaven.cfg:171
        new("Static 0xF13", 3505, 2551, 23), // NewHaven.cfg:174
        new("Static 0xF8D", 3468, 2520, 51), // NewHaven.cfg:181
        new("Static 0xF88", 3468, 2518, 51), // NewHaven.cfg:184
        new("Static 0xB9E", 3473, 2543, 36), // NewHaven.cfg:191
        new("Static 0xB9E", 3498, 2557, 24), // NewHaven.cfg:192
        new("Static 0xB9E", 3459, 2532, 55), // NewHaven.cfg:193
        new("Static 0xB9E", 3434, 2573, 55), // NewHaven.cfg:194
        new("Static 0xB9E", 3510, 2557, 22), // NewHaven.cfg:195
        new("Static 0xBC8", 3473, 2543, 36), // NewHaven.cfg:198
        new("Static 0xBAE", 3495, 2536, 28), // NewHaven.cfg:201
        new("Static 0xBC5", 3475, 2520, 52), // NewHaven.cfg:204
        new("Static 0xBB3", 3510, 2525, 27), // NewHaven.cfg:207
        new("Static 0xBA6", 3498, 2557, 24), // NewHaven.cfg:210
        new("Static 0xC0B", 3494, 2569, 23), // NewHaven.cfg:213
        new("Static 0xBB5", 3497, 2594, 37), // NewHaven.cfg:216
        new("Static 0xBA3", 3469, 2574, 37), // NewHaven.cfg:219
        new("Static 0xBAB", 3469, 2558, 37), // NewHaven.cfg:222
        new("Static 0xBC5", 3471, 2566, 20), // NewHaven.cfg:225
        new("Static 0xC02", 3459, 2532, 55), // NewHaven.cfg:228
        new("Static 0xBC3", 3451, 2547, 50), // NewHaven.cfg:231
        new("Static 0xBC4", 3434, 2573, 55), // NewHaven.cfg:234
        new("Static 0xBB7", 3522, 2574, 1), // NewHaven.cfg:237
        new("Static 0xBC2", 3510, 2557, 22), // NewHaven.cfg:240
        new("Static 0xB95", 3478, 2528, 50), // NewHaven.cfg:243
        new("Static 0xBA1", 3450, 2607, 27), // NewHaven.cfg:246
        new("Static 0xBA1", 3478, 2528, 50), // NewHaven.cfg:247
        new("Static 0xBA1", 3522, 2574, 1), // NewHaven.cfg:248
        new("Static 0xBA1", 3451, 2547, 50), // NewHaven.cfg:249
        new("Static 0xBA1", 3471, 2566, 20), // NewHaven.cfg:250
        new("Static 0xBA1", 3469, 2558, 37), // NewHaven.cfg:251
        new("Static 0xBA1", 3469, 2574, 37), // NewHaven.cfg:252
        new("Static 0xBA1", 3497, 2594, 37), // NewHaven.cfg:253
        new("Static 0xBA1", 3494, 2569, 23), // NewHaven.cfg:254
        new("Static 0xBA1", 3475, 2520, 52), // NewHaven.cfg:255
        new("Static 0xBA1", 3510, 2525, 27), // NewHaven.cfg:256
        new("AnkhNorth 0x0004", 3526, 2516, 25), // haven.cfg:5
        new("AnvilEastAddon 0x0FAF", 3467, 2540, 36), // haven.cfg:265
        new("SmallForgeAddon 0x0FB1", 3469, 2535, 41), // haven.cfg:285
    };

    // Pinned's Vendors.json Healer spawner (count 2, 5 to 10 minutes, homeRange 0, walkingRange 4),
    // at ServUO's healer tile.
    public static readonly Point3D HealerSpawnerLocation = new(3462, 2559, 35);
    public const int HealerCount = 2;
    public const int HealerWalkingRange = 4;

    // The New Haven town region (Distribution/Data/regions.json, "New Haven").
    public static bool IsNewHaven(Point3D p) => p.X >= 3416 && p.X <= 3545 && p.Y >= 2480 && p.Y <= 2648;

    private static bool _seedOnWorldLoad;

    public static void Configure()
    {
        _seedOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("clusterf.newHavenServices.seedOnWorldLoad", false);

        EventSink.WorldLoad += OnWorldLoad;
        CommandSystem.Register("ClusterFSeedNewHavenServices", AccessLevel.Administrator, OnCommand);
    }

    private static void OnWorldLoad()
    {
        if (_seedOnWorldLoad)
        {
            Console.WriteLine(Seed(false).Message);
        }
    }

    [Usage("ClusterFSeedNewHavenServices [dryrun]")]
    [Description("Places ServUO's New Haven decoration (forges, anvils, ankh, doors, statics) and a Healer spawner where missing. Moves and deletes nothing; a re-run places nothing twice.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places New Haven forges, anvils, ankh, doors, statics and a Healer spawner where missing. Moves and deletes nothing.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

        if (mode != "" && mode != "dryrun")
        {
            e.Mobile.SendMessage("Usage: [ClusterFSeedNewHavenServices [dryrun]");
            return;
        }

        e.Mobile.SendMessage(Seed(mode == "dryrun").Message);
    }

    public readonly record struct SeedResult(
        int Placed, int Present, int KindAlreadyThere, bool HealerPlaced, string Message, List<string> KeptOut
    );

    public static SeedResult Seed(bool dryRun)
    {
        var map = Map.Trammel;
        var placed = 0;
        var present = 0;
        var kindThere = 0;
        var keptOut = new List<string>();

        foreach (var entry in Decoration)
        {
            if (KindAlreadyThere(map, entry))
            {
                kindThere++;
                keptOut.Add($"{entry.Header} {entry.Location}");
                continue;
            }

            if (dryRun)
            {
                // An estimate: pinned's FindItem also compares item id, light and name.
                if (SameTypeAt(map, entry))
                {
                    present++;
                }
                else
                {
                    placed++;
                }

                continue;
            }

            var generated = entry.Read().Generate([map]);

            if (generated > 0)
            {
                placed += generated;
            }
            else
            {
                present++;
            }
        }

        var healerPlaced = !HasHealer(map);

        if (healerPlaced && !dryRun)
        {
            PlaceHealerSpawner(map);
        }

        var verb = dryRun ? "would place" : "placed";
        var message = "New Haven services" + (dryRun ? " (dry run)" : "") +
                      $": decoration {verb} {placed}, already there {present}, kept out for a door, forge, anvil or ankh already there {kindThere}; " +
                      "Healer spawner " + (healerPlaced ? verb : "already there") + ".";

        return new SeedResult(placed, present, kindThere, healerPlaced, message, keptOut);
    }

    private static bool KindAlreadyThere(Map map, DecorEntry entry)
    {
        var p = entry.Location;

        if (entry.Kind == DecorKind.Ankh)
        {
            foreach (var item in map.GetItemsInRange(p, 10))
            {
                if (item is AnkhNorth or AnkhWest)
                {
                    return true;
                }
            }

            return false;
        }

        var lit = entry.IsLightSource;

        foreach (var item in map.GetItemsAt(p.X, p.Y))
        {
            var dz = Math.Abs(item.Z - p.Z);

            // What FindItem would delete (Decorate.cs:1135-1168, :1170-1190).
            if (entry.Kind == DecorKind.Door && item is BaseDoor && dz < 8)
            {
                return true;
            }

            if (lit && dz == 0 && (item.ItemID == entry.ItemId || item.ItemData.LightSource))
            {
                return true;
            }

            // The kind already there.
            if (entry.Kind == DecorKind.Smithy && dz <= 3 && IsSmithy(item))
            {
                return true;
            }
        }

        return false;
    }

    // What Blacksmithy counts as an anvil or a forge (DefBlacksmithy.cs:45-46).
    public static bool IsSmithy(Item item)
    {
        var type = item.GetType();
        return type.IsDefined(typeof(AnvilAttribute), false) || type.IsDefined(typeof(ForgeAttribute), false) ||
               item.ItemID is 4015 or 4016 or 11733 or 11734 or 4017 or >= 6522 and <= 6569 or 11736;
    }

    internal enum DecorKind
    {
        Other,
        Door,
        Smithy,
        Ankh
    }

    private static bool SameTypeAt(Map map, DecorEntry entry)
    {
        var type = AssemblyHandler.FindTypeByName(entry.TypeName);

        foreach (var item in map.GetItemsAt(entry.Location.X, entry.Location.Y))
        {
            if (item.GetType() == type && item.Z == entry.Location.Z)
            {
                return true;
            }
        }

        return false;
    }

    // A Healer (not a guildmaster, wandering or evil healer) in New Haven, or a spawner there that
    // spawns one: a Healer from any source is what a ghost needs.
    internal static bool HasHealer(Map map)
    {
        foreach (var mobile in World.Mobiles.Values)
        {
            if (!mobile.Deleted && mobile.Map == map && mobile.GetType() == typeof(Healer) && IsNewHaven(mobile.Location))
            {
                return true;
            }
        }

        foreach (var item in World.Items.Values)
        {
            if (item is BaseSpawner spawner && !spawner.Deleted && spawner.Map == map && IsNewHaven(spawner.Location))
            {
                foreach (var e in spawner.Entries)
                {
                    if (string.Equals(e.SpawnedName, "Healer", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static void PlaceHealerSpawner(Map map)
    {
        var at = HealerSpawnerLocation;
        var spawner = new Spawner(
            HealerCount,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(10),
            0,
            new Rectangle3D(at.X, at.Y, at.Z, 1, 1, 0),
            "Healer"
        )
        {
            WalkingRange = HealerWalkingRange
        };

        // As pinned's importer does (ImportSpawnersCommand.cs:248-249).
        spawner.MoveToWorld(at, map);
        spawner.Respawn();
    }

    internal sealed class DecorEntry
    {
        public DecorEntry(string header, int x, int y, int z)
        {
            Header = header;
            Location = new Point3D(x, y, z);
        }

        public string Header { get; }
        public Point3D Location { get; }

        public string TypeName => Header.Split(' ')[0];

        public int ItemId => Utility.ToInt32(Header.Split(' ')[1]);

        // FindItem's own test is the tile's LightSource flag (Decorate.cs:1170); a Light= parameter is the data's.
        public bool IsLightSource => Header.Contains("Light=") || TileData.ItemTable[ItemId & TileData.MaxItemValue].LightSource;

        public DecorKind Kind
        {
            get
            {
                var type = AssemblyHandler.FindTypeByName(TypeName);

                if (type == null)
                {
                    return DecorKind.Other;
                }

                if (type.IsSubclassOf(typeof(BaseDoor)))
                {
                    return DecorKind.Door;
                }

                if (type == typeof(AnkhNorth) || type == typeof(AnkhWest))
                {
                    return DecorKind.Ankh;
                }

                // Forge, AnvilSouthAddon, AnvilEastAddon, SmallForgeAddon; not the forge-light Static.
                return type == typeof(Forge) || typeof(BaseAddon).IsAssignableFrom(type) ? DecorKind.Smithy : DecorKind.Other;
            }
        }

        // A .cfg block holding this entry alone, read by pinned's reader.
        public DecorationList Read()
        {
            var text = $"{Header}\n{Location.X} {Location.Y} {Location.Z}\n";
            using var reader = new StreamReader(new MemoryStream(Encoding.ASCII.GetBytes(text)));
            return DecorationList.Read(reader);
        }
    }
}
