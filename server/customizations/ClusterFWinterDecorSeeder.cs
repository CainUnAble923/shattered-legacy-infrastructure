// cc-P34 (F-22): the dated winter decor for New Haven. OSI holiday art (pinned already has it, every client has the art)
// and community art by MrRiots and Vrark (our own art IDs 0x3C40-0x3C6E, shipped in the player package). Notes in
// shard-migration notes/cc-P34-winter-decor.md.
//
// What it does
//   Up from December 1 to January 6, inclusive, on the shard's local clock: Core.Now (UTC, pinned Server/Main.cs:657)
//   in the server's time zone, which both compose files set to America/Chicago (docker/uo/docker-compose.yml:29,
//   docker-compose.test.yml:48). Chicago because that is where Chase and the players are: "December 1" means their
//   December 1, not UTC's, which starts six hours early.
//   On world load and then every hour, Sync compares the season with the state it last brought the world to (saved,
//   so a restart remembers it) and acts only when the season has changed since: up when December 1 comes, down after
//   January 6. So staff can put the decor up before December 1 on the test shard ([ClusterFWinterDecor up), or take
//   it down early, and the automatic check leaves that alone until the season next changes. A world first seen in
//   season is brought up; a world first seen out of season is left as it is.
//   clusterf.winterDecor.enabled (default true; modernuo.json) set to false keeps the automatic check off on a world.
//   The commands still work.
//
// What it places, and what it takes down
//   One WinterDecor per row of ClusterFWinterDecorLayout (generated from the checked placement table). WinterDecor is
//   a type of its own, created only here, so take-down deletes exactly those and nothing else: never a player's item,
//   never another seeder's, never pinned's own holiday items. Each knows its row's key, so up skips a row already
//   placed and places only what is missing: up twice places once, down twice removes once, up after down restores
//   the same set.
//
// [ClusterFWinterDecor status|up|down [dryrun]]

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModernUO.Serialization;
using Server.Commands;
using Server.Items;

namespace Server
{
    /// <summary>
    /// Our tiledata records for the winter art that animates or blocks (cc-P34). The values are records.json's: change
    /// the two together (player-package\tests\Build-UoOverrides.Tests.ps1 compares them). ShatteredLegacyArt.Apply
    /// writes them into the server's in-memory TileData.ItemTable, as it does the runestone's.
    /// </summary>
    public static class ClusterFWinterDecorArt
    {
        public sealed record Record(int Id, string Name, TileFlag Flags, int Weight, int Height);

        // The 47 IDs registered for the winter decor (player-package\vendor\shattered-legacy-art\registry.csv).
        public const int FirstId = 0x3C40;
        public const int LastId = 0x3C6E;

        public static readonly Record[] Records =
        {
            // OSI's statue 0x1224 is 0x4000, height 0; ours adds Animation and Impassable so the statue blocks.
            new(0x3C40, "father christmas", TileFlag.Animation | TileFlag.ArticleA | TileFlag.Impassable, 255, 20),
            // OSI's animated fire pit 0x29FD.
            new(0x3C45, "raised firepit", TileFlag.Animation | TileFlag.LightSource | TileFlag.ArticleA | TileFlag.NoShoot | TileFlag.Surface | TileFlag.Impassable, 255, 0),
            // OSI's animated oven 0x462 (height 35 there; 20 here, the stoves are smaller).
            new(0x3C4A, "iron stove", TileFlag.Animation | TileFlag.LightSource | TileFlag.ArticleAn | TileFlag.NoShoot | TileFlag.Impassable | TileFlag.Wall, 255, 20),
            new(0x3C4E, "stone stove", TileFlag.Animation | TileFlag.LightSource | TileFlag.ArticleAn | TileFlag.NoShoot | TileFlag.Impassable | TileFlag.Wall, 255, 20),
            // OSI's tree 0xCCA.
            new(0x3C52, "christmas tree", TileFlag.ArticleA | TileFlag.Impassable | TileFlag.Background, 255, 20),
            // OSI's fireplace 0x45D.
            new(0x3C60, "christmas fireplace", TileFlag.ArticleA | TileFlag.NoShoot | TileFlag.Impassable | TileFlag.Wall, 255, 20),
            // Ours.
            new(0x3C6E, "christmas sleigh", TileFlag.ArticleA | TileFlag.Impassable, 255, 10)
        };

        public static void Apply()
        {
            foreach (var r in Records)
            {
                TileData.ItemTable[r.Id] = new ItemData(r.Name, r.Flags, r.Weight, 0, 0, 0, 0, r.Height);
            }
        }
    }

    public static class ClusterFWinterDecor
    {
        public const string SettingKey = "clusterf.winterDecor.enabled";

        // The shard's local date. Tests replace it.
        public static Func<DateTime> Clock = () => Core.Now.ToLocalTime();

        public static Map Facet => Map.Trammel;

        private static bool _enabled = true;
        private static Timer _timer;

        public static void Configure()
        {
            _enabled = ServerConfiguration.GetOrUpdateSetting(SettingKey, true);
            EventSink.WorldLoad += OnWorldLoad;
            CommandSystem.Register("ClusterFWinterDecor", AccessLevel.Administrator, OnCommand);
        }

        private static void OnWorldLoad()
        {
            if (!_enabled)
            {
                return;
            }

            foreach (var line in Sync(Clock()))
            {
                Console.WriteLine(line);
            }

            _timer?.Stop();
            _timer = Timer.DelayCall(TimeSpan.FromHours(1), TimeSpan.FromHours(1), () =>
            {
                foreach (var line in Sync(Clock()))
                {
                    Console.WriteLine(line);
                }
            });
        }

        /// <summary>December 1 to January 6, both inclusive.</summary>
        public static bool IsInSeason(DateTime local) =>
            local.Month == 12 || local.Month == 1 && local.Day <= 6;

        /// <summary>
        /// The automatic check. Acts only when the season differs from the state it last brought the world to (saved),
        /// so a staff up or down in between stands until the season next changes. Returns what it did.
        /// </summary>
        public static List<string> Sync(DateTime local)
        {
            var lines = new List<string>();
            var season = IsInSeason(local);
            var last = ClusterFWinterDecorState.LastAutomatic;
            if (last == season || last == null && !season)
            {
                if (last == null)
                {
                    ClusterFWinterDecorState.LastAutomatic = season;
                }

                return lines;
            }

            lines.Add($"[WinterDecor] {local:yyyy-MM-dd}: {(season ? "the season has begun" : "the season is over")}.");
            lines.AddRange(season ? Up(false) : Down(false));
            ClusterFWinterDecorState.LastAutomatic = season;
            return lines;
        }

        /// <summary>Every item this seeder placed, on any map.</summary>
        public static List<WinterDecor> Placed() => World.Items.Values.OfType<WinterDecor>().Where(i => !i.Deleted).ToList();

        public static List<string> Up(bool dryRun)
        {
            var lines = new List<string>();
            var have = new HashSet<string>(Placed().Select(i => i.Key), StringComparer.Ordinal);
            var placed = 0;
            foreach (var row in ClusterFWinterDecorLayout.Rows)
            {
                if (have.Contains(row.Key))
                {
                    continue;
                }

                placed++;
                if (dryRun)
                {
                    lines.Add($"  would place {row.Key}: {row.Name} (0x{row.ItemId:X4}) at {row.X},{row.Y},{row.Z} ({row.Area})");
                    continue;
                }

                new WinterDecor(row).MoveToWorld(new Point3D(row.X, row.Y, row.Z), Facet);
            }

            lines.Add($"[WinterDecor] {(dryRun ? "dry run: would place" : "placed")} {placed} of {ClusterFWinterDecorLayout.Rows.Length}; {have.Count} already up.");
            return lines;
        }

        public static List<string> Down(bool dryRun)
        {
            var lines = new List<string>();
            var items = Placed();
            foreach (var item in items)
            {
                if (dryRun)
                {
                    lines.Add($"  would remove {item.Key}: {item.Name} at {item.Location} ({item.Map})");
                }
                else
                {
                    item.Delete();
                }
            }

            lines.Add($"[WinterDecor] {(dryRun ? "dry run: would remove" : "removed")} {items.Count} winter decor item(s); nothing else is touched.");
            return lines;
        }

        public static List<string> Status()
        {
            var now = Clock();
            var placed = Placed();
            var last = ClusterFWinterDecorState.LastAutomatic;
            return new List<string>
            {
                $"[WinterDecor] local date {now:yyyy-MM-dd HH:mm} ({TimeZoneInfo.Local.Id}): {(IsInSeason(now) ? "in season (December 1 to January 6)" : "out of season (up December 1 to January 6)")}.",
                $"[WinterDecor] up: {placed.Count} of {ClusterFWinterDecorLayout.Rows.Length} rows placed.",
                $"[WinterDecor] automatic check: {(_enabled ? "on" : $"off ({SettingKey} is false)")}; it last brought the world {(last == null ? "to nothing yet" : last.Value ? "up" : "down")}, and acts again only when the season changes."
            };
        }

        [Usage("ClusterFWinterDecor [status|up|down] [dryrun]")]
        [Description("The New Haven winter decor (December 1 to January 6). status says the date, the season and what is placed. up places the rows not already placed (allowed outside the dates, for testing; it says so). down removes every winter decor item and nothing else. dryrun lists and changes nothing.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Puts up (or takes down) New Haven's winter decor. It goes up by itself on December 1 and comes down after January 6; 'up' early is for testing. Down removes only the decor. Dry run: 'up dryrun' or 'down dryrun'.")]
        private static void OnCommand(CommandEventArgs e)
        {
            var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
            var dryRun = e.Arguments.Any(a => a.Equals("dryrun", StringComparison.OrdinalIgnoreCase));
            List<string> lines;
            switch (verb)
            {
                case "up":
                    lines = new List<string>();
                    if (!IsInSeason(Clock()))
                    {
                        lines.Add("[WinterDecor] Outside the dates (December 1 to January 6): putting it up anyway, for testing. It stays up until 'down', or until the season ends after January 6.");
                    }

                    lines.AddRange(Up(dryRun));
                    break;
                case "down":
                    lines = Down(dryRun);
                    break;
                case "status":
                case "dryrun":
                    lines = Status();
                    break;
                default:
                    e.Mobile.SendMessage("Usage: [ClusterFWinterDecor [status|up|down] [dryrun]");
                    return;
            }

            foreach (var line in lines)
            {
                e.Mobile.SendMessage(line);
            }
        }
    }

    /// <summary>
    /// The state the automatic check last brought the world to (cc-P34): null before it has run, then true (up) or
    /// false (down). Saved through pinned's GenericPersistence (Server/Serialization/GenericPersistence.cs), versioned.
    /// An unknown version fails loudly, as ClusterFStaffHubSpots does.
    /// </summary>
    public class ClusterFWinterDecorState : GenericPersistence
    {
        public const int CurrentVersion = 0;

        private static ClusterFWinterDecorState _instance;

        public static bool? LastAutomatic { get; set; }

        public ClusterFWinterDecorState() : base("WinterDecor", 10)
        {
        }

        public static void Configure()
        {
            _instance ??= new ClusterFWinterDecorState();
        }

        public override void Serialize(IGenericWriter writer) => WriteTo(writer);

        public override void Deserialize(IGenericReader reader) => ReadFrom(reader);

        // Static, so a test can round-trip the state without making a second persistence (a Persistence registers
        // itself when it is constructed, pinned Server/Serialization/Persistence.cs:29-33).
        public static void WriteTo(IGenericWriter writer)
        {
            writer.WriteEncodedInt(CurrentVersion);
            writer.Write((byte)(LastAutomatic == null ? 0 : LastAutomatic.Value ? 2 : 1));
        }

        public static void ReadFrom(IGenericReader reader)
        {
            var version = reader.ReadEncodedInt();
            if (version is < 0 or > CurrentVersion)
            {
                throw new InvalidDataException($"ClusterFWinterDecorState version {version} is not one this build reads (0 to {CurrentVersion}).");
            }

            var b = reader.ReadByte();
            LastAutomatic = b == 0 ? null : b == 2;
        }
    }
}

namespace Server.Items
{
    /// <summary>
    /// One piece of the New Haven winter decor (cc-P34): a row of ClusterFWinterDecorLayout, by its key. Placed and
    /// removed only by ClusterFWinterDecor; not constructible, so no [add makes one. Not movable, does not decay.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class WinterDecor : Item
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private string _key;

        public WinterDecor(ClusterFWinterDecorLayout.Row row) : base(row.ItemId)
        {
            _key = row.Key;
            Name = row.Name;
            Hue = row.Hue;
            Movable = false;
        }

        public override bool Decays => false;
    }
}
