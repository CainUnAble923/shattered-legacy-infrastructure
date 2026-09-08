using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Virtue/HonestyGorget.cs (CC9 batch 4).
    // ServUO derives from BaseArmor and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that instead; nothing else changes.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x2B0E, 0x2B0F)]
    [SerializationGenerator(0, false)]
    public partial class HonestyGorget : BaseSetArmor
    {
        [Constructible]
        public HonestyGorget() : base(0x2B0E)
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

        public override double DefaultWeight => 2.0;
        public override int LabelNumber => 1075189;
        public override SetItem SetID => SetItem.Virtue;
        public override int Pieces => 8;
        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 9;
        public override int BasePoisonResistance => 5;
        public override int BaseEnergyResistance => 7;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
        public override int AosStrReq => 45;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }
}
