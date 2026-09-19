// ServUO declares this enum inside Mobiles/Normal/BaseCreature.cs (:128-135), next to HideType, and gives
// BaseCreature two virtuals over it, `int Fur` and `FurType FurType` (:3881-3882), which the boura and kepetch
// override and which BaseCreature's corpse-carve path reads (:2338). Pinned ModernUO has neither the enum nor
// the virtuals, and BaseCreature is not patched for content (AGENTS.md: the layering pattern). So the enum is
// declared here, additively, in the same namespace, and the two members live on each fur-bearing creature
// itself (CC6 batch 2). What that costs: nothing else can ask "does this creature bear fur" through
// BaseCreature, and the corpse-carve fur is not ported (D-48 in notes/cc6-creatures-batch2.md).

namespace Server.Mobiles;

public enum FurType
{
    None,
    Green,
    LightBrown,
    Yellow,
    Brown
}
