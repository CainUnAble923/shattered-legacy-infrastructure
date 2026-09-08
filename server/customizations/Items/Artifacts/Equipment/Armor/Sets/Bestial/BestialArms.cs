using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Bestial/BestialArms.cs (CC9 batch 4).
    // ServUO derives from GargishLeatherArms (ModernUO's GargishLeatherArmsType2, 0x302) and then sets ItemID
    // to 0x4052 in the constructor; here 0x4052 goes straight to the base call. Set state lives on BaseSetArmor
    // (S1), so the piece derives from that and reproduces stock GargishLeatherArmsType2's own members.
    // The berserk mechanic behind BestialSetHelper is not hooked into damage or healing (D-14).
    [SerializationGenerator(0, false)]
    public partial class BestialArms : BaseSetArmor
    {
        [Constructible]
        public BestialArms() : base(0x4052)
        {
            Hue = 2010;
            StrRequirement = 25;
        }

        public override int LabelNumber => 1151545; // Bestial Arms
        public override SetItem SetID => SetItem.Bestial;
        public override int Pieces => 4;

        public override int BasePhysicalResistance => 7;
        public override int BaseFireResistance => 8;
        public override int BaseColdResistance => 21;
        public override int BasePoisonResistance => 8;
        public override int BaseEnergyResistance => 8;
        public override int InitMinHits => 125;
        public override int InitMaxHits => 125;

        // Stock GargishLeatherArmsType2 members, reproduced because the parent changed.
        public override double DefaultWeight => 4.0;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
        public override int AosStrReq => 25;
        public override int OldStrReq => 25;
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
