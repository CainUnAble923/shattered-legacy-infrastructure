using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/TitanCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class TitanCostume : BaseCostume
    {
        [Constructible]
        public TitanCostume()
        {
            CostumeBody = 76;
        }

        public override string CreatureName => "titan";

        public override int LabelNumber => 1114238; // titan costume
    }
}
