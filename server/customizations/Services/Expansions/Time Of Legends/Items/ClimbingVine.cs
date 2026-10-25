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
//
// Added (cc-P458, Crystal's P428 #2): a paralyzed, frozen or casting player cannot climb ("You cannot use that
// while paralyzed.", 1075857, pinned's own refusal for the same three states in Firebomb.cs:33-37). ServUO has no
// check, so one double-click lifted a held player 40-60 tiles up the cliff, out of a fight.

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

        if (from.Paralyzed || from.Frozen || from.Spell?.IsCasting == true)
        {
            from.SendLocalizedMessage(1075857); // You cannot use that while paralyzed.
            return;
        }

        if (_climbLocation != Point3D.Zero && from.InRange(GetWorldLocation(), 2) && Z >= from.Z)
        {
            from.MoveToWorld(_climbLocation, Map);
        }
    }
}
