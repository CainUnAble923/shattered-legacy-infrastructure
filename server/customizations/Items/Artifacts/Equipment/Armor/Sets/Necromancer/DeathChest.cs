using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Necromancer/DeathChest.cs (CC9 batch 4).
    // ServUO derives from LeatherChest and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock LeatherChest's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from LeatherChest: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x13cc, 0x13d3)]
    [SerializationGenerator(0, false)]
    public partial class DeathChest : BaseSetArmor
    {
        [Constructible]
        public DeathChest() : base(0x13CC)
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

        // Stock LeatherChest members, reproduced because the parent changed.
        public override double DefaultWeight => 6.0;
        public override int InitMinHits => 30;
        public override int InitMaxHits => 40;
        public override int AosStrReq => 25;
        public override int OldStrReq => 15;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
