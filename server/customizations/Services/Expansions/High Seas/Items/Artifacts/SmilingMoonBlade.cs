// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Artifacts/SmilingMoonBlade.cs -> ours, the High Seas
// fishing port. An Osiredon unique artifact: a crescent blade doing 100% cold damage.
//
// Dropped: WeaponAttributes.BattleLust = 1 and HitManaDrain = 10. Pinned has no BattleLust weapon attribute (its one mention
// is a TODO comment in Items/Weapons/Throwing/StormCaller.cs:12-13, where pinned's own Storm Caller drops it too) and no
// HitManaDrain (Misc/AOS.cs:755-790). Recorded in the notes; reverses when those attributes exist.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SmilingMoonBlade : CrescentBlade
{
    [Constructible]
    public SmilingMoonBlade()
    {
        Hue = 2567;
        WeaponAttributes.HitFireball = 45;
        WeaponAttributes.HitLowerDefend = 40;
        Attributes.WeaponSpeed = 30;
        Attributes.WeaponDamage = 45;
    }

    public override int LabelNumber => 1116628; // Smiling Moon Blade

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;

    public override void GetDamageTypes(
        Mobile wielder, out int phys, out int fire, out int cold, out int pois, out int nrgy, out int chaos, out int direct
    )
    {
        phys = fire = chaos = nrgy = pois = direct = 0;
        cold = 100;
    }
}
