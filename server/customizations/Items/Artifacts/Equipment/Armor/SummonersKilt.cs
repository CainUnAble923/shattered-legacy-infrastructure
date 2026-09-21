using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/SummonersKilt.cs (CC6 batch 5; a shared drop of AncientLichRenowned).
    // ServUO derives from GargishClothKilt, a BaseClothing there (Items/Equipment/Clothing/OuterLegs.cs: 0x408,
    // Layer.Gloves - the gargoyle kilt slot - gargoyles only, weight 2.0), and gets absorption state from BaseClothing.
    // Here that state lives on BaseSetClothing (S10), so the piece derives from that with the same item ID, layer, race
    // and weight (the SpinedBloodwormBracers / MantleOfTheFallen shape). GargishClothKilt itself is not ported: pinned
    // GargishClothKiltType1 declares [TypeAlias("Server.Items.GargishClothKilt", ...)] (see MantleOfTheFallen).
    // Dropped: IsArtifact (D-1); CanBeWornByGargoyles (D-2); IRepairable / RepairSystem => DefTailoring (the CC9
    // GargishGlasses call: pinned Repair.cs dispatches on the item's own type and has no such interface, no behaviour
    // change).
    [SerializationGenerator(0, false)]
    public partial class SummonersKilt : BaseSetClothing
    {
        [Constructible]
        public SummonersKilt() : base(0x408, Layer.Gloves)
        {
            Hue = 1266;
            Attributes.BonusMana = 5;
            Attributes.RegenMana = 2;
            Attributes.SpellDamage = 5;
            AbsorptionAttributes.CastingFocus = 2;
            Attributes.LowerManaCost = 8;
            Attributes.LowerRegCost = 10;
        }

        public override int LabelNumber => 1113540; // Summoner's Kilt

        public override int BasePhysicalResistance => 5;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 21;
        public override int BasePoisonResistance => 6;
        public override int BaseEnergyResistance => 21;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock (ServUO) GargishClothKilt members, reproduced because the parent changed.
        public override double DefaultWeight => 2.0;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
