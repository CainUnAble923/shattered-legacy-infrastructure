using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/GargishSignOfChaos.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1); ArmorAttributes.SoulCharge = 20 (D-21: AosArmorAttribute has no SoulCharge).
    [SerializationGenerator(0, false)]
    public partial class GargishSignOfChaos : GargishChaosShield
    {
        [Constructible]
        public GargishSignOfChaos()
        {
            Hue = 2075;
            Attributes.AttackChance = 5;
            Attributes.DefendChance = 10;
            Attributes.CastSpeed = 1;
        }

        public override int LabelNumber => 1113535;
        public override int BasePhysicalResistance => 3;
        public override int BaseFireResistance => 2;
        public override int BaseColdResistance => 2;
        public override int BasePoisonResistance => 2;
        public override int BaseEnergyResistance => 2;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
