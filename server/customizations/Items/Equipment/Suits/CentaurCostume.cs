using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/CentaurCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class CentaurCostume : BaseCostume
    {
        [Constructible]
        public CentaurCostume()
        {
            CostumeBody = 101;
        }

        public override string CreatureName => "centaur";

        public override int LabelNumber => 1114235; // centaur costume
    }
}
