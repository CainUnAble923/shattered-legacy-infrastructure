using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Virtuosos/VirtuososKilt.cs (armour-set completion). The fourth
    // piece of the set's gargoyle half (Armbands, Earpieces, Necklace, Kilt); without it that half stopped at three.
    // ServUO derives from GargishClothKilt, a BaseClothing there (Items/Equipment/Clothing/OuterLegs.cs: 0x408,
    // Layer.Gloves - the gargoyle kilt slot - gargoyles only), and gets set state from BaseClothing. Here that state
    // lives on BaseSetClothing (S10), so the piece derives from that with the stock kilt's item ID, layer and race
    // inlined: the SummonersKilt shape (CC6 batch 5). GargishClothKilt itself is not ported: pinned
    // GargishClothKiltType1 declares [TypeAlias("Server.Items.GargishClothKilt", ...)].
    // Dropped: CanBeWornByGargoyles (D-2); the stock kilt's swap to 0x407 on a female wearer (D-91).
    [SerializationGenerator(0, false)]
    public partial class VirtuososKilt : BaseSetClothing
    {
        [Constructible]
        public VirtuososKilt() : base(0x408, Layer.Gloves)
        {
            Hue = 1374;
            SetHue = 1374;
        }

        public override int LabelNumber => 1151559; // Virtuoso's Kilt
        public override SetItem SetID => SetItem.Virtuoso;
        public override int Pieces => 4;
        public override bool BardMasteryBonus => true;

        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 8;
        public override int BaseColdResistance => 21;
        public override int BasePoisonResistance => 8;
        public override int BaseEnergyResistance => 8;
        public override int InitMinHits => 125;
        public override int InitMaxHits => 125;

        // ServUO sets Weight = 5 over the stock kilt's 2.0; the race gate is the stock kilt's, reproduced because the
        // parent changed.
        public override double DefaultWeight => 5.0;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
