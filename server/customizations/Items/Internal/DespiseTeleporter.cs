// ServUO: Items/Internal/DespiseTeleporter.cs (CC4 Despise). Three types, as ServUO keeps them, except that
// ServUO nests InternalTeleporter inside GateTeleporter and it is a top-level GateInternalTeleporter here
// (the serialization generator works on top-level partials).
//
// DespiseTeleporter replaces the stock teleporters at the dungeon mouth so that a possessed creature
// cannot ride along when its master leaves: CanTeleport refuses DespiseCreatures, and its own TeleportPets
// skips them where the stock Teleporter.DoTeleport would move every following pet within three tiles.
//
// GateTeleporter is the six blue gates between the good side, the evil side and the lower level. It is a
// static gate graphic (0x0F6C) with eight invisible teleporters around it, one per direction, so that
// walking onto the gate from any side arrives at the destination. Destination changes fan out to the eight.
//
// Conversion notes: PointDest/MapDest/SoundID/SourceEffect/DestEffect exist unchanged on ModernUO's
// Teleporter; Effects.SendLocationEffect takes four arguments; Movement.Offset is Server.Movement.Movement;
// ColUtility.Free is a List.Clear; Delete() overrides become OnAfterDelete().

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.Despise;
using Server.Mobiles;
using Server.Movement;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class DespiseTeleporter : Teleporter
{
    [Constructible]
    public DespiseTeleporter()
    {
    }

    public override bool CanTeleport(Mobile m)
    {
        if (m is DespiseCreature)
        {
            return false;
        }

        return base.CanTeleport(m);
    }

    public override void DoTeleport(Mobile m)
    {
        var map = MapDest;

        if (map == null || map == Map.Internal)
        {
            map = m.Map;
        }

        var p = PointDest;

        if (p == Point3D.Zero)
        {
            p = m.Location;
        }

        TeleportPets(m, p, map);

        var sendEffect = !m.Hidden || m.AccessLevel == AccessLevel.Player;

        if (SourceEffect && sendEffect)
        {
            Effects.SendLocationEffect(m.Location, m.Map, 0x3728, 10);
        }

        m.MoveToWorld(p, map);

        if (DestEffect && sendEffect)
        {
            Effects.SendLocationEffect(m.Location, m.Map, 0x3728, 10);
        }

        if (SoundID > 0 && sendEffect)
        {
            Effects.PlaySound(m.Location, m.Map, SoundID);
        }
    }

    public static void TeleportPets(Mobile master, Point3D loc, Map map)
    {
        var move = new List<Mobile>();

        foreach (var m in master.GetMobilesInRange(3))
        {
            if (m is BaseCreature pet && m is not DespiseCreature && pet.Controlled && pet.ControlMaster == master &&
                pet.ControlOrder is OrderType.Guard or OrderType.Follow or OrderType.Come)
            {
                move.Add(pet);
            }
        }

        foreach (var m in move)
        {
            m.MoveToWorld(loc, map);
        }
    }
}

[SerializationGenerator(0, false)]
public partial class GateTeleporter : Item
{
    // _destination and _destinationMap are declared by the serialization generator from the
    // [SerializableProperty] members below.

    [SerializableField(2)]
    private List<GateInternalTeleporter> _teleporters;

    [Constructible]
    public GateTeleporter() : this(19343, 0, Point3D.Zero, null)
    {
    }

    public GateTeleporter(int id, int hue, Point3D destination, Map destinationMap) : base(id)
    {
        Hue = hue;

        Movable = false;

        _destination = destination;
        _destinationMap = destinationMap;

        AssignTeleporters();
    }

    [SerializableProperty(0)]
    [CommandProperty(AccessLevel.GameMaster)]
    public Point3D Destination
    {
        get => _destination;
        set
        {
            if (_destination != value)
            {
                _destination = value;
                AssignDestination(value);
            }

            this.MarkDirty();
        }
    }

    [SerializableProperty(1)]
    [CommandProperty(AccessLevel.GameMaster)]
    public Map DestinationMap
    {
        get => _destinationMap;
        set
        {
            if (_destinationMap != value)
            {
                _destinationMap = value;
                AssignMap(value);
            }

            this.MarkDirty();
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (_teleporters == null)
        {
            return;
        }

        for (var i = _teleporters.Count - 1; i >= 0; i--)
        {
            var tele = _teleporters[i];

            if (tele?.Deleted != false)
            {
                _teleporters.RemoveAt(i);
            }
            else
            {
                tele.Master = this;
            }
        }
    }

    private void AssignTeleporters()
    {
        if (_teleporters != null)
        {
            foreach (var tele in _teleporters)
            {
                if (tele?.Deleted == false)
                {
                    tele.Delete();
                }
            }

            _teleporters.Clear();
        }

        _teleporters = new List<GateInternalTeleporter>();

        for (var i = 0; i <= 7; i++)
        {
            var offset = (Direction)i;

            var tele = new GateInternalTeleporter(this, _destination, _destinationMap);

            var x = X;
            var y = Y;
            var z = Z;

            Server.Movement.Movement.Offset(offset, ref x, ref y);
            tele.MoveToWorld(new Point3D(x, y, z), Map);

            _teleporters.Add(tele);
        }

        this.MarkDirty();
    }

    public void AssignDestination(Point3D p)
    {
        if (_teleporters == null)
        {
            AssignTeleporters();
        }
        else
        {
            _teleporters.ForEach(t => t.PointDest = p);
        }
    }

    public void AssignMap(Map map)
    {
        if (_teleporters == null)
        {
            AssignTeleporters();
        }
        else
        {
            _teleporters.ForEach(t => t.MapDest = map);
        }
    }

    public override void OnMapChange()
    {
        base.OnMapChange();

        _teleporters?.ForEach(t => t.Map = Map);
    }

    public override void OnLocationChange(Point3D oldLocation)
    {
        base.OnLocationChange(oldLocation);

        _teleporters?.ForEach(t =>
        {
            t.Location = new Point3D(X + (t.X - oldLocation.X), Y + (t.Y - oldLocation.Y), Z + (t.Z - oldLocation.Z));
        });
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        if (_teleporters != null)
        {
            foreach (var t in _teleporters)
            {
                t?.Delete();
            }

            _teleporters.Clear();
        }
    }
}

[SerializationGenerator(0, false)]
public partial class GateInternalTeleporter : Teleporter
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private GateTeleporter _master;

    public GateInternalTeleporter(GateTeleporter master, Point3D dest, Map destMap) : base(dest, destMap, true) =>
        _master = master;

    public override bool OnMoveOver(Mobile m) => true;

    public override bool HandlesOnMovement =>
        _master != null && Utility.InRange(_master.Location, Location, 1) && Map == _master.Map;

    public override void OnMovement(Mobile m, Point3D oldLocation)
    {
        if (_master == null || _master.Destination == Point3D.Zero || _master.Map == null || _master.Map == Map.Internal)
        {
            return;
        }

        if (m.Location == Location)
        {
            foreach (var item in Map.GetItemsInRange(oldLocation, 0))
            {
                if (item is GateInternalTeleporter || item == _master)
                {
                    return;
                }
            }

            base.OnMoveOver(m);
        }
    }
}
