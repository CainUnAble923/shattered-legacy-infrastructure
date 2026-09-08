using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Acolyte/GreymistLegs.cs (CC9).
    // Re-parented from LeatherLegs onto BaseSetArmor; see GreymistArms for why.
    [Flippable(0x13cb, 0x13d2)]
    [SerializationGenerator(0, false)]
    public partial class GreymistLegs : BaseSetArmor
    {
        [Constructible]
        public GreymistLegs() : base(0x13CB)
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

        // Stock LeatherLegs members, reproduced because the parent changed.
        public override double DefaultWeight => 4.0;
        public override int InitMinHits => 30;
        public override int InitMaxHits => 40;
        public override int AosStrReq => 20;
        public override int OldStrReq => 10;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
