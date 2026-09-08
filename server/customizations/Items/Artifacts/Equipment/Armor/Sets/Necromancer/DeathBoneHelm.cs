using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Necromancer/DeathBoneHelm.cs (CC9 batch 4).
    // ServUO derives from BoneHelm and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock BoneHelm's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from BoneHelm: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x1451, 0x1456)]
    [SerializationGenerator(0, false)]
    public partial class DeathBoneHelm : BaseSetArmor
    {
        [Constructible]
        public DeathBoneHelm() : base(0x1451)
        {
            SetHue = 0x455;
            Attributes.RegenHits = 1;
            Attributes.RegenMana = 1;
            SetAttributes.LowerManaCost = 10;
            SetSkillBonuses.SetValues(0, SkillName.Necromancy, 10);
            SetSelfRepair = 3;
            SetPhysicalBonus = 4;
            SetFireBonus = 5;
            SetColdBonus = 3;
            SetPoisonBonus = 4;
            SetEnergyBonus = 4;
        }

        public override int LabelNumber => 1074305;
        public override SetItem SetID => SetItem.Necromancer;
        public override int Pieces => 5;
        public override int BasePhysicalResistance => 4;
        public override int BaseFireResistance => 9;
        public override int BaseColdResistance => 3;
        public override int BasePoisonResistance => 6;
        public override int BaseEnergyResistance => 8;

        // Stock BoneHelm members, reproduced because the parent changed.
        public override double DefaultWeight => 3.0;
        public override int InitMinHits => 25;
        public override int InitMaxHits => 30;
        public override int AosStrReq => 20;
        public override int OldStrReq => 40;
        public override int ArmorBase => 30;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Bone;
    }
}
