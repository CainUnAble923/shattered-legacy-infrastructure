using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Items
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Pack Mule Deed
    //
    // Delivered by the Cross-Guild Exchange after spending scrip from all four
    // trade guilds.  Double-clicking the deed summons a PackMule at the player's
    // feet, pre-bonded and ready for service.
    //
    // Restrictions:
    //   • Must be in the player's backpack to use.
    //   • Player must have enough free follower slots (mule costs 1 control slot).
    //   • Player must be alive and not a ghost.
    // ─────────────────────────────────────────────────────────────────────────────

    [SerializationGenerator(0, false)]
    public partial class PackMuleDeed : Item
    {
        [Constructible]
        public PackMuleDeed() : base(0x14F0) // standard deed parchment graphic
        {
            Name  = "a pack mule deed";
            Hue   = 1161; // warm brown to hint at the mule's heritage
            LootType = LootType.Blessed;
        }

        public override string DefaultName => "a pack mule deed";

        public override void OnDoubleClick(Mobile from)
        {
            if (from is not PlayerMobile pm)
                return;

            if (!IsChildOf(pm.Backpack))
            {
                pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
                return;
            }

            if (!pm.Alive)
            {
                pm.SendMessage(0x22, "You cannot summon a pack mule while dead.");
                return;
            }

            // Check follower slot capacity (mule costs 2 control slots like a Nightmare).
            if (pm.Followers + 2 > pm.FollowersMax)
            {
                pm.SendMessage(0x22,
                    "You have too many followers to summon your pack mule. " +
                    "Stable or dismiss a current follower first.");
                return;
            }

            // Create the mule, bond it, and place it at the player's feet.
            var mule = new PackMule();

            mule.Controlled      = true;
            mule.ControlMaster   = pm;
            mule.IsBonded        = true;
            mule.BondingBegin    = DateTime.UtcNow;
            mule.OwnerAbandonTime = DateTime.MinValue;

            mule.MoveToWorld(pm.Location, pm.Map);

            pm.SendMessage(0x44,
                "Your pack mule arrives, sturdy and dependable. " +
                "It is pre-bonded and will follow you faithfully.");
            pm.PlaySound(0xA8); // horse whinny

            Delete(); // consume the deed
        }
    }
}
