using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Mage/LeafweavePauldrons.cs (CC9 batch 4).
    // ServUO derives from HidePauldrons and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock HidePauldrons's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x2B77, 0x316E)]
    [SerializationGenerator(0, false)]
    public partial class LeafweavePauldrons : BaseSetArmor
    {
        [Constructible]
        public LeafweavePauldrons() : base(0x2B77)
        {
            SetHue = 0x47E;
            Attributes.RegenMana = 1;
            ArmorAttributes.MageArmor = 1;
            SetAttributes.BonusInt = 10;
            SetAttributes.SpellDamage = 15;
            SetSelfRepair = 3;
            SetPhysicalBonus = 4;
            SetFireBonus = 5;
            SetColdBonus = 3;
            SetPoisonBonus = 4;
            SetEnergyBonus = 4;
        }

        public override int LabelNumber => 1074299;
        public override SetItem SetID => SetItem.Mage;
        public override int Pieces => 4;
        public override int BasePhysicalResistance => 4;
        public override int BaseFireResistance => 9;
        public override int BaseColdResistance => 3;
        public override int BasePoisonResistance => 6;
        public override int BaseEnergyResistance => 8;

        // Stock HidePauldrons members, reproduced because the parent changed.
        public override double DefaultWeight => 4.0;
        public override int RequiredRaces => Race.AllowElvesOnly;
        public override int InitMinHits => 35;
        public override int InitMaxHits => 45;
        public override int AosStrReq => 20;
        public override int OldStrReq => 20;
        public override int ArmorBase => 15;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Studded;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.Half;
    }
}
