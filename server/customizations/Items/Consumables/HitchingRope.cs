// cc-P128: ServUO pub57 Scripts/Items/Consumables/HitchingRope.cs -> ours. A hitching rope (ML; Tinkering 60 with
// Animal Lore 15, from a rope and Resolve's Bridle): it resupplies a hitching post with 30 uses (15 for a replica).
// Neither tree has the hitching post yet (ServUO Items/Addons/HitchingPost.cs is in the port list's base-items row),
// so the rope's target refuses everything with "That has no effect" until the post lands; the check it will make is
// written here against the post's members (UsesRemaining, Replica, Charges) so the port is one `is HitchingPost` away.

using ModernUO.Serialization;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class HitchingRope : Item
{
    [Constructible]
    public HitchingRope() : base(0x14F8)
    {
        Hue = 0x41F; // guessed (ServUO's comment)
        Weight = 5;
    }

    public override int LabelNumber => 1071124; // hitching rope

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack) || from.InRange(GetWorldLocation(), 2) && Movable)
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1071159); // Select the hitching post you want to supply hitching rope.
            from.Target = new InternalTarget(this);
        }
        else
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
        }
    }

    private class InternalTarget : Target
    {
        private readonly HitchingRope _rope;

        public InternalTarget(HitchingRope rope) : base(-1, false, TargetFlags.None) => _rope = rope;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_rope.Deleted)
            {
                return;
            }

            if (!from.InRange(_rope.GetWorldLocation(), 2))
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
                return;
            }

            // ServUO: a HitchingPost with UsesRemaining 0 takes the rope (Charges - 1, UsesRemaining + 30, or + 15 for a
            // replica; a replica with no charges left is "damaged", 1071157). No post exists here yet.
            from.SendLocalizedMessage(1062020); // That has no effect.
        }
    }
}
