using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Sorcerers/SorcererFemaleChest.cs (CC9 batch 4).
    // ServUO derives from FemaleLeatherChest and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock FemaleLeatherChest's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from FemaleLeatherChest: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x1c06, 0x1c07)]
    [SerializationGenerator(0, false)]
    public partial class SorcererFemaleChest : BaseSetArmor
    {
        [Constructible]
        public SorcererFemaleChest() : base(0x1C06)
        {
            Hue = 1165;
            Attributes.BonusInt = 1;
            Attributes.LowerRegCost = 10;
            SetAttributes.BonusInt = 6;
            SetAttributes.RegenMana = 2;
            SetAttributes.DefendChance = 10;
            SetAttributes.LowerManaCost = 5;
            SetAttributes.LowerRegCost = 40;
            SetHue = 1165;
            SetPhysicalBonus = 28;
            SetFireBonus = 28;
            SetColdBonus = 28;
            SetPoisonBonus = 28;
            SetEnergyBonus = 28;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1080469;
        public override SetItem SetID => SetItem.Sorcerer;
        public override int Pieces => 6;
        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 7;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 7;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock FemaleLeatherChest members, reproduced because the parent changed.
        public override int AosStrReq => 25;
        public override int OldStrReq => 15;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
        public override bool AllowMaleWearer => false;
    }
}
