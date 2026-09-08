using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Grizzle/GrizzleGreaves.cs (CC9 batch 4).
    // ServUO derives from BoneLegs and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock BoneLegs's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from BoneLegs: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x1452, 0x1457)]
    [SerializationGenerator(0, false)]
    public partial class GrizzleGreaves : BaseSetArmor
    {
        [Constructible]
        public GrizzleGreaves() : base(0x1452)
        {
            SetHue = 0x278;
            ArmorAttributes.MageArmor = 1;
            Attributes.BonusHits = 5;
            Attributes.NightSight = 1;
            SetAttributes.DefendChance = 10;
            SetAttributes.BonusStr = 12;
            SetSelfRepair = 3;
            SetPhysicalBonus = 3;
            SetFireBonus = 5;
            SetColdBonus = 3;
            SetPoisonBonus = 3;
            SetEnergyBonus = 5;
        }

        public override int LabelNumber => 1074468;
        public override SetItem SetID => SetItem.Grizzle;
        public override int Pieces => 5;
        public override int BasePhysicalResistance => 6;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 10;

        // Stock BoneLegs members, reproduced because the parent changed.
        public override double DefaultWeight => 3.0;
        public override int InitMinHits => 25;
        public override int InitMaxHits => 30;
        public override int AosStrReq => 55;
        public override int OldStrReq => 40;
        public override int OldDexBonus => -4;
        public override int ArmorBase => 30;
        public override int RevertArmorBase => 7;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Bone;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
    }
}
