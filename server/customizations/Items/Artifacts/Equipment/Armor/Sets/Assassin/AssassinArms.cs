using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Assassin/AssassinArms.cs (CC9 batch 4).
    // ServUO derives from LeatherArms and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock LeatherArms's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from LeatherArms: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x13cd, 0x13c5)]
    [SerializationGenerator(0, false)]
    public partial class AssassinArms : BaseSetArmor
    {
        [Constructible]
        public AssassinArms() : base(0x13CD)
        {
            SetHue = 0x455;
            Attributes.BonusStam = 2;
            Attributes.WeaponSpeed = 5;
            SetSkillBonuses.SetValues(0, SkillName.Stealth, 30);
            SetSelfRepair = 3;
            SetAttributes.BonusDex = 12;
            SetPhysicalBonus = 5;
            SetFireBonus = 4;
            SetColdBonus = 3;
            SetPoisonBonus = 4;
            SetEnergyBonus = 4;
        }

        public override int LabelNumber => 1074304;
        public override SetItem SetID => SetItem.Assassin;
        public override int Pieces => 4;
        public override int BasePhysicalResistance => 9;
        public override int BaseFireResistance => 6;
        public override int BaseColdResistance => 3;
        public override int BasePoisonResistance => 8;
        public override int BaseEnergyResistance => 4;

        // Stock LeatherArms members, reproduced because the parent changed.
        public override double DefaultWeight => 2.0;
        public override int InitMinHits => 30;
        public override int InitMaxHits => 40;
        public override int AosStrReq => 20;
        public override int OldStrReq => 15;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
