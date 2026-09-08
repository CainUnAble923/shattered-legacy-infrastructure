using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Virtue/JusticeBreastplate.cs (CC9 batch 4).
    // ServUO derives from BaseArmor and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that instead; nothing else changes.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x2B08, 0x2B09)]
    [SerializationGenerator(0, false)]
    public partial class JusticeBreastplate : BaseSetArmor
    {
        [Constructible]
        public JusticeBreastplate() : base(0x2B08)
        {
            LootType = LootType.Blessed;
            SetHue = 0;
            Hue = 0x226;
            SetSelfRepair = 5;
            SetPhysicalBonus = 5;
            SetFireBonus = 5;
            SetColdBonus = 5;
            SetPoisonBonus = 5;
            SetEnergyBonus = 5;
        }

        public override double DefaultWeight => 7.0;
        public override int LabelNumber => 1075190;
        public override SetItem SetID => SetItem.Virtue;
        public override int Pieces => 8;
        public override int BasePhysicalResistance => 10;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 8;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 8;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
        public override int AosStrReq => 65;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }
}
