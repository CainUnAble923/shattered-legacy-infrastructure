// cc-P199: ServUO pub57 Scripts/Items/Functional/AcidVine.cs. The magic vines over a wall (art 3313, immovable): a
// double-click within one tile says the vines stop you opening the door, within four that something about them is
// odd, and further away that you cannot reach it. It does nothing else; ServUO places none in its decoration data
// (only Data/objects.xml lists it, for the [add menu).
//
// One change, invisible to a player: the 1.0 stone weight is DefaultWeight (pinned's shape) rather than a Weight
// assignment in the constructor (AcidVine.cs:14).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class AcidVine : Item
{
    [Constructible]
    public AcidVine() : base(3313) => Movable = false;

    public override int LabelNumber => 1111655; // magic vines
    public override bool ForceShowProperties => ObjectPropertyList.Enabled;
    public override double DefaultWeight => 1.0;

    public override void OnDoubleClick(Mobile from)
    {
        if (from.InRange(GetWorldLocation(), 1))
        {
            from.SendLocalizedMessage(1111658); // The vines tighten their grip, stopping you from opening the door.
        }
        else if (from.InRange(GetWorldLocation(), 4))
        {
            from.SendLocalizedMessage(1111665); // You notice something odd about the vines covering the wall.
        }
        else
        {
            from.SendLocalizedMessage(1019045); // I can't reach that.
        }

        base.OnDoubleClick(from);
    }
}
