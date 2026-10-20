// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Artifacts/CullingBlade.cs -> ours, the High Seas
// fishing port. A Charybdis shared artifact: a bone harvester doing 100% chaos damage.
//
// Dropped: WeaponAttributes.HitManaDrain = 30 and HitFatigue = 30. Pinned's AosWeaponAttribute has neither (Misc/AOS.cs:755-
// 790: HitLeechMana is the leech, not the drain), as TheImpalersPick found before this. Recorded in the notes; reverses when
// the two Stygian Abyss hit effects exist.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class CullingBlade : BoneHarvester
{
    [Constructible]
    public CullingBlade()
    {
        WeaponAttributes.HitLowerDefend = 40;
        Attributes.RegenHits = 3;
        Attributes.WeaponSpeed = 20;
        Attributes.WeaponDamage = 50;
    }

    public override int LabelNumber => 1116630; // Culling Blade

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
