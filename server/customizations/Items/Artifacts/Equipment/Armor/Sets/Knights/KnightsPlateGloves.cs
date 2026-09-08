using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Knights/KnightsPlateGloves.cs (CC9 batch 4).
    // ServUO derives from PlateGloves and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock PlateGloves's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from PlateGloves: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x1414, 0x1418)]
    [SerializationGenerator(0, false)]
    public partial class KnightsPlateGloves : BaseSetArmor
    {
        [Constructible]
        public KnightsPlateGloves() : base(0x1414)
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

        public override double DefaultWeight => 2.0;
        public override int LabelNumber => 1080161;
        public override SetItem SetID => SetItem.Knights;
        public override int Pieces => 6;
        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 7;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 7;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock PlateGloves members, reproduced because the parent changed.
        public override int AosStrReq => 70;
        public override int OldStrReq => 30;
        public override int OldDexBonus => -2;
        public override int ArmorBase => 40;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }
}
