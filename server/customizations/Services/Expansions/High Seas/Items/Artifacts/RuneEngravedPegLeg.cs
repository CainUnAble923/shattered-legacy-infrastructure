// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Artifacts/RuneEngravedPegLeg.cs -> ours, the High
// Seas fishing port. A Charybdis shared artifact: a club doing 100% chaos damage.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class RuneEngravedPegLeg : Club
{
    [Constructible]
    public RuneEngravedPegLeg()
    {
        WeaponAttributes.HitLightning = 40;
        WeaponAttributes.HitLowerDefend = 40;
        Attributes.RegenHits = 3;
        Attributes.AttackChance = 5;
        Attributes.WeaponSpeed = 30;
        Attributes.WeaponDamage = 50;
    }

    public override int LabelNumber => 1116622; // Rune Engraved Peg Leg

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;

    public override void GetDamageTypes(
        Mobile wielder, out int phys, out int fire, out int cold, out int pois, out int nrgy, out int chaos, out int direct
    )
    {
        phys = fire = cold = nrgy = pois = direct = 0;
        chaos = 100;
    }
}
