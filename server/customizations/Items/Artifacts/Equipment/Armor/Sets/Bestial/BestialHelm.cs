using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Bestial/BestialHelm.cs (CC9 batch 5).
    // ServUO derives from BearMask and gets set/absorption state from BaseClothing. Here that state lives on
    // BaseSetClothing (S10), so the piece derives from that and reproduces stock BearMask's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 4 for the set-armour bucket.
    // Dropped: IsArtifact (D-1); BaseHat's IsShipwreckedItem field, its two shipwreck tooltip lines and its exceptional-craft bonus (D-18).
    // The berserk mechanic behind BestialSetHelper is not hooked into damage or healing (D-14).
    [SerializationGenerator(0, false)]
    public partial class BestialHelm : BaseSetClothing
    {
        [Constructible]
        public BestialHelm() : base(0x1545, Layer.Helm)
        {
            Hue = 2010;
            StrRequirement = 10;
        }

        public override double DefaultWeight => 5.0;
        public override int LabelNumber => 1151197;
        public override SetItem SetID => SetItem.Bestial;
        public override int Pieces => 4;
        public override int BasePhysicalResistance => 8;
        public override int BaseFireResistance => 6;
        public override int BaseColdResistance => 22;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 7;
        public override int InitMinHits => 125;
        public override int InitMaxHits => 125;

        // Stock BearMask.Dye, reproduced because the parent changed.
        public override bool Dye(Mobile from, DyeTub sender)
        {
            from.SendLocalizedMessage(sender.FailMessage);
            return false;
        }

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
