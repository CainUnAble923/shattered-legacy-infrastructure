// ServUO: Services/Revamped Dungeons/Shame Revamped/ShameAltar.cs (CC4 Shame).
//
// The Guardian's Altar on each of the three levels (two facets, six altars). A player with at least SummonCost
// Crystals of Shame double-clicks it to summon the level's guardian at SpawnLocation; they then have one
// hour, and only the summoner (or their pets, or anyone in the last ten minutes) can hurt it. Killing it
// starts a ten-minute cooldown. The altar also owns a ShameTeleporter at TeleporterLocation, re-placed a
// second after the altar is constructed and whenever a summon finds it missing.
//
// The crystal points are Services/PointsSystems (S7), not on this shard: CheckSummon goes through the
// ShameCrystals seam, whose GetPoints returns 0 until S7 lands, so every altar answers 1151623 "You are not
// yet worthy". A GM can [props an altar's SummonCost to 0 to run a fight. Deviation in notes/cc4-shame.md.
//
// One deliberate deviation (notes/cc4-shame.md): when the hour runs out, ServUO's EndDeadLineTimer deletes the
// guardian but leaves the Guardian reference set, so every later CheckSummon on that altar answers 1151621
// "already been summoned" until the server restarts (Deserialize is the only place that clears it). Read, not
// run, and one line: Guardian = null after the Delete. Reversible by removing that line.
//
// Conversion: the ten serialized members are generator fields in ServUO's order; GuardianType is serialized
// as a Type (SmallBOD.cs:20 precedent) rather than ServUO's name string + ScriptCompiler.FindTypeByName.
// NextSummon is not serialized on ServUO either. ServUO's Deserialize tail is [AfterDeserialization]. The
// deadline timer is a TimerExecutionToken. OnAfterDelete deletes the altar's teleporter and a live guardian,
// which ServUO leaves behind when [DeleteShame removes the altar (GM tooling, paper-only).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.Points;
using Server.Mobiles;

namespace Server.Engines.ShameRevamped;

[SerializationGenerator(0, false)]
public partial class ShameAltar : Item
{
    public static readonly int CoolDown = 10;
    public static readonly bool AllowParties = false;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ShameTeleporter _teleporter;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Point3D _teleporterLocation;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Point3D _teleporterDestination;

    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Point3D _spawnLocation;

    [SerializableField(4)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ShameGuardian _guardian;

    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Type _guardianType;

    [SerializableField(6)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _active;

    [SerializableField(7)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _deadLine;

    [SerializableField(8)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _summonCost;

    [SerializableField(9)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _summoner;

    private TimerExecutionToken _deadLineTimer;

    [CommandProperty(AccessLevel.GameMaster)]
    public DateTime NextSummon { get; set; }

    public bool DeadLineTimerRunning => _deadLineTimer.Running;

    public override int LabelNumber => 1151636; // Guardian's Altar
    public override bool ForceShowProperties => true;

    public ShameAltar(Type type, Point3D teleLoc, Point3D teleDest, Point3D spawnLoc, int cost, bool active = true)
        : base(13801)
    {
        Movable = false;
        Hue = 2619;

        _guardianType = type;
        _teleporterLocation = teleLoc;
        _teleporterDestination = teleDest;
        _spawnLocation = spawnLoc;
        _summonCost = cost;

        Timer.StartTimer(TimeSpan.FromSeconds(1), SpawnTeleporter);

        _active = active;
        _deadLine = DateTime.MinValue;
        NextSummon = Core.Now;
    }

    public override void OnMapChange()
    {
        base.OnMapChange();

        if (_teleporter != null)
        {
            _teleporter.Map = Map;
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (Active && from.InRange(Location, 3))
        {
            CheckSummon(from);
        }
    }

    public void CheckSummon(Mobile from)
    {
        if (ShameCrystals.GetPoints(from) < SummonCost)
        {
            from.SendLocalizedMessage(1151623); // You are not yet worthy of challenging the champion.
        }
        else if (Guardian != null)
        {
            from.SendLocalizedMessage(1151621); // The champion for this dungeon level has already been summoned.
        }
        else if (NextSummon > Core.Now)
        {
            from.SendLocalizedMessage(1151622); // The champion has recently been defeated, and cannot be summoned again for a few minutes.
        }
        else
        {
            SpawnGuardian();
            Summoner = from;

            ShameCrystals.DeductPoints(from, SummonCost);

            if (_teleporter?.Deleted != false)
            {
                SpawnTeleporter();
            }

            from.SendLocalizedMessage(1151620); // The champion accepts your challenge. You have one hour to find and defeat him!
            from.SendLocalizedMessage(1151625, SummonCost.ToString()); // Summoning the dungeon level's champion costs you ~1_COST~ Crystals of Shame.
            StartDeadlineTimer();
        }
    }

    public void SpawnGuardian()
    {
        Guardian = Activator.CreateInstance(GuardianType) as ShameGuardian;
        Guardian.Altar = this;
        Guardian.MoveToWorld(SpawnLocation, Map);

        Guardian.Home = SpawnLocation;
        Guardian.RangeHome = 8;

        DeadLine = Core.Now + TimeSpan.FromHours(1);
    }

    public void OnGuardianKilled()
    {
        Guardian = null;

        EndDeadLineTimer();
        NextSummon = Core.Now + TimeSpan.FromMinutes(CoolDown);
        Summoner = null;
    }

    public void CheckDeadLine()
    {
        if (DeadLine < Core.Now)
        {
            EndDeadLineTimer();
        }
    }

    public void StartDeadlineTimer()
    {
        _deadLineTimer.Cancel();
        Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), CheckDeadLine, out _deadLineTimer);
    }

    public void EndDeadLineTimer()
    {
        _deadLineTimer.Cancel();

        if (Guardian?.Alive == true) // Failed
        {
            Guardian.Delete();
            Guardian = null; // NOT on ServUO: see the header. Without it the altar answers "already summoned" until restart.

            Summoner?.SendLocalizedMessage(1151628); // You failed to defeat the champion in time.

            NextSummon = Core.Now;
        }
    }

    private void SpawnTeleporter()
    {
        _teleporter?.Delete();

        if (Map == null || Map == Map.Internal)
        {
            return;
        }

        var old = new List<Item>();

        foreach (var item in Map.GetItemsInRange(TeleporterLocation, 0))
        {
            if (item is ShameTeleporter)
            {
                old.Add(item);
            }
        }

        foreach (var item in old)
        {
            item.Delete();
        }

        Teleporter = new ShameTeleporter(TeleporterDestination, Map);
        Teleporter.MoveToWorld(TeleporterLocation, Map);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (DeadLine > Core.Now)
        {
            if (Guardian != null)
            {
                Guardian.Altar = this;
                StartDeadlineTimer();
            }
            else
            {
                DeadLine = DateTime.MinValue;
            }
        }
        else if (Guardian != null)
        {
            Guardian.Delete();
            Guardian = null;

            NextSummon = Core.Now;
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        _deadLineTimer.Cancel();
        _teleporter?.Delete();

        if (_guardian?.Deleted == false)
        {
            _guardian.Delete();
        }
    }
}
