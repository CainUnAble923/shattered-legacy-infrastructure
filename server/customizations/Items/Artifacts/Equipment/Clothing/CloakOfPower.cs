using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/CloakOfPower.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [Flippable(0x2FB9, 0x3173)]
    [SerializationGenerator(0, false)]
    public partial class CloakOfPower : BaseOuterTorso
    {
        [Constructible]
        public CloakOfPower() : base(0x2FB9)
        {
            Hue = 0xFE;
            Attributes.BonusStr = 2;
            Attributes.BonusDex = 2;
            Attributes.BonusInt = 2;
        }

        public override double DefaultWeight => 2.0;
        public override int LabelNumber => 1112882;
    }
}
