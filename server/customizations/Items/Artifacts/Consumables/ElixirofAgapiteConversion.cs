using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Consumables/ElixirofAgapiteConversion.cs (CC9). Logic unchanged:
    // exactly 500 shadow iron ingots in the backpack become 500 agapite ingots.
    [SerializationGenerator(0, false)]
    public partial class ElixirofAgapiteConversion : Item
    {
        [Constructible]
        public ElixirofAgapiteConversion() : base(0x99B)
        {
            Hue = 2425;
            Movable = true;
        }

        public override int LabelNumber => 1113008; // Elixir of Agapite Conversion

        public override void OnDoubleClick(Mobile from)
        {
            var ingots = from.Backpack?.FindItemByType<ShadowIronIngot>();

            if (ingots == null)
            {
                from.SendLocalizedMessage(1078618); // The item must be in your backpack to be exchanged.
                return;
            }

            if (ingots.Amount == 500)
            {
                ingots.Delete();
                from.SendLocalizedMessage(1113048); // You've successfully converted the metal.
                from.AddToBackpack(new AgapiteIngot(500));
                Delete();
            }
            else
            {
                from.SendLocalizedMessage(1113046); // You can only convert five hundred ingots at a time.
            }
        }
    }
}
