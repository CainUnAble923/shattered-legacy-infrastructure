using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Bestial/BestialNecklace.cs (CC9 batch 4).
    // GargishNecklace is already on BaseSetArmor (S10), so this is a plain derivation; the parameterless base
    // constructor is ServUO's too (0x4210).
    // The berserk mechanic behind BestialSetHelper is not hooked into damage or healing (D-14).
    [SerializationGenerator(0, false)]
    public partial class BestialNecklace : GargishNecklace
    {
        [Constructible]
        public BestialNecklace()
        {
            Hue = 2010;
        }

        public override int LabelNumber => 1151544; // Bestial Necklace
        public override SetItem SetID => SetItem.Bestial;
        public override int Pieces => 4;

        public override double DefaultWeight => 1.0;
        public override int BasePhysicalResistance => 3;
        public override int BaseFireResistance => 4;
        public override int BaseColdResistance => 4;
        public override int BasePoisonResistance => 4;
        public override int BaseEnergyResistance => 17;
        public override int InitMinHits => 125;
        public override int InitMaxHits => 125;

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
