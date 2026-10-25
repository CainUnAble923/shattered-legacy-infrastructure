// ShardMusicVerification.cs
//
// cc-P368: shard music for chosen places and fights (server/customizations/Music/). Notes: shard-migration
// notes/cc-P368-added-music-areas-monsters-bosses.md.
//
// Facts:
//   1. A player entering a mapped region gets the region track: the play-music packet (0x6D, the ID) is on the wire from
//      pinned's own region code, a sub-area inside it plays its own track, and leaving the box plays the region's again.
//   2. A boss engage switches the player to the boss track; a monster engage during a live boss fight does not; the
//      music returns to the area's track after the fight (the creature dead, the return delay passed), and not before.
//   3. Turning a layer off stops it: the boss layer off ignores a boss, the monster layer off a monster, the region layer
//      off maps no region, and the whole system off plays nothing.
//   4. No stock music ID is changed: every MusicName is below the first ID of ours, every shipped track and every table
//      row is inside our range, an unmapped region keeps its music, and Reset puts a mapped region's stock music back.
//   5. A player who turns shard music off keeps stock music: no fight music, the stock track after a step into a mapped
//      region, and [ShardMusic on brings the shard track back.
//   6. cc-P456: a fight that ends where the area has no music (the default region, MusicName.Invalid) never sends
//      0x6D FF FF, which TazUO ignores so the fight track loops on: it sends the music from before the fight, or, when
//      there was none, the stop (0x6D 1F FF).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using Server;
using Server.Mobiles;
using Server.Music;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ShardMusicVerification : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly List<(PlayerMobile Pm, int Id, ShardMusicLayer Layer)> _played = new();
    private readonly List<IDisposable> _disposables = new();
    private readonly List<Region> _regions = new();
    private readonly Dictionary<string, bool> _keptSettings = new();

    // An empty corner of Trammel, as the other region facts use (nothing in pinned regions.json near x 5136+ but ours,
    // and the Burrows are at y 1128-1263; this sits well south of them).
    private const int X0 = 5150, Y0 = 1600;
    private static readonly Point3D Outside = new(X0 - 20, Y0 + 10, 0);
    private static readonly Point3D InRegion = new(X0 + 5, Y0 + 5, 0);
    private static readonly Point3D InBox = new(X0 + 25, Y0 + 25, 0);

    public ShardMusicVerification(ITestOutputHelper output)
    {
        _out = output;
        ClusterFShardMusic.Initialize(); // the host runs no Initialize: hooks EventSink once (the table is empty)
        ClusterFShardMusic.Reset();
        ClusterFShardMusic.Played += OnPlayed;
    }

    private void OnPlayed(PlayerMobile pm, int id, ShardMusicLayer layer)
    {
        _played.Add((pm, id, layer));
        _out.WriteLine($"played {id} ({layer}) to {pm.Serial}");
    }

    public void Dispose()
    {
        ClusterFShardMusic.Played -= OnPlayed;
        ClusterFShardMusic.Reset();
        foreach (var r in _regions)
        {
            r.Unregister();
        }

        foreach (var d in _disposables)
        {
            d.Dispose();
        }

        foreach (var (key, value) in _keptSettings)
        {
            ServerConfiguration.SetSetting(key, value);
        }
    }

    private void Setting(string key, bool value)
    {
        if (!_keptSettings.ContainsKey(key))
        {
            _keptSettings[key] = ServerConfiguration.GetOrUpdateSetting(key, true);
        }

        ServerConfiguration.SetSetting(key, value);
    }

    /// <summary>A stock-shaped region (a plain Region with a stock track, as regions.json makes) over a 40x40 square.</summary>
    private Region TownLike(string name, MusicName music = MusicName.Britain1)
    {
        var r = new Region(name, Map.Trammel, Region.DefaultPriority,
            new Rectangle3D(new Point3D(X0, Y0, Region.MinZ), new Point3D(X0 + 40, Y0 + 40, Region.MaxZ)))
        {
            Music = music
        };
        r.Register();
        _regions.Add(r);
        return r;
    }

    private (PlayerMobile Pm, Socket Client) Player(Point3D at)
    {
        var ns = PacketTestUtilities.CreateTestNetState(out var client);
        var pm = new PlayerMobile { Player = true };
        // Placed first, then connected: a map change with a client attached sends the login-shaped packets, which want
        // an account (SendSupportedFeature). Every later move in these facts stays on Trammel.
        pm.MoveToWorld(at, Map.Trammel);
        pm.NetState = ns;
        ns.Mobile = pm;
        _disposables.Add(new Cleanup(() =>
        {
            pm.NetState = null;
            ns.Mobile = null;
            ns.Dispose();
            client.Close();
            pm.Delete();
        }));
        return (pm, client);
    }

    private sealed class Cleanup(Action a) : IDisposable
    {
        public void Dispose() => a();
    }

    /// <summary>Everything the server delivered to the client so far.</summary>
    private static byte[] Wire(Socket client)
    {
        var bytes = new List<byte>();
        var buffer = new byte[8192];
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 1500)
        {
            NetState.Slice();
            if (client.Poll(100_000, SelectMode.SelectRead))
            {
                var read = client.Receive(buffer);
                if (read <= 0)
                {
                    break;
                }

                bytes.AddRange(buffer.Take(read));
            }
            else if (bytes.Count > 0)
            {
                break;
            }
        }

        return bytes.ToArray();
    }

    /// <summary>The music IDs of every play-music packet (0x6D hi lo) in the bytes, in order. A 3-byte packet with no
    /// length field; the search accepts any position, so the IDs looked for are ones no other packet here carries.</summary>
    private static List<int> MusicOnTheWire(byte[] bytes)
    {
        var ids = new List<int>();
        for (var i = 0; i + 2 < bytes.Length; i++)
        {
            if (bytes[i] == 0x6D)
            {
                ids.Add(bytes[i + 1] << 8 | bytes[i + 2]);
            }
        }

        return ids;
    }

    private static BaseCreature Spawn<T>(Point3D at) where T : BaseCreature, new()
    {
        var bc = new T();
        bc.MoveToWorld(at, Map.Trammel);
        return bc;
    }

    [Fact]
    public void APlayerEnteringAMappedRegionGetsTheRegionTrackAndASubAreaItsOwn()
    {
        var town = TownLike("P368 Town");
        var log = ClusterFShardMusic.Apply(new ShardMusicRow[]
        {
            new(ShardMusicKind.Region, "P368 Town", 105, "Trammel"),
            new(ShardMusicKind.Area, "P368 Square", 106, "Trammel", new Rectangle2D(X0 + 20, Y0 + 20, 10, 10))
        });
        foreach (var line in log)
        {
            _out.WriteLine(line);
        }

        Assert.Equal((MusicName)105, town.Music);
        Assert.Equal(MusicName.Britain1, ClusterFShardMusic.StockMusic(town));
        var square = Assert.Single(ClusterFShardMusic.Areas);
        Assert.Same(town, square.Parent);
        Assert.Equal(town.Priority + 1, square.Priority);
        Assert.Equal((MusicName)106, square.Music);
        Assert.Same(square, Region.Find(InBox, Map.Trammel));
        Assert.Same(town, Region.Find(InRegion, Map.Trammel));

        var (pm, client) = Player(Outside);
        Wire(client); // anything sent so far, skipped

        pm.MoveToWorld(InRegion, Map.Trammel);
        Assert.Same(town, pm.Region);
        Assert.Contains(105, MusicOnTheWire(Wire(client)));

        pm.MoveToWorld(InBox, Map.Trammel);
        Assert.Same(square, pm.Region);
        Assert.Contains(106, MusicOnTheWire(Wire(client)));

        pm.MoveToWorld(InRegion, Map.Trammel);
        Assert.Contains(105, MusicOnTheWire(Wire(client)));

        Assert.Empty(_played); // all of it pinned's own sends; nothing from the fight or off-switch paths
    }

    [Fact]
    public void ABossEngageSwitchesThePlayerToTheBossTrackAndTheMusicReturnsAfterTheFight()
    {
        TownLike("P368 Town");
        ClusterFShardMusic.Apply(new ShardMusicRow[]
        {
            new(ShardMusicKind.Region, "P368 Town", 105, "Trammel"),
            new(ShardMusicKind.Creature, nameof(Orc), 107),
            new(ShardMusicKind.Boss, nameof(Dragon), 108)
        });
        Assert.Equal(2, ClusterFShardMusic.FightRows.Count);

        var (pm, _) = Player(InRegion);
        var orc = Spawn<Orc>(new Point3D(X0 + 6, Y0 + 5, 0));
        var dragon = Spawn<Dragon>(new Point3D(X0 + 7, Y0 + 5, 0));
        var now = DateTime.UtcNow;
        try
        {
            // The monster, through the real path: the creature names the player as Combatant, which calls DoHarmful,
            // which raises EventSink.AggressiveAction.
            orc.Combatant = pm;
            Assert.Equal((pm, 107, ShardMusicLayer.Monster), _played.Last());
            Assert.Equal((orc, ShardMusicLayer.Monster, (DateTime?)null), ClusterFShardMusic.CurrentFight(pm));

            // The boss over the monster.
            Assert.True(ClusterFShardMusic.Engage(pm, dragon, now));
            Assert.Equal((pm, 108, ShardMusicLayer.Boss), _played.Last());
            dragon.Combatant = pm;
            Assert.Equal((dragon, ShardMusicLayer.Boss, (DateTime?)null), ClusterFShardMusic.CurrentFight(pm));

            // A monster engage during a live boss fight changes nothing.
            var count = _played.Count;
            Assert.False(ClusterFShardMusic.Engage(pm, orc, now));
            Assert.Equal(count, _played.Count);

            // Still fighting: the watch sees a live fight.
            ClusterFShardMusic.Check(pm, now + TimeSpan.FromSeconds(30));
            Assert.Equal(count, _played.Count);
            Assert.Equal((dragon, ShardMusicLayer.Boss, (DateTime?)null), ClusterFShardMusic.CurrentFight(pm));

            // The orc leaves the fight, the dragon dies: the fight has ended (the death event marks it at once, the watch
            // would a second later), and the music waits the return delay.
            orc.Combatant = null;
            pm.Combatant = null;
            orc.Delete();
            dragon.Kill();
            Assert.False(dragon.Alive);
            var fight = ClusterFShardMusic.CurrentFight(pm);
            Assert.NotNull(fight);
            Assert.Same(dragon, fight.Value.Creature);
            Assert.NotNull(fight.Value.EndedAt); // the death event marked the end (Core.Now)
            var ended = fight.Value.EndedAt.Value;
            ClusterFShardMusic.Check(pm, ended + TimeSpan.FromSeconds(1));
            Assert.Equal(count, _played.Count);
            Assert.Equal(ended, ClusterFShardMusic.CurrentFight(pm)?.EndedAt); // the watch keeps the first end time

            ClusterFShardMusic.Check(pm, ended + ClusterFShardMusic.ReturnDelay - TimeSpan.FromSeconds(1));
            Assert.Equal(count, _played.Count);
            Assert.NotNull(ClusterFShardMusic.CurrentFight(pm));

            ClusterFShardMusic.Check(pm, ended + ClusterFShardMusic.ReturnDelay);
            Assert.Equal((pm, 105, ShardMusicLayer.Area), _played.Last());
            Assert.Null(ClusterFShardMusic.CurrentFight(pm));
        }
        finally
        {
            orc.Delete();
            dragon.Delete();
        }
    }

    [Fact]
    public void AFightThatEndsInTheDefaultRegionStopsTheFightMusic()
    {
        ClusterFShardMusic.Apply(new ShardMusicRow[] { new(ShardMusicKind.Creature, nameof(Orc), 107) });
        var (pm, client) = Player(Outside);
        Assert.Equal(MusicName.Invalid, pm.Region.Music); // the default region: no area music
        var orc = Spawn<Orc>(new Point3D(Outside.X + 1, Outside.Y, 0));
        try
        {
            orc.Combatant = pm;
            Assert.Equal((pm, 107, ShardMusicLayer.Monster), _played.Last());
            pm.Combatant = null;
            orc.Kill();
            var ended = ClusterFShardMusic.CurrentFight(pm)?.EndedAt;
            Assert.NotNull(ended);

            ClusterFShardMusic.Check(pm, ended.Value + ClusterFShardMusic.ReturnDelay);
            Assert.Null(ClusterFShardMusic.CurrentFight(pm));
            Assert.Equal((pm, (int)MusicName.NoMusic, ShardMusicLayer.Area), _played.Last());
            var wire = MusicOnTheWire(Wire(client));
            Assert.Contains(0x1FFF, wire); // 0x6D 1F FF: the stop, which TazUO acts on
            Assert.DoesNotContain(0xFFFF, wire);
        }
        finally
        {
            orc.Delete();
        }
    }

    [Fact]
    public void AFightThatMovesIntoTheDefaultRegionReturnsToTheMusicFromBeforeIt()
    {
        TownLike("P368 Town");
        ClusterFShardMusic.Apply(new ShardMusicRow[]
        {
            new(ShardMusicKind.Region, "P368 Town", 105, "Trammel"),
            new(ShardMusicKind.Creature, nameof(Orc), 107),
            new(ShardMusicKind.Boss, nameof(Dragon), 108)
        });
        var (pm, _) = Player(InRegion);
        var orc = Spawn<Orc>(new Point3D(X0 + 6, Y0 + 5, 0));
        var dragon = Spawn<Dragon>(new Point3D(X0 + 7, Y0 + 5, 0));
        var now = DateTime.UtcNow;
        try
        {
            orc.Combatant = pm;
            Assert.True(ClusterFShardMusic.Engage(pm, dragon, now)); // the boss takes over: still the town from before
            Assert.Equal((pm, 108, ShardMusicLayer.Boss), _played.Last());

            // The fight drifts out of town into the default region and ends there.
            pm.MoveToWorld(Outside, Map.Trammel);
            Assert.Equal(MusicName.Invalid, pm.Region.Music);
            orc.Combatant = null;
            dragon.Combatant = null;
            pm.Combatant = null;
            orc.Delete();
            dragon.Kill();
            var ended = ClusterFShardMusic.CurrentFight(pm)?.EndedAt;
            Assert.NotNull(ended);
            var count = _played.Count;

            ClusterFShardMusic.Check(pm, ended.Value + ClusterFShardMusic.ReturnDelay);
            Assert.Equal(count + 1, _played.Count);
            Assert.Equal((pm, 105, ShardMusicLayer.Area), _played.Last());
        }
        finally
        {
            orc.Delete();
            dragon.Delete();
        }
    }

    [Fact]
    public void TurningALayerOffStopsIt()
    {
        var town = TownLike("P368 Town");
        var rows = new ShardMusicRow[]
        {
            new(ShardMusicKind.Region, "P368 Town", 105, "Trammel"),
            new(ShardMusicKind.Creature, nameof(Orc), 107),
            new(ShardMusicKind.Boss, nameof(Dragon), 108)
        };
        var (pm, _) = Player(InRegion);
        var orc = Spawn<Orc>(new Point3D(X0 + 6, Y0 + 5, 0));
        var dragon = Spawn<Dragon>(new Point3D(X0 + 7, Y0 + 5, 0));
        var now = DateTime.UtcNow;
        try
        {
            ClusterFShardMusic.Apply(rows);
            Assert.Equal((MusicName)105, town.Music);

            Setting(ClusterFShardMusic.BossLayerKey, false);
            Assert.False(ClusterFShardMusic.Engage(pm, dragon, now));
            Assert.True(ClusterFShardMusic.Engage(pm, orc, now));
            Assert.Equal((pm, 107, ShardMusicLayer.Monster), _played.Last());
            ClusterFShardMusic.Reset();

            ClusterFShardMusic.Apply(rows);
            Setting(ClusterFShardMusic.BossLayerKey, true);
            Setting(ClusterFShardMusic.MonsterLayerKey, false);
            Assert.False(ClusterFShardMusic.Engage(pm, orc, now));
            Assert.True(ClusterFShardMusic.Engage(pm, dragon, now));
            Assert.Equal((pm, 108, ShardMusicLayer.Boss), _played.Last());
            ClusterFShardMusic.Reset();

            // The region layer off: the table's region rows are not applied (the region keeps its stock music).
            Setting(ClusterFShardMusic.MonsterLayerKey, true);
            Setting(ClusterFShardMusic.RegionLayerKey, false);
            var log = ClusterFShardMusic.Apply(rows);
            Assert.Equal(MusicName.Britain1, town.Music);
            Assert.Empty(ClusterFShardMusic.MappedRegions);
            Assert.Contains(log, l => l.Contains("region layer is off"));
            Assert.True(ClusterFShardMusic.Engage(pm, dragon, now)); // the other layers still work
            ClusterFShardMusic.Reset();

            // Everything off.
            Setting(ClusterFShardMusic.RegionLayerKey, true);
            Setting(ClusterFShardMusic.EnabledKey, false);
            var before = _played.Count;
            ClusterFShardMusic.Apply(rows);
            Assert.Equal(MusicName.Britain1, town.Music);
            Assert.False(ClusterFShardMusic.Engage(pm, dragon, now));
            Assert.False(ClusterFShardMusic.Engage(pm, orc, now));
            Assert.Equal(before, _played.Count);
        }
        finally
        {
            orc.Delete();
            dragon.Delete();
        }
    }

    [Fact]
    public void NoStockMusicIdIsChanged()
    {
        // The stock list: 0 to 104 in the client's Config.txt; pinned's MusicName stops at 102 (TheVesperMist: it has no
        // name for 103 Townlife and 104 DungeonOutpost). Every one is below the first ID of ours.
        var stock = Enum.GetValues<MusicName>().Where(m => m != MusicName.Invalid && m != MusicName.NoMusic).Select(m => (int)m).ToList();
        Assert.Equal(0, stock.Min());
        Assert.Equal((int)MusicName.TheVesperMist, stock.Max());
        Assert.True(stock.Max() <= ShardMusicTracks.LastStockId, $"MusicName reaches {stock.Max()}, past the stock Config.txt's {ShardMusicTracks.LastStockId}");
        Assert.All(stock, id => Assert.True(id < ShardMusicTracks.FirstId, $"stock {id} is not below {ShardMusicTracks.FirstId}"));
        Assert.True(ShardMusicTracks.FirstId <= ShardMusicTracks.LastId);
        Assert.Equal(149, ShardMusicTracks.LastId); // TazUO's MAX_MUSIC_DATA_INDEX_COUNT (150) less one, at pin 3212623f

        // Every shipped track is ours, once, and every table row names a shipped track.
        Assert.All(ShardMusicTracks.All, t => Assert.True(ShardMusicTracks.IsOurs(t.Id), $"track {t.Id} is outside our range"));
        Assert.Equal(ShardMusicTracks.All.Length, ShardMusicTracks.All.Select(t => t.Id).Distinct().Count());
        Assert.Equal(ShardMusicTracks.All.Length, ShardMusicTracks.All.Select(t => t.File.ToLowerInvariant()).Distinct().Count());
        Assert.All(ShardMusicTable.Rows, r => Assert.NotNull(ShardMusicTracks.Find(r.Track)));

        // A row with a stock ID is refused (a stock ID can never be a target either way).
        var town = TownLike("P368 Town");
        var other = new Region("P368 Other", Map.Trammel, Region.DefaultPriority,
            new Rectangle3D(new Point3D(X0 + 100, Y0, Region.MinZ), new Point3D(X0 + 140, Y0 + 40, Region.MaxZ))) { Music = MusicName.Yew };
        other.Register();
        _regions.Add(other);

        var log = ClusterFShardMusic.Apply(new ShardMusicRow[]
        {
            new(ShardMusicKind.Region, "P368 Town", (int)MusicName.Vesper, "Trammel"),
            new(ShardMusicKind.Region, "P368 Town", 105, "Trammel")
        });
        Assert.Contains(log, l => l.Contains("not ours"));
        Assert.Equal((MusicName)105, town.Music);

        // An unmapped region keeps its music; Reset puts the mapped one's stock music back.
        Assert.Equal(MusicName.Yew, other.Music);
        Assert.False(ClusterFShardMusic.IsMapped(other));
        ClusterFShardMusic.Reset();
        Assert.Equal(MusicName.Britain1, town.Music);
        Assert.Equal(MusicName.Yew, other.Music);
        Assert.Empty(ClusterFShardMusic.MappedRegions);
    }

    [Fact]
    public void APlayerWhoTurnsShardMusicOffKeepsStockMusic()
    {
        var town = TownLike("P368 Town");
        ClusterFShardMusic.Apply(new ShardMusicRow[]
        {
            new(ShardMusicKind.Region, "P368 Town", 105, "Trammel"),
            new(ShardMusicKind.Boss, nameof(Dragon), 108)
        });
        var (pm, _) = Player(Outside);
        var dragon = Spawn<Dragon>(new Point3D(X0 + 7, Y0 + 5, 0));
        var now = DateTime.UtcNow;
        try
        {
            Assert.False(ClusterFShardMusic.IsOff(pm));
            Assert.Equal((MusicName)105, town.Music);

            ClusterFShardMusic.SetOff(pm, true);
            Assert.True(ClusterFShardMusic.IsOff(pm));
            Assert.Equal((pm, (int)pm.Region.Music, ShardMusicLayer.Area), _played.Last()); // outside: the region's own music

            // No fight music.
            var count = _played.Count;
            Assert.False(ClusterFShardMusic.Engage(pm, dragon, now));
            Assert.Equal(count, _played.Count);

            // A step into the mapped region: pinned sends our track, then the stock one follows.
            pm.MoveToWorld(InRegion, Map.Trammel);
            Assert.Same(town, pm.Region);
            Assert.Equal((MusicName)9, ClusterFShardMusic.StockMusic(town)); // Britain1
            ClusterFShardMusic.AfterStep(pm);
            Assert.Equal((pm, 9, ShardMusicLayer.Area), _played.Last());
            Assert.Equal(MusicName.Britain1, ClusterFShardMusic.AreaMusic(pm));

            // Another step inside the same region sends nothing more.
            count = _played.Count;
            ClusterFShardMusic.AfterStep(pm);
            Assert.Equal(count, _played.Count);

            // Back on: the shard track, and fights again.
            ClusterFShardMusic.SetOff(pm, false);
            Assert.Equal((pm, 105, ShardMusicLayer.Area), _played.Last());
            Assert.True(ClusterFShardMusic.Engage(pm, dragon, now));
            Assert.Equal((pm, 108, ShardMusicLayer.Boss), _played.Last());
        }
        finally
        {
            dragon.Delete();
        }
    }
}
