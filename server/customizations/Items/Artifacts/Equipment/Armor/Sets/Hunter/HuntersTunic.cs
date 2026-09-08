using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Hunter/HuntersTunic.cs (CC9 batch 4).
    // ServUO derives from LeafChest and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock LeafChest's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from LeafChest: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x2FC5, 0x317B)]
    [SerializationGenerator(0, false)]
    public partial class HunterChest : BaseSetArmor
    {
        [Constructible]
        public HunterChest() : base(0x2FC5)
        {
            SetHue = 0x483;
            Attributes.RegenHits = 1;
            Attributes.Luck = 50;
            SetAttributes.BonusDex = 10;
            SetSkillBonuses.SetValues(0, SkillName.Stealth, 40);
            SetSelfRepair = 3;
            SetPhysicalBonus = 5;
            SetFireBonus = 4;
            SetColdBonus = 3;
            SetPoisonBonus = 4;
            SetEnergyBonus = 4;
        }

        public override int LabelNumber => 1074301;
        public override SetItem SetID => SetItem.Hunter;
        public override int Pieces => 4;
        public override int BasePhysicalResistance => 9;
        public override int BaseFireResistance => 6;
        public override int BaseColdResistance => 3;
        public override int BasePoisonResistance => 8;
        public override int BaseEnergyResistance => 4;

        // Stock LeafChest members, reproduced because the parent changed.
        public override double DefaultWeight => 2.0;
        public override int RequiredRaces => Race.AllowElvesOnly;
        public override int InitMinHits => 30;
        public override int InitMaxHits => 40;
        public override int AosStrReq => 20;
        public override int OldStrReq => 20;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
