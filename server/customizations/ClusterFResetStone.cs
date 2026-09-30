// cc-P18, F-7: the Reset Stone in New Haven, and the seeder that places it.
//
// TEST-SHARD TOOL. The stone, its seeder, [ClusterFSeedResetStone and the player command
// [ResetMyAccount (ClusterFDevTools.cs) all come out when the live shard is reborn from the test
// shard's work (Chase, 2026-09-30). They are on the go-live checklist in
// shard-migration notes/cc-P18-reset-stone-young-craftx.md.
//
// Double-clicking the stone opens the same reset gump [ResetMyAccount opens (ClusterFDevTools.OpenSelfReset):
// the user's own account and current character, nothing else. The stone takes no configuration and
// holds no account, so no stone can be set up to reset someone else.
//
// Where it stands: 3501,2575,14 Trammel, two tiles south-west of the New Haven arrival tile (3503,2574,14,
// CharacterCreation; Sir Helper) and across the arrival tile from the Guild Board (3502,2573,14). Open
// ground at z14 with no static on the tile, clear of the inn's stone steps to the east (checked with
// cc-P15's map tools; notes/cc-P18-reset-stone-young-craftx.md).
//
// Placing it: at world load only if clusterf.resetStone.seedOnWorldLoad is true (default false, as the
// other New Haven seeders; the server writes the default into modernuo.json on first start). Otherwise
// an Administrator runs [ClusterFSeedResetStone [dryrun]. A Reset Stone anywhere in New Haven counts as
// already placed, so a re-run places nothing. Nothing is ever moved or deleted.

using Server.Commands;
using Server.Items;
using ModernUO.Serialization;

namespace Server
{
    public static class ClusterFResetStoneSeeder
    {
        public static readonly Point3D Location = new(3501, 2575, 14);

        private static bool _seedOnWorldLoad;

        public static void Configure()
        {
            _seedOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("clusterf.resetStone.seedOnWorldLoad", false);

            EventSink.WorldLoad += OnWorldLoad;
            CommandSystem.Register("ClusterFSeedResetStone", AccessLevel.Administrator, OnCommand);
        }

        private static void OnWorldLoad()
        {
            if (_seedOnWorldLoad)
            {
                System.Console.WriteLine(Seed(false).Message);
            }
        }

        [Usage("ClusterFSeedResetStone [dryrun]")]
        [Description("Test shard: places the Reset Stone in New Haven if none is there. Moves and deletes nothing; a re-run places nothing.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places the Reset Stone in New Haven if none is there.")]
        private static void OnCommand(CommandEventArgs e)
        {
            var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

            if (mode != "" && mode != "dryrun")
            {
                e.Mobile.SendMessage("Usage: [ClusterFSeedResetStone [dryrun]");
                return;
            }

            e.Mobile.SendMessage(Seed(mode == "dryrun").Message);
        }

        public readonly record struct SeedResult(bool Placed, ResetStone? Existing, string Message);

        public static SeedResult Seed(bool dryRun)
        {
            var map = Map.Trammel;
            var existing = FindInNewHaven(map);

            if (existing != null)
            {
                return new SeedResult(false, existing,
                    $"Reset Stone: already there at {existing.Location}; nothing placed.");
            }

            if (dryRun)
            {
                return new SeedResult(false, null, $"Reset Stone (dry run): would place one at {Location}.");
            }

            var stone = new ResetStone();
            stone.MoveToWorld(Location, map);
            return new SeedResult(true, null, $"Reset Stone: placed at {Location}.");
        }

        // Any Reset Stone in New Haven, wherever a GM may have moved it.
        public static ResetStone? FindInNewHaven(Map map)
        {
            foreach (var item in World.Items.Values)
            {
                if (item is ResetStone stone && !stone.Deleted && stone.Map == map &&
                    ClusterFNewHavenServicesSeeder.IsNewHaven(stone.Location))
                {
                    return stone;
                }
            }

            return null;
        }
    }
}

namespace Server.Items
{
    /// <summary>
    /// TEST-SHARD TOOL (cc-P18, F-7): opens the account reset for the user's own account and current
    /// character. Remove at the live rebirth. Not movable and does not decay.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class ResetStone : Item
    {
        // The pinned "gift stone" graphic (IngotStone, RegStone, AlchemyStone ...). One constant, so F-8 can
        // give the stone custom art later by changing this line.
        public const int StoneGraphic = 0xED4;
        public const int StoneHue     = 0x2B;
        public const int UseRange     = 2;

        [Constructible]
        public ResetStone() : base(StoneGraphic)
        {
            Movable = false;
            Hue = StoneHue;
        }

        public override string DefaultName => "a Reset Stone (test shard)";

        public override bool Decays => false;

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Resets your own account: every character on it, for guilds and exploration.");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!from.InRange(GetWorldLocation(), UseRange))
            {
                from.SendLocalizedMessage(500446); // That is too far away.
                return;
            }

            ClusterFDevTools.OpenSelfReset(from);
        }
    }
}
