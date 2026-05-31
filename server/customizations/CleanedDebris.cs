using ModernUO.Serialization;
using Server.Items;

namespace Server.Items;

// ─────────────────────────────────────────────────────────────────────────────
// CleanedDebris — Custodians guild work-order currency
//
// Awarded when a player processes 5+ items at once via [cleanupall or the
// TrashBag "Dump Now" action.  Rate: 1 bundle per 5 items cleaned in a batch.
//
// Used to fulfil Custodian guild work orders (Civic Contracts).
// Cannot be purchased or crafted — earned only through cleanup activity.
// ─────────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class CleanedDebris : Item
{
    [Constructible]
    public CleanedDebris() : base(0xE75) // bag graphic
    {
        Name      = "civic waste bundle";
        Hue       = 0x497; // muted olive-grey — looks like a dirty bag
        Stackable = true;
        Amount    = 1;
        Weight    = 2.0;
    }

    public CleanedDebris(Serial serial) : base(serial) { }

    public override string DefaultName => "civic waste bundle";
}
