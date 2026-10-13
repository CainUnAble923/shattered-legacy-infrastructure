// cc-P77 (F-43, F-9 table note): the Order of the Alembic, guild 18. It owns Alchemy and Taste ID (Glassblowing
// follows Alchemy: it is a book unlock inside the Alchemy menu, not a SkillName), so every craft skill has one
// owning guild. The Healers' Covenant keeps Healing and Anatomy. Notes: shard-migration notes/cc-P77-alembic-guild.md.
//
// What lives here:
//   ClusterFAlembicGuild    the guild key, its rank names (placeholders until Chase picks), its currency name, and the
//                           hall decor seeder.
//   AlembicGuildmaster      the guildmaster, a StarterGuildmasterBase like the Dojo's and the Keepers'.
//   AlembicHallAddon        the hall's stock-art props.
//
// Everything else about the guild is data in the shared tables: its GuildDef (ClusterFGuildSystem), its skills
// (GuildSkillTable, which also decides its starter kit: the creation items of Alchemy and Taste ID), and its post
// (GuildLocations: the Little Shop of Alchemy, New Haven, 3463,2565,35).
//
// The hall is the Little Shop of Alchemy (walls x3456-3465, y2562-2571, floor z35; pinned's Alchemist and Herbalist
// spawn at 3461,2566 and Sadrah the courier stands at 3462,2567). The props stand along the north wall, the back
// wall in the client's view; props against the south or east wall are hidden behind it. Every tile was checked
// standable or blocked offline with the port of pinned Map.CanFit (notes/cc-P77-tools/walk.py): the guildmaster's
// tile fits at z35 and all eight tiles around it are reachable from both doors.
//
// Placing the guildmaster and the props changes a world, so neither happens at world load by default:
//   [ClusterFSeedGuildHalls [dryrun]      places the guildmaster with the other Seeded guildmasters (existing command)
//   [ClusterFSeedAlembicHall [dryrun]     places the props if they are not on Trammel; moves and deletes nothing
// clusterf.alembicHall.seedOnWorldLoad (default false) places the props at world load.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server
{
    public static class ClusterFAlembicGuild
    {
        public const string Key = "alembic";
        public const string Name = "Order of the Alembic";

        // The guild's scrip, by name (stored per guild key like every guild's, CharacterGuildData.GuildCurrency).
        // A placeholder name until Chase picks; MuleUpgrades.CurrencyName reads it.
        public const string CurrencyName = "Alembic Drams";

        // Rank names by GuildRankIndex 0 to 5 on the generic six-rank ladder (ClusterFGuildSystem.RankThresholds:
        // 0, 1,000, 5,000, 15,000, 50,000, 100,000 standing). PLACEHOLDERS: the generic names until Chase picks from
        // the proposals in notes/cc-P77-alembic-guild.md. Change the names here; the steps stay the ladder's.
        public static readonly string[] RankNames =
        [
            "Initiate",
            "Apprentice",
            "Journeyman",
            "Master",
            "Grandmaster",
            "Legendary"
        ];

        public static string GetRankName(int standing) =>
            RankNames[Math.Clamp(ClusterFGuildSystem.GuildRankIndex(Key, standing), 0, RankNames.Length - 1)];

        // -- The hall decor --

        // Stock art only; third-party pieces wait for an art registration prompt (notes, Part D). In placing order;
        // the addon sits at the first component.
        public static readonly MineCampComponent[] Hall =
        [
            new("hall", 0x2DD4, "alchemist table", 3458, 2563, 35),
            new("hall", 0x2DD4, "alchemist table", 3459, 2563, 35),
            new("hall", 0x0E9B, "mortar and pestle", 3458, 2563, 40),
            new("hall", 0x182A, "flask", 3458, 2563, 40),
            new("hall", 0x1852, "scales", 3459, 2563, 40),
            new("hall", 0x0A9D, "wooden shelf", 3462, 2563, 35),
            new("hall", 0x0C41, "dried herbs", 3462, 2563, 47),
            new("hall", 0x0975, "cauldron", 3463, 2563, 35),
            new("hall", 0x2DD4, "alchemist table", 3464, 2563, 35),
            new("hall", 0x184A, "heating stand", 3464, 2563, 40),
            new("hall", 0x185D, "full vials", 3464, 2563, 40),
        ];

        internal static readonly (Type Type, Func<BaseAddon> Create, MineCampComponent[] Components)[] Addons =
        [
            (typeof(AlembicHallAddon), () => new AlembicHallAddon(), Hall),
        ];

        private static bool _seedOnWorldLoad;

        public static void Configure()
        {
            _seedOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("clusterf.alembicHall.seedOnWorldLoad", false);
            EventSink.WorldLoad += OnWorldLoad;
            CommandSystem.Register("ClusterFSeedAlembicHall", AccessLevel.Administrator, OnSeedCommand);
        }

        private static void OnWorldLoad()
        {
            if (!_seedOnWorldLoad)
            {
                return;
            }

            foreach (var line in Seed(false))
            {
                Console.WriteLine(line);
            }
        }

        [Usage("ClusterFSeedAlembicHall [dryrun]")]
        [Description("Places the Order of the Alembic's hall props in the Little Shop of Alchemy, New Haven, if they are not on Trammel. dryrun lists what it would place and changes nothing. Moves and deletes nothing.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places the Order of the Alembic's hall props in New Haven if they are not on Trammel. Moves and deletes nothing.")]
        private static void OnSeedCommand(CommandEventArgs e)
        {
            var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

            if (mode is not ("" or "dryrun"))
            {
                e.Mobile.SendMessage("Usage: [ClusterFSeedAlembicHall [dryrun]");
                return;
            }

            foreach (var line in Seed(mode == "dryrun"))
            {
                e.Mobile.SendMessage(line);
            }
        }

        internal static List<string> Seed(bool dryRun) =>
            ClusterFMineCampSeeder.SeedAddons("Alembic hall", "addons", Addons, dryRun);
    }
}

namespace Server.Items
{
    // The Order of the Alembic's hall props (ClusterFAlembicGuild.Hall): pinned AddonComponents, not movable, no deed.
    [SerializationGenerator(0, false)]
    public partial class AlembicHallAddon : BaseAddon
    {
        [Constructible]
        public AlembicHallAddon() => ClusterFMineCampSeeder.AddComponents(this, ClusterFAlembicGuild.Hall);
    }
}

namespace Server.Mobiles
{
    // The Order of the Alembic's guildmaster. Placed by ClusterFGuildHallSeeder (GuildLocations, Seeded).
    [SerializationGenerator(0, false)]
    public partial class AlembicGuildmaster : StarterGuildmasterBase
    {
        [Constructible]
        public AlembicGuildmaster()
        {
            Name  = "Distiller Orla Vance";
            Title = "of the Order of the Alembic";
            Body  = 0x191;
            Female = true;
            Hue   = 0x83EA;

            AddItem(new Robe { Movable = false, Hue = 0x1F5 });
            AddItem(new HalfApron { Movable = false, Hue = 0x455 });
            AddItem(new Sandals { Movable = false });

            var hair = new Item(0x203D) { Movable = false, Hue = 0x47E, Layer = Layer.Hair };
            AddItem(hair);
        }

        public override string GuildKey => ClusterFAlembicGuild.Key;
    }
}
