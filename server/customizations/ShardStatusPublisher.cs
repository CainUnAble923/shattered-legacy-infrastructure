// ShardStatusPublisher: the shard writes who is in the world to status.json every 30 seconds.
//
// The contract (schema 1) and every consumer are in docker/uo-status/README.md. The file is
// PUBLIC: it carries a count, character names, a start time and a save time, and nothing else.
// No IPs, accounts, locations, skills or serials, ever.
//
// Liveness is the reader's job ("generatedAt older than three minutes means offline"), so the
// file is rewritten on every tick whether or not anything changed. Do not add a skip-if-unchanged
// optimisation: a healthy shard with a stale timestamp reads as a dead one.
//
// Every pinned member used here, with file and line at 7c9215d97, is cited in
// shard-migration/notes/cc-P6-status-publisher.md. In short:
//
//   who is connected    NetState.Instances            Server/Network/NetState/NetState.cs:47
//   in the world        NetState.Mobile is set only once a character is chosen
//                       (UOContent/Network/Packets/IncomingAccountPackets.cs:174, :261), so
//                       account login and character select have a null Mobile.
//   timer               Timer.StartTimer runs OnTick on the main loop with NO try/catch around
//                       it (Server/Timer/Timer.TimerWheel.cs:152), so an exception here would
//                       leave the event loop. Publish() catches everything for that reason.
//   last save           EventSink.WorldSave, raised on the main thread once the world has been
//                       serialized (Server/World/World.cs:308).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Server.Logging;
using Server.Network;

namespace Server.Misc;

public static class ShardStatusPublisher
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(ShardStatusPublisher));

    // WHO IS LISTED. Characters above this access level are left out of both count and names.
    // Raise it to AccessLevel.Owner to list staff as well.
    public const AccessLevel HighestListedAccessLevel = AccessLevel.Player;

    // Hidden characters are left out of both count and names unless this is true.
    public const bool ListHiddenCharacters = false;

    public const string ShardName = "Shattered Legacy";
    public const int MaxNames = 50;
    public const string DefaultPath = "/var/lib/uo/modernuo/status/status.json";

    // Optional override in Configuration/modernuo.json. Read with GetSetting, not
    // GetOrUpdateSetting, so the publisher never writes the default back into the file.
    public const string PathSetting = "shardStatus.path";

    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WarningEvery = TimeSpan.FromMinutes(10);

    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private static string _path = DefaultPath;
    private static DateTime _startedAt;
    private static DateTime? _lastSaveAt;
    private static bool _failing;
    private static DateTime _lastWarningAt = DateTime.MinValue;

    public static string OutputPath => _path;

    public static void Configure()
    {
        _path = ServerConfiguration.GetSetting(PathSetting, DefaultPath);

        // Core.Now is DateTime.UtcNow as of Main.cs:436 and Core.Uptime counts from the tick taken
        // on the next line (:437), both before Configure runs (:441). So this is the start time.
        _startedAt = Core.Now.AddMilliseconds(-Core.Uptime);

        EventSink.WorldSave += OnWorldSave;
    }

    public static void Initialize()
    {
        // Delay zero: the first file is written on the first turn of the event loop, so the site
        // recovers as soon as the shard is up rather than 30 seconds later.
        Timer.StartTimer(TimeSpan.Zero, Interval, Publish);
    }

    private static void OnWorldSave() => _lastSaveAt = DateTime.UtcNow;

    private static void Publish()
    {
        try
        {
            var (count, names) = Players(InWorld(NetState.Instances));
            var bytes = Render(DateTime.UtcNow, _startedAt, Core.Uptime / 1000, count, names, _lastSaveAt);
            WriteAtomically(_path, bytes);

            if (_failing)
            {
                _failing = false;
                logger.Information("Shard status: writing {Path} again", _path);
            }
        }
        catch (Exception e)
        {
            // Never rethrow: nothing wraps a timer callback, and this must not be able to hurt the
            // shard. One line, then quiet for ten minutes; the next tick simply tries again.
            _failing = true;

            var now = DateTime.UtcNow;
            if (now - _lastWarningAt >= WarningEvery)
            {
                _lastWarningAt = now;
                logger.Warning(
                    "Shard status: could not write {Path} ({Error}: {Message}). Retrying every tick, warning at most every 10 minutes.",
                    _path,
                    e.GetType().Name,
                    e.Message
                );
            }
        }
    }

    // The characters in the world behind a set of connections, one per character.
    internal static List<Mobile> InWorld(IEnumerable<NetState> states)
    {
        // P6 RED: no in-world test
        var l = new List<Mobile>(); foreach (var ns in states) { if (ns?.Mobile != null) l.Add(ns.Mobile); } return l;
    }

    internal static bool IsListed(Mobile m) =>
        m.AccessLevel <= HighestListedAccessLevel && (ListHiddenCharacters || !m.Hidden);

    // count is every listed character; names are sorted case-insensitively, deduplicated and capped.
    // Name, not RawName: a disguised character is published as the name other players see.
    internal static (int Count, List<string> Names) Players(IEnumerable<Mobile> inWorld)
    {
        // P6 RED: no filter, no sort, no dedup, no cap
        var n = new List<string>(); foreach (var m in inWorld) { n.Add(m.Name); } return (n.Count, n);
    }

    // Schema 1, exactly as docker/uo-status/README.md shows it. The default encoder escapes every
    // non-ASCII character, so the file is ASCII whatever a character is called.
    internal static byte[] Render(
        DateTime generatedAt, DateTime startedAt, long uptimeSeconds, int count, IReadOnlyList<string> names,
        DateTime? lastSaveAt
    )
    {
        // P6 RED: empty document
        return System.Text.Encoding.ASCII.GetBytes("{}");
    }

    private static string Timestamp(DateTime t) =>
        (t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : t).ToString(TimestampFormat, CultureInfo.InvariantCulture);

    // Write beside the target, then rename over it. rename(2) replaces the directory entry in one
    // step, so a reader (nginx) opens either the whole old file or the whole new one, never a
    // half-written one. The temp file is removed if anything fails before the rename.
    internal static void WriteAtomically(string path, byte[] bytes)
    {
        // P6 RED: rewrite in place
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes);
    }
}
