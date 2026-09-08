using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Knights/KnightsPlateChest.cs (CC9 batch 4).
    // ServUO derives from PlateChest and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock PlateChest's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from PlateChest: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x1415, 0x1416)]
    [SerializationGenerator(0, false)]
    public partial class KnightsPlateChest : BaseSetArmor
    {
        [Constructible]
        public KnightsPlateChest() : base(0x1415)
        {
            Hue = 1150;
            Attributes.BonusHits = 1;
            SetAttributes.BonusHits = 6;
            SetAttributes.RegenHits = 2;
            SetAttributes.RegenMana = 2;
            SetAttributes.AttackChance = 10;
            SetAttributes.DefendChance = 10;
            SetHue = 1150;
            SetPhysicalBonus = 28;
            SetFireBonus = 28;
            SetColdBonus = 28;
            SetPoisonBonus = 28;
            SetEnergyBonus = 28;
        }

        public override double DefaultWeight => 10.0;
        public override int LabelNumber => 1080164;
        public override SetItem SetID => SetItem.Knights;
        public override int Pieces => 6;
        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 7;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 7;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock PlateChest members, reproduced because the parent changed.
        public override int AosStrReq => 95;
        public override int OldStrReq => 60;
        public override int OldDexBonus => -8;
        public override int ArmorBase => 40;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }
}
