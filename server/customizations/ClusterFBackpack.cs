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

    // cc-P137 (F-50): loot or a pickup of what Unravel produces goes into the Artificers' Essence Satchel, when one is
    // in this pack. Dropped on the pack's icon or into its open gump; never AddToBackpack, so crafting, buying and
    // quest rewards still land here.
    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (ArtificersSatchel.IsLootPickup(dropped) && ArtificersSatchel.TryStore(from, dropped))
        {
            from.SendSound(GetDroppedSound(dropped), GetWorldLocation());
            return true;
        }

        return base.OnDragDrop(from, dropped);
    }

    public override bool OnDragDropInto(Mobile from, Item item, Point3D p)
    {
        if (ArtificersSatchel.IsLootPickup(item) && ArtificersSatchel.TryStore(from, item))
        {
            from.SendSound(GetDroppedSound(item), GetWorldLocation());
            return true;
        }

        return base.OnDragDropInto(from, item, p);
    }
}
