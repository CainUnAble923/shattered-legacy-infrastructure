// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Items/PrismaticLenses.cs (cc-P194).
//
// Glasses, hue 2068: hit lower defence 30, hit point regeneration 2, stamina regeneration 3, damage 25, resists
// 18/4/7/17/6, 255 hit points. A void spawn drop.
//
// ServUO's Glasses base (Items/Artifacts/Equipment/Glasses/Glasses.cs) is pinned's ElvenGlasses member for member
// (0x2FB8, weight 2, strength 45/40, armour 30, leather, meditation allowed, weapon attributes with their
// property lines): the "already in ModernUO under another name" case, so the lenses derive from it and only
// the label changes. Deviation (notes): GargishPrismaticLenses needs a GargishGlasses base pinned does not have;
// [Alterable] has no pinned counterpart.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class PrismaticLenses : ElvenGlasses
{
    [Constructible]
    public PrismaticLenses()
    {
        Hue = 2068;
        WeaponAttributes.HitLowerDefend = 30;
        Attributes.RegenHits = 2;
        Attributes.RegenStam = 3;
        Attributes.WeaponDamage = 25;
    }

    public override int LabelNumber => 1152716; // Prismatic Lenses

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;

    public override int BasePhysicalResistance => 18;
    public override int BaseFireResistance => 4;
    public override int BaseColdResistance => 7;
    public override int BasePoisonResistance => 17;
    public override int BaseEnergyResistance => 6;
}
