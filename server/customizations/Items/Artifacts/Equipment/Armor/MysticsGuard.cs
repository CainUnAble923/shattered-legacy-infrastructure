using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/MysticsGuard.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1); ArmorAttributes.SoulCharge = 30 (D-21: AosArmorAttribute has no SoulCharge).
    [SerializationGenerator(0, false)]
    public partial class MysticsGuard : GargishWoodenShield
    {
        [Constructible]
        public MysticsGuard()
        {
            Attributes.SpellChanneling = 1;
            Attributes.DefendChance = 10;
            Attributes.CastRecovery = 2;
            Hue = 0x671;
        }

        public override int LabelNumber => 1113536;
        public override int ArtifactRarity => 5;
        public override int BasePhysicalResistance => 10;
        public override int BaseFireResistance => 0;
        public override int BaseColdResistance => 0;
        public override int BasePoisonResistance => 0;
        public override int BaseEnergyResistance => 1;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
