using System;
using System.Collections.Generic;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.MLQuests.Mobiles;
using Server.Mobiles;

namespace Server;

public static class ClusterFNewHavenSeeder
{
    // Every entry states its z: the floor the NPC stands on, checked standable (Map.CanFit at
    // height 16) against the client map, statics and pinned's New Haven decoration
    // (notes/cc-P17-playtest-bugs-1.md, PT-05). An entry without a z used to take
    // Map.GetAverageZ, the land height, which is under a building's floor: 16 NPCs were sunk that
    // way, and two more stood in a wall, water or rock.
    //
    // WasAt names the tile an entry used before PT-05. World-load repair accepts an NPC standing
    // there, so an existing world keeps its NPCs where they are until an Administrator runs
    // [ClusterFSeedNewHaven repair; a fresh world places them on the floor.
    internal static readonly SeedEntry[] Entries =
    {
        new("Sir Helper", "Town square", typeof(SirHelper), () => new SirHelper(), 3503, 2574, Direction.South, 14),

        new("Andric", "New Haven Bowyer", typeof(Andric), () => new Andric(), 3535, 2536, Direction.South, 20),
        new("Kashiel", "New Haven Bowyer", typeof(Kashiel), () => new Kashiel(), 3538, 2536, Direction.South, 20),
        new SeedEntry("Asandos", "Bountiful Harvest Inn", typeof(Asandos), () => new Asandos(), 3504, 2519, Direction.South, 27).WasAt(3504, 2519),
        new SeedEntry("Clairesse", "A Stitch In Time", typeof(Clairesse), () => new Clairesse(), 3497, 2551, Direction.South, 20).WasAt(3497, 2551),
        new("Gervis", "Mountainside (south)", typeof(Gervis), () => new Gervis(), 3505, 2749, Direction.South, 0),
        new("Mugg", "Mine (south mountains)", typeof(Mugg), () => new Mugg(), 3507, 2747, Direction.South, 0),
        new SeedEntry("Lowel", "Carpenters of New Haven", typeof(Lowel), () => new Lowel(), 3444, 2638, Direction.South, 28).WasAt(3444, 2638),
        new SeedEntry("Lyle", "New Haven Magery School", typeof(Lyle), () => new Lyle(), 3487, 2498, Direction.South, 52).WasAt(3487, 2498),
        new SeedEntry("Nibbet", "Springs N Things", typeof(Nibbet), () => new Nibbet(), 3488, 2568, Direction.South, 20).WasAt(3488, 2568),
        new SeedEntry("Norton", "New Haven Docks", typeof(Norton), () => new Norton(), 3507, 2603, Direction.South, 1).WasAt(3510, 2603),
        new SeedEntry("Sadrah", "Little Shop of Alchemy", typeof(Sadrah), () => new Sadrah(), 3462, 2567, Direction.South, 35).WasAt(3461, 2567),
        new SeedEntry("Hargrove", "Carpenters of New Haven", typeof(Hargrove), () => new Hargrove(), 3446, 2640, Direction.South, 28).WasAt(3446, 2640),

        new("Aelorn", "Warrior's Guild Hall", typeof(Aelorn), () => new Aelorn(), 3524, 2536, Direction.South, 20),
        new("Dimethro", "Warrior's Guild Hall", typeof(Dimethro), () => new Dimethro(), 3527, 2536, Direction.South, 20),
        new("Churchill", "Warriors Training Area", typeof(Churchill), () => new Churchill(), 3531, 2531, Direction.South, 20),
        new("Robyn", "Warriors Training Area", typeof(Robyn), () => new Robyn(), 3535, 2531, Direction.South, 20),
        new("Recaro", "Warriors Training Area", typeof(Recaro), () => new Recaro(), 3535, 2534, Direction.South, 20),
        new("Alden Armstrong", "Warriors Training Area", typeof(AldenArmstrong), () => new AldenArmstrong(), 3535, 2537, Direction.South, 20),
        new("Jockles", "Warriors Training Area", typeof(Jockles), () => new Jockles(), 3535, 2544, Direction.South, 20),
        new("Tyl Ariadne", "Warriors Training Area", typeof(TylAriadne), () => new TylAriadne(), 3525, 2556, Direction.South, 20),

        new SeedEntry("Alefian", "New Haven Magery School", typeof(Alefian), () => new Alefian(), 3469, 2492, Direction.South, 71).WasAt(3468, 2492),
        new SeedEntry("Gustar", "New Haven Magery School", typeof(Gustar), () => new Gustar(), 3472, 2492, Direction.South, 71).WasAt(3472, 2492),
        new SeedEntry("Jillian", "New Haven Magery School", typeof(Jillian), () => new Jillian(), 3476, 2493, Direction.South, 72).WasAt(3476, 2492),
        new SeedEntry("Kaelynna", "New Haven Magery School", typeof(Kaelynna), () => new Kaelynna(), 3479, 2492, Direction.South, 71).WasAt(3480, 2492),
        new SeedEntry("Mithneral", "New Haven Magery School", typeof(Mithneral), () => new Mithneral(), 3484, 2493, Direction.South, 52).WasAt(3484, 2492),

        new("Amelia Youngstone", "Springs N Things", typeof(AmeliaYoungstone), () => new AmeliaYoungstone(), 3459, 2529, Direction.South, 53),
        new SeedEntry("Andreas Vesalius", "Healer's Hall", typeof(AndreasVesalius), () => new AndreasVesalius(), 3458, 2551, Direction.South, 35).WasAt(3458, 2551),
        new SeedEntry("Avicenna", "Healer's Hall", typeof(Avicenna), () => new Avicenna(), 3461, 2551, Direction.South, 35).WasAt(3461, 2551),
        new("Sarsmea Smythe", "New Haven Bank", typeof(SarsmeaSmythe), () => new SarsmeaSmythe(), 3492, 2577, Direction.South, 15),

        new("Ryuichi", "Ninja Dojo", typeof(Ryuichi), () => new Ryuichi(), 3422, 2520, Direction.South, 21),
        new("Chiyo", "Ninja Dojo", typeof(Chiyo), () => new Chiyo(), 3424, 2520, Direction.South, 21),
        new SeedEntry("Jun", "Ninja Dojo", typeof(Jun), () => new Jun(), 3420, 2520, Direction.South, 21).WasAt(3426, 2520, 21),
        new SeedEntry("Walker", "Ninja Dojo", typeof(Walker), () => new Walker(), 3418, 2520, Direction.South, 21).WasAt(3428, 2520, 21),
        new("Hamato", "Hamato Dojo", typeof(Hamato), () => new Hamato(), 3494, 2414, Direction.South, 55),

        new("Mulcivikh", "Necromancers Guild Hall", typeof(Mulcivikh), () => new Mulcivikh(), 3555, 2457, Direction.South, 15),
        new("Morganna", "Necromancers Guild Hall", typeof(Morganna), () => new Morganna(), 3547, 2462, Direction.South, 15),
        new SeedEntry("Jacob Waltz", "Mine camp (south mountains)", typeof(JacobWaltz), () => new JacobWaltz(), 3510, 2745, Direction.South, 0).WasAt(3511, 2744, 0),
        new("George Hephaestus", "Forge and Anvil", typeof(GeorgeHephaestus), () => new GeorgeHephaestus(), 3471, 2542, Direction.South, 36),

        // League of Extraordinary Citizens field office
        new("League Registrar", "League of Extraordinary Citizens Field Office", typeof(LeagueRegistrar), () => new LeagueRegistrar(), 3459, 2601, Direction.North, 18),
    };

    private static bool _enabled;
    private static bool _seedOnWorldLoad;
    private static bool _repairOnWorldLoad;

    public static void Configure()
    {
        _enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.newHavenSeeder.enabled", true);
        _seedOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("clusterf.newHavenSeeder.seedOnWorldLoad", false);
        _repairOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("clusterf.newHavenSeeder.repairOnWorldLoad", true);

        EventSink.WorldLoad += OnWorldLoad;
        CommandSystem.Register("ClusterFSeedNewHaven", AccessLevel.Administrator, ClusterFSeedNewHaven_OnCommand);
    }

    [Usage("ClusterFSeedNewHaven [missing|dryrun|repair|replace]")]
    [Description("Seeds or repairs the ClusterF New Haven quest NPC set. Default mode creates only missing NPCs. repair also moves NPCs off their pre-PT-05 tiles, which world load leaves alone.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places missing New Haven quest NPCs. repair moves them onto their floors. replace DELETES and recreates them.")]
    private static void ClusterFSeedNewHaven_OnCommand(CommandEventArgs e)
    {
        if (!_enabled)
        {
            e.Mobile.SendMessage("ClusterF New Haven seeder is disabled.");
            return;
        }

        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "missing";
        var dryRun = mode is "dryrun" or "status";
        var repair = mode is "repair" or "sync" or "status" or "dryrun";
        var replace = mode is "replace" or "force";

        if (!dryRun && !repair && !replace && mode != "missing")
        {
            e.Mobile.SendMessage("Usage: [ClusterFSeedNewHaven [missing|dryrun|repair|replace]");
            return;
        }

        var result = Seed(dryRun, replace, repair);
        e.Mobile.SendMessage(result.Message);
    }

    private static void OnWorldLoad()
    {
        if (!_enabled || !_seedOnWorldLoad)
        {
            return;
        }

        var result = Seed(false, false, _repairOnWorldLoad, acceptLegacyTiles: true);
        Console.WriteLine(result.Message);
    }

    internal static SeedResult Seed(bool dryRun, bool replace, bool repair, bool acceptLegacyTiles = false)
    {
        var deleted = 0;
        var created = 0;
        var repaired = 0;
        var skipped = 0;

        if (replace)
        {
            var existing = FindExistingSeedMobiles();
            deleted = existing.Count;

            if (!dryRun)
            {
                foreach (var mobile in existing)
                {
                    mobile.Delete();
                }
            }
        }

        foreach (var entry in Entries)
        {
            var existing = replace ? null : FindExisting(entry.Type);

            if (existing != null)
            {
                if (repair && NeedsRepair(existing, entry, acceptLegacyTiles))
                {
                    if (!dryRun)
                    {
                        ApplyEntry(existing, entry);
                    }

                    repaired++;
                    continue;
                }

                skipped++;
                continue;
            }

            if (!dryRun)
            {
                var mobile = entry.Create();
                ApplyEntry(mobile, entry);
            }

            created++;
        }

        return new SeedResult(
            $"ClusterF New Haven seed {(dryRun ? "dry run" : "complete")}: created {created}, repaired {repaired}, skipped {skipped}, deleted {deleted}."
        );
    }

    private static void ApplyEntry(Mobile mobile, SeedEntry entry)
    {
        var location = entry.GetLocation();

        mobile.Direction = entry.Direction;
        mobile.CantWalk = true;

        if (mobile is BaseCreature creature)
        {
            creature.Home = location;
            creature.RangeHome = 0;
        }

        mobile.MoveToWorld(location, Map.Trammel);
    }

    internal static bool NeedsRepair(Mobile mobile, SeedEntry entry, bool acceptLegacyTiles = false)
    {
        if (acceptLegacyTiles && entry.HasLegacyLocation && !NeedsRepairAt(mobile, entry, entry.GetLegacyLocation()))
        {
            return false;
        }

        return NeedsRepairAt(mobile, entry, entry.GetLocation());
    }

    private static bool NeedsRepairAt(Mobile mobile, SeedEntry entry, Point3D location)
    {
        if (mobile.Map != Map.Trammel || mobile.Location != location || mobile.Direction != entry.Direction || !mobile.CantWalk)
        {
            return true;
        }

        return mobile is BaseCreature creature && (creature.Home != location || creature.RangeHome != 0);
    }

    private static Mobile FindExisting(Type type)
    {
        foreach (var mobile in World.Mobiles.Values)
        {
            if (!mobile.Deleted && mobile.Map == Map.Trammel && mobile.GetType() == type && IsNewHavenArea(mobile.Location))
            {
                return mobile;
            }
        }

        return null;
    }

    private static List<Mobile> FindExistingSeedMobiles()
    {
        var results = new List<Mobile>();

        foreach (var entry in Entries)
        {
            foreach (var mobile in World.Mobiles.Values)
            {
                if (!mobile.Deleted && mobile.Map == Map.Trammel && mobile.GetType() == entry.Type && IsNewHavenArea(mobile.Location))
                {
                    results.Add(mobile);
                }
            }
        }

        return results;
    }

    private static bool IsNewHavenArea(Point3D location) =>
        location.X >= 3400 && location.X <= 3560 && location.Y >= 2400 && location.Y <= 2760;

    internal sealed class SeedEntry
    {
        private const int AutoZ = int.MinValue;

        public SeedEntry(
            string label,
            string area,
            Type type,
            Func<Mobile> factory,
            int x,
            int y,
            Direction direction,
            int z = AutoZ
        )
        {
            Label = label;
            Area = area;
            Type = type;
            _factory = factory;
            _x = x;
            _y = y;
            _z = z;
            Direction = direction;
        }

        public string Label { get; }
        public string Area { get; }
        public Type Type { get; }
        public Direction Direction { get; }

        private readonly Func<Mobile> _factory;
        private readonly int _x;
        private readonly int _y;
        private readonly int _z;

        private int _legacyX;
        private int _legacyY;
        private int _legacyZ = AutoZ;

        public bool HasAutoZ => _z == AutoZ;
        public bool HasLegacyLocation { get; private set; }

        public Mobile Create() => _factory();

        public Point3D GetLocation() => Resolve(_x, _y, _z);

        /// <summary>Where this entry placed its NPC before PT-05, resolved as the seeder did then.</summary>
        public Point3D GetLegacyLocation() => Resolve(_legacyX, _legacyY, _legacyZ);

        public SeedEntry WasAt(int x, int y, int z = AutoZ)
        {
            _legacyX = x;
            _legacyY = y;
            _legacyZ = z;
            HasLegacyLocation = true;
            return this;
        }

        private static Point3D Resolve(int x, int y, int z) =>
            new(x, y, z == AutoZ ? Map.Trammel.GetAverageZ(x, y) : z);
    }

    internal sealed class SeedResult
    {
        public SeedResult(string message)
        {
            Message = message;
        }

        public string Message { get; }
    }
}
