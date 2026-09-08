using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Knights/KnightsPlateLegs.cs (CC9 batch 4).
    // ServUO derives from PlateLegs and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock PlateLegs's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from PlateLegs: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x1411, 0x141a)]
    [SerializationGenerator(0, false)]
    public partial class KnightsPlateLegs : BaseSetArmor
    {
        [Constructible]
        public KnightsPlateLegs() : base(0x1411)
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

        public override double DefaultWeight => 7.0;
        public override int LabelNumber => 1080163;
        public override SetItem SetID => SetItem.Knights;
        public override int Pieces => 6;
        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 7;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 7;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock PlateLegs members, reproduced because the parent changed.
        public override int AosStrReq => 90;
        public override int OldStrReq => 60;
        public override int OldDexBonus => -6;
        public override int ArmorBase => 40;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }
}
