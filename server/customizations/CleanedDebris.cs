namespace Server.Items
{

    // ─────────────────────────────────────────────────────────────────────────
    // CleanedDebris — Custodians guild work-order currency
    //
    // Awarded when a player processes 5+ items at once via [cleanupall or the
    // TrashBag "Dump Now" action.  Rate: 1 bundle per 5 items cleaned in a batch.
    //
    // Used to fulfil Custodian guild work orders (Civic Contracts).
    // Cannot be purchased or crafted — earned only through cleanup activity.
    // ─────────────────────────────────────────────────────────────────────────

    public class CleanedDebris : Item
    {
        [Constructable]
        public CleanedDebris()
            : base(0xE75) // bag graphic
        {
            Name = "civic waste bundle";
            Hue = 0x497; // muted olive-grey — looks like a dirty bag
            Stackable = true;
            Amount = 1;
            Weight = 2.0;
        }

        public CleanedDebris(Serial serial)
            : base(serial)
        {
        }

        public override string DefaultName
        {
            get { return "civic waste bundle"; }
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.WriteEncodedInt(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadEncodedInt();
        }
    }
}
