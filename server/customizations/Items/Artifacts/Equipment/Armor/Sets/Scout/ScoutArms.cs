using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Scout/ScoutArms.cs (CC9 batch 4).
    // ServUO derives from StuddedArms and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock StuddedArms's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from StuddedArms: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x13dc, 0x13d4)]
    [SerializationGenerator(0, false)]
    public partial class ScoutArms : BaseSetArmor
    {
        [Constructible]
        public ScoutArms() : base(0x13DC)
        {
            Hue = 1148;
            Attributes.BonusDex = 1;
            ArmorAttributes.MageArmor = 1;
            SetAttributes.BonusDex = 6;
            SetAttributes.RegenHits = 2;
            SetAttributes.RegenMana = 2;
            SetAttributes.AttackChance = 10;
            SetAttributes.DefendChance = 10;
            SetHue = 1148;
            SetPhysicalBonus = 28;
            SetFireBonus = 28;
            SetColdBonus = 28;
            SetPoisonBonus = 28;
            SetEnergyBonus = 28;
        }

        public override double DefaultWeight => 4.0;
        public override int LabelNumber => 1080475;
        public override SetItem SetID => SetItem.Scout;
        public override int Pieces => 6;
        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 7;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 7;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock StuddedArms members, reproduced because the parent changed.
        public override int AosStrReq => 25;
        public override int OldStrReq => 25;
        public override int ArmorBase => 16;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Studded;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.Half;
    }
}
