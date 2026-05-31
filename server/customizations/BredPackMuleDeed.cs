using System;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

// ─────────────────────────────────────────────────────────────────────────────
// Bred Pack Mule Deed
//
// Produced by the breeding system (PackMuleBreedingSystem) when two Pack Mules
// complete their 24-hour gestation period.  Functionally identical to a regular
// PackMuleDeed but sports a golden hue so players can identify bred stock at a
// glance.
// ─────────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class BredPackMuleDeed : Item
{
    [Constructible]
    public BredPackMuleDeed() : base(0x14F0)
    {
        Name     = "a bred pack mule deed";
        Hue      = 1154; // golden — distinct from the standard brown deed
        LootType = LootType.Blessed;
    }

    public override string DefaultName => "a bred pack mule deed";

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

        // Mule costs 2 control slots.
        if (pm.Followers + 2 > pm.FollowersMax)
        {
            pm.SendMessage(0x22,
                "You have too many followers to summon your pack mule. " +
                "Stable or dismiss a current follower first.");
            return;
        }

        var mule = new PackMule();

        mule.Controlled      = true;
        mule.ControlMaster   = pm;
        mule.IsBonded        = true;
        mule.BondingBegin    = DateTime.UtcNow;
        mule.OwnerAbandonTime = DateTime.MinValue;

        mule.MoveToWorld(pm.Location, pm.Map);

        pm.SendMessage(0x44,
            "Your bred pack mule arrives, carrying the strength of its lineage.");
        pm.PlaySound(0xA8);

        Delete();
    }
}
