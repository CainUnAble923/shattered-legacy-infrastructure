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
///   - Miners' Compact Liaison at Trammel 3510, 2748, Z=0
///   - Survey Archivist at Trammel 3516, 2747, Z=1 (temporary — mine encampment TBD)
///   - Outriders' Guildmaster at Trammel 3524, 2574, Z=7 (New Haven stables area)
///   - Foresters' Guildmaster at Trammel 3441, 2637, Z=28 (New Haven carpenter shop area)
///   - Artificers' Guildmaster at TerMur 797, 3431, Z=-10 (Royal City enchanter district)
///   - Artificers' Guildmaster at Trammel 3490, 2627, Z=0 (New Haven mage school area)
///
/// Seeding is duplicate-safe: each NPC type is only spawned if no instance
/// of that type exists anywhere in the world. Use [ClusterFSeedInstitutions
/// to manually trigger seeding or check status.
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

    private static void Seed(bool verbose)
    {
        SeedNpc(
            typeof(MinersCompactLiaison),
            () => new MinersCompactLiaison(),
            x: 3510, y: 2748, z: 0,
            Direction.South,
            map: Map.Trammel,
            label: "Miners' Compact Liaison",
            verbose);

        // Temporary location — near the south mountain mine entrance.
        // Relocate to the mine encampment when that area is built.
        SeedNpc(
            typeof(SurveyArchivist),
            () => new SurveyArchivist(),
            x: 3516, y: 2747, z: 1,
            Direction.West,
            map: Map.Trammel,
            label: "Survey Archivist",
            verbose);

        // Sanitation Warden — seated at the civic hall table in New Haven.
        SeedNpc(
            typeof(SanitationWarden),
            () => new SanitationWarden(),
            x: 3506, y: 2560, z: 21,
            Direction.South,
            map: Map.Trammel,
            label: "Sanitation Warden",
            verbose);

        // Outriders' Guildmaster — New Haven stables area.
        SeedNpc(
            typeof(OutridersGuildmaster),
            () => new OutridersGuildmaster(),
            x: 3524, y: 2574, z: 7,
            Direction.South,
            map: Map.Trammel,
            label: "Outriders' Guildmaster",
            verbose);

        // Foresters' Guildmaster — New Haven wood yard.
        SeedNpc(
            typeof(ForestersGuildmaster),
            () => new ForestersGuildmaster(),
            x: 3441, y: 2637, z: 28,
            Direction.South,
            map: Map.Trammel,
            label: "Foresters' Guildmaster",
            verbose);

        // Artificers' Guildmaster — Royal City enchanter district (Ter Mur).
        SeedNpc(
            typeof(ArtificersGuildmaster),
            () => new ArtificersGuildmaster(),
            x: 797, y: 3431, z: -10,
            Direction.West,
            map: Map.TerMur,
            label: "Artificers' Guildmaster (Ter Mur)",
            verbose);

        // Artificers' Guildmaster — New Haven mage school area.
        SeedNpc(
            typeof(ArtificersGuildmaster),
            () => new ArtificersGuildmaster(),
            x: 3490, y: 2627, z: 0,
            Direction.South,
            map: Map.Trammel,
            label: "Artificers' Guildmaster (New Haven)",
            verbose);
    }

    private static void SeedNpc(Type type, Func<Mobile> factory,
        int x, int y, int z, Direction dir, Map map, string label, bool verbose)
    {
        // Duplicate-safe: check for an existing instance of this type within
        // 15 tiles of the target location on the same map. This allows the same
        // NPC type to be seeded in multiple distinct locations (e.g. New Haven
        // and Ter Mur both having an ArtificersGuildmaster).
        foreach (var mobile in World.Mobiles.Values)
        {
            if (!mobile.Deleted && mobile.GetType() == type && mobile.Map == map)
            {
                if (Math.Abs(mobile.X - x) <= 15 && Math.Abs(mobile.Y - y) <= 15)
                {
                    if (verbose)
                        Console.WriteLine($"[ClusterFInstitutionSeeder] {label} already exists at {mobile.Location} — skipping.");
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
    private static void OnSeedCommand(CommandEventArgs e)
    {
        Seed(verbose: true);
        e.Mobile.SendMessage("Institution seed complete. Check server console for details.");
    }
}
