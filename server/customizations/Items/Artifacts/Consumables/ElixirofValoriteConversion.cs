using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Consumables/ElixirofValoriteConversion.cs (CC9 batch 5). Logic unchanged, the batch 1
    // ElixirofAgapiteConversion shape: exactly 500 bronze ingots in the backpack become 500 valorite ingots.
    [SerializationGenerator(0, false)]
    public partial class ElixirofValoriteConversion : Item
    {
        [Constructible]
        public ElixirofValoriteConversion() : base(0x99B)
        {
            Hue = 2219;
            Movable = true;
        }

        public override int LabelNumber => 1113010; // Elixir of Valorite Conversion

        public override void OnDoubleClick(Mobile from)
        {
            var ingots = from.Backpack?.FindItemByType<BronzeIngot>();

            if (ingots == null)
            {
                from.SendLocalizedMessage(1078618); // The item must be in your backpack to be exchanged.
                return;
            }

            if (ingots.Amount == 500)
            {
                ingots.Delete();
                from.SendLocalizedMessage(1113048); // You've successfully converted the metal.
                from.AddToBackpack(new ValoriteIngot(500));
                Delete();
            }
            else
            {
                from.SendLocalizedMessage(1113046); // You can only convert five hundred ingots at a time.
            }
        }
    }
}
