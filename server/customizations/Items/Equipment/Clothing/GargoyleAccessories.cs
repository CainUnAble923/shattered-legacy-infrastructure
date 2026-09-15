using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Clothing/Gargoyle Accessories.cs (CC9 batch 5).
    // Dropped: CanBeWornByGargoyles (D-2).
    [Flippable(0x450D, 0x450D)]
    [SerializationGenerator(0, false)]
    public partial class GargoyleTailMale : BaseWaist
    {
        [Constructible]
        public GargoyleTailMale(int hue = 0) : base(0x450D, hue)
        {
        }

        public override double DefaultWeight => 2.0;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }

    // ServUO: Items/Equipment/Clothing/Gargoyle Accessories.cs (CC9 batch 5).
    // Dropped: CanBeWornByGargoyles (D-2).
    [Flippable(0x44C1, 0x44C2)]
    [SerializationGenerator(0, false)]
    public partial class GargoyleTailFemale : BaseWaist
    {
        [Constructible]
        public GargoyleTailFemale(int hue = 0) : base(0x44C1, hue)
        {
        }

        public override double DefaultWeight => 2.0;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
