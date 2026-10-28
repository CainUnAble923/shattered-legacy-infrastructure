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
    // [ClusterFSeedNewHaven repair; a fresh world places them on the floor. WasAt may also name the
    // facing the entry used before (cc-P19, the League Registrar); world-load repair accepts that too.
    internal static readonly SeedEntry[] Entries =
    {
        // cc-P678: on the new start, as on the old (Tutorial/HavenSpots.cs).
        new("Sir Helper", "Town square", typeof(SirHelper), () => new SirHelper(), 3634, 2586, Direction.South, 0),

        new("Andric", "the Bullseye Marksmen", typeof(Andric), () => new Andric(), 3743, 2577, Direction.South, 40),
        new("Kashiel", "the Bullseye Marksmen", typeof(Kashiel), () => new Kashiel(), 3743, 2579, Direction.South, 40),
        new("Asandos", "the Haven inn", typeof(Asandos), () => new Asandos(), 3665, 2609, Direction.South, 0),
        new("Clairesse", "A Stitch in Time", typeof(Clairesse), () => new Clairesse(), 3689, 2465, Direction.South, 0),
        new("Gervis", "Mountainside (south)", typeof(Gervis), () => new Gervis(), 3505, 2749, Direction.South, 0),
        new("Mugg", "Mine (south mountains)", typeof(Mugg), () => new Mugg(), 3507, 2747, Direction.South, 0),
        new("Lowel", "Carpenters of Haven", typeof(Lowel), () => new Lowel(), 3633, 2497, Direction.South, 0),
        new("Lyle", "Haven Magery School", typeof(Lyle), () => new Lyle(), 3641, 2465, Direction.South, 0),
        new("Nibbet", "Haven Clockworks and Tinker Shop", typeof(Nibbet), () => new Nibbet(), 3664, 2496, Direction.South, 0),
        new("Norton", "the docks", typeof(Norton), () => new Norton(), 3650, 2654, Direction.South, 0),
        new("Sadrah", "Order of the Alembic hall", typeof(Sadrah), () => new Sadrah(), 3610, 2497, Direction.South, 0),
        new("Hargrove", "Carpenters of Haven", typeof(Hargrove), () => new Hargrove(), 3635, 2498, Direction.South, 0),

        new("Aelorn", "the Proving Grounds", typeof(Aelorn), () => new Aelorn(), 3729, 2578, Direction.South, 40),
        new("Dimethro", "the Proving Grounds", typeof(Dimethro), () => new Dimethro(), 3731, 2576, Direction.South, 40),
        new("Churchill", "the Proving Grounds", typeof(Churchill), () => new Churchill(), 3731, 2578, Direction.South, 40),
        new("Robyn", "the Proving Grounds", typeof(Robyn), () => new Robyn(), 3729, 2580, Direction.South, 40),
        new("Recaro", "the Proving Grounds", typeof(Recaro), () => new Recaro(), 3733, 2575, Direction.South, 40),
        new("Alden Armstrong", "the Proving Grounds", typeof(AldenArmstrong), () => new AldenArmstrong(), 3733, 2577, Direction.South, 40),
        new("Jockles", "the Proving Grounds", typeof(Jockles), () => new Jockles(), 3731, 2580, Direction.South, 40),
        new("Tyl Ariadne", "the Proving Grounds", typeof(TylAriadne), () => new TylAriadne(), 3733, 2579, Direction.South, 40),

        new("Alefian", "Haven Magery School", typeof(Alefian), () => new Alefian(), 3641, 2467, Direction.South, 0),
        new("Gustar", "Haven Magery School", typeof(Gustar), () => new Gustar(), 3643, 2466, Direction.South, 0),
        new("Jillian", "Haven Magery School", typeof(Jillian), () => new Jillian(), 3643, 2468, Direction.South, 0),
        new("Kaelynna", "Haven Magery School", typeof(Kaelynna), () => new Kaelynna(), 3641, 2469, Direction.South, 0),
        new("Mithneral", "Haven Magery School", typeof(Mithneral), () => new Mithneral(), 3645, 2465, Direction.South, 0),

        new("Amelia Youngstone", "Haven Clockworks and Tinker Shop", typeof(AmeliaYoungstone), () => new AmeliaYoungstone(), 3664, 2498, Direction.South, 0),
        new("Andreas Vesalius", "the Healers of Haven", typeof(AndreasVesalius), () => new AndreasVesalius(), 3660, 2577, Direction.South, 0),
        new("Avicenna", "the Healers of Haven", typeof(Avicenna), () => new Avicenna(), 3660, 2579, Direction.South, 0),
        new("Sarsmea Smythe", "the Second Bank of Haven", typeof(SarsmeaSmythe), () => new SarsmeaSmythe(), 3624, 2616, Direction.South, 0),

        new("Ryuichi", "the east cottage (Twin Paths dojo)", typeof(Ryuichi), () => new Ryuichi(), 3739, 2562, Direction.South, 40),
        new("Chiyo", "the east cottage (Twin Paths dojo)", typeof(Chiyo), () => new Chiyo(), 3738, 2564, Direction.South, 40),
        new("Jun", "the east cottage (Twin Paths dojo)", typeof(Jun), () => new Jun(), 3740, 2564, Direction.South, 40),
        new("Walker", "the east cottage (Twin Paths dojo)", typeof(Walker), () => new Walker(), 3737, 2566, Direction.South, 40),
        new("Hamato", "the east cottage (Twin Paths dojo)", typeof(Hamato), () => new Hamato(), 3741, 2562, Direction.South, 40),

        new("Mulcivikh", "the east house (Keepers of the Last Door)", typeof(Mulcivikh), () => new Mulcivikh(), 3729, 2635, Direction.South, 40),
        new("Morganna", "the east house (Keepers of the Last Door)", typeof(Morganna), () => new Morganna(), 3731, 2633, Direction.South, 40),
        new SeedEntry("Jacob Waltz", "Mine camp (south mountains)", typeof(JacobWaltz), () => new JacobWaltz(), 3510, 2745, Direction.South, 0).WasAt(3511, 2744, 0),
        new("George Hephaestus", "The Haven Blacksmith", typeof(GeorgeHephaestus), () => new GeorgeHephaestus(), 3634, 2616, Direction.South, 0),

        // League of Extraordinary Citizens field office: cc-P678, the free hall north-east of the square in SoS Haven
        // (3659-3673, 2548-2561). She faces South, with her desk on the counter east of her (3661,2549); before, she
        // faced West to the desk of the house at 3453-3462, 2596-2603, which is forest on SoS.
        new("League Registrar", "League field office", typeof(LeagueRegistrar), () => new LeagueRegistrar(), 3660, 2549, Direction.South, 0),
    };

    // cc-P51: where the Staff Hub's Travel tab sends staff, read from Entries so a moved entry moves the row too.
    // Each is the seeded NPC's own tile; nothing here changes what the seeder places.
    public static Map Facet => Map.Trammel;

    public static Point3D SpotOf(Type type)
    {
        foreach (var entry in Entries)
        {
            if (entry.Type == type)
            {
                return entry.GetLocation();
            }
        }

        throw new InvalidOperationException($"ClusterFNewHavenSeeder places no {type.Name}");
    }

    /// <summary>The New Haven banker's tile (Sarsmea Smythe, "New Haven Bank").</summary>
    public static Point3D BankAnchor => SpotOf(typeof(SarsmeaSmythe));

    /// <summary>The League Registrar's tile, inside the field office.</summary>
    public static Point3D RegistrarAnchor => SpotOf(typeof(LeagueRegistrar));

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
        if (acceptLegacyTiles && entry.HasLegacyLocation &&
            !NeedsRepairAt(mobile, entry.GetLegacyLocation(), entry.LegacyDirection))
        {
            return false;
        }

        return NeedsRepairAt(mobile, entry.GetLocation(), entry.Direction);
    }

    private static bool NeedsRepairAt(Mobile mobile, Point3D location, Direction direction)
    {
        if (mobile.Map != Map.Trammel || mobile.Location != location || mobile.Direction != direction || !mobile.CantWalk)
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
        private Direction? _legacyDirection;

        public bool HasAutoZ => _z == AutoZ;
        public bool HasLegacyLocation { get; private set; }

        /// <summary>The facing world load accepts on the legacy tile: the old one if WasAt named it, else today's.</summary>
        public Direction LegacyDirection => _legacyDirection ?? Direction;

        public Mobile Create() => _factory();

        public Point3D GetLocation() => Resolve(_x, _y, _z);

        /// <summary>Where this entry placed its NPC before PT-05, resolved as the seeder did then.</summary>
        public Point3D GetLegacyLocation() => Resolve(_legacyX, _legacyY, _legacyZ);

        public SeedEntry WasAt(int x, int y, int z = AutoZ, Direction? facing = null)
        {
            _legacyX = x;
            _legacyY = y;
            _legacyZ = z;
            _legacyDirection = facing;
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
