using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Consumables/ElixirofMetalConversion.cs (CC9 batch 5). Logic unchanged, the batch 1
    // ElixirofAgapiteConversion shape: exactly 500 iron ingots in the backpack become 500 one random lesser-metal ingot type (dull copper, copper, shadow iron or bronze).
    [SerializationGenerator(0, false)]
    public partial class ElixirofMetalConversion : Item
    {
        [Constructible]
        public ElixirofMetalConversion() : base(0x99B)
        {
            Hue = 1159;
            Movable = true;
        }

        public override int LabelNumber => 1113011; // Elixir of Metal Conversion

        public override void OnDoubleClick(Mobile from)
        {
            var ingots = from.Backpack?.FindItemByType<IronIngot>();

            if (ingots == null)
            {
                from.SendLocalizedMessage(1078618); // The item must be in your backpack to be exchanged.
                return;
            }

            if (ingots.Amount == 500)
            {
                ingots.Delete();
                // ServUO's case order is 0, 2, 1, 3; the outcome is the same uniform choice.
                switch (Utility.Random(4))
                {
                    case 0:
                        from.AddToBackpack(new DullCopperIngot(500));
                        break;
                    case 1:
                        from.AddToBackpack(new CopperIngot(500));
                        break;
                    case 2:
                        from.AddToBackpack(new ShadowIronIngot(500));
                        break;
                    case 3:
                        from.AddToBackpack(new BronzeIngot(500));
                        break;
                }

                from.SendLocalizedMessage(1113048); // You've successfully converted the metal.
                Delete();
            }
            else
            {
                from.SendLocalizedMessage(1113046); // You can only convert five hundred ingots at a time.
            }
        }
    }
}
