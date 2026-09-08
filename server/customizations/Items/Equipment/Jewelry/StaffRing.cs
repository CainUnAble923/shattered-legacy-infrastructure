using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Jewelry/StaffRing.cs (CC9 batch 3). Not OSI content: a ServUO staff-only ring
    // ("By Nerun") that deletes itself if a player double-clicks or equips it. ServUO's Mobile.IsPlayer()
    // is AccessLevel == Player; pinned ModernUO has no such method, so the comparison is written out.
    [SerializationGenerator(0, false)]
    public partial class StaffRing : BaseRing
    {
        [Constructible]
        public StaffRing() : base(0x108A)
        {
            Attributes.NightSight = 1;
            Attributes.AttackChance = 20;
            Attributes.LowerRegCost = 100;
            Attributes.LowerManaCost = 100;
            Attributes.RegenHits = 12;
            Attributes.RegenStam = 24;
            Attributes.RegenMana = 18;
            Attributes.SpellDamage = 30;
            Attributes.CastRecovery = 6;
            Attributes.CastSpeed = 4;
            LootType = LootType.Blessed;
        }

        public override string DefaultName => "The Staff Ring";

        public override void OnDoubleClick(Mobile from)
        {
            if (from.AccessLevel == AccessLevel.Player)
            {
                from.SendMessage("This item is to only be used by staff members.");
                Delete();
            }
        }

        public override bool OnEquip(Mobile from)
        {
            if (from.AccessLevel == AccessLevel.Player)
            {
                from.SendMessage("This item is to only be used by staff members.");
                Delete();
            }

            return true;
        }
    }
}
