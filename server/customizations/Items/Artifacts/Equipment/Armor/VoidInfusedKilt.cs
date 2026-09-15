using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/VoidInfusedKilt.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // GargishPlateKilt (batch 3) is on BaseSetArmor, so AbsorptionAttributes resolves.
    [SerializationGenerator(0, false)]
    public partial class VoidInfusedKilt : GargishPlateKilt
    {
        [Constructible]
        public VoidInfusedKilt()
        {
            Hue = 2124;
            Attributes.AttackChance = 5;
            Attributes.BonusStr = 5;
            Attributes.BonusDex = 5;
            Attributes.RegenMana = 1;
            Attributes.RegenStam = 1;
            AbsorptionAttributes.EaterDamage = 10;
        }

        public override int LabelNumber => 1113868;
        public override int BasePhysicalResistance => 13;
        public override int BaseFireResistance => 12;
        public override int BaseColdResistance => 8;
        public override int BasePoisonResistance => 9;
        public override int BaseEnergyResistance => 9;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
