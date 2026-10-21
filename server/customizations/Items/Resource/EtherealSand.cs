// cc-P199: ServUO pub57 Scripts/Items/Resource/EtherealSand.cs. Ethereal sand (1125984), a stackable commodity of
// weight 0.1. ServUO's (amountFrom, amountTo) constructor stays: it makes a random amount in that range.
//
// No deliberate differences from ServUO. ServUO's () and (int amount) pair is one (int amount = 1) here.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class EtherealSand : Item, ICommodity
    {
        [Constructible]
        public EtherealSand(int amountFrom, int amountTo) : this(Utility.RandomMinMax(amountFrom, amountTo))
        {
        }

        [Constructible]
        public EtherealSand(int amount = 1) : base(0xA3E8)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1125984; // ethereal sand
        public override double DefaultWeight => 0.1;

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;
    }
}
