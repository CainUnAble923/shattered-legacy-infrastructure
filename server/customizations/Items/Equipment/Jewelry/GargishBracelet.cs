using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Jewelry/GargishBracelet.cs (CC9 batch 3). A BaseJewel, unlike the armour-class
    // gargish earrings and necklace; ServUO leaves the weight commented out and so does this.
    [SerializationGenerator(0, false)]
    public partial class GargishBracelet : BaseBracelet
    {
        [Constructible]
        public GargishBracelet() : base(0x4211)
        {
        }

        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
