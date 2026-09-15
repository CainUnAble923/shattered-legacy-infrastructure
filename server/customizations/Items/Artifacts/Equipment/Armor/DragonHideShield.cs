using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/DragonHideShield.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // GargishKiteShield (batch 3) is on BaseSetShield, so AbsorptionAttributes resolves.
    [SerializationGenerator(0, false)]
    public partial class DragonHideShield : GargishKiteShield
    {
        [Constructible]
        public DragonHideShield()
        {
            Hue = 44;
            AbsorptionAttributes.EaterFire = 20;
            Attributes.RegenHits = 2;
            Attributes.DefendChance = 10;
        }

        public override int LabelNumber => 1113532;
        public override int BasePhysicalResistance => 15;
        public override int BaseFireResistance => 0;
        public override int BaseColdResistance => 0;
        public override int BasePoisonResistance => 0;
        public override int BaseEnergyResistance => -4;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
