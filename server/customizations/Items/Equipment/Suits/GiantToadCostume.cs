using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/GiantToadCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class GiantToadCostume : BaseCostume
    {
        [Constructible]
        public GiantToadCostume()
        {
            CostumeBody = 80;
        }

        public override string CreatureName => "giant toad";

        public override int LabelNumber => 1114226; // giant toad costume
    }
}
