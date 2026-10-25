// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/VoidPoolController.cs (cc-P194).
//
// The battle for the Void Pool, one controller per facet (an invisible item at 5605,1998,10 placed by
// [SetupNewCovetous). While Active it owns the Void Pool region, a one-second timer and the level-3 spawner.
// When players are in the region and the start time has passed, a battle begins: one of the five void types is
// rolled, and waves of void creatures (ceil(max(5, 2 sqrt(wave)) * 1.5) of them, in the type's five kinds by
// weight, each built at the wave's level) pour from two gates at 5592,2012 and 5466,2007 and follow the waypoint
// paths to the pool at 5500,1998. Wave two comes ten seconds after wave one, later waves after 30-70 seconds that
// shorten as the waves climb. A creature standing on the pool walls has a 3 in 4 chance each second of damaging
// them, and each hit has a coin's chance of costing the pool one of its fifteen hits; at zero the battle is lost:
// scores are tallied (VoidPoolStats), each player's wave credits become Void Pool points (doubled on Felucca),
// everyone is told their waves and score, the survivors burn away, and the next battle is fifteen minutes off.
//
// Score: a kill is worth its creature's fame / 998 to everyone with looting rights (a pet's master counts);
// clearing a wave is worth stage x 125 to everyone credited who is still in the region.
//
// Conversion notes (the rest in notes/cc-P194-port-wrong-and-covetous-void.md):
//   ServUO saves the level-3 spawner inline; here it is an item of its own (Level3Spawner.cs) held by reference.
//   The Active setter's work is ApplyActive, run by the property and again after a load (a load sets the
//     field, not the property), as ServUO's Deserialize assigns Active.
//   NoLootOnDeath has no pinned member; a void spawn's GenerateLoot already gives nothing (CovetousCreature).
//   QuestHelper/AForcedSacraficeQuest is a Town Cryer quest, not on this shard.
//   PointsSystem.VoidPool is the shard's P33 points framework (Services/PointsSystems/VoidPool.cs).
//   Activator.CreateInstance(t, wave, true) builds each creature as on ServUO.
//   The context-menu entry ServUO's PlayerMobile adds inside the region is server/patches/
//     PlayerMobile-void-pool-context.patch, one hook line (VoidPoolContextMenu.AddEntries).

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Points;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.VoidPool;

public enum VoidType
{
    Abyss,
    Repond,
    Undead,
    Reptile,
    Elemental
}

[SerializationGenerator(0, false)]
public partial class VoidPoolController : Item
{
    public const int RestartSpan = 15;
    public const int PoolStartHits = 15;
    public const int StartPointVariance = 8;

    public static readonly Point3D StartPoint1 = new(5592, 2012, 0);
    public static readonly Point3D StartPoint2 = new(5466, 2007, 0);

    public static readonly Point3D EndPoint = new(5500, 1998, 5);
    public static readonly Rectangle2D PoolWalls = new(5495, 1993, 10, 10);

    public static readonly Type[][] SpawnTable =
    {
        new[] { typeof(DaemonMongbat), typeof(GargoyleAssassin), typeof(CovetousDoppleganger), typeof(LesserOni), typeof(CovetousFireDaemon) },
        new[] { typeof(LizardmanWitchdoctor), typeof(OrcFootSoldier), typeof(RatmanAssassin), typeof(OgreBoneCrusher), typeof(TitanRockHunter) },
        new[] { typeof(AngeredSpirit), typeof(BoneSwordSlinger), typeof(VileCadaver), typeof(DiseasedLich), typeof(CovetousRevenant) },
        new[] { typeof(WarAlligator), typeof(MagmaLizard), typeof(ViciousDrake), typeof(CorruptedWyvern), typeof(CovetousWyrm) },
        new[] { typeof(CovetousEarthElemental), typeof(CovetousWaterElemental), typeof(VortexElemental), typeof(SearingElemental), typeof(VenomElemental) }
    };

    // Saved as BattleActive; Active below is the property with ServUO's setter work.
    [SerializableField(0, setter: "private")]
    private bool _battleActive;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _respawnMin;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _respawnMax;

    // The generator emits the public WaypointsA / WaypointsB lists from these two fields.
    [SerializableField(3)]
    private List<WayPoint> _waypointsA;

    [SerializableField(4)]
    private List<WayPoint> _waypointsB;

    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Level3Spawner _level3Spawner;

    private Timer _timer;

    public VoidPoolController(Map map) : base(3803)
    {
        Name = "Void Pool Controller";
        Visible = false;
        Movable = false;

        PoolHits = PoolStartHits;

        if (map == Map.Trammel)
        {
            if (InstanceTram != null)
            {
                Delete();
            }
            else
            {
                InstanceTram = this;
            }
        }
        else if (map == Map.Felucca)
        {
            if (InstanceFel != null)
            {
                Delete();
            }
            else
            {
                InstanceFel = this;
            }
        }
        else
        {
            Delete();
        }

        _waypointsA = new List<WayPoint>();
        _waypointsB = new List<WayPoint>();

        if (Deleted)
        {
            return;
        }

        Region = new VoidPoolRegion(this, map);
        Region.Register();

        _respawnMin = 60;
        _respawnMax = 90;

        ClearSpawners();
        Active = true;

        ResetLevel3Spawners();
    }

    public static VoidPoolController InstanceTram { get; set; }
    public static VoidPoolController InstanceFel { get; set; }

    public static VoidPoolController GetInstance(Map map) =>
        map == Map.Felucca ? InstanceFel : map == Map.Trammel ? InstanceTram : null;

    [CommandProperty(AccessLevel.GameMaster)]
    public int Wave { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public int Stage => Wave < 2 ? 0 : Math.Max(1, Wave / 5);

    public List<WaveInfo> Waves { get; set; }
    public int WaypointACount { get; set; }
    public int WaypointBCount { get; set; }

    public VoidPoolRegion Region { get; set; }
    public Dictionary<Mobile, long> CurrentScore { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool OnGoing { get; private set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public VoidType VoidType { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public DateTime NextStart { get; private set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public DateTime NextWave { get; private set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public int PoolHits { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool Active
    {
        get => _battleActive;
        set
        {
            if (_battleActive == value)
            {
                return;
            }

            _battleActive = value;
            this.MarkDirty();
            ApplyActive(value);
        }
    }

    /// <summary>ServUO's Active setter.</summary>
    private void ApplyActive(bool value)
    {
        if (Deleted)
        {
            return;
        }

        if (!value)
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }

            if (Region != null)
            {
                Region.Unregister();
                Region = null;
            }

            if (_level3Spawner != null)
            {
                _level3Spawner.Deactivate();
                _level3Spawner = null;
            }
        }
        else
        {
            if (Region == null)
            {
                Region = new VoidPoolRegion(this, Map == null || Map == Map.Internal ? InstanceMap() : Map);
                Region.Register();
            }

            if (_timer == null)
            {
                _timer = Timer.DelayCall(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), OnTick);

                NextStart = Core.Now + TimeSpan.FromMinutes(RestartSpan);

                Region?.SendRegionMessage(1152526, RestartSpan.ToString()); // The battle for the Void Pool will begin in ~1_VALUE~ minutes.
            }

            if (_level3Spawner == null || _level3Spawner.Deleted)
            {
                _level3Spawner = new Level3Spawner(this);
            }
        }
    }

    // Which facet this controller is for, before it has been moved to the world (the constructor registers the
    // region from its map argument).
    private Map InstanceMap() => this == InstanceFel ? Map.Felucca : Map.Trammel;

    public override void OnDoubleClick(Mobile from)
    {
        if (from.AccessLevel >= AccessLevel.GameMaster)
        {
            from.SendGump(new PropertiesGump(from, this));
        }
    }

    private void OnTick()
    {
        if (Deleted)
        {
            return;
        }

        if (!OnGoing && Core.Now > NextStart && Region != null && Region.GetPlayerCount() > 0)
        {
            NextStart = DateTime.MaxValue;
            OnGoing = true;
            VoidType = (VoidType)Utility.Random(5);
            PoolHits = PoolStartHits;
            Wave = 0;

            CurrentScore?.Clear();
            Waves?.Clear();

            CurrentScore = new Dictionary<Mobile, long>();
            Waves = new List<WaveInfo>();

            Region.SendRegionMessage(1152527, 0x2B); // The battle for the Void Pool is beginning now!

            if (WaypointACount != WaypointsA.Count || WaypointBCount != WaypointsB.Count)
            {
                Generate.AddWaypoints();
            }

            SpawnWave();
        }
        else if (OnGoing)
        {
            if (Core.Now > NextWave)
            {
                SpawnWave();
            }

            var onWalls = new List<BaseCreature>();

            foreach (var m in Map.GetMobilesInBounds(PoolWalls))
            {
                if (m is BaseCreature { Controlled: false, Summoned: false } bc)
                {
                    onWalls.Add(bc);
                }
            }

            foreach (var bc in onWalls)
            {
                if (!OnGoing)
                {
                    break;
                }

                if (Utility.RandomDouble() > 0.25)
                {
                    OnVoidWallDamaged(bc);
                }
            }
        }
    }

    public void SpawnWave()
    {
        Wave++;

        Region.SendRegionMessage(1152528, Wave.ToString()); // Wave ~1_WAVE~ of enemy forces are charging the Void Pool!

        var toSpawn = (int)Math.Ceiling(Math.Max(5, Math.Sqrt(Wave) * 2) * 1.5);
        var creatures = new List<BaseCreature>();

        for (var i = 0; i < toSpawn; i++)
        {
            var start = i % 2 == 0 ? StartPoint1 : StartPoint2;

            for (var j = 0; j < 25; j++)
            {
                var x = start.X + Utility.RandomMinMax(start.X - StartPointVariance / 2, start.X + StartPointVariance / 2);
                var y = start.Y + Utility.RandomMinMax(start.Y - StartPointVariance / 2, start.Y + StartPointVariance / 2);
                var z = Map.GetAverageZ(x, y);

                if (Map.CanSpawnMobile(x, y, z))
                {
                    start = new Point3D(x, y, z);
                    break;
                }
            }

            var ran = Utility.RandomMinMax(0, Stage < 10 ? 12 : Stage < 15 ? 14 : 15);

            var t = ran switch
            {
                5 or 6 or 7 or 8 => SpawnTable[(int)VoidType][1],
                9 or 10 or 11    => SpawnTable[(int)VoidType][2],
                12 or 13         => SpawnTable[(int)VoidType][3],
                14 or 15         => SpawnTable[(int)VoidType][4],
                _                => SpawnTable[(int)VoidType][0]
            };

            if (Activator.CreateInstance(t, Wave, true) is not BaseCreature bc)
            {
                continue;
            }

            var spawnAt = start;

            Timer.DelayCall(
                TimeSpan.FromSeconds(i * .75),
                () =>
                {
                    if (OnGoing && !Deleted)
                    {
                        bc.MoveToWorld(spawnAt, Map);
                        bc.Home = EndPoint;
                        bc.RangeHome = 1;

                        creatures.Add(bc);

                        bc.CurrentWayPoint = GetNearestWaypoint(bc);
                    }
                    else
                    {
                        bc.Delete();
                    }
                }
            );
        }

        var gate1 = new VoidPoolGate();
        gate1.MoveToWorld(StartPoint1, Map);
        Effects.PlaySound(StartPoint1, Map, 0x20E);

        var gate2 = new VoidPoolGate();
        gate2.MoveToWorld(StartPoint2, Map);
        Effects.PlaySound(StartPoint2, Map, 0x20E);

        Timer.DelayCall(
            TimeSpan.FromSeconds(toSpawn * .80),
            () =>
            {
                Effects.SendLocationParticles(EffectItem.Create(gate1.Location, gate1.Map, EffectItem.DefaultDuration), 0x376A, 9, 20, 5042);
                Effects.PlaySound(gate1.GetWorldLocation(), gate1.Map, 0x201);

                Effects.SendLocationParticles(EffectItem.Create(gate2.Location, gate2.Map, EffectItem.DefaultDuration), 0x376A, 9, 20, 5042);
                Effects.PlaySound(gate2.GetWorldLocation(), gate2.Map, 0x201);

                gate1.Delete();
                gate2.Delete();
            }
        );

        Waves.Add(new WaveInfo(Wave, creatures));
        NextWave = GetNextWaveTime();
    }

    public WayPoint GetNearestWaypoint(Mobile m, int range = 15)
    {
        var closestRange = 15;
        WayPoint closest = null;

        foreach (var item in Map.GetItemsInRange(m.Location, range))
        {
            if (item is WayPoint waypoint)
            {
                var dist = (int)m.GetDistanceToSqrt(item);

                if (dist < closestRange || closest == null)
                {
                    closest = waypoint;
                    closestRange = dist;
                }
            }
        }

        return closest;
    }

    public Item GetNearestVoidPoolWall(Mobile m)
    {
        var closestRange = 5;
        Item closest = null;

        foreach (var item in Map.GetItemsInRange(m.Location, 5))
        {
            if (item.Name == "Void Pool")
            {
                var dist = (int)m.GetDistanceToSqrt(item);

                if (dist < closestRange || closest == null)
                {
                    closest = item;
                    closestRange = dist;
                }
            }
        }

        return closest;
    }

    public DateTime GetNextWaveTime()
    {
        if (Wave == 1)
        {
            return Core.Now + TimeSpan.FromSeconds(10);
        }

        var min = Math.Max(30, _respawnMin - Wave) + Utility.RandomMinMax(0, 10);
        var max = Math.Max(45, _respawnMax - Wave) - Utility.RandomMinMax(0, 5);

        return Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(min, max));
    }

    public void OnVoidWallDamaged(Mobile damager)
    {
        if (0.5 > Utility.RandomDouble())
        {
            PoolHits--;
        }

        Region.SendRegionMessage(1152529); // The Void Pool walls have been damaged! Defend the Void Pool!

        var item = GetNearestVoidPoolWall(damager);

        if (item != null)
        {
            var p = new Point3D(item.X, item.Y, item.Z + 5);
            Effects.SendLocationParticles(EffectItem.Create(p, item.Map, EffectItem.DefaultDuration), Utility.RandomList(0x36BD, 0x36B0, 0x3728), 20, 10, 5044);
            Effects.PlaySound(p, item.Map, 0x307);
        }

        if (PoolHits <= 0 && OnGoing)
        {
            OnGoing = false;
            EndInvasion();
        }
    }

    public void EndInvasion()
    {
        Region.SendRegionMessage(1152530); // Cora's forces have destroyed the Void Pool walls. The battle is lost!

        VoidPoolStats.OnInvasionEnd(this);

        NextStart = Core.Now + TimeSpan.FromMinutes(RestartSpan);

        Region.SendRegionMessage(1152526, RestartSpan.ToString()); // The battle for the Void Pool will begin in ~1_VALUE~ minutes.

        var list = Region.GetPlayers();

        foreach (var m in list)
        {
            var points = GetCurrentPoints(m);

            if (points > 0)
            {
                Engines.Points.VoidPool.Instance.AwardPoints(m, points);
            }
        }

        foreach (var m in list)
        {
            if (CurrentScore.TryGetValue(m, out var score))
            {
                // During the battle, you helped fight back ~1_COUNT~ out of ~2_TOTAL~ waves of enemy forces. Your final wave was ~3_MAX~. Your total score for the battle was ~4_SCORE~ points.
                m.SendLocalizedMessage(1152650, $"{GetTotalWaves(m)}\t{Wave}\t{Wave}\t{score}");
            }
        }

        ClearSpawn(true);
    }

    public void OnCreatureKilled(BaseCreature killed)
    {
        if (Waves == null)
        {
            return;
        }

        foreach (var info in Waves)
        {
            if (!info.Creatures.Contains(killed))
            {
                continue;
            }

            var list = BaseCreature.GetLootingRights(killed.DamageEntries, killed.HitsMax);
            list.Sort();

            for (var i = 0; i < list.Count; i++)
            {
                var ds = list[i];
                var m = ds.m_Mobile;

                if (ds.m_Mobile is BaseCreature pet && pet.GetMaster() is PlayerMobile master)
                {
                    m = master;
                }

                if (!info.Credit.Contains(m))
                {
                    info.Credit.Add(m);
                }

                CurrentScore[m] = CurrentScore.GetValueOrDefault(m) + killed.Fame / 998;
            }

            info.Creatures.Remove(killed);

            if (info.Creatures.Count == 0)
            {
                foreach (var m in info.Credit)
                {
                    if (m.Region == Region && m is PlayerMobile)
                    {
                        double award = Math.Max(0, Map == Map.Felucca ? Stage * 2 : Stage);

                        if (award > 0)
                        {
                            // Score Bonus
                            CurrentScore[m] = CurrentScore.GetValueOrDefault(m) + Stage * 125;
                        }
                    }
                }
            }

            if (killed.Corpse is Corpse { Deleted: false } corpse)
            {
                corpse.BeginDecay(TimeSpan.FromMinutes(1));
            }
        }
    }

    /// <summary>ServUO: every spawner in the Void Pool region is emptied and stopped.</summary>
    public void ClearSpawners()
    {
        if (Region == null)
        {
            return;
        }

        foreach (var item in Region.GetItems())
        {
            if (item is Spawners.Spawner spawner)
            {
                spawner.RemoveSpawns();
                spawner.Running = false;
            }
        }
    }

    public void ResetLevel3Spawners()
    {
        ResetLevel3Spawners(Map.Trammel);
        ResetLevel3Spawners(Map.Felucca);
    }

    /// <summary>ServUO: the stock spawners of level 3 (5501-5627 x 1799-1927) are emptied and stopped.</summary>
    public static void ResetLevel3Spawners(Map map)
    {
        var r = Server.Region.Find(new Point3D(5574, 1859, 0), map);

        if (r == null)
        {
            return;
        }

        foreach (var item in r.GetItems())
        {
            if (item is Spawners.Spawner spawner && item.X >= 5501 && item.X <= 5627 && item.Y >= 1799 && item.Y <= 1927)
            {
                spawner.RemoveSpawns();
                spawner.Running = false;
            }
        }
    }

    public void ClearSpawn() => ClearSpawn(false);

    public void ClearSpawn(bool effects)
    {
        if (Region == null)
        {
            return;
        }

        var doomed = new List<Mobile>();

        foreach (var m in Region.GetMobiles())
        {
            if (m is CovetousCreature)
            {
                doomed.Add(m);
            }
        }

        foreach (var m in doomed)
        {
            if (effects)
            {
                Effects.SendLocationEffect(m.Location, m.Map, 0xDDA, 30, 10);
            }

            m.Delete();
        }
    }

    public int GetCurrentPoints(Mobile from)
    {
        if (Waves == null)
        {
            return 0;
        }

        var points = 0;

        foreach (var info in Waves)
        {
            if (info.Credit.Contains(from))
            {
                points += Map == Map.Felucca ? Stage * 2 : Stage;
            }
        }

        return points;
    }

    public int GetTotalWaves(Mobile from) => Waves?.Count(i => i.Wave > 2 && i.Credit.Contains(from)) ?? 0;

    public static int GetPlayerScore(Dictionary<Mobile, long> score, Mobile m) =>
        score == null || m == null || !score.TryGetValue(m, out var s) ? 0 : (int)s;

    public override void OnDelete()
    {
        if (OnGoing)
        {
            OnGoing = false;
            EndInvasion();
        }

        if (Region != null)
        {
            Region.Unregister();
            Region = null;
        }

        if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
        }

        foreach (var wp in WaypointsA.ToList())
        {
            if (wp?.Deleted == false)
            {
                wp.Delete();
            }
        }

        foreach (var wp in WaypointsB.ToList())
        {
            if (wp?.Deleted == false)
            {
                wp.Delete();
            }
        }

        if (_level3Spawner != null)
        {
            _level3Spawner.Deactivate();
            _level3Spawner.Delete();
            _level3Spawner = null;
        }

        if (InstanceTram == this)
        {
            InstanceTram = null;
        }

        if (InstanceFel == this)
        {
            InstanceFel = null;
        }

        base.OnDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _waypointsA ??= new List<WayPoint>();
        _waypointsB ??= new List<WayPoint>();
        _waypointsA.RemoveAll(w => w == null || w.Deleted);
        _waypointsB.RemoveAll(w => w == null || w.Deleted);

        if (Map == Map.Felucca)
        {
            InstanceFel = this;
        }
        else
        {
            InstanceTram = this;
        }

        PoolHits = PoolStartHits;

        // ServUO: Active = reader.ReadBool() runs the setter; NextStart = now; ten seconds later the old spawn is
        // cleared. cc-P610 (P489 #8): a player already inside could start a battle in those ten seconds, and the
        // clear then deleted its first wave. No battle starts until the clear has run, on the first tick after the
        // world has loaded; then the battle may start at once, as in ServUO.
        ApplyActive(_battleActive);
        NextStart = DateTime.MaxValue;

        Timer.DelayCall(ClearAfterLoad);
    }

    private void ClearAfterLoad()
    {
        if (Deleted)
        {
            return;
        }

        ClearSpawn();
        ClearSpawners();
        Waves?.Clear();

        if (!OnGoing)
        {
            NextStart = Core.Now;
        }
    }
}

// ServUO: VoidPoolGate, the moongate the waves pour from; one loaded from a save is deleted.
[SerializationGenerator(0, false)]
public partial class VoidPoolGate : Static
{
    [Constructible]
    public VoidPoolGate() : base(0xF6C)
    {
        Light = LightType.Circle300;
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Delete();
    }
}

// ServUO: PlayerMobile.GetContextMenuEntries adds "The Void Pool" when the player stands in the region (the
// VoidPoolInfo entry of Services/PointsSystems/VoidPool.cs). The patch calls this.
public static class VoidPoolContextMenu
{
    public static void AddEntries(PlayerMobile pm, ref PooledRefList<ContextMenuEntry> list)
    {
        if (pm?.Region?.IsPartOf<VoidPoolRegion>() != true)
        {
            return;
        }

        var controller = VoidPoolController.GetInstance(pm.Map);

        if (controller != null)
        {
            list.Add(new VoidPoolInfo(pm, controller));
        }
    }
}

public class VoidPoolInfo : ContextMenuEntry
{
    private readonly VoidPoolController _controller;
    private readonly Mobile _from;

    public VoidPoolInfo(Mobile from, VoidPoolController controller) : base(1152531) // The Void Pool
    {
        _from = from;
        _controller = controller;
    }

    public override void OnClick(Mobile from, IEntity target)
    {
        if (_from is PlayerMobile pm && _controller?.Deleted == false)
        {
            pm.SendGump(new VoidPoolGump(_controller, pm));
        }
    }
}
