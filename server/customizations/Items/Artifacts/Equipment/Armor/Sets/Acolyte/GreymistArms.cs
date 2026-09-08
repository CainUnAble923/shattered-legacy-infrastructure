using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Acolyte/GreymistArms.cs (CC9).
    // ServUO derives from LeatherArms and gets set state from BaseArmor. Here set state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock LeatherArms's own
    // members, the way S1 did for the TigerPelt pieces under Aloron's set.
    [Flippable(0x13cd, 0x13c5)]
    [SerializationGenerator(0, false)]
    public partial class GreymistArms : BaseSetArmor
    {
        [Constructible]
        public GreymistArms() : base(0x13CD)
        {
            SetHue = 0xCB;

            Attributes.BonusMana = 2;
            Attributes.SpellDamage = 2;

            SetAttributes.Luck = 100;
            SetAttributes.NightSight = 1;

            SetSelfRepair = 3;

            SetPhysicalBonus = 3;
            SetFireBonus = 3;
            SetColdBonus = 3;
            SetPoisonBonus = 3;
            SetEnergyBonus = 3;
        }

        public override int LabelNumber => 1074307; // Greymist Armor

        public override SetItem SetID => SetItem.Acolyte;
        public override int Pieces => 4;

        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 3;
        public override int BasePoisonResistance => 4;
        public override int BaseEnergyResistance => 4;

        // Stock LeatherArms members, reproduced because the parent changed.
        public override double DefaultWeight => 2.0;
        public override int InitMinHits => 30;
        public override int InitMaxHits => 40;
        public override int AosStrReq => 20;
        public override int OldStrReq => 15;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
