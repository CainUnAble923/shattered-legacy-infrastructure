// cc-P141 (F-45, Part D): what the map batch places in the Rootdeep Burrows once its walls exist. Notes, with the layout
// spec every coordinate below comes from: shard-migration notes/cc-P141-rootdeep-burrows.md; the mockup Chase approves
// first: notes/art/rootdeep-mockup/ (layout.json there carries the same rows; check_layout.py compares the two).
//
// [ClusterFSeedRootdeep [dryrun | force] places:
//   - every spawner of the zones (stock Spawner; Group = true where a room respawns only when it is cleared,
//     BaseSpawner._group),
//   - the landing's working ankh (P137's AnkhWest through pinned's DecorationList, ClusterFAnkhs' method), because a
//     ghost that is not Young walks to the landing, and a Young one is sent to the healer nearest the region's Entrance
//     (PlayerMobile.cs:2563 and 4065-4128 at d4531cd9: New Haven's 3665,2587, not the landing),
//   - the Vault's crawlway (RootdeepCrawlway),
//   - cc-P188: the two teleporters of the way in: down, on the hole in Old Haven's ruined Second Bank (3619,2620, the
//     region's Entrance; the map patch draws the hole 0x1B71 there and opens the bank's south wall), landing on the
//     Landing's arrival tile; and up, at the foot of the rope ladder on the Landing's north wall (5193,1132; the ladder
//     0x08A6 is a map static on the wall tile 5193,1131), landing beside the hole (3619,2621). Stock Teleporter items
//     (pinned Items/Misc/Teleporter.cs: invisible, Active, players only, no delay), so nothing new is saved.
//     cc-P413: the hole is now an ant-hill burrow (OSI's 6 x 6 sinkhole set in the map patch, x 3615-3620,
//     y 2616-2621; shard-migration notes/cc-P413-rootdeep-anthill-entrance.md): a teleporter down on every tile of its
//     dark mouth (x 3617-3619, y 2617-2620, which holds P188's hole tile), and the ladder up lands outside the bank's
//     collapsed south wall (3619,2623), facing the mound and two tiles from the mouth. A re-run on a world seeded by
//     P188 keeps the hole's teleporter, adds the other eleven and points the ladder's at the new landing (retargeted,
//     not duplicated).
//     cc-P678 (SoS swap S7): on the Siege of Sosaria map the ruined Second Bank is the author's rebuilt Second Bank of
//     Haven, New Haven's bank, and the ant hill is gone (S2 drops it from the map patch). The way down is a cellar hatch in
//     the corner of the bank's main hall (3618,2620, one tile west of the old hole, three from the bankers): the
//     seeder places the hatch (art 0x3E65 "hatch", a surface 3 high set one below the floor so it is walkable; a server
//     item, so the town's statics are untouched) and one teleporter down on its tile; the mouth is that one tile. The ladder up lands three tiles from it
//     in the same hall (3621,2620). The retarget above still points an older ladder at the new landing.
//   - cc-P275: the eight stock floor traps of "watch the floor" (shard-migration notes/cc-P201-deployable-traps-design.md
//     section 6.1, every tile checked against the built floor, notes/cc-P167-tools/plan-cells.json, and clear of
//     decor): mushroom puffs by the decoy chests, spikes on one tile of each Warren descent, a saw at the Thicket's
//     gap, greater gas on the Gloom ring and the stone face at the Queen's door. Pinned's classes as they are
//     (Items/Traps/). The mushroom, spike, saw and stone face hurt creatures too, so those five tiles sit outside every
//     spawner's home range (a fact checks it); the gas trap hurts players only, so its two may sit inside one.
// It refuses while the landing is still void land (the map batch has not run), unless "force". Idempotent: a row
// already standing (a spawner of the same first type within 3 tiles, a working ankh within 2, a crawlway at its tile,
// a teleporter at its tile) is skipped, and nothing is moved or deleted. Not run at world load.
//
// cc-P610 (P425 #1, P533 U7): the way down is shut while the map overlay is missing. A world seeded with the map built
// keeps its stock teleporters; started without the overlay (a test shard may start without it, docker/uo
// Start-TestShard.ps1), they sent players into void land. Once the world is up (CheckMapAtStartup), each saved way-down
// teleporter of ours is switched off and marked while the landing is void, and switched on again (only those marked)
// at a start where it is built. The ladder up stays on, so anyone inside can still climb out. Characters saved inside
// are not moved (no real account is touched): the server log names how many there are for staff. Chosen over refusing
// the startup, which would stop the whole shard (and every other test) over one dungeon.
//
// Pack sizes respect the Burrows' limit of 3 hostiles on one Young player (OldHavenBurrowsRegion.MaxHostilesOnYoung):
// no spawner where a Young player can walk holds more than 3, and no two packs share a room's line of sight by
// design (the mockup shows each room). Home ranges stay inside their room: a spawner picks any walkable tile in its
// bounds, so a range that crosses a wall would spawn into the next room. The Hoarding Rat calls 2 Penny Rats, so it spawns alone; the Burrow Mother's
// brood (3 at once) makes her room's 4, and the region's limit stops the fourth from striking a Young player.

using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Regions;

namespace Server;

public static class ClusterFRootdeepSeeder
{
    public readonly record struct SpawnRow(
        string Label, string Zone, int X, int Y, int Count, int HomeRange, bool Group, int MinMinutes, int MaxMinutes,
        params string[] Types
    )
    {
        public Point2D Location => new(X, Y);
    }

    /// <summary>The landing's arrival tile from the B1 hole (the map batch's teleporter lands here).</summary>
    public static readonly Point2D Arrival = new(5193, 1138);

    /// <summary>The landing's working ankh: AnkhWest against the landing's east wall, facing the arrival.</summary>
    public const string AnkhHeader = "AnkhWest 0x0003";
    public static readonly Point2D AnkhAt = new(5206, 1136);

    public const int VoidLand = 0x244;

    /// <summary>cc-P188: a teleporter of the way in: where it stands and where it puts the player (both Trammel, Z 0).</summary>
    public readonly record struct TeleporterRow(string Label, int X, int Y, int ToX, int ToY)
    {
        public Point3D At => new(X, Y, 0);
        public Point3D To => new(ToX, ToY, 0);
    }

    /// <summary>cc-P678: the cellar hatch in the Second Bank of Haven's main hall (OldHavenBurrowsRegion.HoleEntrance).</summary>
    public static readonly Point2D Hole = new(3618, 2620);

    /// <summary>The hatch: OSI's "hatch" art, a Surface 3 high, so a player walks onto it and the teleporter fires.</summary>
    public const int HatchItemId = 0x3E65;

    public const string HatchName = "a cellar hatch";

    /// <summary>
    /// The tiles that take a player down, inclusive. cc-P413 made it the ant hill's twelve-tile mouth; cc-P678 makes it
    /// the hatch's one tile.
    /// </summary>
    public const int MouthX1 = 3618, MouthY1 = 2620, MouthX2 = 3618, MouthY2 = 2620;

    public static bool IsMouth(int x, int y) => x >= MouthX1 && x <= MouthX2 && y >= MouthY1 && y <= MouthY2;

    /// <summary>The foot of the rope ladder on the Landing's north wall, the way up.</summary>
    public static readonly Point2D LadderFoot = new(5193, 1132);

    /// <summary>
    /// cc-P678: where the ladder up lands: the bank's main hall, three tiles east of the hatch (cc-P413's was outside the
    /// ruined bank's south wall, by the mound), so nobody is sent straight back down. Floor Z 0, reached on foot from the
    /// town (notes/cc-P678-tools/soswalk.py).
    /// </summary>
    public static readonly Point2D BesideHatch = new(3621, 2620);

    public static readonly TeleporterRow LadderUp =
        new("the rope ladder up to the Second Bank of Haven", LadderFoot.X, LadderFoot.Y, BesideHatch.X, BesideHatch.Y);

    /// <summary>One teleporter down on every mouth tile, row by row, then the ladder up.</summary>
    public static readonly TeleporterRow[] Teleporters = MouthTeleporters();

    private static TeleporterRow[] MouthTeleporters()
    {
        var rows = new List<TeleporterRow>();

        for (var y = MouthY1; y <= MouthY2; y++)
        {
            for (var x = MouthX1; x <= MouthX2; x++)
            {
                rows.Add(new TeleporterRow($"the cellar hatch ({x},{y}) down to the Landing", x, y, Arrival.X, Arrival.Y));
            }
        }

        rows.Add(LadderUp);
        return rows.ToArray();
    }

    /// <summary>cc-P275: one stock floor trap (Kind names the class) on one tile.</summary>
    public readonly record struct FloorTrapRow(string Label, string Zone, int X, int Y, string Kind)
    {
        public Point2D Location => new(X, Y);
    }

    public static readonly FloorTrapRow[] FloorTraps =
    [
        new("T1 Deposit Hall: mushroom puff by decoy chest 1", "Vault", 5146, 1134, nameof(MushroomTrap)),
        new("T2 Deposit Hall: mushroom puff by decoy chest 3", "Vault", 5151, 1139, nameof(MushroomTrap)),
        new("T3 Warren west descent: spikes", "Warren", 5161, 1195, nameof(SpikeTrap)),
        new("T4 Warren east descent: spikes", "Warren", 5201, 1195, nameof(SpikeTrap)),
        new("T5 The Thicket's gap: floor saw", "Tangle", 5157, 1222, nameof(SawTrap)),
        new("T6 Gloom ring, north run: greater gas", "Gloom", 5165, 1235, nameof(GasTrap)),
        new("T7 Gloom ring, east run: greater gas", "Gloom", 5232, 1252, nameof(GasTrap)),
        new("T8 The Queen's lair mouth: stone face", "Gloom lair", 5183, 1247, nameof(StoneFaceTrap)),
    ];

    public static readonly SpawnRow[] Spawners =
    [
        // -- The Vault (tutorial participants only; TutorialCreatures with fixed drops). Lesson order: swarm, pack, teeth, boss.
        new("Counting Room: Ledger Bats", "Vault", 5174, 1137, 3, 4, true, 1, 2, "LedgerBat"),
        new("The Stacks: Penny Rats", "Vault", 5163, 1141, 2, 1, true, 1, 2, "PennyRat"),
        new("The Stacks: the Hoarding Rat", "Vault", 5160, 1133, 1, 2, false, 2, 3, "HoardingRat"),
        new("Deposit Hall: decoy chest 1", "Vault", 5150, 1133, 1, 0, false, 2, 3, "SafeDepositBox"),
        new("Deposit Hall: decoy chest 2", "Vault", 5143, 1137, 1, 0, false, 2, 3, "SafeDepositBox"),
        new("Deposit Hall: decoy chest 3", "Vault", 5152, 1141, 1, 0, false, 2, 3, "SafeDepositBox"),
        // Respawns after the crawlway has closed again (RootdeepVault.CrawlwayOpenFor, 2 minutes).
        new("Strongroom: the Overdraft", "Vault", 5160, 1151, 1, 6, false, 3, 4, "TheOverdraft"),

        // -- The Warren (tier 1): Burrowers.
        new("Crawl Den: grubs", "Warren", 5158, 1167, 2, 5, true, 3, 5, "BurrowGrub"),
        new("Crawl Den: hog", "Warren", 5150, 1170, 1, 4, false, 5, 8, "BurrowHog"),
        new("Tunnel Foot: scamps", "Warren", 5193, 1166, 2, 4, true, 3, 5, "BurrowScamp"),
        new("Great Burrow west: grubs and scamps", "Warren", 5156, 1184, 3, 6, true, 4, 6, "BurrowGrub", "BurrowScamp"),
        new("Great Burrow middle: scamps", "Warren", 5177, 1184, 2, 6, true, 4, 6, "BurrowScamp", "RedcapScamp"),
        new("Great Burrow east: redcap and grub", "Warren", 5200, 1186, 2, 5, true, 4, 6, "RedcapScamp", "BurrowGrub"),
        new("Great Burrow: hogs", "Warren", 5190, 1188, 2, 6, false, 5, 8, "BurrowHog"),
        new("Great Burrow: rotworm", "Warren", 5170, 1190, 1, 6, false, 8, 12, "Rotworm"),
        new("Great Burrow: grubber", "Warren", 5186, 1178, 1, 8, false, 20, 30, "Grubber"),
        new("Brood Pit: the Burrow Mother", "Brood Pit", 5226, 1178, 1, 4, false, 20, 30, "TheBurrowMother"),

        // -- The Tangle (tier 2): Roots.
        new("Tangle north: rootlings", "Tangle", 5160, 1202, 2, 5, true, 4, 6, "Rootling"),
        new("Tangle: sporecaps", "Tangle", 5176, 1201, 2, 5, true, 4, 6, "Sporecap"),
        new("Tangle south: roots", "Tangle", 5168, 1224, 3, 6, true, 5, 7, "Rootling", "Sporecap"),
        new("Tangle east: rootlings", "Tangle", 5184, 1216, 2, 4, true, 4, 6, "Rootling"),
        new("The Thicket: Bramblehulk", "Tangle", 5148, 1223, 1, 4, false, 10, 15, "Bramblehulk"),

        // -- The Dawnseam (tier 2): Stone.
        new("Dawnseam north: shards", "Dawnseam", 5209, 1201, 2, 5, true, 4, 6, "Dawnshard"),
        new("Dawnseam west: shards", "Dawnseam", 5200, 1222, 2, 5, true, 4, 6, "Dawnshard"),
        new("Dawnseam east: shards", "Dawnseam", 5222, 1209, 3, 6, true, 5, 7, "Dawnshard"),
        new("The Seam Heart: Seamguard", "Dawnseam", 5236, 1223, 1, 4, false, 10, 15, "Seamguard"),

        // -- The Gloom (tier 3): Spiders.
        new("Gloom: north hatchery", "Gloom", 5166, 1236, 3, 5, true, 4, 6, "Gloomling"),
        new("Gloom: west spinners", "Gloom", 5156, 1248, 2, 5, true, 5, 7, "Gloomspinner"),
        new("Gloom: south run", "Gloom", 5205, 1259, 3, 2, true, 5, 7, "Gloomling", "Gloomspinner"),
        new("Gloom: east weavers", "Gloom", 5232, 1250, 2, 4, true, 6, 8, "Gloomweaver", "Gloomspinner"),
        new("Gloom: bloodworm", "Gloom", 5176, 1258, 1, 6, false, 15, 20, "BloodWorm"),
        new("Queen's lair: guards", "Gloom lair", 5198, 1242, 2, 3, true, 10, 15, "Gloomweaver", "Gloomspinner"),
        new("Queen's lair: the Gloom Queen", "Gloom lair", 5212, 1252, 1, 5, false, 30, 45, "TheGloomQueen"),
    ];

    /// <summary>Whether the map batch has built the landing (the arrival tile is no longer void land). Tests replace it.</summary>
    public static Func<bool> MapBuilt { get; set; } = () =>
        Map.Trammel is { } map && map.Tiles.GetLandTile(Arrival.X, Arrival.Y).ID != VoidLand;

    public static void Configure()
    {
        CommandSystem.Register("ClusterFSeedRootdeep", AccessLevel.Administrator, OnSeedCommand);
    }

    public static void Initialize() => CheckMapAtStartup();

    /// <summary>What a way-down teleporter's name ends with while it is shut for a missing map.</summary>
    public const string ShutForMissingMap = " (shut: the Rootdeep map is missing)";

    /// <summary>
    /// cc-P610: once the world is up, the saved way down follows the map: shut (and marked) while the landing is void,
    /// open again (only what this shut) once it is built. Returns how many teleporters changed.
    /// </summary>
    public static int CheckMapAtStartup()
    {
        if (Map.Trammel is not { } map)
        {
            return 0;
        }

        var built = MapBuilt();
        var arrival = new Point3D(Arrival.X, Arrival.Y, 0);
        var changed = 0;
        foreach (var row in Teleporters)
        {
            if (row.To != arrival || FindOurTeleporterAt(map, row) is not { } t)
            {
                continue;
            }

            var shut = t.Name?.EndsWith(ShutForMissingMap, StringComparison.Ordinal) == true;
            if (!built && t.Active && !shut)
            {
                t.Active = false;
                t.Name += ShutForMissingMap;
                changed++;
            }
            else if (built && shut)
            {
                t.Name = t.Name![..^ShutForMissingMap.Length];
                t.Active = true;
                changed++;
            }
        }

        if (!built && changed > 0)
        {
            var inside = 0;
            foreach (var m in World.Mobiles.Values)
            {
                if (m is Mobiles.PlayerMobile pm && !pm.Deleted &&
                    (pm.Map == map && OldHavenBurrowsRegion.IsInside(map, pm.Location) ||
                     pm.LogoutMap == map && OldHavenBurrowsRegion.IsInside(map, pm.LogoutLocation)))
                {
                    inside++;
                }
            }

            Console.WriteLine(
                $"Rootdeep: the map overlay is missing; the way down is shut ({changed} teleporters). {inside} character(s) saved inside the Burrows; nobody was moved."
            );
        }

        return changed;
    }

    [Usage("ClusterFSeedRootdeep [dryrun | force]")]
    [Description("Places the Rootdeep Burrows' spawners, the landing's working ankh, the Vault's crawlway, the eight floor traps, the cellar hatch in the Second Bank of Haven and the two teleporters of the way in (the hatch, the ladder up), where missing. Refuses until the map batch has built the landing, unless force. Moves and deletes nothing.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places the Rootdeep Burrows' spawners, landing ankh, crawlway and the way-in teleporters once the map batch has built them.")]
    private static void OnSeedCommand(CommandEventArgs e)
    {
        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";
        if (mode is not ("" or "dryrun" or "force"))
        {
            e.Mobile.SendMessage("Usage: [ClusterFSeedRootdeep [dryrun | force]");
            return;
        }

        e.Mobile.SendMessage(Seed(mode == "dryrun", mode == "force").Message);
    }

    public readonly record struct SeedResult(
        bool Refused, int Spawners, int Ankhs, int Crawlways, int Present, string Message, int Teleporters = 0, int FloorTraps = 0,
        int FloorTrapsPresent = 0, int TeleportersRetargeted = 0, int Hatches = 0, int HatchesPresent = 0
    );

    public static SeedResult Seed(bool dryRun, bool force = false)
    {
        var map = Map.Trammel;

        if (!force && !MapBuilt())
        {
            return new SeedResult(true, 0, 0, 0, 0,
                "Rootdeep Burrows: not seeded. The landing is still void land; the map batch builds it first (or use force).");
        }

        int spawners = 0, ankhs = 0, crawlways = 0, teleporters = 0, retargeted = 0, present = 0;

        foreach (var row in Spawners)
        {
            if (FindSpawner(map, row) != null)
            {
                present++;
                continue;
            }

            if (!dryRun)
            {
                Place(map, row);
            }

            spawners++;
        }

        var ankh = new Point3D(AnkhAt.X, AnkhAt.Y, map.GetAverageZ(AnkhAt.X, AnkhAt.Y));
        if (ClusterFAnkhs.WorkingAnkhNear(map, ankh))
        {
            present++;
        }
        else
        {
            if (!dryRun)
            {
                new ClusterFDecorLine(AnkhHeader, ankh.X, ankh.Y, ankh.Z).Read().Generate([map]);
            }

            ankhs++;
        }

        if (FindCrawlway(map) != null)
        {
            present++;
        }
        else
        {
            if (!dryRun)
            {
                var mouth = RootdeepVault.CrawlwayMouth;
                new RootdeepCrawlway().MoveToWorld(new Point3D(mouth.X, mouth.Y, map.GetAverageZ(mouth.X, mouth.Y)), map);
            }

            crawlways++;
        }

        foreach (var row in Teleporters)
        {
            if (FindTeleporter(map, row) != null)
            {
                present++;
                continue;
            }

            // cc-P413: one of ours already on the tile pointing elsewhere (P188's ladder, which landed at 3619,2621):
            // point it where the row says rather than stand a second teleporter on the same tile.
            if (FindOurTeleporterAt(map, row) is { } old)
            {
                if (!dryRun)
                {
                    old.PointDest = row.To;
                    old.MapDest = map;
                    old.Name = $"Rootdeep: {row.Label}";
                }

                retargeted++;
                continue;
            }

            if (!dryRun)
            {
                PlaceTeleporter(map, row);
            }

            teleporters++;
        }

        int hatches = 0, hatchesPresent = 0;
        if (FindHatch(map) != null)
        {
            hatchesPresent++;
        }
        else
        {
            if (!dryRun)
            {
                PlaceHatch(map);
            }

            hatches++;
        }

        int floorTraps = 0, floorTrapsPresent = 0;

        foreach (var row in FloorTraps)
        {
            if (FindFloorTrap(map, row) != null)
            {
                floorTrapsPresent++;
                continue;
            }

            if (!dryRun)
            {
                PlaceFloorTrap(map, row);
            }

            floorTraps++;
        }

        var verb = dryRun ? "would place" : "placed";
        return new SeedResult(false, spawners, ankhs, crawlways, present,
            $"Rootdeep Burrows{(dryRun ? " (dry run)" : "")}: {verb} {spawners} spawners, {ankhs} ankh, {crawlways} crawlway, {teleporters} teleporters, {hatches} hatch, {floorTraps} floor traps; {(dryRun ? "would point" : "pointed")} {retargeted} teleporters at their new landing; already there {present} (and {hatchesPresent} hatch, {floorTrapsPresent} floor traps).",
            teleporters, floorTraps, floorTrapsPresent, retargeted, hatches, hatchesPresent);
    }

    /// <summary>cc-P275: a new stock trap of the row's kind (spikes facing west, the saw and gas on the floor, greater gas).</summary>
    public static BaseTrap MakeFloorTrap(FloorTrapRow row) => row.Kind switch
    {
        nameof(MushroomTrap)  => new MushroomTrap(),
        nameof(SpikeTrap)     => new SpikeTrap(SpikeTrapType.WestFloor),
        nameof(SawTrap)       => new SawTrap(SawTrapType.NorthFloor),
        nameof(GasTrap)       => new GasTrap(GasTrapType.Floor, Poison.Greater),
        nameof(StoneFaceTrap) => new StoneFaceTrap(),
        _                     => throw new ArgumentException($"no floor trap called {row.Kind}")
    };

    public static BaseTrap PlaceFloorTrap(Map map, FloorTrapRow row)
    {
        var trap = MakeFloorTrap(row);
        trap.MoveToWorld(new Point3D(row.X, row.Y, map.GetAverageZ(row.X, row.Y)), map);
        return trap;
    }

    /// <summary>The trap of the row's class standing on the row's tile, or null.</summary>
    public static BaseTrap FindFloorTrap(Map map, FloorTrapRow row)
    {
        foreach (var item in map.GetItemsInRange<BaseTrap>(new Point3D(row.X, row.Y, 0), 0))
        {
            if (!item.Deleted && item.GetType().Name == row.Kind)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// The hatch sits one below the bank's floor (land Z 0): its top is then Z 2, within pinned's StepHeight 2
    /// (Engines/Pathing/Movement.cs:265, :346), so a player walks onto it and the teleporter under it fires. At Z 0 its top
    /// (3) is a step too high and nobody could reach the tile (notes/cc-P678-tools/soswalk.py shows both).
    /// </summary>
    public static Point3D HatchAt => new(Hole.X, Hole.Y, -1);

    /// <summary>cc-P678: the cellar hatch, an immovable Static on the bank's floor.</summary>
    public static Static PlaceHatch(Map map)
    {
        var hatch = new Static(HatchItemId) { Name = HatchName };
        hatch.MoveToWorld(HatchAt, map);
        return hatch;
    }

    public static Static FindHatch(Map map)
    {
        foreach (var item in map.GetItemsInRange<Static>(HatchAt, 0))
        {
            if (!item.Deleted && item.ItemID == HatchItemId)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>A stock teleporter on the row's tile to the row's destination: players only, no delay, no effect.</summary>
    public static Teleporter PlaceTeleporter(Map map, TeleporterRow row)
    {
        var t = new Teleporter(row.To, map) { Name = $"Rootdeep: {row.Label}" };
        t.MoveToWorld(row.At, map);
        return t;
    }

    /// <summary>cc-P413: a teleporter the seeder placed (its name starts "Rootdeep: ") on the row's tile, wherever it points.</summary>
    public static Teleporter FindOurTeleporterAt(Map map, TeleporterRow row)
    {
        foreach (var item in map.GetItemsInRange<Teleporter>(row.At, 0))
        {
            if (!item.Deleted && item.Name?.StartsWith("Rootdeep: ", StringComparison.Ordinal) == true)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>The teleporter standing on the row's tile that points where the row does, or null.</summary>
    public static Teleporter FindTeleporter(Map map, TeleporterRow row)
    {
        foreach (var item in map.GetItemsInRange<Teleporter>(row.At, 0))
        {
            if (!item.Deleted && item.PointDest == row.To && (item.MapDest == null || item.MapDest == map))
            {
                return item;
            }
        }

        return null;
    }

    public static Spawner Place(Map map, SpawnRow row)
    {
        var z = map.GetAverageZ(row.X, row.Y);
        var spawner = new Spawner(row.Count, TimeSpan.FromMinutes(row.MinMinutes), TimeSpan.FromMinutes(row.MaxMinutes), 0, default, row.Types)
        {
            Name = $"Rootdeep: {row.Label}",
            Group = row.Group
        };
        spawner.MoveToWorld(new Point3D(row.X, row.Y, z), map);
        spawner.HomeRange = row.HomeRange; // after the move: the bounds are centred on the spawner's location (BaseSpawner.cs:189-219)
        spawner.Respawn();
        return spawner;
    }

    public static Spawner FindSpawner(Map map, SpawnRow row)
    {
        foreach (var item in map.GetItemsInRange<Spawner>(new Point3D(row.X, row.Y, 0), 3))
        {
            if (!item.Deleted && item.Entries.Count > 0 && item.Entries[0].SpawnedName.Equals(row.Types[0], StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    public static RootdeepCrawlway FindCrawlway(Map map)
    {
        var mouth = RootdeepVault.CrawlwayMouth;
        foreach (var item in map.GetItemsInRange<RootdeepCrawlway>(mouth, 0))
        {
            if (!item.Deleted)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Every type a row names: the data's own check that each resolves (the facts call it).</summary>
    public static IEnumerable<string> AllTypeNames()
    {
        foreach (var row in Spawners)
        {
            foreach (var t in row.Types)
            {
                yield return t;
            }
        }
    }
}
