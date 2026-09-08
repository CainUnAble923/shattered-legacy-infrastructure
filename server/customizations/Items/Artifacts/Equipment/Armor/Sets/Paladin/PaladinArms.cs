using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Paladin/PaladinArms.cs (CC9 batch 4).
    // ServUO derives from PlateArms and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock PlateArms's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from PlateArms: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x1410, 0x1417)]
    [SerializationGenerator(0, false)]
    public partial class PaladinArms : BaseSetArmor
    {
        [Constructible]
        public PaladinArms() : base(0x1410)
        {
            SetHue = 0x47E;
            Attributes.RegenHits = 1;
            Attributes.AttackChance = 5;
            SetAttributes.ReflectPhysical = 25;
            SetAttributes.NightSight = 1;
            SetSkillBonuses.SetValues(0, SkillName.Chivalry, 10);
            SetSelfRepair = 3;
            SetPhysicalBonus = 2;
            SetFireBonus = 5;
            SetColdBonus = 5;
            SetPoisonBonus = 3;
            SetEnergyBonus = 5;
        }

        public override int LabelNumber => 1074303;
        public override SetItem SetID => SetItem.Paladin;
        public override int Pieces => 6;
        public override int BasePhysicalResistance => 8;
        public override int BaseFireResistance => 5;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 5;

        // Stock PlateArms members, reproduced because the parent changed.
        public override double DefaultWeight => 5.0;
        public override int InitMinHits => 50;
        public override int InitMaxHits => 65;
        public override int AosStrReq => 80;
        public override int OldStrReq => 40;
        public override int OldDexBonus => -2;
        public override int ArmorBase => 40;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }
}
