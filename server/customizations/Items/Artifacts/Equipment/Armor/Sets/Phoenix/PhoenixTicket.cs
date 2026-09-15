using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Phoenix/PhoenixTicket.cs (CC9 batch 5). Double-click in the
    // backpack for one random piece of the Phoenix suit (batch 4).
    [SerializationGenerator(0, false)]
    public partial class PhoenixTicket : Item
    {
        [Constructible]
        public PhoenixTicket() : base(0x14F0) => LootType = LootType.Blessed;

        public override int LabelNumber => 1041234; // Ticket for a piece of phoenix armor

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
                return;
            }

            switch (Utility.Random(6))
            {
                case 0:
                    from.AddToBackpack(new PhoenixArms());
                    break;
                case 1:
                    from.AddToBackpack(new PhoenixChest());
                    break;
                case 2:
                    from.AddToBackpack(new PhoenixGloves());
                    break;
                case 3:
                    from.AddToBackpack(new PhoenixGorget());
                    break;
                case 4:
                    from.AddToBackpack(new PhoenixHelm());
                    break;
                case 5:
                    from.AddToBackpack(new PhoenixLegs());
                    break;
            }

            Delete();
            from.SendLocalizedMessage(502064); // A piece of phoenix armor has been placed in your backpack.
        }
    }
}
