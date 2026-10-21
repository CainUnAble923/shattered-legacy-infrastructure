// ServUO: Services/Revamped Dungeons/WrongDungeon/Items/WrongBedrolls.cs (cc-P194).
//
// The prisoners' bedrolls in the cell blocks (laid by BedrollSpawner). Double-click within two tiles: a bedroll
// with a destination opens a mysterious tunnel on its tile that leads back outside; any other bedroll is torn
// apart: half the time it is a trap (40 fire damage, one of three effects), otherwise one time in a hundred a bar
// of soap (into the pack, or dropped at the bedroll), else "You tear the bedroll to pieces but find nothing."
// Either way the bedroll is gone until the spawner re-lays it.
//
// Conversion notes: ServUO's private SendMessageTo helpers (a MessageLocalized packet from the item) are
// Item.LabelTo here; the generator carries the spawner and the destination.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class WrongBedrollBase : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private BedrollSpawner _bedrollSpawner;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Point3D _pointDest;

    [Constructible]
    public WrongBedrollBase(int id) : base(id)
    {
        Movable = false;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(GetWorldLocation(), 2))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        from.RevealingAction();

        if (_pointDest != Point3D.Zero)
        {
            var mt = new MysteriousTunnel();
            Effects.PlaySound(from.Location, from.Map, 0x3BD);

            mt.PointDest = _pointDest;
            mt.MoveToWorld(Location, Map);
            _bedrollSpawner?.AddTunnel(mt);
        }
        else if (Utility.RandomDouble() < 0.5)
        {
            var loc = GetWorldLocation();
            var facet = Map;

            LabelTo(from, 502999); // You set off a trap!
            AOS.Damage(from, 40, 0, 100, 0, 0, 0);

            switch (Utility.Random(3))
            {
                case 0:
                    {
                        Effects.SendLocationEffect(loc, facet, 0x36BD, 15, 10);
                        Effects.PlaySound(loc, facet, 0x307);
                        break;
                    }
                case 1:
                    {
                        Effects.PlaySound(loc, facet, 0x307);
                        Effects.SendLocationEffect(new Point3D(loc.X - 1, loc.Y, loc.Z), facet, 0x36BD, 15);
                        Effects.SendLocationEffect(new Point3D(loc.X + 1, loc.Y, loc.Z), facet, 0x36BD, 15);
                        Effects.SendLocationEffect(new Point3D(loc.X, loc.Y - 1, loc.Z), facet, 0x36BD, 15);
                        Effects.SendLocationEffect(new Point3D(loc.X, loc.Y + 1, loc.Z), facet, 0x36BD, 15);
                        Effects.SendLocationEffect(new Point3D(loc.X + 1, loc.Y + 1, loc.Z + 11), facet, 0x36BD, 15);
                        break;
                    }
                case 2:
                    {
                        Effects.SendLocationEffect(loc, facet, 0x113A, 10, 20);
                        Effects.PlaySound(loc, facet, 0x231);
                        break;
                    }
            }
        }
        else if (Utility.RandomDouble() < 0.01)
        {
            var soap = new Soap();

            Effects.PlaySound(from.Location, from.Map, 0x247); // powder

            if (Utility.RandomBool())
            {
                from.AddToBackpack(soap);
                from.SendLocalizedMessage(1152268, "soap"); // You find ~1_ITEM~ and put it in your pack.
            }
            else
            {
                soap.MoveToWorld(Location, Map);
            }
        }
        else
        {
            Effects.PlaySound(from.Location, from.Map, 0x3E3); // leather
            from.SendLocalizedMessage(1152212); // You tear the bedroll to pieces but find nothing.
        }

        Delete();
    }
}

[SerializationGenerator(0, false)]
public partial class WrongBedrollSouth : WrongBedrollBase
{
    [Constructible]
    public WrongBedrollSouth() : base(0x0A56)
    {
    }

    public override int LabelNumber => 1022645; // bedroll
}

[SerializationGenerator(0, false)]
public partial class WrongBedrollEast : WrongBedrollBase
{
    [Constructible]
    public WrongBedrollEast() : base(0xA55)
    {
    }

    public override int LabelNumber => 1022645; // bedroll
}
