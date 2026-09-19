// ServUO: Items/Artifacts/Equipment/Armor/BouraTailShield.cs (CC6 batch 2). Values verbatim; serialization by
// the generator. WoodenKiteShield is the final parent: no set member anywhere in the file, so no carrier.
// Dropped: IsArtifact (D-1: no consumer in pinned ModernUO); ArmorAttributes.ReactiveParalyze = 1 (D-49:
// pinned AosArmorAttribute has LowerStatReq, SelfRepair, MageArmor, DurabilityBonus and nothing else,
// UOContent/Misc/AOS.cs:892). Attributes.ReflectPhysical exists and is kept.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BouraTailShield : WoodenKiteShield
{
    [Constructible]
    public BouraTailShield()
    {
        Hue = 554;
        Attributes.ReflectPhysical = 10;
    }

    public override int LabelNumber => 1112361; // boura tail shield

    public override int BasePhysicalResistance => 8;
    public override int BaseFireResistance => 0;
    public override int BaseColdResistance => 0;
    public override int BasePoisonResistance => 0;
    public override int BaseEnergyResistance => 1;

    public override int InitMinHits => 255;
    public override int InitMaxHits => 255;
}
