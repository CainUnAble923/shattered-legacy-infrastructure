using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Jewelry/GargishRing.cs (CC9 batch 3). A BaseJewel, unlike the armour-class
    // gargish earrings and necklace; ServUO leaves the weight commented out and so does this.
    [SerializationGenerator(0, false)]
    public partial class GargishRing : BaseRing
    {
        [Constructible]
        public GargishRing() : base(0x4212)
        {
        }

        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
