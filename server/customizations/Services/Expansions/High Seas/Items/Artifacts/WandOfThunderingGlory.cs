// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Artifacts/WandOfThunderingGlory.cs -> ours, the High
// Seas fishing port. An Osiredon unique artifact: a wand with no spell and no charges, fought with as a mace, doing 100%
// chaos damage.
//
// ServUO constructs it as BaseWand(WandEffect.None, 0, 0). Pinned's WandEffect has no None, so the wand carries
// Clumsiness with zero charges: pinned BaseWand casts nothing at zero charges and the name is this label, so the effect
// is never seen. Recorded in the notes.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class WandOfThunderingGlory : BaseWand
{
    [Constructible]
    public WandOfThunderingGlory() : base(WandEffect.Clumsiness, 0, 0)
    {
        WeaponAttributes.HitLightning = 40;
        Attributes.AttackChance = 5;
        Attributes.WeaponSpeed = 10;
        Attributes.WeaponDamage = 50;
        Attributes.SpellDamage = 10;
    }

    public override int LabelNumber => 1116623; // Wand of Thundering Glory

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
