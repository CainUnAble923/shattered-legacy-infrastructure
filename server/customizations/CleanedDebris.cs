using ModernUO.Serialization;
using Server.Items;

namespace Server.Items;

// ─────────────────────────────────────────────────────────────────────────────
// CleanedDebris — the civic waste bundle, RETIRED by cc-P414 (Chase, 2026-10-22)
//
// Cleaning used to drop 1 per 5 items with Clean Up value in one batch ([cleanupall, the
// TrashBag's "Dump Now"), the turn-in for the Custodians' Civic Contracts. Nothing makes them
// now: the contracts ask for Clean Up points. The type stays so saves that hold bundles load
// (serialization unchanged, version 0). Handing bundles to the Sanitation Warden redeems
// them once for Clean Up points (ClusterFCustodianSystem.RedeemBundles) and deletes them.
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
