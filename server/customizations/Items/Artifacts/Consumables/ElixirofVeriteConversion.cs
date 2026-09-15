using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Consumables/ElixirofVeriteConversion.cs (CC9 batch 5). Logic unchanged, the batch 1
    // ElixirofAgapiteConversion shape: exactly 500 copper ingots in the backpack become 500 verite ingots.
    [SerializationGenerator(0, false)]
    public partial class ElixirofVeriteConversion : Item
    {
        [Constructible]
        public ElixirofVeriteConversion() : base(0x99B)
        {
            Hue = 2207;
            Movable = true;
        }

        public override int LabelNumber => 1113009; // Elixir of Verite Conversion

        public override void OnDoubleClick(Mobile from)
        {
            var ingots = from.Backpack?.FindItemByType<CopperIngot>();

            if (ingots == null)
            {
                from.SendLocalizedMessage(1078618); // The item must be in your backpack to be exchanged.
                return;
            }

            if (ingots.Amount == 500)
            {
                ingots.Delete();
                from.SendLocalizedMessage(1113048); // You've successfully converted the metal.
                from.AddToBackpack(new VeriteIngot(500));
                Delete();
            }
            else
            {
                from.SendLocalizedMessage(1113046); // You can only convert five hundred ingots at a time.
            }
        }
    }
}
