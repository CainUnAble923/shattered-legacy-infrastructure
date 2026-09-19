// CC6 batch 1. ServUO's TribeType.GrayGoblin / TribeType.GreenGoblin pair, as a ModernUO OppositionGroup.
//
// ServUO (pub57, Core.TOL on this shard): BaseCreature.IsEnemy returns true and IsFriend false when
// IsTribeEnemy(m) (Mobiles/Normal/BaseCreature.cs:1183-1205, :1239, :1349), and the gray and green goblin
// tribes are each other's enemy. ModernUO has no TribeType; its OppositionGroup (Mobiles/AI/OppositionGroup.cs)
// is consulted at the same two points (BaseCreature.cs:1316 IsEnemy, :4083 IsFriend) with the same semantics
// for the three pairs it ships. This is the fourth pair, additive, no patch, as AGENTS.md priced it.
//
// Membership is by type list rather than by a property, so a goblin joins its tribe by being listed here.
// ServUO's Renowned goblins (Mobiles/Named) carry the same Tribe overrides and belong in these lists when they
// are ported.

using System;
using Server.Mobiles;

namespace Server;

public static class GoblinOppositionGroup
{
    public static OppositionGroup GrayAndGreenGoblins { get; } = new(
        [
            [
                typeof(GrayGoblin),
                typeof(GrayGoblinMage),
                typeof(GrayGoblinKeeper)
            ],
            [
                typeof(GreenGoblin),
                typeof(GreenGoblinAlchemist),
                typeof(GreenGoblinScout)
            ]
        ]
    );
}
