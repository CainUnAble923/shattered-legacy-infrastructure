// cc-P368: shard music for chosen places and fights, with every stock track kept. Notes: shard-migration
// notes/cc-P368-added-music-areas-monsters-bosses.md.
//
// What it does. ShardMusicTable maps a track (ShardMusicTracks: client music IDs 105 and up, shipped in the player
// package) to a region, a sub-area, a creature type or a boss. Three layers, highest first: boss, monster, area.
//   Area    A mapped region's Music becomes its track (Region.Music, a settable property; its stock value is remembered),
//           so pinned's own region code plays it: on entering the region (Region.OnRegionChange, Server/Regions/
//           Region.cs:908-919 at d4531cd9) and at login (IncomingAccountPackets.cs:339). Child regions that were
//           inheriting the stock track follow. A sub-area is a ShardMusicRegion: a child of the region that holds its
//           centre, one priority above it so it wins Region.Find (Region.CompareTo), with the track as its music and
//           every other rule the parent's (each Region virtual asks Parent). Nothing else about a region changes.
//   Fight   When a mapped creature and a player start fighting (EventSink.AggressiveAction, raised from Mobile.DoHarmful
//           in both directions), that player hears the creature's track. A watch timer (one second) sees the fight end:
//           the creature dead, deleted or gone from the map, out of FightRange, or neither of the two naming the other as
//           Combatant. ReturnDelay later the area's music comes back (pm.Region.Music). A boss engage while a monster
//           plays switches at once; a monster engage during a live boss fight does not. Pets and summons never count.
//           Where the area has no music (MusicName.Invalid, the default region) the music from before the fight comes
//           back instead, or the music stops (cc-P456: Invalid goes out as 0x6D FF FF, which TazUO ignores, so the
//           fight track would loop on; it acts on the stop, 0x6D 1F FF).
//   Off     [ShardMusic off (per account, ClusterFAccountData FlagValues "music.shardOff") keeps stock music: no fight
//           music, and in a mapped region the stock track is sent right after pinned's send of ours (at login, and after
//           the first step into the region: EventSink.Movement is raised before the step, so the check runs on the next
//           tick). A recall or gate straight into a mapped region plays our track until that first step; recorded in the
//           notes as the one gap of the no-patch route.
//   clusterf.music.enabled turns everything off; clusterf.music.regionLayer, monsterLayer and bossLayer each layer;
//   returnSeconds and fightRange are the two numbers above. Settings are read at use, so a change needs no restart
//   (the region layer is applied at start: a change there needs one).
//
// What it does not do. No stock music ID is reassigned: a stock ID keeps its stock track, and the package only ADDS
// Config.txt lines (ShardMusicTracks). No patch: pinned Region, Mobile and PlayerMobile are untouched. The alternatives
// weighed (a PlayerMobile.OnRegionChange patch; wrapping a mapped stock region in a child region, which changes what
// m.Region is for four direct type checks in pinned and every name check) are in the notes.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Commands;
using Server.Mobiles;
using Server.Network;
using Server.Regions;

namespace Server.Music;

public enum ShardMusicLayer
{
    Area = 0,
    Monster = 1,
    Boss = 2
}

/// <summary>A sub-area with its own track: a child of the region that holds it, one priority above it, every other rule
/// the parent's.</summary>
public sealed class ShardMusicRegion : BaseRegion
{
    public ShardMusicRow Row { get; }

    public ShardMusicRegion(ShardMusicRow row, Map map, Region? parent, Rectangle3D area)
        : base(row.Target, map, parent, (parent?.Priority ?? DefaultPriority) + 1, area)
    {
        Row = row;
        Music = (MusicName)row.Track;
    }
}

public static class ClusterFShardMusic
{
    public const string EnabledKey = "clusterf.music.enabled";
    public const string RegionLayerKey = "clusterf.music.regionLayer";
    public const string MonsterLayerKey = "clusterf.music.monsterLayer";
    public const string BossLayerKey = "clusterf.music.bossLayer";
    public const string ReturnSecondsKey = "clusterf.music.returnSeconds";
    public const string FightRangeKey = "clusterf.music.fightRange";

    /// <summary>The per-account flag ([ShardMusic off): "1" when the player keeps stock music.</summary>
    public const string OffFlag = "music.shardOff";

    public static bool Enabled => ServerConfiguration.GetOrUpdateSetting(EnabledKey, true);
    public static bool RegionLayer => Enabled && ServerConfiguration.GetOrUpdateSetting(RegionLayerKey, true);
    public static bool MonsterLayer => Enabled && ServerConfiguration.GetOrUpdateSetting(MonsterLayerKey, true);
    public static bool BossLayer => Enabled && ServerConfiguration.GetOrUpdateSetting(BossLayerKey, true);
    public static TimeSpan ReturnDelay => TimeSpan.FromSeconds(ServerConfiguration.GetOrUpdateSetting(ReturnSecondsKey, 8));
    public static int FightRange => ServerConfiguration.GetOrUpdateSetting(FightRangeKey, 24);

    private static readonly TimeSpan WatchInterval = TimeSpan.FromSeconds(1);

    // ---------------------------------------------------------------- what Apply did
    private static readonly Dictionary<Region, MusicName> _stock = new();      // mapped region -> its stock music
    private static readonly List<ShardMusicRegion> _areas = new();
    private static readonly Dictionary<Type, ShardMusicRow> _creatures = new(); // Creature and Boss rows by exact type
    private static readonly List<string> _applyLog = new();
    private static bool _hooked;

    public static IReadOnlyDictionary<Region, MusicName> MappedRegions => _stock;
    public static IReadOnlyList<ShardMusicRegion> Areas => _areas;
    public static IReadOnlyDictionary<Type, ShardMusicRow> FightRows => _creatures;
    public static IReadOnlyList<string> ApplyLog => _applyLog;

    /// <summary>Raised for every music packet this system sends (the player, the ID, the layer). The tests listen.</summary>
    public static event Action<PlayerMobile, int, ShardMusicLayer>? Played;

    public static void Configure()
    {
        CommandSystem.Register("ShardMusic", AccessLevel.Player, OnShardMusicCommand);
        CommandSystem.Register("ShardMusicPlay", AccessLevel.GameMaster, OnShardMusicPlayCommand);
    }

    public static void Initialize()
    {
        Hook();
        Apply(ShardMusicTable.Rows);
        foreach (var line in _applyLog)
        {
            Console.WriteLine($"[ShardMusic] {line}");
        }
    }

    private static void Hook()
    {
        if (_hooked)
        {
            return;
        }

        _hooked = true;
        EventSink.AggressiveAction += OnAggressiveAction;
        EventSink.Movement += OnMovement;
        EventSink.Logout += OnLogout;
    }

    // ---------------------------------------------------------------- the table

    /// <summary>Applies rows on top of what is applied already. Returns what it found and what it could not.</summary>
    public static IReadOnlyList<string> Apply(IEnumerable<ShardMusicRow> rows)
    {
        var log = new List<string>();
        foreach (var row in rows)
        {
            if (!ShardMusicTracks.IsOurs(row.Track))
            {
                log.Add($"{row.Kind} \"{row.Target}\": track {row.Track} is not ours ({ShardMusicTracks.FirstId} to {ShardMusicTracks.LastId}); skipped");
                continue;
            }

            switch (row.Kind)
            {
                case ShardMusicKind.Region:
                    ApplyRegion(row, log);
                    break;
                case ShardMusicKind.Area:
                    ApplyArea(row, log);
                    break;
                case ShardMusicKind.Creature:
                case ShardMusicKind.Boss:
                    var type = AssemblyHandler.FindTypeByName(row.Target);
                    if (type == null)
                    {
                        log.Add($"{row.Kind} \"{row.Target}\": no such type; skipped");
                    }
                    else
                    {
                        if (_creatures.TryGetValue(type, out var had))
                        {
                            log.Add($"{row.Kind} \"{row.Target}\": replaces the {had.Kind} row (track {had.Track})");
                        }

                        _creatures[type] = row;
                        log.Add($"{row.Kind} {type.Name}: track {row.Track}");
                    }

                    break;
            }
        }

        _applyLog.AddRange(log);
        return log;
    }

    private static void ApplyRegion(ShardMusicRow row, List<string> log)
    {
        if (!RegionLayer)
        {
            log.Add($"Region \"{row.Target}\": the region layer is off ({RegionLayerKey}); not mapped");
            return;
        }

        var maps = new List<Map>();
        if (row.Map == null)
        {
            foreach (var map in Map.AllMaps)
            {
                if (map != null)
                {
                    maps.Add(map);
                }
            }
        }
        else if (ParseMap(row.Map) is { } map)
        {
            maps.Add(map);
        }
        else
        {
            log.Add($"Region \"{row.Target}\": no facet named \"{row.Map}\"; skipped");
            return;
        }

        var found = 0;
        foreach (var map in maps)
        {
            if (map == Map.Internal)
            {
                continue;
            }

            var region = Region.Find(row.Target, map, true);
            if (region == null)
            {
                continue;
            }

            found++;
            var stock = region.Music;
            var n = MapRegion(region, row.Track);
            log.Add($"Region \"{region.Name}\" ({map.Name}): {stock} -> track {row.Track}" + (n > 1 ? $", with {n - 1} child region(s)" : ""));
        }

        if (found == 0)
        {
            log.Add($"Region \"{row.Target}\": no region of that name" + (row.Map == null ? "" : $" on {row.Map}") + "; skipped");
        }
    }

    /// <summary>Sets the region's music, remembering the stock value, and follows into children that inherited it.
    /// Returns how many regions changed.</summary>
    private static int MapRegion(Region region, int track)
    {
        if (_stock.ContainsKey(region))
        {
            return 0;
        }

        var stock = region.Music;
        _stock[region] = stock;
        region.Music = (MusicName)track;
        var n = 1;
        foreach (var child in region.Children)
        {
            if (child.Music == stock)
            {
                n += MapRegion(child, track);
            }
        }

        return n;
    }

    private static void ApplyArea(ShardMusicRow row, List<string> log)
    {
        if (!RegionLayer)
        {
            log.Add($"Area \"{row.Target}\": the region layer is off ({RegionLayerKey}); not made");
            return;
        }

        if (row.Box is not { } box || row.Map == null)
        {
            log.Add($"Area \"{row.Target}\": needs a Map and a Box; skipped");
            return;
        }

        if (ParseMap(row.Map) is not { } map)
        {
            log.Add($"Area \"{row.Target}\": no facet named \"{row.Map}\"; skipped");
            return;
        }

        var area = new Rectangle3D(
            new Point3D(box.X, box.Y, Region.MinZ),
            new Point3D(box.X + box.Width, box.Y + box.Height, Region.MaxZ)
        );
        var centre = new Point3D(box.X + box.Width / 2, box.Y + box.Height / 2, 0);
        var parent = Region.Find(centre, map);
        if (parent == map.DefaultRegion)
        {
            parent = null;
        }

        var region = new ShardMusicRegion(row, map, parent, area);
        region.Register();
        _areas.Add(region);
        log.Add($"Area \"{row.Target}\" ({map.Name} {box.X},{box.Y} {box.Width}x{box.Height}) inside \"{parent?.Name ?? "(no region)"}\": track {row.Track}");
    }

    private static Map? ParseMap(string name)
    {
        foreach (var map in Map.AllMaps)
        {
            if (map != null && string.Equals(map.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return map;
            }
        }

        return null;
    }

    /// <summary>Undoes Apply: stock music back on every mapped region, the sub-areas unregistered, the fight rows and
    /// any running fight cleared. For the tests.</summary>
    public static void Reset()
    {
        foreach (var (region, stock) in _stock)
        {
            region.Music = stock;
        }

        _stock.Clear();

        foreach (var area in _areas)
        {
            area.Unregister();
        }

        _areas.Clear();
        _creatures.Clear();
        _applyLog.Clear();

        foreach (var fight in _fights.Values)
        {
            fight.Token.Cancel();
        }

        _fights.Clear();
        _lastRegion.Clear();
    }

    /// <summary>True for a region whose music is ours (a mapped region or a sub-area).</summary>
    public static bool IsMapped(Region? region) => region != null && (_stock.ContainsKey(region) || region is ShardMusicRegion);

    /// <summary>The music the region had before the table: what a player who turned shard music off hears there.</summary>
    public static MusicName StockMusic(Region? region)
    {
        if (region == null)
        {
            return MusicName.Invalid;
        }

        if (_stock.TryGetValue(region, out var stock))
        {
            return stock;
        }

        if (region is ShardMusicRegion area)
        {
            return StockMusic(area.Parent);
        }

        return region.Music;
    }

    /// <summary>The area music this player hears where they stand: the region's track, or its stock music when they
    /// turned shard music off.</summary>
    public static MusicName AreaMusic(PlayerMobile pm) => IsOff(pm) ? StockMusic(pm.Region) : pm.Region.Music;

    /// <summary>The music after a fight: the area's; where the area has none (Invalid), the music from before the fight;
    /// and when that was none too, the stop (NoMusic, 0x6D 1F FF). Never Invalid (cc-P456).</summary>
    public static MusicName ReturnMusic(PlayerMobile pm, MusicName before)
    {
        var area = AreaMusic(pm);
        if (area != MusicName.Invalid)
        {
            return area;
        }

        return before != MusicName.Invalid ? before : MusicName.NoMusic;
    }

    // ---------------------------------------------------------------- off switch

    private static readonly HashSet<PlayerMobile> _off = new();          // the flag, cached for online players
    private static readonly Dictionary<PlayerMobile, Region> _lastRegion = new(); // opted-out players: the last region seen

    public static bool IsOff(PlayerMobile pm) => _off.Contains(pm);

    public static bool ReadOff(PlayerMobile pm) =>
        pm.Account is IAccount acct && ClusterFAccountPersistence.Get(acct)?.GetFlagValue(OffFlag) == "1";

    /// <summary>Turns shard music off or on for the player's account, and plays what they should hear now.</summary>
    public static void SetOff(PlayerMobile pm, bool off)
    {
        if (pm.Account is IAccount acct)
        {
            ClusterFAccountPersistence.GetOrCreate(acct).SetFlagValue(OffFlag, off ? "1" : "0");
        }

        if (off)
        {
            _off.Add(pm);
            Drop(pm);
            _lastRegion[pm] = pm.Region;
        }
        else
        {
            _off.Remove(pm);
            _lastRegion.Remove(pm);
        }

        Send(pm, AreaMusic(pm), ShardMusicLayer.Area);
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        if (!ReadOff(pm))
        {
            _off.Remove(pm);
            return;
        }

        _off.Add(pm);
        _lastRegion[pm] = pm.Region;
        if (IsMapped(pm.Region))
        {
            Send(pm, StockMusic(pm.Region), ShardMusicLayer.Area); // after pinned's send of our track (DoLogin)
        }
    }

    private static void OnMovement(MovementEventArgs e)
    {
        if (e.Mobile is PlayerMobile pm && _off.Contains(pm))
        {
            Timer.StartTimer(() => AfterStep(pm)); // the event is raised before the step; look after it
        }
    }

    /// <summary>For a player who turned shard music off: after a step into a mapped region, the stock track again.</summary>
    public static void AfterStep(PlayerMobile pm)
    {
        if (pm.Deleted || !_off.Contains(pm))
        {
            return;
        }

        var region = pm.Region;
        _lastRegion.TryGetValue(pm, out var last);
        _lastRegion[pm] = region;
        if (last == region || !IsMapped(region))
        {
            return;
        }

        // Pinned sent our track only if the music changed between the two regions; the stock track is resent on the
        // same rule, so a step between two regions that share a stock track does not restart it.
        if (last != null && IsMapped(last) && StockMusic(last) == StockMusic(region))
        {
            return;
        }

        Send(pm, StockMusic(region), ShardMusicLayer.Area);
    }

    private static void OnLogout(Mobile m)
    {
        if (m is PlayerMobile pm)
        {
            Drop(pm);
            _off.Remove(pm);
            _lastRegion.Remove(pm);
        }
    }

    // ---------------------------------------------------------------- fights

    private sealed class Fight
    {
        public required BaseCreature Creature;
        public required ShardMusicRow Row;
        public MusicName Before; // the area music when the first fight of a run began (ReturnMusic)
        public DateTime? EndedAt;
        public TimerExecutionToken Token;
    }

    private static readonly Dictionary<PlayerMobile, Fight> _fights = new();

    public static ShardMusicLayer LayerOf(ShardMusicKind kind) =>
        kind == ShardMusicKind.Boss ? ShardMusicLayer.Boss : ShardMusicLayer.Monster;

    public static bool LayerOn(ShardMusicLayer layer) => layer switch
    {
        ShardMusicLayer.Boss => BossLayer,
        ShardMusicLayer.Monster => MonsterLayer,
        _ => RegionLayer
    };

    /// <summary>The Creature or Boss row for a creature, by its exact type; null for one not in the table, a pet or a summon.</summary>
    public static ShardMusicRow? FightRowFor(BaseCreature bc) =>
        !bc.Controlled && !bc.Summoned && _creatures.TryGetValue(bc.GetType(), out var row) ? row : null;

    /// <summary>What the player is fighting to music right now (null between fights), and when the watch saw it end
    /// (null while it is live).</summary>
    public static (BaseCreature Creature, ShardMusicLayer Layer, DateTime? EndedAt)? CurrentFight(PlayerMobile pm) =>
        _fights.TryGetValue(pm, out var f) ? (f.Creature, LayerOf(f.Row.Kind), f.EndedAt) : null;

    private static void OnAggressiveAction(AggressiveActionEventArgs e)
    {
        if (e.Aggressor is PlayerMobile pm && e.Aggressed is BaseCreature bc)
        {
            Engage(pm, bc, Core.Now);
        }
        else if (e.Aggressed is PlayerMobile pm2 && e.Aggressor is BaseCreature bc2)
        {
            Engage(pm2, bc2, Core.Now);
        }
    }

    /// <summary>A mapped creature and a player start fighting. Returns true when the player's music switched.</summary>
    public static bool Engage(PlayerMobile pm, BaseCreature bc, DateTime now) => Engage(pm, bc, now, null);

    private static bool Engage(PlayerMobile pm, BaseCreature bc, DateTime now, MusicName? before)
    {
        if (!Enabled || pm.Deleted || bc.Deleted || !bc.Alive || pm.NetState == null || IsOff(pm))
        {
            return false;
        }

        var row = FightRowFor(bc);
        if (row == null)
        {
            return false;
        }

        var layer = LayerOf(row.Kind);
        if (!LayerOn(layer))
        {
            return false;
        }

        if (_fights.TryGetValue(pm, out var fight))
        {
            if (fight.Creature == bc)
            {
                fight.EndedAt = null; // the same fight, resumed before the music returned
                return false;
            }

            if (fight.EndedAt == null && LayerOf(fight.Row.Kind) >= layer && IsLive(pm, fight.Creature))
            {
                return false; // boss over monster, and the first of equals, while it is live
            }

            fight.Token.Cancel();
            before ??= fight.Before;
        }

        fight = new Fight { Creature = bc, Row = row, Before = before ?? AreaMusic(pm) };
        _fights[pm] = fight;
        Timer.StartTimer(WatchInterval, WatchInterval, () => Check(pm, Core.Now), out fight.Token);
        Send(pm, (MusicName)row.Track, layer);
        return true;
    }

    /// <summary>True while the two are fighting: both alive on one map, within FightRange, and one naming the other.</summary>
    public static bool IsLive(PlayerMobile pm, BaseCreature bc) =>
        !pm.Deleted && !bc.Deleted && pm.Alive && bc.Alive && pm.Map != null && pm.Map != Map.Internal && bc.Map == pm.Map &&
        Utility.InRange(pm.Location, bc.Location, FightRange) && (pm.Combatant == bc || bc.Combatant == pm);

    /// <summary>The watch: once a second while a fight plays. Sees the fight end, then after ReturnDelay sends the area's
    /// music, unless another mapped creature took over.</summary>
    public static void Check(PlayerMobile pm, DateTime now)
    {
        if (!_fights.TryGetValue(pm, out var fight))
        {
            return;
        }

        if (pm.Deleted || pm.NetState == null)
        {
            Drop(pm);
            return;
        }

        if (fight.EndedAt == null)
        {
            if (IsLive(pm, fight.Creature))
            {
                return;
            }

            if (pm.Combatant is BaseCreature other && other != fight.Creature && FightRowFor(other) is { } next &&
                LayerOn(LayerOf(next.Kind)) && IsLive(pm, other))
            {
                Drop(pm);
                Engage(pm, other, now, fight.Before);
                return;
            }

            fight.EndedAt = now;
            return;
        }

        if (now - fight.EndedAt.Value < ReturnDelay)
        {
            return;
        }

        Drop(pm);
        Send(pm, ReturnMusic(pm, fight.Before), ShardMusicLayer.Area);
    }

    private static void Drop(PlayerMobile pm)
    {
        if (_fights.Remove(pm, out var fight))
        {
            fight.Token.Cancel();
        }
    }

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        foreach (var fight in _fights.Values)
        {
            if (fight.Creature == bc && fight.EndedAt == null)
            {
                fight.EndedAt = Core.Now;
            }
        }
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile pm) => Drop(pm); // the client plays its own death music

    private static void Send(PlayerMobile pm, MusicName music, ShardMusicLayer layer)
    {
        if (pm.NetState is { } ns)
        {
            ns.SendPlayMusic(music);
            Played?.Invoke(pm, (int)music, layer);
        }
    }

    // ---------------------------------------------------------------- commands

    [Usage("ShardMusic [on|off]")]
    [Description("Turns Shattered Legacy's own music on or off for your account (off keeps the stock Ultima Online music everywhere). With no word it switches.")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnShardMusicCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm)
        {
            return;
        }

        var off = e.Length >= 1 ? e.GetString(0).Equals("off", StringComparison.OrdinalIgnoreCase) : !IsOff(pm);
        if (e.Length >= 1 && !off && !e.GetString(0).Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            pm.SendMessage("Usage: [ShardMusic on, or [ShardMusic off.");
            return;
        }

        SetOff(pm, off);
        pm.SendMessage(off
            ? "Shard music is off: you hear the stock Ultima Online music everywhere. [ShardMusic on brings it back."
            : "Shard music is on.");
    }

    [Usage("ShardMusicPlay <id|stop|list>")]
    [Description("Staff: plays music ID <id> to you (a stock ID 0-104 or one of ours, 105 up), stops the music, or lists the shard music table and what applied at start.")]
    [ShardCommand(CommandCategory.Diagnostic, Shard = CommandShard.Any)]
    private static void OnShardMusicPlayCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm)
        {
            return;
        }

        var word = e.Length >= 1 ? e.GetString(0) : "list";
        if (word.Equals("stop", StringComparison.OrdinalIgnoreCase))
        {
            pm.NetState?.SendStopMusic();
            pm.SendMessage("Music stopped.");
            return;
        }

        if (int.TryParse(word, out var id))
        {
            if (id < 0 || id > ShardMusicTracks.LastId)
            {
                pm.SendMessage($"An ID is 0 to {ShardMusicTracks.LastId} (stock to {ShardMusicTracks.LastStockId}, ours from {ShardMusicTracks.FirstId}).");
                return;
            }

            Send(pm, (MusicName)id, ShardMusicLayer.Area);
            var track = ShardMusicTracks.Find(id);
            pm.SendMessage(track != null
                ? $"Playing {id}: {track.Title} ({track.File}.mp3, {track.Author})."
                : id <= ShardMusicTracks.LastStockId
                    ? $"Playing stock {id} ({(MusicName)id})."
                    : $"Playing {id}: no track of ours is shipped for it (silence in the client).");
            return;
        }

        foreach (var line in Describe())
        {
            pm.SendMessage(line);
        }
    }

    /// <summary>The table as applied, one line each, for the staff list.</summary>
    public static IEnumerable<string> Describe()
    {
        yield return $"Shard music: {(Enabled ? "on" : "OFF")}; layers region {(RegionLayer ? "on" : "off")}, monster {(MonsterLayer ? "on" : "off")}, boss {(BossLayer ? "on" : "off")}; return after {ReturnDelay.TotalSeconds:0}s; fight range {FightRange}.";
        yield return ShardMusicTracks.All.Length == 0
            ? $"Tracks: none shipped yet (IDs {ShardMusicTracks.FirstId} to {ShardMusicTracks.LastId} are ours)."
            : $"Tracks: {ShardMusicTracks.All.Length} (IDs {ShardMusicTracks.FirstId} to {ShardMusicTracks.LastId} are ours).";
        foreach (var t in ShardMusicTracks.All)
        {
            yield return $"  {t.Id} {t.File}{(t.Loop ? " (loop)" : "")}: {t.Title}, {t.Author}";
        }

        yield return ShardMusicTable.Rows.Length == 0 ? "Table: no rows yet." : $"Table: {ShardMusicTable.Rows.Length} row(s).";
        foreach (var line in _applyLog)
        {
            yield return "  " + line;
        }
    }
}
