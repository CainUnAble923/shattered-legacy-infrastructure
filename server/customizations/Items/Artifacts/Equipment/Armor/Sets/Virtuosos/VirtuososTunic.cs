using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Virtuosos/VirtuososTunic.cs (CC9 batch 4).
    // ServUO derives from StuddedChest and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock StuddedChest's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from StuddedChest: ModernUO reads it with inherit: false.
    [Flippable(0x13db, 0x13e2)]
    [SerializationGenerator(0, false)]
    public partial class VirtuososTunic : BaseSetArmor
    {
        [Constructible]
        public VirtuososTunic() : base(0x13DB)
        {
            Hue = 1374;
            StrRequirement = 35;
            SetHue = 1374;
        }

        public override int LabelNumber => 1151321;
        public override SetItem SetID => SetItem.Virtuoso;
        public override int Pieces => 4;
        public override bool BardMasteryBonus => true;
        public override int BasePhysicalResistance => 6;
        public override int BaseFireResistance => 20;
        public override int BaseColdResistance => 7;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 8;
        public override int InitMinHits => 125;
        public override int InitMaxHits => 125;

        // Stock StuddedChest members, reproduced because the parent changed.
        public override double DefaultWeight => 8.0;
        public override int AosStrReq => 35;
        public override int OldStrReq => 35;
        public override int ArmorBase => 16;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Studded;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.Half;
    }
}
