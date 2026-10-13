using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items
{
    // -------------------------------------------------------------------------------------------------------------------
    // Pack Mule Deed
    //
    // cc-P69: nothing sells it any more. The base mule is earned through the League's Compact Mule errand (MuleErrand,
    // which summons the mule itself); this deed stays for staff and as a reward item. Double-clicked in the backpack, it
    // summons a bonded tier 1 Compact Mule with a fresh bloodline at the player's feet, if they have a free follower slot
    // (1: PackMule.Compute's SlotCost, the one place the cost is set).
    // -------------------------------------------------------------------------------------------------------------------

    [SerializationGenerator(0, false)]
    public partial class PackMuleDeed : Item
    {
        [Constructible]
        public PackMuleDeed() : base(0x14F0) // standard deed parchment graphic
        {
            Name = "a pack mule deed";
            Hue = 1161; // warm brown to hint at the mule's heritage
            LootType = LootType.Blessed;
        }

        public override string DefaultName => "a pack mule deed";

        public override void OnDoubleClick(Mobile from)
        {
            if (from is not PlayerMobile pm)
            {
                return;
            }

            if (!IsChildOf(pm.Backpack))
            {
                pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
                return;
            }

            if (PackMule.SummonFor(pm, MuleData.MinTier, MuleGenes.Fresh(),
                    "Your pack mule arrives, sturdy and dependable. It is pre-bonded and will follow you faithfully.") != null)
            {
                Delete();
            }
        }
    }
}
