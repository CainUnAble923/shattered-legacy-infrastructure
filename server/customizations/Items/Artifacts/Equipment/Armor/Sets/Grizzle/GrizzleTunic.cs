using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Grizzle/GrizzleTunic.cs (CC9 batch 4).
    // ServUO derives from BoneChest and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock BoneChest's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from BoneChest: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x144f, 0x1454)]
    [SerializationGenerator(0, false)]
    public partial class GrizzleTunic : BaseSetArmor
    {
        [Constructible]
        public GrizzleTunic() : base(0x144F)
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

        public override int LabelNumber => 1074467;
        public override SetItem SetID => SetItem.Grizzle;
        public override int Pieces => 5;
        public override int BasePhysicalResistance => 6;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 10;

        // Stock BoneChest members, reproduced because the parent changed.
        public override double DefaultWeight => 6.0;
        public override int InitMinHits => 25;
        public override int InitMaxHits => 30;
        public override int AosStrReq => 60;
        public override int OldStrReq => 40;
        public override int OldDexBonus => -6;
        public override int ArmorBase => 30;
        public override int RevertArmorBase => 11;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Bone;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
    }
}
