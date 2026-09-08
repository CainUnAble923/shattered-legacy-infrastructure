using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Sorcerers/SorcererGorget.cs (CC9 batch 4).
    // ServUO derives from LeatherGorget and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock LeatherGorget's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class SorcererGorget : BaseSetArmor
    {
        [Constructible]
        public SorcererGorget() : base(0x13C7)
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
        public override int LabelNumber => 1080466;
        public override SetItem SetID => SetItem.Sorcerer;
        public override int Pieces => 6;
        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 7;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 7;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock LeatherGorget members, reproduced because the parent changed.
        public override int AosStrReq => 20;
        public override int OldStrReq => 10;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
