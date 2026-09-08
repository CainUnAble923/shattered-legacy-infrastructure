using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/ProtectoroftheBattleMage.cs (CC9 batch 4).
    // ServUO derives from LeatherChest and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock LeatherChest's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from LeatherChest: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x13cc, 0x13d3)]
    [SerializationGenerator(0, false)]
    public partial class ProtectoroftheBattleMage : BaseSetArmor
    {
        [Constructible]
        public ProtectoroftheBattleMage() : base(0x13CC)
        {
            Hue = 1159;
            Attributes.LowerManaCost = 8;
            Attributes.RegenMana = 2;
            Attributes.LowerRegCost = 10;
            Attributes.SpellDamage = 5;
            AbsorptionAttributes.CastingFocus = 3;
        }

        public override int LabelNumber => 1113761;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
        public override int BasePhysicalResistance => 10;
        public override int BaseFireResistance => 16;
        public override int BaseColdResistance => 10;
        public override int BasePoisonResistance => 8;
        public override int BaseEnergyResistance => 8;

        // Stock LeatherChest members, reproduced because the parent changed.
        public override double DefaultWeight => 6.0;
        public override int AosStrReq => 25;
        public override int OldStrReq => 15;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
