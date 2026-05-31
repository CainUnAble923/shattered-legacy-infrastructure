using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

// ─────────────────────────────────────────────────────────────────────────────
// ClusterFBackpack — player backpack with strength-scaled weight limit
//
// Replaces the vanilla ML backpack hard-cap of 550 stones with the player's
// actual MaxWeight, which already scales correctly with Strength:
//
//   ML + Human:  100 + (int)(3.5 × Str)
//   Otherwise:    40 + (int)(3.5 × Str)
//
// Examples (ML + Human):
//   Str 100  → 450 stones
//   Str 150  → 625 stones
//   Str 200  → 800 stones
//   Str 260  → 1,010 stones
//
// Existing player backpacks are migrated to this type on first login via
// ClusterFWeightPatch.Configure().
// ─────────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class ClusterFBackpack : Backpack
{
    [Constructible]
    public ClusterFBackpack() { }

    public ClusterFBackpack(Serial serial) : base(serial) { }

    public override int DefaultMaxWeight
    {
        get
        {
            // Delegate to the player's strength-based carry limit.
            if (Parent is Mobile m && m.Player && m.Backpack == this)
                return m.MaxWeight;

            return base.DefaultMaxWeight;
        }
    }
}
