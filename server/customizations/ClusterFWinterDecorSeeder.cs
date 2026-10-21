// cc-P34 and cc-P266 (F-22): the dated winter decor for New Haven. OSI holiday art (pinned already has it, every client
// has the art) and community art by MrRiots and Vrark (our own art IDs 0x3C40-0x3C6E from P34 and 0x3388-0x33B6 from
// P266, shipped in the player package). Notes in shard-migration notes/cc-P34-winter-decor.md and
// notes/cc-P266-new-haven-winter-decor.md.
//
// The switch (ClusterFSeasonalSwitch, shared with the Halloween decor since cc-P267): clusterf.seasonal.winter.start
// and .end in modernuo.json, defaults 12-01 to 01-06, both inclusive, on the shard's local (America/Chicago) date;
// clusterf.seasonal.winter.automatic (default true) keeps the hourly check on. The check acts only when the season
// changes: up when December 1 comes, down after January 6, and a staff place or clear in between stands until then.
// (P34's own copy of the switch, with clusterf.winterDecor.enabled, is gone: P266 moved the decor onto the shared one.)
//
// What it places, and what it takes down: one item per row of ClusterFWinterDecorLayout (generated from the checked
// placement table: notes/cc-P266-tools): a WinterDecor for a decoration, a WinterAdventCalendar for the row whose layer
// is "calendar". Every item placed is remembered BY SERIAL in ClusterFSeasonalState (saved with the world), and clear
// deletes exactly those: never a player's item, never another seeder's, never a same-art lookalike on the same tile.
// Both types are made only by this seeder, so a stray one (a serial the save lost) is swept too and said so. Place
// skips a row whose key is already up: place twice places once, clear twice removes once, place after clear restores
// the same set.
//
// [ClusterFWinterDecor status | preview [area] | place [area] | clear [area] [dryrun]]   (up and down still work)
//   preview lists what place would do (a dry run). An area is a row's area (bank, square, main road, inn, entrance,
//   healer hall, field office, docks). place works before the dates, for the test shard, and says so. The same verbs
//   run on the server console as "winter ...".

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Commands;
using Server.Items;

namespace Server
{
    /// <summary>
    /// Our tiledata records for the winter art that animates, blocks or gives light (cc-P34, cc-P266). The values are
    /// records.json's: change the two together (player-package\tests\Build-UoOverrides.Tests.ps1 compares them).
    /// ShatteredLegacyArt.Apply writes them into the server's in-memory TileData.ItemTable, as it does the runestone's.
    /// Light is the light shape (the tiledata quality byte), 0 for no light.
    /// </summary>
    public static class ClusterFWinterDecorArt
    {
        public sealed record Record(int Id, string Name, TileFlag Flags, int Weight, int Height, int Light = 0);

        // P34's 47 IDs (player-package\vendor\shattered-legacy-art\registry.csv).
        public const int FirstId = 0x3C40;
        public const int LastId = 0x3C6E;

        // P266's 47 IDs, the second block.
        public const int FirstId2 = 0x3388;
        public const int LastId2 = 0x33B6;

        public static bool IsOurs(int id) => id is >= FirstId and <= LastId or >= FirstId2 and <= LastId2;

        public static readonly Record[] Records =
        {
            // cc-P34, 0x3C40-0x3C6E.
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
            new(0x3C6E, "christmas sleigh", TileFlag.ArticleA | TileFlag.Impassable, 255, 10),

            // cc-P266, 0x3388-0x33B6. Every value from a named OSI tile (notes/cc-P266-tools/records.py).
            // OSI's tree 0xCCA (Background | ArticleA | Impassable, height 20), Animation added: the lights twinkle.
            new(0x3388, "christmas tree", TileFlag.Animation | TileFlag.ArticleA | TileFlag.Impassable | TileFlag.Background, 255, 20),
            // The small tree: the same flags, height 10 as OSI's small banana tree 0xCAB.
            new(0x3393, "christmas tree", TileFlag.Animation | TileFlag.ArticleA | TileFlag.Impassable | TileFlag.Background, 255, 10),
            // OSI's tall candelabra 0xB26 (Animation | LightSource | ArticleA | Impassable | Unknown3, light 29, height 15).
            new(0x33A0, "candle stand", TileFlag.Animation | TileFlag.LightSource | TileFlag.ArticleA | TileFlag.Impassable | TileFlag.Unknown3, 255, 15, 29),
            // OSI's lit candle 0xB1A (Animation | LightSource | ArticleA, light 2, height 1): walk-over.
            new(0x33A4, "candle", TileFlag.Animation | TileFlag.LightSource | TileFlag.ArticleA, 255, 1, 2),
            // OSI's candelabra 0xB1D (Animation | LightSource | ArticleA | Impassable, light 1, height 5); ArticleAn.
            new(0x33A8, "antler candelabra", TileFlag.Animation | TileFlag.LightSource | TileFlag.ArticleAn | TileFlag.Impassable, 255, 5, 1),
            // OSI's holiday candle 0x236E (Animation | LightSource | ArticleA, light 2, height 3) without PartialHue.
            new(0x33AC, "advent candles", TileFlag.Animation | TileFlag.LightSource | TileFlag.ArticleA, 255, 3, 2),
            // OSI's potted tree 0x11C8 (ArticleA | Impassable, height 5).
            new(0x33B1, "potted tree", TileFlag.ArticleA | TileFlag.Impassable, 255, 5),
        };

        public static void Apply()
        {
            foreach (var r in Records)
            {
                // ItemData(name, flags, weight, quality, quantity, value, height): quality is the light shape.
                TileData.ItemTable[r.Id] = new ItemData(r.Name, r.Flags, r.Weight, r.Light, 0, 0, 0, r.Height);
            }
        }
    }

    /// <summary>A piece the winter seeder placed: a WinterDecor or the WinterAdventCalendar. Both know their row's key.</summary>
    public interface IWinterDecorItem
    {
        string Key { get; }
    }

    public static class ClusterFWinterDecor
    {
        public const string SeasonName = "winter";
        public const string Label = "WinterDecor";
        public const string DefaultStart = "12-01";
        public const string DefaultEnd = "01-06";
        public const string CalendarLayer = "calendar";

        public static readonly ClusterFSeasonalSwitch Season =
            new(SeasonName, Label, DefaultStart, DefaultEnd, dry => Up(dry, null), dry => Down(dry, null));

        public static Map Facet => Map.Trammel;

        public static void Configure()
        {
            Season.Configure();
            CommandSystem.Register("ClusterFWinterDecor", AccessLevel.Administrator, OnCommand);
            // The same verbs on the server console (the test shard runs with a tty: docker attach sl-modernuo-test), so
            // the decor can be placed and cleared without a client. Console commands run on a background thread
            // (ConsoleInputHandler.RegisterCommand's note), so the work is posted to the game loop as pinned's
            // ConsoleCommands.cs does for "save".
            ConsoleInputHandler.RegisterCommand(
                ["winter"],
                "New Haven winter decor: winter status|preview|place|clear [area] [dryrun]",
                args => Core.LoopContext.Post(() =>
                {
                    var parts = (args ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in Run(parts))
                    {
                        Console.WriteLine(line);
                    }
                })
            );
            // One line at every start, so a log (and nothing else) shows what the world holds.
            EventSink.WorldLoad += () =>
            {
                foreach (var line in Status())
                {
                    Console.WriteLine(line);
                }
            };
        }

        /// <summary>The command verbs, shared by the in-game command and the console command. args: [verb] [area] [dryrun].</summary>
        public static List<string> Run(string[] args)
        {
            var verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            var dryRun = verb == "preview" || args.Any(a => a.Equals("dryrun", StringComparison.OrdinalIgnoreCase));
            // An area may be two words ("main road", "healer hall", "field office"): everything after the verb but "dryrun".
            var filter = string.Join(' ', args.Skip(1).Where(a => !a.Equals("dryrun", StringComparison.OrdinalIgnoreCase)));
            if (!KnownFilter(filter))
            {
                return new List<string> { $"No area called '{filter}'. Areas: {string.Join(", ", Areas())}." };
            }

            switch (verb)
            {
                case "place":
                case "preview":
                case "up":
                    var lines = new List<string>();
                    if (!Season.IsInSeason(Season.Clock()) && !dryRun)
                    {
                        lines.Add($"[{Label}] Outside the dates ({Season.Window}): putting it up anyway, for testing. It stays up until 'clear', or until the season ends.");
                    }

                    lines.AddRange(Up(dryRun, filter));
                    return lines;
                case "clear":
                case "down":
                    return Down(dryRun, filter);
                case "status":
                case "dryrun":
                    return Status();
                default:
                    return new List<string> { "Usage: ClusterFWinterDecor [status|preview|place|clear] [area] [dryrun]" };
            }
        }

        public static IEnumerable<string> Areas() => ClusterFWinterDecorLayout.Rows.Select(r => r.Area).Distinct();

        /// <summary>The rows of one area, or all of them.</summary>
        public static IEnumerable<ClusterFWinterDecorLayout.Row> Rows(string filter) =>
            string.IsNullOrWhiteSpace(filter)
                ? ClusterFWinterDecorLayout.Rows
                : ClusterFWinterDecorLayout.Rows.Where(r => r.Area.Equals(filter, StringComparison.OrdinalIgnoreCase));

        public static bool KnownFilter(string filter) => string.IsNullOrWhiteSpace(filter) || Rows(filter).Any();

        private static HashSet<Serial> Tracked => ClusterFSeasonalState.Placed(SeasonName);

        private static string AreaOf(Item item) =>
            item is IWinterDecorItem w ? ClusterFWinterDecorLayout.Rows.FirstOrDefault(r => r.Key == w.Key)?.Area ?? "" : "";

        /// <summary>The items placed and remembered by serial, resolved; stale serials (no such item any more) are dropped.</summary>
        public static List<Item> Placed()
        {
            var list = new List<Item>();
            var stale = new List<Serial>();
            foreach (var serial in Tracked)
            {
                if (World.FindItem(serial) is { Deleted: false } item && item is IWinterDecorItem)
                {
                    list.Add(item);
                }
                else
                {
                    stale.Add(serial);
                }
            }

            foreach (var s in stale)
            {
                Tracked.Remove(s);
            }

            return list;
        }

        /// <summary>Winter items in the world that the saved serial list does not know (should be none).</summary>
        public static List<Item> Strays() =>
            World.Items.Values.Where(i => i is IWinterDecorItem && !i.Deleted && !Tracked.Contains(i.Serial)).ToList();

        public static Item Make(ClusterFWinterDecorLayout.Row row) =>
            row.Layer == CalendarLayer ? new WinterAdventCalendar(row) : new WinterDecor(row);

        public static List<string> Up(bool dryRun, string filter)
        {
            var lines = new List<string>();
            var have = new HashSet<string>(Placed().Concat(Strays()).Select(i => ((IWinterDecorItem)i).Key), StringComparer.Ordinal);
            var placed = 0;
            var byArea = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var rows = Rows(filter).ToList();
            foreach (var row in rows)
            {
                if (have.Contains(row.Key))
                {
                    continue;
                }

                placed++;
                byArea[row.Area] = byArea.GetValueOrDefault(row.Area) + 1;
                if (dryRun)
                {
                    lines.Add($"  would place {row.Key}: {row.Name} (0x{row.ItemId:X4}) at {row.X},{row.Y},{row.Z} ({row.Area})");
                    continue;
                }

                var item = Make(row);
                item.MoveToWorld(new Point3D(row.X, row.Y, row.Z), Facet);
                Tracked.Add(item.Serial);
            }

            lines.Add($"[{Label}] {(dryRun ? "dry run: would place" : "placed")} {placed} of {rows.Count} row(s){(string.IsNullOrWhiteSpace(filter) ? "" : $" for '{filter}'")}; {have.Count} already up.");
            if (byArea.Count > 0)
            {
                lines.Add("  " + string.Join(", ", byArea.Select(kv => $"{kv.Key} {kv.Value}")));
            }

            return lines;
        }

        public static List<string> Down(bool dryRun, string filter)
        {
            var lines = new List<string>();
            var items = Placed();
            var strays = Strays();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                bool Match(Item i) => AreaOf(i).Equals(filter, StringComparison.OrdinalIgnoreCase);
                items = items.Where(Match).ToList();
                strays = strays.Where(Match).ToList();
            }

            foreach (var item in items.Concat(strays))
            {
                if (dryRun)
                {
                    lines.Add($"  would remove {((IWinterDecorItem)item).Key}: {item.Name} at {item.Location} ({item.Map}){(Tracked.Contains(item.Serial) ? "" : " [stray: not in the saved list]")}");
                }
                else
                {
                    Tracked.Remove(item.Serial);
                    item.Delete();
                }
            }

            lines.Add($"[{Label}] {(dryRun ? "dry run: would remove" : "removed")} {items.Count} tracked item(s) by serial" +
                      (strays.Count > 0 ? $" and {strays.Count} stray winter item(s) the saved list did not know" : "") +
                      $"{(string.IsNullOrWhiteSpace(filter) ? "" : $" for '{filter}'")}; nothing else is touched.");
            return lines;
        }

        public static List<string> Status()
        {
            var now = Season.Clock();
            var placed = Placed();
            var strays = Strays();
            var lines = new List<string>
            {
                $"[{Label}] local date {now:yyyy-MM-dd HH:mm} ({TimeZoneInfo.Local.Id}): {Season.SeasonText(now)}.",
                $"[{Label}] up: {placed.Count} of {ClusterFWinterDecorLayout.Rows.Length} rows placed (by serial){(strays.Count > 0 ? $", plus {strays.Count} stray item(s) not in the saved list" : "")}.",
                $"[{Label}] {Season.AutomaticText()}"
            };
            var byArea = placed.GroupBy(AreaOf).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}/{ClusterFWinterDecorLayout.Rows.Count(r => r.Area == g.Key)}").ToList();
            if (byArea.Count > 0)
            {
                lines.Add("  " + string.Join(", ", byArea));
            }

            return lines;
        }

        [Usage("ClusterFWinterDecor [status|preview|place|clear] [area] [dryrun]")]
        [Description("The New Haven winter decor (clusterf.seasonal.winter.start to .end, default December 1 to January 6). status says the date, the season and what is placed. preview lists what place would do. place puts up the rows not already placed, all areas or one (bank, square, main road, inn, entrance, healer hall, field office, docks); allowed outside the dates, for testing, and it says so. clear removes every item the seeder placed, by serial, and nothing else. dryrun lists and changes nothing. up and down still mean place and clear.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "preview", Summary = "Puts up (or clears) New Haven's winter decor: the square, the bank, the main roads, the inn, the halls and the docks. It goes up by itself on December 1 and comes down after January 6 (settings); 'place' early is for the test shard. clear removes only what it placed, by serial. Dry run: 'preview' or 'clear dryrun'.")]
        private static void OnCommand(CommandEventArgs e)
        {
            foreach (var line in Run(e.Arguments))
            {
                e.Mobile.SendMessage(line);
            }
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
    public partial class WinterDecor : Item, IWinterDecorItem
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
