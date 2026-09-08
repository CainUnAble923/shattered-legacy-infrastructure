using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/CrownOfArcaneTemperament.cs (CC9 batch 4).
    // ServUO derives from Circlet and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock Circlet's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from Circlet: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    // ServUO clears Circlet's elf-only requirement with RequiredRace => null; RequiredRaces => Race.AllowAllRaces is the ModernUO spelling.
    [Flippable(0x2B6E, 0x3165)]
    [SerializationGenerator(0, false)]
    public partial class CrownOfArcaneTemperament : BaseSetArmor
    {
        [Constructible]
        public CrownOfArcaneTemperament() : base(0x2B6E)
        {
            Attributes.BonusMana = 8;
            Attributes.RegenMana = 3;
            Attributes.SpellDamage = 8;
            Attributes.LowerManaCost = 6;
            Hue = 2012;
            AbsorptionAttributes.CastingFocus = 2;
        }

        public override int RequiredRaces => Race.AllowAllRaces;
        public override int LabelNumber => 1113762;
        public override int BasePhysicalResistance => 10;
        public override int BaseFireResistance => 14;
        public override int BaseColdResistance => 4;
        public override int BasePoisonResistance => 12;
        public override int BaseEnergyResistance => 7;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock Circlet members, reproduced because the parent changed.
        public override double DefaultWeight => 2.0;
        public override int AosStrReq => 10;
        public override int OldStrReq => 10;
        public override int ArmorBase => 30;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
