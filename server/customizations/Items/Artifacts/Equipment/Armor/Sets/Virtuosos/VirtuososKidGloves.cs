using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Virtuosos/VirtuososKidGloves.cs (CC9 batch 4).
    // ServUO derives from LeatherGloves and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock LeatherGloves's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1); LeatherGloves's IArcaneEquip machinery (D-7: an artifact glove is never crafted arcane).
    [SerializationGenerator(0, false)]
    public partial class VirtuososKidGloves : BaseSetArmor
    {
        [Constructible]
        public VirtuososKidGloves() : base(0x13C6)
        {
            Hue = 1374;
            StrRequirement = 20;
            SetHue = 1374;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1151322;
        public override SetItem SetID => SetItem.Virtuoso;
        public override int Pieces => 4;
        public override bool BardMasteryBonus => true;
        public override int BasePhysicalResistance => 4;
        public override int BaseFireResistance => 19;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 5;
        public override int BaseEnergyResistance => 5;
        public override int InitMinHits => 125;
        public override int InitMaxHits => 125;

        // Stock LeatherGloves members, reproduced because the parent changed.
        public override int AosStrReq => 20;
        public override int OldStrReq => 10;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
