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
//   - the Vault's crawlway (RootdeepCrawlway).
// It refuses while the landing is still void land (the map batch has not run), unless "force". Idempotent: a row
// already standing (a spawner of the same first type within 3 tiles, a working ankh within 2, a crawlway at its tile)
// is skipped, and nothing is moved or deleted. Not run at world load.
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

    [Usage("ClusterFSeedRootdeep [dryrun | force]")]
    [Description("Places the Rootdeep Burrows' spawners, the landing's working ankh and the Vault's crawlway, where missing. Refuses until the map batch has built the landing, unless force. Moves and deletes nothing.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places the Rootdeep Burrows' spawners, landing ankh and crawlway once the map batch has built them.")]
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

    public readonly record struct SeedResult(bool Refused, int Spawners, int Ankhs, int Crawlways, int Present, string Message);

    public static SeedResult Seed(bool dryRun, bool force = false)
    {
        var map = Map.Trammel;

        if (!force && !MapBuilt())
        {
            return new SeedResult(true, 0, 0, 0, 0,
                "Rootdeep Burrows: not seeded. The landing is still void land; the map batch builds it first (or use force).");
        }

        int spawners = 0, ankhs = 0, crawlways = 0, present = 0;

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
                new ClusterFNewHavenServicesSeeder.DecorEntry(AnkhHeader, ankh.X, ankh.Y, ankh.Z).Read().Generate([map]);
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

        var verb = dryRun ? "would place" : "placed";
        return new SeedResult(false, spawners, ankhs, crawlways, present,
            $"Rootdeep Burrows{(dryRun ? " (dry run)" : "")}: {verb} {spawners} spawners, {ankhs} ankh, {crawlways} crawlway; already there {present}.");
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
