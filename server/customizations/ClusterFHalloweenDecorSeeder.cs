// cc-P267: Halloween decorations at the shard's main banks, on a date switch. Stock art only (pumpkins, carved
// pumpkins, lanterns, candles, skulls, bones, cobwebs, hay, a cauldron, scarecrows, gravestones), so every client shows
// it without a package update. Notes in shard-migration notes/cc-P267-halloween-bank-decor.md.
//
// The switch (ClusterFSeasonalSwitch, shared): clusterf.seasonal.halloween.start and .end in modernuo.json, defaults
// 10-24 to 11-02, both inclusive, on the shard's local (America/Chicago) date; clusterf.seasonal.halloween.automatic
// (default true) keeps the hourly check on. The check acts only when the season changes: up when the window opens,
// down after it closes, and a staff place or clear in between stands until then.
//
// What it places, and what it takes down: one HalloweenDecor per row of ClusterFHalloweenDecorLayout (generated from
// the checked placement table: notes/cc-P267-tools). Every item placed is remembered BY SERIAL in ClusterFSeasonalState
// (saved with the world), and clear deletes exactly those: never a player's item, never another seeder's, never a
// same-art lookalike on the same tile. HalloweenDecor is a type only this seeder creates, so a stray one (a serial the
// save lost) is swept too and said so. Place skips a row whose key is already up: place twice places once, clear twice
// removes once, place after clear restores the same set.
//
// [ClusterFHalloweenDecor status | preview [bank|facet] | place [bank|facet] | clear [bank|facet] [dryrun]]
//   preview lists what place would do (a dry run). A bank is its key (britain, luna, new-haven, britain-fel ...) or a
//   facet name (Trammel, Felucca, Malas, Tokuno, TerMur). place works before the dates, for the test shard, and says so.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Commands;
using Server.Items;

namespace Server
{
    public static class ClusterFHalloweenDecor
    {
        public const string SeasonName = "halloween";
        public const string Label = "HalloweenDecor";
        public const string DefaultStart = "10-24";
        public const string DefaultEnd = "11-02";

        public static readonly ClusterFSeasonalSwitch Season =
            new(SeasonName, Label, DefaultStart, DefaultEnd, dry => Up(dry, null), dry => Down(dry, null));

        public static void Configure()
        {
            Season.Configure();
            CommandSystem.Register("ClusterFHalloweenDecor", AccessLevel.Administrator, OnCommand);
            // The same verbs on the server console (the test shard runs with a tty: docker attach sl-modernuo-test), so
            // the decor can be placed and cleared without a client. Console commands run on a background thread
            // (ConsoleInputHandler.RegisterCommand's note), so the work is posted to the game loop as pinned's
            // ConsoleCommands.cs does for "save".
            ConsoleInputHandler.RegisterCommand(
                ["halloween"],
                "Halloween bank decor: halloween status|preview|place|clear [bank|facet] [dryrun]",
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

        /// <summary>The command verbs, shared by the in-game command and the console command. args: [verb] [bank|facet] [dryrun].</summary>
        public static List<string> Run(string[] args)
        {
            var verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            var dryRun = verb == "preview" || args.Any(a => a.Equals("dryrun", StringComparison.OrdinalIgnoreCase));
            var filter = args.Skip(1).FirstOrDefault(a => !a.Equals("dryrun", StringComparison.OrdinalIgnoreCase));
            if (!KnownFilter(filter))
            {
                return new List<string> { $"No bank or facet called '{filter}'. Banks: {string.Join(", ", ClusterFHalloweenDecorLayout.Banks.Select(b => b.Key))}." };
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
                    return new List<string> { "Usage: ClusterFHalloweenDecor [status|preview|place|clear] [bank|facet] [dryrun]" };
            }
        }

        public static Map MapOf(string facet)
        {
            foreach (var m in Map.Maps)
            {
                if (m != null && m != Map.Internal && string.Equals(m.Name, facet, StringComparison.OrdinalIgnoreCase))
                {
                    return m;
                }
            }

            return null;
        }

        /// <summary>The rows of one bank key, of one facet, or all of them.</summary>
        public static IEnumerable<ClusterFHalloweenDecorLayout.Row> Rows(string filter) =>
            string.IsNullOrWhiteSpace(filter)
                ? ClusterFHalloweenDecorLayout.Rows
                : ClusterFHalloweenDecorLayout.Rows.Where(r => r.Bank.Equals(filter, StringComparison.OrdinalIgnoreCase) || r.Facet.Equals(filter, StringComparison.OrdinalIgnoreCase));

        public static bool KnownFilter(string filter) =>
            string.IsNullOrWhiteSpace(filter) || Rows(filter).Any();

        private static HashSet<Serial> Tracked => ClusterFSeasonalState.Placed(SeasonName);

        /// <summary>The items placed and remembered by serial, resolved; stale serials (no such item any more) are dropped.</summary>
        public static List<HalloweenDecor> Placed()
        {
            var list = new List<HalloweenDecor>();
            var stale = new List<Serial>();
            foreach (var serial in Tracked)
            {
                if (World.FindItem(serial) is HalloweenDecor { Deleted: false } item)
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

        /// <summary>HalloweenDecor items in the world that the saved serial list does not know (should be none).</summary>
        public static List<HalloweenDecor> Strays() =>
            World.Items.Values.OfType<HalloweenDecor>().Where(i => !i.Deleted && !Tracked.Contains(i.Serial)).ToList();

        public static List<string> Up(bool dryRun, string filter)
        {
            var lines = new List<string>();
            var have = new HashSet<string>(Placed().Concat(Strays()).Select(i => i.Key), StringComparer.Ordinal);
            var placed = 0;
            var skippedFacet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var byBank = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var rows = Rows(filter).ToList();
            foreach (var row in rows)
            {
                if (have.Contains(row.Key))
                {
                    continue;
                }

                var map = MapOf(row.Facet);
                if (map == null)
                {
                    skippedFacet.Add(row.Facet);
                    continue;
                }

                placed++;
                byBank[row.Bank] = byBank.GetValueOrDefault(row.Bank) + 1;
                if (dryRun)
                {
                    lines.Add($"  would place {row.Key}: {row.Name} (0x{row.ItemId:X4}) at {row.X},{row.Y},{row.Z} {row.Facet}");
                    continue;
                }

                var item = new HalloweenDecor(row);
                item.MoveToWorld(new Point3D(row.X, row.Y, row.Z), map);
                Tracked.Add(item.Serial);
            }

            lines.Add($"[{Label}] {(dryRun ? "dry run: would place" : "placed")} {placed} of {rows.Count} row(s){(string.IsNullOrWhiteSpace(filter) ? "" : $" for '{filter}'")}; {have.Count} already up" +
                      (skippedFacet.Count > 0 ? $"; rows on {string.Join(", ", skippedFacet)} skipped (facet not open)" : "") + ".");
            if (byBank.Count > 0)
            {
                lines.Add("  " + string.Join(", ", byBank.Select(kv => $"{kv.Key} {kv.Value}")));
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
                bool Match(HalloweenDecor i) => i.Bank.Equals(filter, StringComparison.OrdinalIgnoreCase) || i.Map?.Name.Equals(filter, StringComparison.OrdinalIgnoreCase) == true;
                items = items.Where(Match).ToList();
                strays = strays.Where(Match).ToList();
            }

            foreach (var item in items.Concat(strays))
            {
                if (dryRun)
                {
                    lines.Add($"  would remove {item.Key}: {item.Name} at {item.Location} ({item.Map}){(Tracked.Contains(item.Serial) ? "" : " [stray: not in the saved list]")}");
                }
                else
                {
                    Tracked.Remove(item.Serial);
                    item.Delete();
                }
            }

            lines.Add($"[{Label}] {(dryRun ? "dry run: would remove" : "removed")} {items.Count} tracked item(s) by serial" +
                      (strays.Count > 0 ? $" and {strays.Count} stray HalloweenDecor item(s) the saved list did not know" : "") +
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
                $"[{Label}] up: {placed.Count} of {ClusterFHalloweenDecorLayout.Rows.Length} rows placed (by serial){(strays.Count > 0 ? $", plus {strays.Count} stray item(s) not in the saved list" : "")}.",
                $"[{Label}] {Season.AutomaticText()}"
            };
            var byBank = placed.GroupBy(i => i.Bank).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}/{ClusterFHalloweenDecorLayout.Rows.Count(r => r.Bank == g.Key)}").ToList();
            if (byBank.Count > 0)
            {
                lines.Add("  " + string.Join(", ", byBank));
            }

            return lines;
        }

        [Usage("ClusterFHalloweenDecor [status|preview|place|clear] [bank|facet] [dryrun]")]
        [Description("The Halloween decor at the main banks (clusterf.seasonal.halloween.start to .end, default October 24 to November 2). status says the date, the season and what is placed. preview lists what place would do. place puts up the rows not already placed, all banks or one bank key or facet (allowed outside the dates, for testing; it says so). clear removes every item the seeder placed, by serial, and nothing else. dryrun lists and changes nothing.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "preview", Summary = "Puts up (or clears) the Halloween decor at the main banks. It goes up by itself on October 24 and comes down after November 2 (settings); 'place' early is for the test shard. clear removes only what it placed, by serial. Dry run: 'preview' or 'clear dryrun'.")]
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
    /// One piece of the Halloween bank decor (cc-P267): a row of ClusterFHalloweenDecorLayout, by its key and bank.
    /// Placed and removed only by ClusterFHalloweenDecor; not constructible, so no [add makes one. Not movable, does
    /// not decay.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class HalloweenDecor : Item
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private string _key;

        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private string _bank;

        public HalloweenDecor(ClusterFHalloweenDecorLayout.Row row) : base(row.ItemId)
        {
            _key = row.Key;
            _bank = row.Bank;
            Name = row.Name;
            Hue = row.Hue;
            Movable = false;
        }

        public override bool Decays => false;
    }
}
