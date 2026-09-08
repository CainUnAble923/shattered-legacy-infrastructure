using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Hunter/HuntersGloves.cs (CC9 batch 4).
    // ServUO derives from LeafGloves and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock LeafGloves's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1); LeafGloves's IArcaneEquip machinery (D-7: an artifact glove is never crafted arcane).
    [SerializationGenerator(0, false)]
    public partial class HunterGloves : BaseSetArmor
    {
        [Constructible]
        public HunterGloves() : base(0x2FC6)
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

        // Stock LeafGloves members, reproduced because the parent changed.
        public override double DefaultWeight => 2.0;
        public override int RequiredRaces => Race.AllowElvesOnly;
        public override int InitMinHits => 30;
        public override int InitMaxHits => 40;
        public override int AosStrReq => 10;
        public override int OldStrReq => 10;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
