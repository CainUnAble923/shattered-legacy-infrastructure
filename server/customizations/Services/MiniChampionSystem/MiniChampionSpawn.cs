// ServUO: Services/MiniChampionSystem/MiniChampionSpawn.cs (cc-P197). The mini champion controller (an invisible,
// unmovable 0xBD2 named "Mini Champion Controller"): set Active, it starts a one-second slice timer and advances to
// level 1 of its type's table (MiniChampType), spawning each level's creatures up to their Required counts within
// SpawnRange (30) of itself; when every type of the level has its Required kills the next level comes, the last
// level being the Renowned (or Meraktus the champion, whose spawn shouts 1073370, 1073367, 1073368 and 1073369 to
// the players in range as levels 2 to 5 begin); past the last level it stops, clears the spawn (the live ones are
// kept in Despawns until the next start deletes them) and restarts after RestartDelay (5 minutes). Properties
// show the type (1060658), the spawn range (1060661), active/inactive (1060742/1060743), the level and each
// type's counters (1150301 onward). Double-click opens the properties gump (staff).
//
// [GenMiniChamp (Administrator, ServUO's) deletes every controller and places the fourteen at ServUO's points
// (thirteen in the Abyss, Meraktus at 395,1913,12 in Malas with his boss point 384,1931,50), all Active;
// [DeleteMiniChamp (ours) removes them. ServUO's GenMiniChamp deletes while iterating its own list (an
// InvalidOperationException there); a copy is iterated here.
//
// Saved: the boss point, active, type, level, range, restart delay, the per-type spawn infos (a nested generated
// class, MiniChampSpawnInfo) and the despawn list, as ServUO's; a loaded active controller restarts its slice timer
// and an inactive one its restart timer, as ServUO's Deserialize. A deserialized Active is read into the field, so
// Start does not run at load (the property's setter is ServUO's Start/Stop).

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;

namespace Server.Engines.MiniChamps;

[SerializationGenerator(0, false)]
public partial class MiniChamp : Item
{
    public static readonly (MiniChampType Type, Point3D At, Map Map, Point3D BossSpawnPoint)[] Placements =
    {
        (MiniChampType.CrimsonVeins, new Point3D(974, 161, -10), Map.TerMur, Point3D.Zero),
        (MiniChampType.AbyssalLair, new Point3D(987, 328, 11), Map.TerMur, Point3D.Zero),
        (MiniChampType.DiscardedCavernClanRibbon, new Point3D(915, 501, -11), Map.TerMur, Point3D.Zero),
        (MiniChampType.DiscardedCavernClanScratch, new Point3D(950, 552, -13), Map.TerMur, Point3D.Zero),
        (MiniChampType.DiscardedCavernClanChitter, new Point3D(980, 491, -11), Map.TerMur, Point3D.Zero),
        (MiniChampType.EnslavedGoblins, new Point3D(578, 799, -45), Map.TerMur, Point3D.Zero),
        (MiniChampType.FairyDragonLair, new Point3D(887, 273, 4), Map.TerMur, Point3D.Zero),
        (MiniChampType.FireTemple, new Point3D(546, 760, -91), Map.TerMur, Point3D.Zero),
        (MiniChampType.LandsoftheLich, new Point3D(530, 658, 9), Map.TerMur, Point3D.Zero),
        (MiniChampType.LavaCaldera, new Point3D(578, 900, -72), Map.TerMur, Point3D.Zero),
        (MiniChampType.PassageofTears, new Point3D(684, 579, -14), Map.TerMur, Point3D.Zero),
        (MiniChampType.SecretGarden, new Point3D(434, 701, 29), Map.TerMur, Point3D.Zero),
        (MiniChampType.SkeletalDragon, new Point3D(677, 824, -108), Map.TerMur, Point3D.Zero),
        (MiniChampType.MeraktusTheTormented, new Point3D(395, 1913, 12), Map.Malas, new Point3D(384, 1931, 50))
    };

    private static readonly List<MiniChamp> _controllers = new();

    public static IReadOnlyList<MiniChamp> Controllers => _controllers;

    private Timer _timer;
    private Timer _restartTimer;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Point3D _bossSpawnPoint;

    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private TimeSpan _restartDelay;

    [SerializableField(6)]
    private List<MiniChampSpawnInfo> _spawn;

    [Tidy]
    [SerializableField(7)]
    private List<Mobile> _despawns;

    public static void Initialize()
    {
        CommandSystem.Register("GenMiniChamp", AccessLevel.Administrator, GenMiniChamp_OnCommand);
        CommandSystem.Register("DeleteMiniChamp", AccessLevel.Administrator, DeleteMiniChamp_OnCommand);
    }

    [Usage("GenMiniChamp")]
    [Description("MiniChampion Generator")]
    [ShardCommand(CommandCategory.WorldGeneration, Rerun = CommandRerun.Replaces, Shard = CommandShard.Any, NoDryRun = true, Summary = "The fourteen Stygian Abyss mini champion spawns (thirteen in the Abyss, Meraktus in Malas), active. Deletes every existing controller first, so it can run again.")]
    public static void GenMiniChamp_OnCommand(CommandEventArgs e)
    {
        Generate();

        e.Mobile.SendMessage("Created Mini Champion Spawns.");
    }

    [Usage("DeleteMiniChamp")]
    [Description("Removes every mini champion controller and its spawn.")]
    [ShardCommand(CommandCategory.WorldRemoval, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.Unverified, NoDryRun = true, Summary = "DELETES every mini champion controller with its live spawn.")]
    private static void DeleteMiniChamp_OnCommand(CommandEventArgs e)
    {
        var deleted = DeleteAll();
        e.Mobile.SendMessage($"Mini Champion Spawns removed ({deleted} controller(s)).");
    }

    /// <summary>ServUO's GenStoneRuins_OnCommand: every controller deleted, then the fourteen placed and started.</summary>
    public static List<MiniChamp> Generate()
    {
        DeleteAll();

        var placed = new List<MiniChamp>();

        foreach (var (type, at, map, boss) in Placements)
        {
            var controller = new MiniChamp
            {
                Type = type,
                BossSpawnPoint = boss
            };

            controller.MoveToWorld(at, map);
            controller.Active = true;
            placed.Add(controller);
        }

        return placed;
    }

    public static int DeleteAll()
    {
        var deleted = 0;

        foreach (var controller in _controllers.ToList())
        {
            controller.Delete();
            deleted++;
        }

        return deleted;
    }

    [Constructible]
    public MiniChamp() : base(0xBD2)
    {
        Movable = false;
        Visible = false;
        Name = "Mini Champion Controller";

        _despawns = new List<Mobile>();
        _spawn = new List<MiniChampSpawnInfo>();
        _restartDelay = TimeSpan.FromMinutes(5.0);
        _spawnRange = 30;
        _bossSpawnPoint = Point3D.Zero;

        _controllers.Add(this);
    }

    [SerializableProperty(4)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int SpawnRange
    {
        get => _spawnRange;
        set
        {
            _spawnRange = value;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(2)]
    [CommandProperty(AccessLevel.GameMaster)]
    public MiniChampType Type
    {
        get => _type;
        set
        {
            _type = value;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(1)]
    [CommandProperty(AccessLevel.GameMaster)]
    public bool Active
    {
        get => _active;
        set
        {
            if (value)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }
    }

    [SerializableProperty(3)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int Level
    {
        get => _level;
        set
        {
            _level = value;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    public IReadOnlyList<MiniChampSpawnInfo> SpawnInfos => _spawn;

    public IReadOnlyList<Mobile> DespawnList => _despawns;

    public void Start()
    {
        if (_active || Deleted)
        {
            return;
        }

        _active = true;
        this.MarkDirty();

        _timer?.Stop();

        _timer = new SliceTimer(this);
        _timer.Start();

        _restartTimer?.Stop();
        _restartTimer = null;

        AdvanceLevel();
        InvalidateProperties();
    }

    public void Stop()
    {
        if (!_active || Deleted)
        {
            return;
        }

        _active = false;
        _level = 0;
        this.MarkDirty();

        ClearSpawn();
        Despawn();

        _timer?.Stop();
        _timer = null;

        _restartTimer?.Stop();
        _restartTimer = null;
        InvalidateProperties();
    }

    public void Despawn()
    {
        foreach (var toDespawn in _despawns)
        {
            toDespawn.Delete();
        }

        _despawns.Clear();
    }

    public void OnSlice()
    {
        if (!_active || Deleted)
        {
            return;
        }

        var changed = false;
        var done = true;

        foreach (var spawn in _spawn)
        {
            if (spawn.Slice() && !changed)
            {
                changed = true;
            }

            if (!spawn.Done && done)
            {
                done = false;
            }
        }

        if (done)
        {
            AdvanceLevel();
        }

        if (_active)
        {
            foreach (var spawn in _spawn)
            {
                if (spawn.Respawn() && !changed)
                {
                    changed = true;
                }
            }
        }

        if (done || changed)
        {
            InvalidateProperties();
        }
    }

    public void ClearSpawn()
    {
        foreach (var spawn in _spawn)
        {
            foreach (var creature in spawn.Creatures)
            {
                _despawns.Add(creature);
            }
        }

        _spawn.Clear();
        this.MarkDirty();
    }

    public void AdvanceLevel()
    {
        Level++;

        var info = MiniChampInfo.GetInfo(_type);
        var levelInfo = info.GetLevelInfo(Level);

        if (levelInfo != null && Level <= info.MaxLevel)
        {
            ClearSpawn();

            if (_type == MiniChampType.MeraktusTheTormented)
            {
                MinotaurShouts();
            }

            foreach (var type in levelInfo.Types)
            {
                _spawn.Add(new MiniChampSpawnInfo(this, type));
            }
        }
        else // begin restart
        {
            Stop();

            _restartTimer = Timer.DelayCall(_restartDelay, Start);
        }
    }

    private void MinotaurShouts()
    {
        int cliloc;

        switch (Level)
        {
            case 1:
                return;
            case 2:
                cliloc = 1073370;
                break;
            case 3:
                cliloc = 1073367;
                break;
            case 4:
                cliloc = 1073368;
                break;
            case 5:
                cliloc = 1073369;
                break;
            default:
                cliloc = 0;
                break;
        }

        if (Map == null)
        {
            return;
        }

        foreach (var x in Map.GetMobilesInRange(Location, _spawnRange))
        {
            if (x is PlayerMobile)
            {
                x.SendLocalizedMessage(cliloc);
            }
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1060658, $"Type\t{_type}");            // ~1_val~: ~2_val~
        list.Add(1060661, $"Spawn Range\t{_spawnRange}"); // ~1_val~: ~2_val~

        if (_active)
        {
            var info = MiniChampInfo.GetInfo(_type);

            list.Add(1060742); // active
            list.Add($"Level {Level} / {(info != null ? info.MaxLevel.ToString() : "???")}");

            for (var i = 0; i < _spawn.Count; i++)
            {
                _spawn[i].AddProperties(list, i + 1150301);
            }
        }
        else
        {
            list.Add(1060743); // inactive
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        from.SendGump(new PropertiesGump(from, this));
    }

    public override void OnDelete()
    {
        _controllers.Remove(this);
        Stop();

        base.OnDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _spawn ??= new List<MiniChampSpawnInfo>();
        _despawns ??= new List<Mobile>();

        if (_active)
        {
            _timer = new SliceTimer(this);
            _timer.Start();
        }
        else
        {
            _restartTimer = Timer.DelayCall(_restartDelay, Start);
        }

        _controllers.Add(this);
    }
}

public class SliceTimer : Timer
{
    private readonly MiniChamp _controller;

    public SliceTimer(MiniChamp controller) : base(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)) =>
        _controller = controller;

    protected override void OnTick()
    {
        _controller.OnSlice();
    }
}
