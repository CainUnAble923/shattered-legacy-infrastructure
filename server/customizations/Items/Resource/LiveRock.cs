// cc-P199: ServUO pub57 Scripts/Items/Resource/LiveRock.cs. Live rock (1125985), a stackable commodity.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class LiveRock : Item, ICommodity
    {
        [Constructible]
        public LiveRock(int amount = 1) : base(0xA3E9)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1125985; // live rock

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
