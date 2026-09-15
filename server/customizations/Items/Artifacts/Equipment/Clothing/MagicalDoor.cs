using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/MagicalDoor.cs (CC9 batch 5). A replica door that shows its
    // "open" graphic (ItemID - 8) while lying in the world and its closed one anywhere else. The two
    // constructors collapse to one with a default, as ServUO's ItemID getter/setter pair collapses to the
    // same override on ModernUO's virtual ItemID.
    [SerializationGenerator(0, false)]
    public partial class MagicalDoor : Item
    {
        [Constructible]
        public MagicalDoor() : this(Utility.RandomList(7905, 7914, 7923, 7932))
        {
        }

        public MagicalDoor(int id) : base(id)
        {
        }

        public override int LabelNumber => 1112410; // Magical Door [Replica]

        public override int ItemID
        {
            get
            {
                if (Parent == null && Map != Map.Internal)
                {
                    return base.ItemID - 8;
                }

                return base.ItemID;
            }
            set => base.ItemID = value;
        }
    }
}
