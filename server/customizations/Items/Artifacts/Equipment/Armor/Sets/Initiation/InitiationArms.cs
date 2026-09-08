using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Initiation/InitiationArms.cs (CC9 batch 4).
    // ServUO derives from LeatherArms and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock LeatherArms's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from LeatherArms: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x13cd, 0x13c5)]
    [SerializationGenerator(0, false)]
    public partial class InitiationArms : BaseSetArmor
    {
        [Constructible]
        public InitiationArms() : base(0x13CD)
        {
            Hue = 0x9C4;
            LootType = LootType.Blessed;
            SetHue = 0x30;
            SetPhysicalBonus = 2;
            SetFireBonus = 5;
            SetColdBonus = 5;
            SetPoisonBonus = 3;
            SetEnergyBonus = 5;
        }

        public override double DefaultWeight => 2.0;
        public override int LabelNumber => 1116255;
        public override SetItem SetID => SetItem.Initiation;
        public override int Pieces => 6;
        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 4;
        public override int BaseColdResistance => 4;
        public override int BasePoisonResistance => 6;
        public override int BaseEnergyResistance => 4;
        public override int InitMinHits => 150;
        public override int InitMaxHits => 150;

        // Stock LeatherArms members, reproduced because the parent changed.
        public override int AosStrReq => 20;
        public override int OldStrReq => 15;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
