namespace Server.Items
{

    /// <summary>
    /// Migration stub — replaced by DevTestingCrystal.
    /// Keeps old saves from throwing type-not-found errors.
    /// Any tokens remaining in the world are inert (no commands, no bypass logic).
    /// GMs can [delete them manually or let them sit harmlessly.
    /// </summary>
    public class CompactTestingToken : Item
    {
        public CompactTestingToken()
            : base(0x1ECD)
        {
        }

        public CompactTestingToken(Serial serial)
            : base(serial)
        {
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
