using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Myrmidon/MyrmidonLegs.cs (CC9 batch 4).
    // ServUO derives from StuddedLegs and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock StuddedLegs's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from StuddedLegs: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x13da, 0x13e1)]
    [SerializationGenerator(0, false)]
    public partial class MyrmidonLegs : BaseSetArmor
    {
        [Constructible]
        public MyrmidonLegs() : base(0x13DA)
        {
            SetHue = 0x331;
            Attributes.BonusStr = 1;
            Attributes.BonusHits = 2;
            SetAttributes.Luck = 500;
            SetAttributes.NightSight = 1;
            SetSelfRepair = 3;
            SetPhysicalBonus = 3;
            SetFireBonus = 3;
            SetColdBonus = 3;
            SetPoisonBonus = 3;
            SetEnergyBonus = 3;
        }

        public override int LabelNumber => 1074306;
        public override SetItem SetID => SetItem.Myrmidon;
        public override int Pieces => 6;
        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 3;
        public override int BasePoisonResistance => 5;
        public override int BaseEnergyResistance => 3;

        // Stock StuddedLegs members, reproduced because the parent changed.
        public override double DefaultWeight => 5.0;
        public override int InitMinHits => 35;
        public override int InitMaxHits => 45;
        public override int AosStrReq => 30;
        public override int OldStrReq => 35;
        public override int ArmorBase => 16;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Studded;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.Half;
    }
}
