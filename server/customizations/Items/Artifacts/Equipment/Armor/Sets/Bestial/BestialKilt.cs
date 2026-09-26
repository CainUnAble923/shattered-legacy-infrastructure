using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Bestial/BestialKilt.cs (armour-set completion). The fourth piece of
    // the set's gargoyle half (Arms, Earrings, Necklace, Kilt); without it that half stopped at three of four.
    // ServUO derives from GargishClothKilt, a BaseClothing there (Items/Equipment/Clothing/OuterLegs.cs: 0x408,
    // Layer.Gloves - the gargoyle kilt slot - gargoyles only), and gets set state from BaseClothing. Here that state
    // lives on BaseSetClothing (S10), so the piece derives from that with the stock kilt's item ID, layer and race
    // inlined: the SummonersKilt shape (CC6 batch 5). GargishClothKilt itself is not ported: pinned
    // GargishClothKiltType1 declares [TypeAlias("Server.Items.GargishClothKilt", ...)].
    // Dropped: IsArtifact (D-1); CanBeWornByGargoyles (D-2); the stock kilt's swap to 0x407 on a female wearer (D-91).
    // ServUO resets the hue to 2010 in Deserialize; the other seven Bestial pieces carry that reset in OnRemoved
    // (CC9 batch 4) and this one matches them. The berserk mechanic behind BestialSetHelper is not hooked (D-14).
    [SerializationGenerator(0, false)]
    public partial class BestialKilt : BaseSetClothing
    {
        [Constructible]
        public BestialKilt() : base(0x408, Layer.Gloves)
        {
            Hue = 2010;
        }

        public override int LabelNumber => 1151546; // Bestial Kilt
        public override SetItem SetID => SetItem.Bestial;
        public override int Pieces => 4;

        public override int BasePhysicalResistance => 24;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 9;
        public override int BasePoisonResistance => 10;
        public override int BaseEnergyResistance => 9;
        public override int InitMinHits => 125;
        public override int InitMaxHits => 125;

        // ServUO sets Weight = 5 over the stock kilt's 2.0; the race gate is the stock kilt's, reproduced because the
        // parent changed.
        public override double DefaultWeight => 5.0;
        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override void OnAdded(IEntity parent)
        {
            base.OnAdded(parent);

            if (parent is Mobile m && !Deleted)
            {
                BestialSetHelper.OnAdded(m, this);
            }
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m && !Deleted)
            {
                BestialSetHelper.OnRemoved(m, this);
            }

            if (Hue != 2010)
            {
                Hue = 2010;
            }
        }
    }
}
