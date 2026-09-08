using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/SpinedBloodwormBracers.cs (CC9 batch 4).
    // ServUO derives from GargishClothArms, which is a BaseClothing there (Items/Equipment/Clothing/Arms.cs:
    // 0x404, Layer.Arms, gargoyles only, weight 2.0), and gets absorption state from BaseClothing. Here that
    // state lives on BaseSetClothing (S10), so the piece derives from that with the same item ID, layer, race
    // and weight. Kept as clothing to match the ServUO item even though ModernUO models its own stock
    // GargishClothArmsType1 (same 0x404) as a BaseArmor; the artifact's resistances come from its own
    // Base*Resistance overrides either way.
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class SpinedBloodwormBracers : BaseSetClothing
    {
        [Constructible]
        public SpinedBloodwormBracers() : base(0x404, Layer.Arms)
        {
            Hue = 1642;
            Attributes.RegenHits = 2;
            Attributes.RegenStam = 2;
            Attributes.WeaponDamage = 10;
            Attributes.ReflectPhysical = 30;
            AbsorptionAttributes.EaterKinetic = 10;
        }

        public override int LabelNumber => 1113865; // Spined Bloodworm Bracers

        public override int BasePhysicalResistance => 11;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 15;
        public override int BasePoisonResistance => 15;
        public override int BaseEnergyResistance => 10;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock (ServUO) GargishClothArms members, reproduced because the parent changed.
        public override double DefaultWeight => 2.0;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
