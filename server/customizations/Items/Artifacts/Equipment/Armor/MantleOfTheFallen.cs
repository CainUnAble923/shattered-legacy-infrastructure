using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/MantleOfTheFallen.cs (CC6 batch 5; a unique drop of FireDaemonRenowned).
    // ServUO derives from GargishClothChest, which is a BaseClothing there (Items/Equipment/Clothing/Shirts.cs: 0x406,
    // Layer.InnerTorso, gargoyles only, weight 2.0), and gets absorption state from BaseClothing. Here that state lives
    // on BaseSetClothing (S10), so the piece derives from that with the same item ID, layer, race and weight: the
    // SpinedBloodwormBracers shape (CC9 batch 4, whose ServUO parent GargishClothArms is the same file family).
    //
    // GargishClothChest itself is NOT ported: pinned GargishClothChestType1 (Items/Armor/Cloth, a BaseArmor at the same
    // 0x406) declares [TypeAlias("Server.Items.GargishClothChest", ...)], so AssemblyHandler already resolves that name
    // to the stock armour and a second declaration would sit under the same name. The artifact's resistances come from
    // its own Base*Resistance overrides either way. ServUO's clothing swaps to 0x405 on a female wearer in OnAdded;
    // pinned ModernUO never swaps gargish gear by gender (Type1/Type2 are separate items) and CC9's bracers do not
    // either, so neither does this (note section 5).
    // Dropped: IsArtifact (D-1); CanBeWornByGargoyles (D-2).
    [SerializationGenerator(0, false)]
    public partial class MantleOfTheFallen : BaseSetClothing
    {
        [Constructible]
        public MantleOfTheFallen() : base(0x406, Layer.InnerTorso)
        {
            Hue = 1512;
            Attributes.LowerRegCost = 25;
            Attributes.BonusInt = 8;
            Attributes.BonusMana = 8;
            Attributes.RegenMana = 1;
            AbsorptionAttributes.CastingFocus = 3;
            Attributes.SpellDamage = 5;
        }

        public override int LabelNumber => 1113819; // Mantle of the Fallen

        public override int BasePhysicalResistance => 5;
        public override int BaseFireResistance => 8;
        public override int BaseColdResistance => 11;
        public override int BasePoisonResistance => 12;
        public override int BaseEnergyResistance => 8;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock (ServUO) GargishClothChest members, reproduced because the parent changed.
        public override double DefaultWeight => 2.0;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
