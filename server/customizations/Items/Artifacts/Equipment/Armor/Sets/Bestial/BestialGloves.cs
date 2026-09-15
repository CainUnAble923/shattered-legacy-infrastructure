using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Bestial/BestialGloves.cs (CC9 batch 5).
    // ServUO derives from LeatherGloves and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock LeatherGloves's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 4 for the set-armour bucket.
    // Dropped: IsArtifact (D-1); LeatherGloves's IArcaneEquip machinery (D-7: an artifact piece is never crafted arcane).
    // The berserk mechanic behind BestialSetHelper is not hooked into damage or healing (D-14).
    [Flippable]
    [SerializationGenerator(0, false)]
    public partial class BestialGloves : BaseSetArmor
    {
        [Constructible]
        public BestialGloves() : base(0x13C6)
        {
            Hue = 2010;
            StrRequirement = 20;
        }

        public override double DefaultWeight => 1.0;
        public override int LabelNumber => 1151198;
        public override SetItem SetID => SetItem.Bestial;
        public override int Pieces => 4;
        public override int BasePhysicalResistance => 4;
        public override int BaseFireResistance => 19;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 5;
        public override int BaseEnergyResistance => 5;
        public override int InitMinHits => 125;
        public override int InitMaxHits => 125;

        // Stock LeatherGloves members, reproduced because the parent changed.
        public override int AosStrReq => 20;
        public override int OldStrReq => 10;
        public override int ArmorBase => 13;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;

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
