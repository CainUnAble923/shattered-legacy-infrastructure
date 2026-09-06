namespace Server.Items
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Pack Mule Backpack
    //
    // The cargo hold attached to every Pack Mule.  Vastly larger than a StrongBackpack
    // (which is 1,600 stone / 125 items) — appropriate for a prestige-tier animal.
    //
    //   Weight limit : 4,000 stone
    //   Item limit   : 400 items
    // ─────────────────────────────────────────────────────────────────────────────

    public class PackMuleBackpack : Backpack
    {
        [Constructable]
        public PackMuleBackpack()
        {
            Layer = Layer.Backpack;
            Movable = false;
        }

        public PackMuleBackpack(Serial serial)
            : base(serial)
        {
        }

        public override int DefaultMaxWeight
        {
            get { return 4000; }
        }

        public override int DefaultMaxItems
        {
            get { return 400; }
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
