using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Scout/ScoutSmallPlateJingasa.cs (CC9 batch 4).
    // ServUO derives from SmallPlateJingasa and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock SmallPlateJingasa's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class ScoutSmallPlateJingasa : BaseSetArmor
    {
        [Constructible]
        public ScoutSmallPlateJingasa() : base(0x2784)
        {
            Hue = 1148;
            Attributes.BonusDex = 1;
            ArmorAttributes.MageArmor = 1;
            SetAttributes.BonusDex = 6;
            SetAttributes.RegenHits = 2;
            SetAttributes.RegenMana = 2;
            SetAttributes.AttackChance = 10;
            SetAttributes.DefendChance = 10;
            SetHue = 0x46D;
            SetPhysicalBonus = 28;
            SetFireBonus = 28;
            SetColdBonus = 28;
            SetPoisonBonus = 28;
            SetEnergyBonus = 28;
        }

        public override double DefaultWeight => 5.0;
        public override int LabelNumber => 1080473;
        public override SetItem SetID => SetItem.Scout;
        public override int Pieces => 6;
        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 7;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 7;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock SmallPlateJingasa members, reproduced because the parent changed.
        public override int AosStrReq => 55;
        public override int OldStrReq => 55;
        public override int ArmorBase => 4;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }
}
