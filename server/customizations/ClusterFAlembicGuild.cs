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
// (GuildLocations: the Order of the Alembic hall, Haven, 3608,2497,0).
//
// cc-P678: New Haven moved into the author's Haven town on SoS, and the Order into a free building there (outer walls
// x3599-3615, y2495-2511, floor z0). It has three rooms: the hall, the north room (x3608-3614, y2496-2502, a bed against
// its north wall), entered from the room below through the arch at 3611,2503; that room's street door is the double
// arch at 3615,2507-2508 in the east wall. The guildmaster stands at 3608,2497 and Sadrah the courier at 3610,2497.
// The props keep the old shop's set: the shelf of dried herbs on the north wall behind Sadrah, the paired alchemist
// tables (mortar and pestle, flask, scales) against the north wall in the north-east corner, and the cauldron with the
// third table (heating stand, full vials) on the carpet's east edge. None stands on or beside the guildmaster, on a
// stock decoration or spawner tile, or beside a doorway; the walk from the street door to every tile around the
// guildmaster and to Sadrah stays open (checked offline against the SoS map files with pinned's movement rules,
// shard-migration notes/cc-P678-tools/office/check.py). Before cc-P678 the hall was the Little Shop of Alchemy in New
// Haven (walls x3456-3465, y2562-2571, floor z35), the props along its north wall.
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
            new("hall", 0x2DD4, "alchemist table", 3613, 2496, 0),
            new("hall", 0x2DD4, "alchemist table", 3614, 2496, 0),
            new("hall", 0x0E9B, "mortar and pestle", 3613, 2496, 5),
            new("hall", 0x182A, "flask", 3613, 2496, 5),
            new("hall", 0x1852, "scales", 3614, 2496, 5),
            new("hall", 0x0A9D, "wooden shelf", 3610, 2496, 0),
            new("hall", 0x0C41, "dried herbs", 3610, 2496, 12),
            new("hall", 0x0975, "cauldron", 3613, 2499, 0),
            new("hall", 0x2DD4, "alchemist table", 3613, 2500, 0),
            new("hall", 0x184A, "heating stand", 3613, 2500, 5),
            new("hall", 0x185D, "full vials", 3613, 2500, 5),
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
        [Description("Places the Order of the Alembic's hall props in its hall in New Haven (SoS Haven), if they are not on Trammel. dryrun lists what it would place and changes nothing. Moves and deletes nothing.")]
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
