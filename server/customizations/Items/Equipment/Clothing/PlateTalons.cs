using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Clothing/PlateTalons.cs (CC9 batch 5).
    // Dropped: CanBeWornByGargoyles (D-2).
    [Flippable(0x42DE, 0x42DF)]
    [SerializationGenerator(0, false)]
    public partial class PlateTalons : BaseShoes
    {
        [Constructible]
        public PlateTalons(int hue = 0) : base(0x42DE, hue)
        {
        }

        public override double DefaultWeight => 5.0;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
        public override CraftResource DefaultResource => CraftResource.Iron;
    }
}
