// cc-P267: the seasonal switch, built once so every dated decoration shares it (the Halloween bank decor here; P266's
// winter decor is meant to move onto it; P34's ClusterFWinterDecor keeps its own older copy of the same idea until
// then). Notes in shard-migration notes/cc-P267-halloween-bank-decor.md.
//
// One switch per season name. Its window comes from two settings in modernuo.json,
//     clusterf.seasonal.<name>.start   "MM-DD", the first day the decor is up (inclusive)
//     clusterf.seasonal.<name>.end     "MM-DD", the last day it stays up (inclusive); may lie in the next year
// written with their defaults on first start (GetOrUpdateSetting, pinned Server/Configuration/ServerConfiguration.cs:95),
// and clusterf.seasonal.<name>.automatic (default true) keeps or stops the automatic check; the staff commands work
// either way. A value that does not parse falls back to the default, with a line on the console.
//
// The clock is the shard's local date: Core.Now (UTC, pinned Server/Main.cs) in the container's time zone, which both
// compose files set to America/Chicago, because "October 24" means the players' October 24 (cc-P34's reasoning).
//
// On world load and then every hour, Sync compares "in season" with the state it last brought the world to (saved in
// ClusterFSeasonalState, so a restart remembers it) and acts only when that changed: up when the window opens, down
// after it closes. A staff "place" before the window, or a "clear" inside it, stands until the season next changes.
// A world first seen in season is brought up; a world first seen out of season is left as it is.
//
// What a decor placed is tracked BY SERIAL in the same persistence (ClusterFSeasonalState.Placed(name)): take-down
// deletes those items and nothing else, however many restarts lie between.
//
// cc-P408: a switch may borrow another season's dates (datesFrom): Treasures of Sorcerer's Dungeon runs on the
// Halloween settings (clusterf.seasonal.halloween.*) but keeps its own saved state under its own name, so a staff
// place or clear of the decor and a staff on or off of the event never undo each other.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Server
{
    /// <summary>A yearly window of days, start to end inclusive, by month and day. It may wrap the year end.</summary>
    public readonly record struct SeasonWindow(int StartMonth, int StartDay, int EndMonth, int EndDay)
    {
        public static bool TryParseDay(string text, out int month, out int day)
        {
            month = day = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var parts = text.Trim().Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out month) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out day))
            {
                return false;
            }

            // 2000 is a leap year, so 02-29 is a day.
            return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(2000, month);
        }

        public static bool TryParse(string start, string end, out SeasonWindow window)
        {
            window = default;
            if (!TryParseDay(start, out var sm, out var sd) || !TryParseDay(end, out var em, out var ed))
            {
                return false;
            }

            window = new SeasonWindow(sm, sd, em, ed);
            return true;
        }

        public static SeasonWindow Parse(string start, string end) =>
            TryParse(start, end, out var w) ? w : throw new FormatException($"season window '{start}' to '{end}': each is MM-DD");

        /// <summary>The day in the window? Start and end are both in; a window whose end is before its start wraps the year.</summary>
        public bool Contains(DateTime local)
        {
            var k = local.Month * 100 + local.Day;
            var a = StartMonth * 100 + StartDay;
            var b = EndMonth * 100 + EndDay;
            return a <= b ? k >= a && k <= b : k >= a || k <= b;
        }

        public string StartText => $"{StartMonth:00}-{StartDay:00}";
        public string EndText => $"{EndMonth:00}-{EndDay:00}";
        public override string ToString() => $"{StartText} to {EndText}";

        public static string DayName(int month, int day) =>
            new DateTime(2000, month, day).ToString("MMMM d", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// One named seasonal switch. The decor hands it its Up and Down (each takes dryRun and returns the lines it
    /// would print); the switch owns the dates, the clock, the hourly check and the saved state.
    /// </summary>
    public sealed class ClusterFSeasonalSwitch
    {
        public static readonly Dictionary<string, ClusterFSeasonalSwitch> All = new(StringComparer.OrdinalIgnoreCase);

        public string Name { get; }
        public string Label { get; }
        public string DefaultStart { get; }
        public string DefaultEnd { get; }

        /// <summary>The season whose settings give the dates: this switch's own name unless it borrows another's.</summary>
        public string DatesFrom { get; }

        public string StartKey => $"clusterf.seasonal.{DatesFrom}.start";
        public string EndKey => $"clusterf.seasonal.{DatesFrom}.end";
        public string AutomaticKey => $"clusterf.seasonal.{DatesFrom}.automatic";

        public SeasonWindow Window { get; set; }
        public bool Automatic { get; set; } = true;

        // The shard's local date. Tests replace it.
        public Func<DateTime> Clock = () => Core.Now.ToLocalTime();

        private readonly Func<bool, List<string>> _up;
        private readonly Func<bool, List<string>> _down;
        private Timer _timer;

        public ClusterFSeasonalSwitch(string name, string label, string defaultStart, string defaultEnd,
            Func<bool, List<string>> up, Func<bool, List<string>> down, string datesFrom = null)
        {
            Name = name;
            DatesFrom = datesFrom ?? name;
            Label = label;
            DefaultStart = defaultStart;
            DefaultEnd = defaultEnd;
            Window = SeasonWindow.Parse(defaultStart, defaultEnd);
            _up = up;
            _down = down;
            All[name] = this;
        }

        /// <summary>Reads the settings (writing the defaults on a first start) and hooks world load.</summary>
        public void Configure()
        {
            ReadSettings(ServerConfiguration.GetOrUpdateSetting(StartKey, DefaultStart), ServerConfiguration.GetOrUpdateSetting(EndKey, DefaultEnd));
            Automatic = ServerConfiguration.GetOrUpdateSetting(AutomaticKey, true);
            EventSink.WorldLoad += OnWorldLoad;
        }

        /// <summary>Applies two setting values; a bad one falls back to the default and says so. Returns the lines.</summary>
        public List<string> ReadSettings(string start, string end)
        {
            var lines = new List<string>();
            if (!SeasonWindow.TryParseDay(start, out _, out _))
            {
                lines.Add($"[{Label}] {StartKey} = '{start}' is not MM-DD; using the default {DefaultStart}.");
                start = DefaultStart;
            }

            if (!SeasonWindow.TryParseDay(end, out _, out _))
            {
                lines.Add($"[{Label}] {EndKey} = '{end}' is not MM-DD; using the default {DefaultEnd}.");
                end = DefaultEnd;
            }

            Window = SeasonWindow.Parse(start, end);
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }

            return lines;
        }

        private void OnWorldLoad()
        {
            if (!Automatic)
            {
                Console.WriteLine($"[{Label}] automatic check off ({AutomaticKey} is false); the commands still work.");
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

        public bool IsInSeason(DateTime local) => Window.Contains(local);

        /// <summary>The state the automatic check last brought the world to: null before it has run, true up, false down.</summary>
        public bool? LastAutomatic
        {
            get => ClusterFSeasonalState.GetLast(Name);
            set => ClusterFSeasonalState.SetLast(Name, value);
        }

        /// <summary>
        /// The automatic check. Acts only when the season differs from the state it last brought the world to, so a
        /// staff place or clear in between stands until the season next changes. Returns what it did.
        /// </summary>
        public List<string> Sync(DateTime local)
        {
            var lines = new List<string>();
            var season = IsInSeason(local);
            var last = LastAutomatic;
            if (last == season || last == null && !season)
            {
                if (last == null)
                {
                    LastAutomatic = season;
                }

                return lines;
            }

            lines.Add($"[{Label}] {local:yyyy-MM-dd}: {(season ? $"the season has begun ({Window})" : $"the season is over ({Window})")}.");
            lines.AddRange(season ? _up(false) : _down(false));
            LastAutomatic = season;
            return lines;
        }

        public string SeasonText(DateTime local) =>
            IsInSeason(local)
                ? $"in season ({SeasonWindow.DayName(Window.StartMonth, Window.StartDay)} to {SeasonWindow.DayName(Window.EndMonth, Window.EndDay)})"
                : $"out of season (up {SeasonWindow.DayName(Window.StartMonth, Window.StartDay)} to {SeasonWindow.DayName(Window.EndMonth, Window.EndDay)})";

        public string AutomaticText()
        {
            var last = LastAutomatic;
            return $"automatic check: {(Automatic ? "on" : $"off ({AutomaticKey} is false)")}; it last brought the world " +
                   $"{(last == null ? "to nothing yet" : last.Value ? "up" : "down")}, and acts again only when the season changes. " +
                   $"Dates: {StartKey} = {Window.StartText}, {EndKey} = {Window.EndText}.";
        }
    }

    /// <summary>
    /// The saved state of every seasonal switch (cc-P267): per season name, the state the automatic check last brought
    /// the world to, and the serials of the items the decor placed. Saved through pinned's GenericPersistence
    /// (Server/Serialization/GenericPersistence.cs), versioned; an unknown version fails loudly.
    /// </summary>
    public class ClusterFSeasonalState : GenericPersistence
    {
        public const int CurrentVersion = 0;

        private static ClusterFSeasonalState _instance;

        private static readonly Dictionary<string, bool?> _last = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, HashSet<Serial>> _placed = new(StringComparer.OrdinalIgnoreCase);

        public ClusterFSeasonalState() : base("SeasonalDecor", 10)
        {
        }

        public static void Configure()
        {
            _instance ??= new ClusterFSeasonalState();
        }

        public static bool? GetLast(string name) => _last.GetValueOrDefault(name);

        public static void SetLast(string name, bool? value) => _last[name] = value;

        /// <summary>The serials the named decor has placed and not yet removed. The set itself: add and remove on it.</summary>
        public static HashSet<Serial> Placed(string name)
        {
            if (!_placed.TryGetValue(name, out var set))
            {
                _placed[name] = set = new HashSet<Serial>();
            }

            return set;
        }

        public static void Clear()
        {
            _last.Clear();
            _placed.Clear();
        }

        public override void Serialize(IGenericWriter writer) => WriteTo(writer);

        public override void Deserialize(IGenericReader reader) => ReadFrom(reader);

        // Static, so a test can round-trip the state without making a second persistence (a Persistence registers
        // itself when it is constructed, pinned Server/Serialization/Persistence.cs).
        public static void WriteTo(IGenericWriter writer)
        {
            writer.WriteEncodedInt(CurrentVersion);
            var names = new HashSet<string>(_last.Keys, StringComparer.OrdinalIgnoreCase);
            names.UnionWith(_placed.Keys);
            writer.WriteEncodedInt(names.Count);
            foreach (var name in names)
            {
                writer.Write(name);
                var last = GetLast(name);
                writer.Write((byte)(last == null ? 0 : last.Value ? 2 : 1));
                var placed = Placed(name);
                writer.WriteEncodedInt(placed.Count);
                foreach (var serial in placed)
                {
                    writer.Write(serial);
                }
            }
        }

        public static void ReadFrom(IGenericReader reader)
        {
            var version = reader.ReadEncodedInt();
            if (version is < 0 or > CurrentVersion)
            {
                throw new InvalidDataException($"ClusterFSeasonalState version {version} is not one this build reads (0 to {CurrentVersion}).");
            }

            Clear();
            var count = reader.ReadEncodedInt();
            for (var i = 0; i < count; i++)
            {
                var name = reader.ReadString();
                var b = reader.ReadByte();
                _last[name] = b == 0 ? null : b == 2;
                var placed = Placed(name);
                var n = reader.ReadEncodedInt();
                for (var k = 0; k < n; k++)
                {
                    placed.Add(reader.ReadSerial());
                }
            }
        }
    }
}
