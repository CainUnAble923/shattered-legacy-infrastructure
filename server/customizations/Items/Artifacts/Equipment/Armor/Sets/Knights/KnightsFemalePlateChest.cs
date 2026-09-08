using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Knights/KnightsFemalePlateChest.cs (CC9 batch 4).
    // ServUO derives from FemalePlateChest and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock FemalePlateChest's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from FemalePlateChest: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x1c04, 0x1c05)]
    [SerializationGenerator(0, false)]
    public partial class KnightsFemalePlateChest : BaseSetArmor
    {
        [Constructible]
        public KnightsFemalePlateChest() : base(0x1C04)
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

        public override double DefaultWeight => 4.0;
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

        // Stock FemalePlateChest members, reproduced because the parent changed.
        public override int AosStrReq => 95;
        public override int OldStrReq => 45;
        public override int OldDexBonus => -5;
        public override bool AllowMaleWearer => false;
        public override int ArmorBase => 30;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }
}
