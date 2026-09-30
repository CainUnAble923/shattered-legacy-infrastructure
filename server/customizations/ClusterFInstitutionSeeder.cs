using System;
using Server.Mobiles;

namespace Server;

/// <summary>
/// Seeds institution NPCs for Shattered Legacy.
///
/// Institution NPCs are the League-affiliated liaisons and advisors placed
/// throughout New Haven and Ter Mur's Royal City. They are distinct from the
/// New Haven quest NPCs managed by ClusterFNewHavenSeeder.
///
/// Current institutions:
///   - Miners' Compact Liaison at Trammel 3498, 2744, Z=4 (mine camp, at its tent's opening)
///   - Survey Archivist at Trammel 3496, 2754, Z=4 (mine camp, at its tent's opening)
///   - Outriders' Guildmaster at Trammel 3524, 2574, Z=7 (New Haven stables area)
///   - Foresters' Guildmaster at Trammel 3441, 2637, Z=28 (New Haven carpenter shop area)
///   - Artificers' Guildmaster at TerMur 797, 3431, Z=-10 (Royal City enchanter district)
///   (The New Haven Artificers' Guildmaster moved to ClusterFGuildHallSeeder in cc-P15.)
///
/// Seeding is duplicate-safe: an NPC is only spawned if no instance of its type
/// stands within 15 tiles of its tile, or of its old tile when it has one. Use
/// [ClusterFSeedInstitutions to manually trigger seeding or check status.
///
/// This seeder runs on every world load, on live and on test. A changed tile
/// must never relocate anything here: the old tile goes in wasAt, so an NPC
/// still standing near it counts as present and is left alone, and the move is
/// a command Chase runs (cc-P16: [ClusterFMoveMineCampNpcs).
/// </summary>
public static class ClusterFInstitutionSeeder
{
    public static void Configure()
    {
        EventSink.WorldLoad += OnWorldLoad;
        CommandSystem.Register("ClusterFSeedInstitutions", AccessLevel.Administrator, OnSeedCommand);
    }

    private static void OnWorldLoad()
    {
        Seed(verbose: false);
    }

    internal static void Seed(bool verbose)
    {
        SeedNpc(
            typeof(MinersCompactLiaison),
            () => new MinersCompactLiaison(),
            x: 3498, y: 2744, z: 4,
            Direction.East,
            map: Map.Trammel,
            label: "Miners' Compact Liaison",
            verbose,
            wasAt: new Point3D(3510, 2748, 0));

        // Mine camp tents (cc-P16, design/mine-camp-tents/layout.json). The Archivist's new tile is 20
        // tiles from its old one, outside the 15-tile duplicate check, so without wasAt a world that
        // still has her at 3516, 2747 would get a second Archivist here.
        SeedNpc(
            typeof(SurveyArchivist),
            () => new SurveyArchivist(),
            x: 3496, y: 2754, z: 4,
            Direction.East,
            map: Map.Trammel,
            label: "Survey Archivist",
            verbose,
            wasAt: new Point3D(3516, 2747, 1));

        // Sanitation Warden - seated at the civic hall table in New Haven.
        SeedNpc(
            typeof(SanitationWarden),
            () => new SanitationWarden(),
            x: 3506, y: 2560, z: 21,
            Direction.South,
            map: Map.Trammel,
            label: "Sanitation Warden",
            verbose);

        // Outriders' Guildmaster - New Haven stables area.
        SeedNpc(
            typeof(OutridersGuildmaster),
            () => new OutridersGuildmaster(),
            x: 3524, y: 2574, z: 7,
            Direction.South,
            map: Map.Trammel,
            label: "Outriders' Guildmaster",
            verbose);

        // Foresters' Guildmaster - New Haven wood yard.
        SeedNpc(
            typeof(ForestersGuildmaster),
            () => new ForestersGuildmaster(),
            x: 3441, y: 2637, z: 28,
            Direction.South,
            map: Map.Trammel,
            label: "Foresters' Guildmaster",
            verbose);

        // Artificers' Guildmaster - Royal City enchanter district (Ter Mur).
        SeedNpc(
            typeof(ArtificersGuildmaster),
            () => new ArtificersGuildmaster(),
            x: 797, y: 3431, z: -10,
            Direction.West,
            map: Map.TerMur,
            label: "Artificers' Guildmaster (Ter Mur)",
            verbose);

        // The New Haven Artificers' Guildmaster is ClusterFGuildHallSeeder's since cc-P15: it stood in the
        // water at 3490, 2627, 0 and moves into the Magery School. It left this list rather than changing
        // tile here, because this seeder runs on every world load and would place a second one beside
        // the Magery School while the old one still stood more than 15 tiles away.
    }

    private static void SeedNpc(Type type, Func<Mobile> factory,
        int x, int y, int z, Direction dir, Map map, string label, bool verbose, Point3D? wasAt = null)
    {
        // Duplicate-safe: check for an existing instance of this type within
        // 15 tiles of the target location on the same map. This allows the same
        // NPC type to be seeded in multiple distinct locations (e.g. New Haven
        // and Ter Mur both having an ArtificersGuildmaster).
        foreach (var mobile in World.Mobiles.Values)
        {
            if (!mobile.Deleted && mobile.GetType() == type && mobile.Map == map)
            {
                if (Math.Abs(mobile.X - x) <= 15 && Math.Abs(mobile.Y - y) <= 15 ||
                    wasAt is { } old && Math.Abs(mobile.X - old.X) <= 15 && Math.Abs(mobile.Y - old.Y) <= 15)
                {
                    if (verbose)
                        Console.WriteLine($"[ClusterFInstitutionSeeder] {label} already exists at {mobile.Location} - skipping.");
                    return;
                }
            }
        }

        var npc = factory();
        var loc = new Point3D(x, y, z);

        if (npc is BaseCreature bc)
        {
            bc.Home      = loc;
            bc.RangeHome = 0;
        }

        npc.Direction = dir;
        npc.CantWalk  = true;
        npc.MoveToWorld(loc, map);

        if (verbose)
            Console.WriteLine($"[ClusterFInstitutionSeeder] Spawned {label} at {loc}.");
        else
            Console.WriteLine($"[ClusterFInstitutionSeeder] {label} seeded at {loc}.");
    }

    [Usage("ClusterFSeedInstitutions")]
    [Description("Seeds League institution NPCs (Miners' Compact Liaison, etc.) if not already present.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.Any, NoDryRun = true, Summary = "Places missing League institution NPCs. It also runs by itself on every world load.")]
    private static void OnSeedCommand(CommandEventArgs e)
    {
        Seed(verbose: true);
        e.Mobile.SendMessage("Institution seed complete. Check server console for details.");
    }
}
