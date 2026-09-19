// ServUO: Services/Revamped Dungeons/DespiseRevamped/DespiseController.cs (CC4 Despise).
//
// The invisible item at (5571, 626, 30) Trammel that runs the revamped dungeon: it registers the four
// dynamic regions while Enabled, ticks once a minute, and when the boss encounter comes due it sounds the
// Call to Arms, gives players sixty seconds to conscript, transports the stronger side's army into the
// lower level, spawns the other side's overlord and its army, and cleans up after ninety minutes or a win.
//
// Conversion notes:
//   XmlSpawner -> BaseSpawner. The controller FINDS its spawners by name ("DespiseRevamped Good #n" and
//     "Evil #n") inside the lower region, exactly as ServUO does; DespiseSpawns.Generate makes them. XmlSpawner's
//     DoReset is Reset() on both (stop and remove spawns); DoRespawn is Running = true then Respawn(), because
//     XmlSpawner.Respawn calls Start() itself and ModernUO's does not.
//   EventSink.Login  -> [OnEvent(PlayerMobile.PlayerLoginEvent)].
//   EventSink.OnEnterRegion has no counterpart; DespiseRegion.OnExit calls OnLeaveDespise, same test, same effect.
//   PointsSystem.DespiseCrystals.ConvertFromOldSystem ran only for version-0 saves; no such save exists here.
//   CheckSpawnersVersion3 (a command and a deserialize hook) fixed two creature-name typos in saved spawner
//     entries and re-created the gate teleporters for saves older than version 3. Fresh content: dropped, and
//     the corrected names (BirlingBlades, Sagittarri) are what DespiseSpawns carries.
//   Enabled's storage is a private generated property so a world load assigns it without running BeginTimer;
//     [AfterDeserialization] carries ServUO's Deserialize tail (instance, spawners, timers, sequence recovery).
//   Deleting the controller unregisters its regions (OnAfterDelete). ServUO leaves them registered until the
//     next restart, which is a defect in GM tooling, not player-facing behaviour.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Despise;

[SerializationGenerator(0, false)]
public partial class DespiseController : Item
{
    private static readonly TimeSpan EncounterCheckDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DeadLineDuration = TimeSpan.FromMinutes(90);

    public static DespiseController Instance { get; set; }

    [SerializableField(0, getter: "private", setter: "private")]
    private bool _enabledValue;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _nextBossEncounter;

    [SerializableField(2, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DespiseBoss _boss;

    [SerializableField(3, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _deadLine;

    [SerializableField(4, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Alignment _sequenceAlignment;

    private bool _sequencing;
    private bool _playersInSequence;

    private Timer _timer;
    private Timer _sequenceTimer;
    private Timer _cleanupTimer;

    private DespiseRegion _goodRegion;
    private DespiseRegion _evilRegion;
    private DespiseRegion _lowerRegion;
    private DespiseRegion _startRegion;

    private readonly List<DespiseCreature> _evilArmy = new();
    private readonly List<DespiseCreature> _goodArmy = new();

    private readonly List<Mobile> _toTransport = new();

    private List<BaseSpawner> _goodSpawners;
    private List<BaseSpawner> _evilSpawners;

    public DespiseController() : base(3806)
    {
        Movable = false;
        Visible = false;

        _enabledValue = true;
        Instance = this;

        _nextBossEncounter = Core.Now;
        _boss = null;

        BeginTimer();

        CreateSpawners();
    }

    public Region GoodRegion => _goodRegion;
    public Region EvilRegion => _evilRegion;
    public Region LowerRegion => _lowerRegion;
    public Region StartRegion => _startRegion;

    [CommandProperty(AccessLevel.GameMaster)]
    public bool Enabled
    {
        get => _enabledValue;
        set
        {
            if (_enabledValue == value)
            {
                return;
            }

            _enabledValue = value;

            if (_enabledValue)
            {
                BeginTimer();
            }
            else
            {
                EndTimer();
            }

            this.MarkDirty();
        }
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool Sequencing => _sequencing;

    public List<DespiseCreature> EvilArmy => _evilArmy;
    public List<DespiseCreature> GoodArmy => _goodArmy;

    public bool IsInSequence => _sequenceTimer != null || _cleanupTimer != null;

    [CommandProperty(AccessLevel.GameMaster)]
    public int GoodSpawnerCount => _goodSpawners?.Count ?? 0;

    [CommandProperty(AccessLevel.GameMaster)]
    public int EvilSpawnerCount => _evilSpawners?.Count ?? 0;

    [CommandProperty(AccessLevel.GameMaster)]
    public bool ResetSpawns
    {
        get => true;
        set
        {
            if (value)
            {
                _goodSpawners?.Clear();
                _evilSpawners?.Clear();

                CreateSpawners();
            }
        }
    }

    public static WispOrb GetWispOrb(Mobile from)
    {
        foreach (var orb in WispOrb.Orbs)
        {
            if (orb?.Deleted == false && orb.Owner == from)
            {
                return orb;
            }
        }

        return null;
    }

    private void BeginTimer()
    {
        EndTimer();

        _timer = Timer.DelayCall(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), OnTick);

        _lowerRegion = new DespiseRegion("Despise Lower", LowerLevelBounds, true);
        _evilRegion = new DespiseRegion("Despise Evil", EvilBounds);
        _goodRegion = new DespiseRegion("Despise Good", GoodBounds);
        _startRegion = new DespiseRegion("Despise Start", new[] { new Rectangle2D(5568, 623, 22, 20) });
    }

    private void EndTimer()
    {
        _timer?.Stop();
        _timer = null;

        _lowerRegion?.Unregister();
        _evilRegion?.Unregister();
        _goodRegion?.Unregister();
        _startRegion?.Unregister();

        _lowerRegion = null;
        _evilRegion = null;
        _goodRegion = null;
        _startRegion = null;
    }

    private void OnTick()
    {
        if (_nextBossEncounter == DateTime.MinValue || _nextBossEncounter > Core.Now)
        {
            return;
        }

        var good = GetArmyPower(Alignment.Good);
        var evil = GetArmyPower(Alignment.Evil);
        var strongest = Alignment.Neutral;

        if (good == 0 && evil == 0)
        {
            _nextBossEncounter = Core.Now + EncounterCheckDuration;
        }
        else
        {
            if (good > evil)
            {
                strongest = Alignment.Good;
            }
            else if (good < evil)
            {
                strongest = Alignment.Evil;
            }
            else
            {
                strongest = 0.5 > Utility.RandomDouble() ? Alignment.Good : Alignment.Evil;
            }
        }

        var players = new List<Mobile>();
        players.AddRange(_goodRegion.GetPlayers());
        players.AddRange(_evilRegion.GetPlayers());
        players.AddRange(_startRegion.GetPlayers());

        foreach (var m in players)
        {
            if (!m.Player)
            {
                continue;
            }

            var orb = GetWispOrb(m);
            m.PlaySound(0x66C);

            if (orb == null || orb.Alignment != strongest)
            {
                m.SendLocalizedMessage(strongest != Alignment.Neutral ? 1153334 : 1153333);
                // The Call to Arms has sounded, but your forces are not yet strong enough to heed it.
                // Your enemy forces are stronger, and they have been called to battle.
            }
            else if (orb.Alignment == strongest)
            {
                m.SendLocalizedMessage(1153332); // The Call to Arms has sounded. The forces of your alignment are strong, and you have been called to battle!

                if (orb.Conscripted)
                {
                    m.SendLocalizedMessage(1153337); // You will be teleported into the depths of the dungeon within 60 seconds to heed the Call to Arms, unless you release your conscripted creature or it dies.
                    _toTransport.Add(m);
                }
                else
                {
                    m.SendLocalizedMessage(1153338); // You have under 60 seconds to conscript a creature to answer the Call to Arms, or you will not be summoned for the battle.
                }
            }
        }

        if (strongest != Alignment.Neutral)
        {
            _sequenceAlignment = strongest;

            Timer.DelayCall(TimeSpan.FromSeconds(60), BeginSequence);
            _nextBossEncounter = DateTime.MinValue;
            _sequencing = true;
        }

        this.MarkDirty();
    }

    public int GetArmyPower(Alignment alignment)
    {
        var power = 0;

        foreach (var orb in WispOrb.Orbs)
        {
            if (orb.Conscripted && orb.Alignment == alignment)
            {
                power += orb.GetArmyPower();
            }
        }

        return power;
    }

    public void TryAddToArmy(WispOrb orb)
    {
        if (orb?.Owner != null && _sequencing && orb.Alignment == _sequenceAlignment && !_toTransport.Contains(orb.Owner))
        {
            _toTransport.Add(orb.Owner);
        }
    }

    private void CreateSpawners()
    {
        _goodSpawners = new List<BaseSpawner>();
        _evilSpawners = new List<BaseSpawner>();

        if (_lowerRegion == null)
        {
            return;
        }

        foreach (var item in _lowerRegion.GetItems())
        {
            if (item is BaseSpawner spawner && spawner.Name != null &&
                spawner.Name.Contains("despiserevamped", StringComparison.OrdinalIgnoreCase))
            {
                if (spawner.Name.Contains("despiserevamped good", StringComparison.OrdinalIgnoreCase))
                {
                    _goodSpawners.Add(spawner);
                }

                if (spawner.Name.Contains("despiserevamped evil", StringComparison.OrdinalIgnoreCase))
                {
                    _evilSpawners.Add(spawner);
                }
            }
        }
    }

    private void ResetSpawners(bool reset)
    {
        if (reset)
        {
            foreach (var spawner in _evilSpawners)
            {
                if (spawner.Running)
                {
                    spawner.Reset();
                }
            }

            foreach (var spawner in _goodSpawners)
            {
                if (spawner.Running)
                {
                    spawner.Reset();
                }
            }
        }
        else
        {
            var useList = _sequenceAlignment == Alignment.Good ? _evilSpawners : _goodSpawners;

            if (useList == null)
            {
                return;
            }

            foreach (var spawner in useList)
            {
                spawner.Running = true;
                spawner.Respawn();
            }

            // ServUO: ColUtility.Free(useList) - the controller then re-finds them on the next CreateSpawners.
            useList.Clear();
        }
    }

    private void BeginSequence()
    {
        _sequencing = false;

        if (_toTransport.Count == 0)
        {
            _nextBossEncounter = Core.Now + EncounterCheckDuration;
            _sequenceAlignment = Alignment.Neutral;
            this.MarkDirty();
            return;
        }

        _boss = _sequenceAlignment == Alignment.Good ? new AndrosTheDreadLord() : new AdrianTheGloriousLord();

        ResetSpawners(false);

        _boss.MoveToWorld(BossLocation, Map.Trammel);
        _deadLine = Core.Now + DeadLineDuration;

        BeginSequenceTimer();
        KickFromBossRegion(false);

        Timer.DelayCall(TimeSpan.FromSeconds(60), TransportPlayers);

        Timer.DelayCall(TimeSpan.FromSeconds(12), () => SendReadyMessage(1153339)); // You have been called to assist in a fight of good versus evil. Fight your way to the Lake, and defeat the enemy overlord and its lieutenants!
        Timer.DelayCall(TimeSpan.FromSeconds(24), () => SendReadyMessage(1153340)); // The Overlord is shielded from all attacks by players, but not by creatures possessed by Wisp Orbs. You must protect your controlled creature as it fights.
        Timer.DelayCall(TimeSpan.FromSeconds(36), () => SendReadyMessage(1153341)); // The Lieutenants are vulnerable to your attacks. If you die during this battle, your possessed creature will fall. Furthermore, your ghost and your corpse will be teleported back to your home base.

        this.MarkDirty();
    }

    private void EndSequence()
    {
        if (_boss?.Deleted == false)
        {
            _boss.Delete();
        }

        _boss = null;
        _playersInSequence = false;
        EndCleanupTimer();
        KickFromBossRegion(false);
        _sequenceAlignment = Alignment.Neutral;

        _deadLine = DateTime.MinValue;
        _toTransport.Clear();

        Timer.DelayCall(TimeSpan.FromSeconds(10), () => ResetSpawners(true));

        _nextBossEncounter = Core.Now + EncounterCheckDuration;
        this.MarkDirty();
    }

    private void OnSequenceTick()
    {
        if (_sequenceTimer != null && _deadLine < Core.Now && _lowerRegion != null)
        {
            EndSequenceTimer();
            SendRegionMessage(_lowerRegion, 1153348); // You were unable to defeat the enemy overlord in the time allotted. He has activated a Doom Spell!

            Timer.DelayCall(TimeSpan.FromSeconds(1), EndSequence);
        }
        else if (_playersInSequence && !HasPlayers(_lowerRegion))
        {
            EndSequenceTimer();
            Timer.DelayCall(TimeSpan.FromSeconds(1), EndSequence);
        }
    }

    public void OnBossSlain()
    {
        EndSequenceTimer();
        SendRegionMessage(_lowerRegion, 1153343); // The battle has ended. The battlefield will be cleared in five minutes and you will be returned to your home base at that time.

        BeginCleanupTimer();
    }

    private static void SendRegionMessage(DespiseRegion region, int cliloc)
    {
        if (region == null)
        {
            return;
        }

        foreach (var m in region.GetPlayers())
        {
            m.SendLocalizedMessage(cliloc);
        }
    }

    private void KickFromBossRegion(bool deletepet)
    {
        if (_lowerRegion == null)
        {
            return;
        }

        var mobiles = _lowerRegion.GetPlayers();
        var bounds = _sequenceAlignment == Alignment.Evil ? EvilKickBounds : GoodKickBounds;

        foreach (var m in mobiles)
        {
            var orb = GetWispOrb(m);
            var p = GetRandomLoc(bounds);

            m.MoveToWorld(p, Map.Trammel);

            if (orb != null && deletepet)
            {
                if (orb.Pet != null)
                {
                    orb.Pet.Delete();
                    orb.Pet = null;
                }

                orb.Delete();
                m.SendLocalizedMessage(1153312); // The Wisp Orb dissolves into aether.
            }
            else if (orb?.Pet?.Alive == true)
            {
                orb.Pet.MoveToWorld(p, Map.Trammel);
            }

            m.SendLocalizedMessage(1153346); // You are summoned back to your stronghold.
        }
    }

    private void TransportPlayers()
    {
        var list = new List<Mobile>(_toTransport);

        foreach (var m in list)
        {
            var orb = GetWispOrb(m);

            if (orb == null || orb.Deleted || !orb.Conscripted || m.Region?.IsPartOf<DespiseRegion>() != true)
            {
                _toTransport.Remove(m);
            }
        }

        if (_toTransport.Count == 0)
        {
            EndSequenceTimer();
            EndSequence();
        }
        else
        {
            foreach (var m in _toTransport)
            {
                if (m?.Region?.IsPartOf<DespiseRegion>() == true)
                {
                    var orb = GetWispOrb(m);

                    if (orb?.Pet?.Alive == true)
                    {
                        var p = GetRandomLoc(BossEntranceLocation);
                        m.MoveToWorld(p, Map.Trammel);
                        orb.Pet.MoveToWorld(p, Map.Trammel);

                        m.SendLocalizedMessage(1153280, "You!"); // Your possessed creature is now anchored to ~1_NAME~
                        orb.Anchor = m;
                        orb.Pet.ControlTarget = m;
                        orb.Pet.ControlOrder = OrderType.Follow;
                    }
                }
            }

            _playersInSequence = true;
        }
    }

    public bool HasPlayers(Region r) => r != null && r.GetPlayerCount() > 0;

    private static Point3D GetRandomLoc(Rectangle2D rec)
    {
        var map = Map.Trammel;
        var p = new Point3D(rec.X, rec.Y, map.GetAverageZ(rec.X, rec.Y));

        for (var i = 0; i < 50; i++)
        {
            var x = Utility.RandomMinMax(rec.X, rec.X + rec.Width);
            var y = Utility.RandomMinMax(rec.Y, rec.Y + rec.Height);
            var z = map.GetAverageZ(x, y);

            if (map.CanSpawnMobile(x, y, z))
            {
                p = new Point3D(x, y, z);
                break;
            }
        }

        return p;
    }

    public void BeginSequenceTimer()
    {
        EndSequenceTimer();

        _sequenceTimer = Timer.DelayCall(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), OnSequenceTick);
    }

    public void EndSequenceTimer()
    {
        _sequenceTimer?.Stop();
        _sequenceTimer = null;
    }

    public void BeginCleanupTimer()
    {
        EndCleanupTimer();
        _cleanupTimer = Timer.DelayCall(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5), EndSequence);

        if (_lowerRegion == null)
        {
            return;
        }

        foreach (var m in _lowerRegion.GetMobiles())
        {
            if (m is DespiseCreature { Orb: not null })
            {
                m.Delete();
            }
        }
    }

    public void EndCleanupTimer()
    {
        _cleanupTimer?.Stop();
        _cleanupTimer = null;
    }

    // ServUO: EventSink.Login. A player who logs back in inside the lower level while no encounter is
    // running is put back at their side's stronghold, with their possessed creature.
    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile from)
    {
        var controller = Instance;

        if (controller?.LowerRegion == null)
        {
            return;
        }

        if (from.Region?.IsPartOf(controller.LowerRegion) == true && !controller.IsInSequence)
        {
            var orb = GetWispOrb(from);
            var bounds = EvilKickBounds;

            if (orb?.Alignment == Alignment.Good)
            {
                bounds = GoodKickBounds;
            }

            while (true)
            {
                var x = Utility.RandomMinMax(bounds.X, bounds.X + bounds.Width);
                var y = Utility.RandomMinMax(bounds.Y, bounds.Y + bounds.Height);
                var z = Map.Trammel.GetAverageZ(x, y);

                if (Map.Trammel.CanSpawnMobile(x, y, z))
                {
                    from.MoveToWorld(new Point3D(x, y, z), Map.Trammel);

                    if (orb?.Pet?.Alive == true)
                    {
                        orb.Pet.MoveToWorld(new Point3D(x, y, z), Map.Trammel);
                    }

                    break;
                }
            }
        }
    }

    // ServUO: EventSink.OnEnterRegion. When an orb's owner is no longer standing in any Despise region -
    // walked out, recalled out, or was internalized on logout - the orb vanishes, which releases the creature.
    // Called from DespiseRegion.OnExit, after the mobile's location has moved on.
    public static void OnLeaveDespise(Mobile m)
    {
        var orb = GetWispOrb(m);

        if (orb != null && !Region.Find(m.Location, m.Map).IsPartOf<DespiseRegion>())
        {
            Timer.DelayCall(
                () =>
                {
                    if (orb.Deleted)
                    {
                        return;
                    }

                    m.SendLocalizedMessage(1153233); // The Wisp Orb vanishes to whence it came...
                    orb.Delete();
                }
            );
        }
    }

    private void SendReadyMessage(int cliloc)
    {
        foreach (var m in _toTransport)
        {
            m.SendLocalizedMessage(cliloc);
        }
    }

    public static Rectangle2D[] EvilBounds { get; } = { new(5381, 644, 149, 120) };

    public static Rectangle2D[] GoodBounds { get; } = { new(5380, 515, 134, 121) };

    public static Rectangle2D[] LowerLevelBounds { get; } = { new(5379, 771, 247, 250) };

    private static readonly Rectangle2D EvilKickBounds = new(5500, 571, 20, 5);
    private static readonly Rectangle2D GoodKickBounds = new(5484, 567, 15, 8);
    private static readonly Rectangle2D BossEntranceLocation = new(5391, 855, 13, 15);

    private static readonly Point3D BossLocation = new(5556, 823, 45);

    // The stock healing ankh at the top of the good side; the revamp replaces it with a DespiseAnkh.
    public static void RemoveAnkh()
    {
        var toDelete = new List<Item>();

        foreach (var item in Map.Trammel.GetItemsInRange(new Point3D(5474, 525, 79), 3))
        {
            if (item is RejuvinationAddonComponent && !item.Deleted)
            {
                toDelete.Add(item);
            }
        }

        foreach (var item in toDelete)
        {
            item.Delete();
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _evilSpawners = new List<BaseSpawner>();
        _goodSpawners = new List<BaseSpawner>();

        Instance = this;

        Timer.DelayCall(CreateSpawners);

        if (!_enabledValue)
        {
            return;
        }

        BeginTimer();

        if (_deadLine > Core.Now)
        {
            if (_boss?.Alive == true)
            {
                BeginSequenceTimer();
                return;
            }
        }
        else if (_deadLine != DateTime.MinValue)
        {
            BeginCleanupTimer();
            return;
        }

        Timer.DelayCall(EndSequence);
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        EndTimer();
        EndSequenceTimer();
        EndCleanupTimer();

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
