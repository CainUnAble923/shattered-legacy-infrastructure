// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Items/Brightblade.cs (cc-P194).
//
// A katana, hue 1756, 100% fire: hit stamina leech 100, stamina regeneration 3, hit chance 10, faster casting 1,
// swing speed 40, damage 50, 255 hit points. A void spawn drop.
//
// Deviations (notes): WeaponAttributes.SplinteringWeapon 20 has no pinned member; GargishBrightblade needs a
// GargishKatana base pinned does not have, and the [Alterable] attribute (ServUO's Blacksmithy alteration) has no
// pinned counterpart.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class Brightblade : Katana
{
    [Constructible]
    public Brightblade()
    {
        WeaponAttributes.HitLeechStam = 100;
        Attributes.RegenStam = 3;
        Attributes.AttackChance = 10;
        Attributes.CastSpeed = 1;
        Attributes.WeaponSpeed = 40;
        Attributes.WeaponDamage = 50;
        AosElementDamages.Fire = 100;
        Hue = 1756;
    }

    public override int LabelNumber => 1152732; // Brightblade

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;
}
