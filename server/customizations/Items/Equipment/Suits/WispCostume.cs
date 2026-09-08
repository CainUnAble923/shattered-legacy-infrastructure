using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/WispCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class WispCostume : BaseCostume
    {
        [Constructible]
        public WispCostume()
        {
            CostumeBody = 58;
        }

        public override string CreatureName => "wisp";

        public override int LabelNumber => 1114225; // wisp costume
    }
}
