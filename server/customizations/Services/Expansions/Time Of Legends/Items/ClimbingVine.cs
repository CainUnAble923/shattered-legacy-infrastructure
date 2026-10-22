// ServUO: Services/Expansions/Time Of Legends/Items/ClimbingVine.cs (cc-P198). Vines on an Eodon cliff: double-click
// says "*The vines looks as though they may be strong enough to support climbing...*" (1156290) and, within 2
// tiles and not above the vine, climbs to ClimbLocation on the same facet. ClimbLocation is the one persisted field
// ([SerializableField(0)], ServUO's version 0 + the Point3D it writes).
//
// Placement moved: ServUO's Initialize places the two vines on Ter Mur (678,1759 at z 40 and z 60, both climbing to
// 679,1757,100) at every startup under Core.TOL. It looks for existing ones at 687,1759 (ClimbingVine.cs:68-69),
// nine tiles from where it puts them, so it never finds them and adds another pair on every restart. Here the two
// vines are part of [GenEodon (EodonGeneration), tagged, and a re-run replaces them; nothing is placed at startup,
// so no world gets them without the command. The coordinates are ServUO's.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ClimbingVine : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Point3D _climbLocation;

    [Constructible]
    public ClimbingVine() : this(Point3D.Zero)
    {
    }

    [Constructible]
    public ClimbingVine(Point3D p) : base(0x1AA1) => _climbLocation = p;

    public override int LabelNumber => 1023307; // vines

    public override void OnDoubleClick(Mobile from)
    {
        // *The vines looks as though they may be strong enough to support climbing...*
        from.SayTo(from, 1156290, 1153);

        if (_climbLocation != Point3D.Zero && from.InRange(GetWorldLocation(), 2) && Z >= from.Z)
        {
            from.MoveToWorld(_climbLocation, Map);
        }
    }
}
